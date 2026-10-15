// Kenh tin nhan giua cac process (API -> gateway, gateway <-> gateway). Co Redis (REDIS_URL) thi
// dung Redis pub/sub (nhieu may, nhieu instance); khong co thi dung EventEmitter trong bo nho (CHI
// dung khi chay 1 process ROLE=all - dev/test). O quy mo rat lon nen dung Redis Cluster voi
// sharded pub/sub (SPUBLISH/SSUBSCRIBE) - xem README.
const { EventEmitter } = require("events");
const { env } = require("../config/env");
const { logger } = require("../utils/logger");

function memoryBus() {
  const em = new EventEmitter();
  em.setMaxListeners(0);
  return {
    kind: "memory",
    publish: async (topic, msg) => { em.emit(topic, msg); },
    subscribe: async (topic, handler) => { em.on(topic, handler); },
    unsubscribe: async (topic, handler) => { em.off(topic, handler); },
    redis: null,
    close: async () => em.removeAllListeners(),
  };
}

function redisBus(url) {
  const Redis = require("ioredis");
  const pub = new Redis(url, { maxRetriesPerRequest: 3 });
  const sub = new Redis(url, { maxRetriesPerRequest: null });
  const handlers = new Map(); // topic -> Set<handler>
  pub.on("error", (err) => logger.error({ err }, "Redis pub loi"));
  sub.on("error", (err) => logger.error({ err }, "Redis sub loi"));
  sub.on("message", (topic, raw) => {
    const set = handlers.get(topic);
    if (!set) return;
    let msg;
    try { msg = JSON.parse(raw); } catch { return; }
    for (const h of set) h(msg);
  });
  return {
    kind: "redis",
    publish: async (topic, msg) => { await pub.publish(topic, JSON.stringify(msg)); },
    subscribe: async (topic, handler) => {
      let set = handlers.get(topic);
      if (!set) { set = new Set(); handlers.set(topic, set); await sub.subscribe(topic); }
      set.add(handler);
    },
    unsubscribe: async (topic, handler) => {
      const set = handlers.get(topic);
      if (!set) return;
      set.delete(handler);
      if (set.size === 0) { handlers.delete(topic); await sub.unsubscribe(topic); }
    },
    redis: pub,
    close: async () => { pub.disconnect(); sub.disconnect(); },
  };
}

let bus;
function getBus() {
  if (!bus) bus = env.REDIS_URL ? redisBus(env.REDIS_URL) : memoryBus();
  return bus;
}

// Gui su kien toi 1 nguoi choi (du dang ket noi o gateway nao).
function notifyPlayer(playerId, event) {
  return getBus().publish(`user:${playerId}`, event).catch((err) => logger.warn({ err }, "Khong gui duoc thong bao"));
}

module.exports = { getBus, notifyPlayer, memoryBus, redisBus };
