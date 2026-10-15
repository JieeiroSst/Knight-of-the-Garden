// Trang thai tran dau tay doi (dau truong) - dung chung giua cac gateway qua Redis (HINCRBY nguyen tu
// cho mau), hoac Map trong bo nho khi chay 1 process. Ban ghi tu het han de khong ro ri.
const crypto = require("crypto");

const TTL = 300;

function memoryDuels() {
  const m = new Map();
  return {
    async create(d) { const id = crypto.randomBytes(6).toString("hex"); m.set(id, { ...d, id }); setTimeout(() => m.delete(id), TTL * 1000).unref(); return id; },
    async get(id) { return m.get(id) || null; },
    async update(id, fields) { const d = m.get(id); if (d) Object.assign(d, fields); return d; },
    async damage(id, targetId, dmg) { const d = m.get(id); if (!d) return null; d[`hp_${targetId}`] -= dmg; return d; },
    async remove(id) { m.delete(id); },
  };
}

function redisDuels(redis) {
  const key = (id) => `duel:${id}`;
  const parse = (h) => {
    if (!h || !h.a) return null;
    const d = { ...h };
    for (const k of Object.keys(d)) if (/^(a|b|started|hp_\d+|atk_\d+|def_\d+|channel)$/.test(k)) d[k] = Number(d[k]);
    return d;
  };
  return {
    async create(d) {
      const id = crypto.randomBytes(6).toString("hex");
      await redis.hset(key(id), { ...d, id });
      await redis.expire(key(id), TTL);
      return id;
    },
    async get(id) { return parse(await redis.hgetall(key(id))); },
    async update(id, fields) { await redis.hset(key(id), fields); return parse(await redis.hgetall(key(id))); },
    async damage(id, targetId, dmg) { await redis.hincrby(key(id), `hp_${targetId}`, -dmg); return parse(await redis.hgetall(key(id))); },
    async remove(id) { await redis.del(key(id)); },
  };
}

module.exports = { memoryDuels, redisDuels };
