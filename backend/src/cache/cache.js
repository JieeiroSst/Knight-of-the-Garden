// Cache doc (read-through) dung chung - giam truy van Postgres cho du lieu doc nhieu, ghi it (nhan
// vat, bang hoi, suc manh PvP, cong trinh/tui do theo o luoi, trang thai hoi sinh).
//
// NGUYEN TAC CHONG MAT DU LIEU: Postgres LUON la nguon su that. Moi thay doi COMMIT vao DB truoc,
// SAU DO moi xoa/cap nhat cache (write-through + invalidate). Redis chet/mat du lieu -> chi cham hon
// (doc thang DB), khong bao gio mat du lieu nguoi choi. Moi khoa deu co TTL nen du co lan xoa cache bi
// loi, du lieu cu cung tu het han.
//
// Co REDIS_URL -> Redis (chia se giua moi process api/gateway). Khong co -> Map trong bo nho (chi
// dung cho ROLE=all 1 process - dev/test).
const { getBus } = require("../realtime/bus");
const { logger } = require("../utils/logger");

const PREFIX = "c:";

function memoryStore() {
  const m = new Map(); // key -> { v, exp }
  const sweep = setInterval(() => {
    const now = Date.now();
    for (const [k, e] of m) if (e.exp <= now) m.delete(k);
  }, 30000);
  sweep.unref();
  return {
    kind: "memory",
    async get(k) { const e = m.get(k); if (!e) return null; if (e.exp <= Date.now()) { m.delete(k); return null; } return e.v; },
    async set(k, v, ttl) { m.set(k, { v, exp: Date.now() + ttl * 1000 }); },
    async del(keys) { for (const k of keys) m.delete(k); },
  };
}

function redisStore(redis) {
  return {
    kind: "redis",
    async get(k) { return redis.get(k); },
    async set(k, v, ttl) { await redis.set(k, v, "EX", ttl); },
    async del(keys) { if (keys.length) await redis.del(...keys); },
  };
}

let store;
function getStore() {
  if (!store) {
    const redis = getBus().redis;
    store = redis ? redisStore(redis) : memoryStore();
  }
  return store;
}

const stats = { hits: 0, misses: 0, errors: 0 };

// Lay tu cache, khong co thi goi loader (DB) roi luu lai. Loi cache -> bo qua cache, van tra DB.
// Gia tri null/undefined cung duoc cache (tranh truy van lai lien tuc khoa khong ton tai).
async function cached(key, ttlSec, loader) {
  const k = PREFIX + key;
  let raw = null;
  try { raw = await getStore().get(k); } catch (err) { stats.errors++; logger.warn({ err }, "cache get loi"); }
  if (raw != null) {
    stats.hits++;
    return JSON.parse(raw).v;
  }
  stats.misses++;
  const v = await loader();
  try { await getStore().set(k, JSON.stringify({ v: v ?? null }), ttlSec); } catch (err) { stats.errors++; logger.warn({ err }, "cache set loi"); }
  return v;
}

// Xoa sau khi DB da commit. Khong nem loi (TTL se tu don neu xoa that bai).
async function invalidate(...keys) {
  const ks = keys.flat().filter(Boolean).map((k) => PREFIX + k);
  if (!ks.length) return;
  try { await getStore().del(ks); } catch (err) { stats.errors++; logger.warn({ err, keys: ks }, "cache del loi"); }
}

// O luoi khong gian cho truy van "quanh vi tri" (cong trinh/tui do): o CELL x CELL don vi the gioi.
const CELL = 1000;
const cellOf = (v) => Math.floor(v / CELL);
function cellsInRadius(x, z, r) {
  const out = [];
  for (let cx = cellOf(x - r); cx <= cellOf(x + r); cx++)
    for (let cz = cellOf(z - r); cz <= cellOf(z + r); cz++) out.push([cx, cz]);
  return out;
}

module.exports = { cached, invalidate, cellOf, cellsInRadius, CELL, stats, _resetForTest: () => { store = null; } };
