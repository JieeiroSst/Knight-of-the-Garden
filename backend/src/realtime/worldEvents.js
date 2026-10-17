// Su kien the gioi (gan vao gateway): ruong tiep te, doan thuong nhan luu dong, Dem Trang Mau.
//  - CHI 1 gateway lam "nguoi dieu phoi" (khoa Redis world:leader, tu gia han) -> khong sinh trung su kien
//    khi chay nhieu gateway. Trang thai luu Redis (world:state) -> khoi dong lai van con.
//  - Moi gateway nghe topic "world" va day trang thai cho nguoi choi cua minh; nguoi moi vao nhan ngay.
//  - Mo ruong / mua hang: kiem toa do theo vi tri server biet, gianh quyen nguyen tu (SET NX / HINCRBY),
//    cap do trong transaction (worldEventService).
const crypto = require("crypto");
const rules = require("../game/worldEventRules");
const boss = require("../game/bossRules");
const characterService = require("../services/characterService");
const worldService = require("../services/worldEventService");
const { catalog } = require("../game/catalog");
const { calendar } = require("../services/timeService");
const { logger } = require("../utils/logger");

const TOPIC = "world";
const TICK_MS = 5000;

function memoryStore() {
  let state = null;
  const claims = new Set();
  const stock = new Map();
  const hps = new Map(), dmgs = new Map(), onces = new Set(), kv = new Map(), members = new Map();
  return {
    async load() { return state; },
    async save(s) { state = s; },
    async lead() { return true; },
    async claimDrop(id) { if (claims.has(id)) return false; claims.add(id); return true; },
    async isClaimed(id) { return claims.has(id); },
    async initStock(id, items) { for (const s of items) stock.set(`${id}:${s.item}`, s.left); },
    async takeStock(id, item, n) {
      const k = `${id}:${item}`, left = stock.get(k);
      if (left == null || left < n) return null;
      stock.set(k, left - n);
      return left - n;
    },
    async returnStock(id, item, n) { const k = `${id}:${item}`; stock.set(k, (stock.get(k) || 0) + n); },
    // Mau trum chung (trum the gioi / trum ham nguc cua to doi).
    async hpInit(key, hp) { if (!hps.has(key)) { hps.set(key, hp); dmgs.set(key, new Map()); } },
    async hpGet(key) { return hps.has(key) ? hps.get(key) : null; },
    async hpHit(key, pid, dmg) {
      if (!hps.has(key)) return null;
      hps.set(key, hps.get(key) - dmg);
      const m = dmgs.get(key); m.set(String(pid), (m.get(String(pid)) || 0) + dmg);
      return hps.get(key);
    },
    async dmgAll(key) { return Object.fromEntries(dmgs.get(key) || []); },
    async once(key) { if (onces.has(key)) return false; onces.add(key); return true; },
    async getJson(key) { return kv.has(key) ? kv.get(key) : null; },
    async setJson(key, v) { kv.set(key, v); },
    async addMember(key, pid) { const s = members.get(key) || new Set(); s.add(pid); members.set(key, s); },
    async membersOf(key) { return [...(members.get(key) || [])]; },
  };
}

function redisStore(redis, gwId) {
  return {
    async load() { const s = await redis.get("world:state"); return s ? JSON.parse(s) : null; },
    async save(s) { await redis.set("world:state", JSON.stringify(s), "EX", 86400); },
    async lead() {
      const ok = await redis.set("world:leader", gwId, "PX", 30000, "NX");
      if (ok) return true;
      if ((await redis.get("world:leader")) === gwId) { await redis.pexpire("world:leader", 30000); return true; }
      return false;
    },
    async claimDrop(id) { return (await redis.set(`world:drop:${id}`, "1", "EX", 7200, "NX")) === "OK"; },
    async isClaimed(id) { return (await redis.exists(`world:drop:${id}`)) === 1; },
    async initStock(id, items) {
      const key = `world:caravan:${id}`;
      await redis.hset(key, Object.fromEntries(items.map((s) => [s.item, s.left])));
      await redis.expire(key, 7200);
    },
    async takeStock(id, item, n) {
      const left = await redis.hincrby(`world:caravan:${id}`, item, -n);
      if (left < 0) { await redis.hincrby(`world:caravan:${id}`, item, n); return null; }
      return left;
    },
    async returnStock(id, item, n) { await redis.hincrby(`world:caravan:${id}`, item, n); },
    async hpInit(key, hp) { await redis.set(`world:bhp:${key}`, hp, "EX", 7200, "NX"); },
    async hpGet(key) { const v = await redis.get(`world:bhp:${key}`); return v == null ? null : Number(v); },
    async hpHit(key, pid, dmg) {
      if (!(await redis.exists(`world:bhp:${key}`))) return null;
      const r = await redis.multi().decrby(`world:bhp:${key}`, dmg).hincrby(`world:bdmg:${key}`, String(pid), dmg).expire(`world:bdmg:${key}`, 7200).exec();
      return Number(r[0][1]);
    },
    async dmgAll(key) { return redis.hgetall(`world:bdmg:${key}`); },
    async once(key) { return (await redis.set(`world:once:${key}`, "1", "EX", 2 * 86400, "NX")) === "OK"; },
    async getJson(key) { const v = await redis.get(`world:kv:${key}`); return v ? JSON.parse(v) : null; },
    async setJson(key, v) { await redis.set(`world:kv:${key}`, JSON.stringify(v), "EX", 7200); },
    async addMember(key, pid) { await redis.multi().sadd(`world:mem:${key}`, String(pid)).expire(`world:mem:${key}`, 7200).exec(); },
    async membersOf(key) { return (await redis.smembers(`world:mem:${key}`)).map(Number); },
  };
}

function attachWorldEvents(ctx, opts = {}) {
  const { bus, send, localByPlayer } = ctx;
  const notify = ctx.notify || (async () => {});
  const store = bus.redis ? redisStore(bus.redis, ctx.gwId) : memoryStore();
  const rand = opts.rand || Math.random;
  const now = opts.now || (() => Date.now());
  let current = null; // trang thai cong khai gan nhat

  const empty = () => ({ drops: [], caravan: null, bloodMoon: false, boss: null, nextDropAt: now() + 60000, nextCaravanAt: now() + 5 * 60000, nextBossAt: now() + boss.BOSS_FIRST_MS });
  let bossHpNow = null; // mau trum the gioi gan nhat gateway nay biet (gan vao trang thai cong khai)
  const publicState = (s) => ({
    drops: s.drops.map(({ loot, ...d }) => d),
    caravan: s.caravan,
    bloodMoon: s.bloodMoon,
    boss: s.boss ? { ...s.boss, hp: bossHpNow ?? s.boss.maxHp } : null,
    serverNow: now(),
  });

  const broadcastLocal = (state) => { for (const c of localByPlayer.values()) send(c, { t: "world", state }); };
  const onTopic = (msg) => {
    if (msg.k === "world") { current = msg.state; broadcastLocal(msg.state); }
    else if (msg.k === "drop_looted" && current) {
      const d = current.drops.find((x) => x.id === msg.id);
      if (d) d.looted = true;
      for (const c of localByPlayer.values()) send(c, { t: "drop_looted", id: msg.id, byName: msg.byName });
    } else if (msg.k === "boss_hp") {
      if (current?.boss?.id === msg.id) { current.boss.hp = msg.hp; bossHpNow = msg.hp; }
      for (const c of localByPlayer.values()) send(c, { t: "boss_hp", id: msg.id, hp: msg.hp });
    } else if (msg.k === "boss_end") {
      if (current?.boss?.id === msg.id) current.boss = null;
      bossHpNow = null;
      for (const c of localByPlayer.values()) send(c, { t: "boss_end", id: msg.id, killed: msg.killed, top: msg.top || [] });
    } else if (msg.k === "caravan_stock" && current?.caravan?.id === msg.id) {
      const s = current.caravan.stock.find((x) => x.item === msg.item);
      if (s) s.left = msg.left;
      for (const c of localByPlayer.values()) send(c, { t: "caravan_stock", item: msg.item, left: msg.left });
    }
  };
  const ready = bus.subscribe(TOPIC, onTopic);

  // ==== Dieu phoi (chi gateway giu khoa) ====
  async function tick(force = false) {
    if (!(await store.lead())) return;
    const t = now();
    const s = (await store.load()) || empty();
    let changed = force;
    const before = s.drops.length;
    s.drops = s.drops.filter((d) => d.expiresAt > t);
    if (s.drops.length !== before) changed = true;
    for (const d of s.drops) if (!d.looted && (await store.isClaimed(d.id))) { d.looted = true; changed = true; }
    if (s.drops.filter((d) => !d.looted).length < rules.MAX_ACTIVE_DROPS && t >= s.nextDropAt) {
      const spot = rules.pickSpot(rand, catalog.markets);
      s.drops.push({ id: crypto.randomBytes(5).toString("hex"), x: spot.x, z: spot.z, landsAt: t + rules.DROP_FALL_MS, expiresAt: t + rules.DROP_FALL_MS + rules.DROP_LIFETIME_MS, looted: false, loot: rules.rollDropLoot(spot.x, spot.z, rand) });
      s.nextDropAt = t + rules.randIn(rand, rules.DROP_EVERY_MS);
      changed = true;
    }
    if (s.caravan && s.caravan.until <= t) { s.caravan = null; changed = true; }
    if (!s.caravan && t >= s.nextCaravanAt) {
      const spot = rules.pickSpot(rand, catalog.markets, 4000, 10000);
      const id = crypto.randomBytes(5).toString("hex");
      s.caravan = { id, x: spot.x, z: spot.z, until: t + rules.CARAVAN_STAY_MS, stock: rules.rollCaravan(rand) };
      await store.initStock(id, s.caravan.stock);
      s.nextCaravanAt = t + rules.randIn(rand, rules.CARAVAN_EVERY_MS);
      changed = true;
    }
    // Trum the gioi: xuat hien -> bao toa do truoc -> thuc day -> bi ha (boss_end tu gateway ra don cuoi) / het gio.
    if (s.nextBossAt == null) s.nextBossAt = t + boss.BOSS_FIRST_MS;
    if (s.boss) {
      const hpLeft = await store.hpGet(`wb:${s.boss.id}`);
      const dead = hpLeft != null && hpLeft <= 0;
      if (hpLeft == null && !dead) await store.hpInit(`wb:${s.boss.id}`, s.boss.maxHp); // Redis mat khoa / ep tao (test)
      if (dead || s.boss.until <= t) {
        if (!dead) await bus.publish(TOPIC, { k: "boss_end", id: s.boss.id, killed: false });
        s.boss = null;
        s.nextBossAt = t + boss.randIn(rand, boss.BOSS_EVERY_MS);
        changed = true;
      }
    } else if (t >= s.nextBossAt) {
      const spot = rules.pickSpot(rand, catalog.markets, 4000, 14000);
      const kind = boss.WORLD_BOSS_KINDS[Math.floor(rand() * boss.WORLD_BOSS_KINDS.length)];
      const id = crypto.randomBytes(5).toString("hex");
      s.boss = { id, kind, x: spot.x, z: spot.z, maxHp: boss.WORLD_BOSS_HP, wakeAt: t + boss.BOSS_WARN_MS, until: t + boss.BOSS_WARN_MS + boss.BOSS_STAY_MS };
      await store.hpInit(`wb:${id}`, boss.WORLD_BOSS_HP);
      bossHpNow = boss.WORLD_BOSS_HP;
      changed = true;
    }
    const bm = rules.bloodMoon(t, calendar.epochMs);
    if (bm !== s.bloodMoon) { s.bloodMoon = bm; changed = true; }
    if (!changed) return;
    await store.save(s);
    await bus.publish(TOPIC, { k: "world", state: publicState(s) });
  }

  const timer = setInterval(() => tick().catch((e) => logger.warn({ err: e }, "Loi lich su kien the gioi")), opts.tickMs || TICK_MS);

  async function onWelcome(conn) {
    if (!current) {
      const s = await store.load();
      if (s?.boss) bossHpNow = await store.hpGet(`wb:${s.boss.id}`);
      if (s) current = publicState(s);
    }
    if (current) send(conn, { t: "world", state: current });
  }

  const err = (conn, msg) => send(conn, { t: "error", msg });
  const near = (conn, x, z, r) => Math.hypot(conn.pos[0] - x, conn.pos[2] - z) <= r;

  async function openDrop(conn, msg) {
    const s = await store.load();
    const d = s?.drops.find((x) => x.id === msg.id);
    if (!d || d.looted) return err(conn, "Ruong tiep te da bi nguoi khac lay.");
    if (now() < d.landsAt) return err(conn, "Ruong chua cham dat.");
    if (!near(conn, d.x, d.z, rules.DROP_OPEN_RANGE)) return err(conn, "Lai gan ruong hon.");
    if (!(await store.claimDrop(d.id))) return err(conn, "Ruong tiep te da bi nguoi khac lay.");
    const r = await worldService.grantLoot(conn.playerId, d.loot.items, d.loot.gold);
    send(conn, { t: "event", e: { type: "drop_opened", items: r.items, gold: r.gold } });
    send(conn, { t: "event", e: { type: "econ_sync" } });
    await bus.publish(TOPIC, { k: "drop_looted", id: d.id, byName: conn.profile.displayName });
  }

  async function caravanBuy(conn, msg) {
    const s = await store.load();
    const cv = s?.caravan;
    const n = Math.floor(Number(msg.n) || 1);
    if (!cv || cv.until <= now()) return err(conn, "Doan thuong nhan da roi di.");
    if (!near(conn, cv.x, cv.z, rules.CARAVAN_RANGE)) return err(conn, "Lai gan doan thuong nhan hon.");
    const offer = cv.stock.find((x) => x.item === msg.item);
    if (!offer || !(n > 0) || n > 99) return err(conn, "Khong co mon hang nay.");
    const left = await store.takeStock(cv.id, offer.item, n);
    if (left == null) return err(conn, "Hang da het.");
    try {
      await worldService.buyFromCaravan(conn.playerId, offer.item, n, offer.price);
    } catch (e) {
      await store.returnStock(cv.id, offer.item, n);
      if (e.isOperational) return err(conn, e.message);
      throw e;
    }
    send(conn, { t: "event", e: { type: "caravan_bought", item: offer.item, n, cost: offer.price * n } });
    send(conn, { t: "event", e: { type: "econ_sync" } });
    await bus.publish(TOPIC, { k: "caravan_stock", id: cv.id, item: offer.item, left });
  }

  // ==== Danh trum (ca trum the gioi lan trum ham nguc) ====
  // Gop cap nhat mau: toi da 1 lan / 250ms moi trum (+ 1 lan cuoi khi het don) - khong ngap bus.
  const pubTimers = new Map();
  function throttled(key, fn) {
    const p = pubTimers.get(key);
    if (p) { p.pending = fn; return; }
    fn();
    const entry = { pending: null };
    entry.timer = setTimeout(() => { pubTimers.delete(key); if (entry.pending) throttled(key, entry.pending); }, 250);
    pubTimers.set(key, entry);
  }
  const hitOk = (conn) => {
    if (conn.dead || (conn.respawnAt || 0) > now()) return false;
    if (now() - (conn.lastBossHit || 0) < boss.HIT_COOLDOWN_MS) return false;
    conn.lastBossHit = now();
    return true;
  };
  const nameOf = async (id) => (await characterService.get(id).catch(() => null))?.displayName || `#${id}`;

  async function bossHit(conn, msg) {
    const s = await store.load();
    const b = s?.boss;
    if (!b || b.id !== msg.id || now() < b.wakeAt || b.until <= now()) return;
    if (!near(conn, b.x, b.z, boss.HIT_RANGE) || !hitOk(conn)) return;
    const dmg = boss.clampHit(msg.dmg);
    if (!dmg) return;
    const key = `wb:${b.id}`;
    const hp = await store.hpHit(key, conn.playerId, dmg);
    if (hp == null) return;
    if (hp > 0) { throttled(key, () => bus.publish(TOPIC, { k: "boss_hp", id: b.id, hp }).catch(() => {})); return; }
    if (!(await store.once(`wbdone:${b.id}`))) return; // gateway khac da chot
    const rewards = boss.worldBossRewards(await store.dmgAll(key), b.maxHp, catalog.enemies.get(b.kind), rand);
    const top = [];
    for (const r of rewards) {
      if (r.rank <= 3) top.push({ name: await nameOf(r.playerId), share: Math.round(r.share * 100) });
      const g = await worldService.grantLoot(r.playerId, r.items, r.gold, "act:boss").catch((e) => { logger.warn({ err: e }, "Loi thuong trum"); return null; });
      if (!g) continue;
      await notify(r.playerId, { type: "boss_reward", kind: b.kind, rank: r.rank, share: Math.round(r.share * 100), gold: g.gold, items: g.items });
      await notify(r.playerId, { type: "econ_sync" });
    }
    await bus.publish(TOPIC, { k: "boss_hp", id: b.id, hp: 0 });
    await bus.publish(TOPIC, { k: "boss_end", id: b.id, killed: true, top });
  }

  // Trum ham nguc: 1 con cho moi to doi (hoac 1 nguoi di le), mau theo so thanh vien.
  const dungeonKey = (conn) => (conn.party?.id ? `p${conn.party.id}` : `s${conn.playerId}`);
  const inDungeon = (conn) => near(conn, boss.DUNGEON_X, boss.DUNGEON_Z, boss.DUNGEON_JOIN_RANGE) && (conn.pos[1] || 0) >= boss.DUNGEON_MIN_Y;
  const today = () => calendar.dayAt(now());

  async function dungeonJoin(conn) {
    if (!inDungeon(conn)) return err(conn, "Hay vao phong Rong Co Dai trong Ham Nguc Co.");
    if (await store.getJson(`dgr:${conn.playerId}:${today()}`)) return send(conn, { t: "dboss", state: "done" });
    const key = dungeonKey(conn);
    let run = await store.getJson(`dg:${key}`);
    if (!run || run.until <= now() || run.done) {
      const n = conn.party?.members?.length || 1;
      run = { key, id: crypto.randomBytes(5).toString("hex"), maxHp: boss.dungeonHp(n), size: n, until: now() + boss.DUNGEON_STAY_MS, done: false };
      await store.setJson(`dg:${key}`, run);
      await store.hpInit(`dg:${run.id}`, run.maxHp);
    }
    await store.addMember(`dg:${run.id}`, conn.playerId);
    send(conn, { t: "dboss", state: "fight", id: run.id, hp: Math.max(0, await store.hpGet(`dg:${run.id}`) ?? run.maxHp), maxHp: run.maxHp, size: run.size });
  }

  async function dungeonHit(conn, msg) {
    const run = await store.getJson(`dg:${dungeonKey(conn)}`);
    if (!run || run.id !== msg.id || run.done || run.until <= now() || !inDungeon(conn) || !hitOk(conn)) return;
    const dmg = boss.clampHit(msg.dmg);
    if (!dmg) return;
    const key = `dg:${run.id}`;
    const hp = await store.hpHit(key, conn.playerId, dmg);
    if (hp == null) return;
    const mem = await store.membersOf(key);
    if (hp > 0) {
      throttled(key, () => { for (const pid of mem) notify(pid, { type: "dboss_hp", id: run.id, hp }).catch(() => {}); });
      return;
    }
    if (!(await store.once(`dgdone:${run.id}`))) return;
    run.done = true;
    await store.setJson(`dg:${run.key}`, run);
    const def = catalog.enemies.get(boss.DUNGEON_BOSS);
    const gold = boss.dungeonGold(Math.max(run.size, mem.length));
    for (const pid of mem) {
      let reward = null;
      if (await store.once(`dgr:${pid}:${today()}`)) {
        await store.setJson(`dgr:${pid}:${today()}`, 1);
        reward = await worldService.grantLoot(pid, boss.rollBossLoot(def, rand), gold, "act:dungeon").catch((e) => { logger.warn({ err: e }, "Loi thuong ham nguc"); return null; });
      }
      await notify(pid, { type: "dboss_end", id: run.id, party: mem.length, gold: reward?.gold || 0, items: reward?.items || {}, rewarded: !!reward });
      if (reward) await notify(pid, { type: "econ_sync" });
    }
  }

  async function handle(conn, msg) {
    if (msg.t === "drop_open") { await openDrop(conn, msg); return true; }
    if (msg.t === "caravan_buy") { await caravanBuy(conn, msg); return true; }
    if (msg.t === "boss_hit") { await bossHit(conn, msg); return true; }
    if (msg.t === "dboss_join") { await dungeonJoin(conn); return true; }
    if (msg.t === "dboss_hit") { await dungeonHit(conn, msg); return true; }
    return false;
  }

  return {
    handle, onWelcome, tick, ready,
    close: () => {
      clearInterval(timer);
      for (const p of pubTimers.values()) clearTimeout(p.timer);
      bus.unsubscribe(TOPIC, onTopic).catch(() => {});
    },
    // Cho test: ep tao su kien ngay.
    _force: async (patch) => {
      const s = (await store.load()) || empty();
      Object.assign(s, patch);
      await store.save(s);
      await tick(true);
    },
  };
}

module.exports = { attachWorldEvents };
