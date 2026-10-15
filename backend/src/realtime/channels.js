// Dang ky KENH theo vung (zone): moi vung (thi tran, rung, phe tich...) chia thanh nhieu kenh, moi
// kenh toi da CHANNEL_CAPACITY nguoi - chi nguoi CUNG KENH moi thay nhau. Day la cach game online
// ho tro hang trieu nguoi choi: khong ai phai nhan vi tri cua hang trieu nguoi khac.
//
// Roster (danh sach nguoi trong kenh + vi tri gan nhat) nam o Redis (nhieu gateway cung doc) hoac
// bo nho (1 process). Dem so nguoi la UOC LUONG (cap nhat tu roster) - co the vuot cap vai nguoi
// khi nhieu nguoi vao cung luc o nhieu gateway, chap nhan duoc (tran mem).

function memoryChannels(capacity) {
  const rosters = new Map(); // `${zone}:${n}` -> Map<playerId, json>
  const key = (zone, n) => `${zone}:${n}`;
  return {
    async assign(zone, wanted) {
      if (wanted && (rosters.get(key(zone, wanted))?.size || 0) < capacity) return wanted;
      for (let n = 1; ; n++) if ((rosters.get(key(zone, n))?.size || 0) < capacity) return n;
    },
    async put(zone, n, playerId, state) {
      if (!rosters.has(key(zone, n))) rosters.set(key(zone, n), new Map());
      rosters.get(key(zone, n)).set(String(playerId), state);
    },
    async remove(zone, n, playerId) {
      const r = rosters.get(key(zone, n));
      if (!r) return;
      r.delete(String(playerId));
      if (r.size === 0) rosters.delete(key(zone, n));
    },
    async roster(zone, n) {
      return [...(rosters.get(key(zone, n))?.values() || [])];
    },
    async list(zone) {
      const out = [];
      for (const [k, r] of rosters) if (k.startsWith(`${zone}:`)) out.push({ channel: Number(k.split(":")[1]), count: r.size });
      return out.sort((a, b) => a.channel - b.channel);
    },
  };
}

function redisChannels(redis, capacity) {
  const rosterKey = (zone, n) => `roster:${zone}:${n}`;
  const countsKey = (zone) => `zonecount:${zone}`;
  return {
    // Chon kenh: uu tien kenh mong muon, neu khong thi kenh so nho nhat con cho.
    async assign(zone, wanted) {
      const counts = await redis.hgetall(countsKey(zone));
      const get = (n) => Number(counts[n] || 0);
      if (wanted && get(wanted) < capacity) return wanted;
      for (let n = 1; ; n++) if (get(n) < capacity) return n;
    },
    async put(zone, n, playerId, state) {
      const added = await redis.hset(rosterKey(zone, n), String(playerId), JSON.stringify(state));
      if (added) await redis.hincrby(countsKey(zone), String(n), 1);
      // Roster tu het han neu tat ca gateway cua kenh chet ma khong kip don (gateway lam moi dinh ky).
      await redis.expire(rosterKey(zone, n), 120);
    },
    async remove(zone, n, playerId) {
      const removed = await redis.hdel(rosterKey(zone, n), String(playerId));
      if (removed) {
        const left = await redis.hincrby(countsKey(zone), String(n), -1);
        if (left <= 0) await redis.hdel(countsKey(zone), String(n));
      }
    },
    async roster(zone, n) {
      const all = await redis.hgetall(rosterKey(zone, n));
      return Object.values(all).map((s) => JSON.parse(s));
    },
    async list(zone) {
      const counts = await redis.hgetall(countsKey(zone));
      return Object.entries(counts).map(([c, v]) => ({ channel: Number(c), count: Number(v) })).sort((a, b) => a.channel - b.channel);
    },
  };
}

module.exports = { memoryChannels, redisChannels };
