// Ai dang online (ket noi gateway): Redis key presence:<id> (het han 90s, gateway lam moi moi 25s)
// de API (process khac) doc duoc; khong co Redis thi dung Map trong bo nho (ROLE=all).
const { getBus } = require("./bus");

const TTL = 90;
const local = new Map(); // id -> expires ms

async function setOnline(playerId) {
  const redis = getBus().redis;
  if (redis) await redis.set(`presence:${playerId}`, "1", "EX", TTL);
  else local.set(Number(playerId), Date.now() + TTL * 1000);
}

async function setOffline(playerId) {
  const redis = getBus().redis;
  if (redis) await redis.del(`presence:${playerId}`);
  else local.delete(Number(playerId));
}

async function onlineSet(ids) {
  if (!ids.length) return new Set();
  const redis = getBus().redis;
  if (redis) {
    const vals = await redis.mget(ids.map((id) => `presence:${id}`));
    return new Set(ids.filter((_, i) => vals[i]));
  }
  const now = Date.now();
  return new Set(ids.filter((id) => (local.get(Number(id)) || 0) > now));
}

module.exports = { setOnline, setOffline, onlineSet };
