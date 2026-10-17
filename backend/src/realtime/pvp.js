// PvP the gioi mo + can cu + to doi (gan vao gateway.js). Moi quyet dinh quan trong do SERVER lam:
//  - Mau PvP: server giu conn.hp (client bao mau do quai/thuoc, server chi cho TANG cham - xem
//    pvpRules.acceptHp). Sat thuong nguoi-danh-nguoi tinh tu vu khi/giap THAT tren server.
//  - Danh: gateway nguoi danh kiem tam (vi tri server biet), hoi chieu -> gui "pvp_damage" qua topic
//    user:<muc tieu> -> gateway CUA MUC TIEU tru mau, kiem dong doi/bang hoi (khong danh nhau duoc),
//    bao ve hoi sinh, va xu ly cai chet (chay duoc khi 2 nguoi o 2 gateway khac nhau).
//  - Chet: xin client danh sach tui chinh (sau khi client day het op kinh te) roi CHOT do roi trong
//    1 transaction (min(tui bao, so huu that)) -> tui do tai cho chet, nguoi ha uu tien nhat 60s.
//  - Can cu: xay/danh/sua/go/ruong deu qua pvpService (transaction), phat thay doi theo topic vung
//    zt:<zone> cho MOI kenh cua vung (cong trinh la the gioi chung, khong theo kenh).
//
// Tin client -> server (JSON): hp, bag, death_ack, pvp_hit, bag_loot, struct_place, struct_hit,
//   struct_repair, struct_remove, chest_open, chest_move, party_invite, party_accept, party_leave,
//   heal_ally, structs_mine.
// Tin server -> client: {t:"bags"|"structs"|"chest"}, {t:"bag"|"bag_gone"|"struct"|"struct_gone"|"hpbar"},
//   {t:"event", e:{type:"pvp_hurt"|"pvp_dead_pending"|"pvp_dead"|"pvp_kill"|"bag_looted"|"healed"|
//   "heal_done"|"party_invite"|"party_changed"|"party_pos"|"base_attacked"|"base_destroyed"|"econ_sync"}}
const bountyService = require("../services/bountyService");
const crypto = require("crypto");
const rules = require("../game/pvpRules");
const pvpService = require("../services/pvpService");
const progressService = require("../services/progressService");
const socialService = require("../services/socialService");
const characterService = require("../services/characterService");
const { logger } = require("../utils/logger");
const { catalog } = require("../game/catalog");

const BAG_VIEW_RADIUS = 4000;
const STRUCT_VIEW_RADIUS = 3500;
const STRUCT_REQUERY_MOVE = 1200;
const POWER_TTL_MS = 30000;
const GUILD_TTL_MS = 60000;
const ATTACK_NOTIFY_MS = 30000;

// ==== To doi (toi da PARTY_MAX nguoi) - LUU TRONG REDIS (AOF, xem docker-compose) ====
// party:<id>      -> { id, leader, members }
// pmember:<pid>   -> id to doi cua nguoi choi
// Rot mang / gateway khoi dong lai / trien khai ban moi KHONG lam roi to doi: luc dang nhap lai
// gateway doc pmember:<id> va tra lai to doi (chi roi khi bam "Roi to doi" hoac het han PARTY_TTL
// khong ai online). Khong co Redis (ROLE=all, dev) thi giu trong bo nho cua process.
const PARTY_TTL = 3 * 86400;

function memoryParties() {
  const m = new Map();
  const member = new Map();
  return {
    async create(leader) { const id = crypto.randomBytes(5).toString("hex"); m.set(id, { id, leader, members: [leader] }); member.set(leader, id); return m.get(id); },
    async get(id) { return m.get(id) || null; },
    async save(p) {
      if (p.members.length) m.set(p.id, p); else m.delete(p.id);
      for (const id of p.members) member.set(id, p.id);
    },
    async partyOf(playerId) { const id = member.get(playerId); const p = id ? m.get(id) : null; return p && p.members.includes(playerId) ? p : null; },
    async unlink(playerId) { member.delete(playerId); },
  };
}

function redisParties(redis) {
  const key = (id) => `party:${id}`;
  const mkey = (pid) => `pmember:${pid}`;
  return {
    async create(leader) {
      const p = { id: crypto.randomBytes(5).toString("hex"), leader, members: [leader] };
      await redis.multi().set(key(p.id), JSON.stringify(p), "EX", PARTY_TTL).set(mkey(leader), p.id, "EX", PARTY_TTL).exec();
      return p;
    },
    async get(id) { const s = await redis.get(key(id)); return s ? JSON.parse(s) : null; },
    async save(p) {
      const tx = redis.multi();
      if (p.members.length) tx.set(key(p.id), JSON.stringify(p), "EX", PARTY_TTL);
      else tx.del(key(p.id));
      for (const id of p.members) tx.set(mkey(id), p.id, "EX", PARTY_TTL);
      await tx.exec();
    },
    async partyOf(playerId) {
      const id = await redis.get(mkey(playerId));
      if (!id) return null;
      const s = await redis.get(key(id));
      const p = s ? JSON.parse(s) : null;
      if (!p || !p.members.includes(playerId)) return null;
      // Dang nhap lai -> gia han to doi (ca nhom con choi thi khong bao gio het han).
      await redis.multi().expire(key(id), PARTY_TTL).expire(mkey(playerId), PARTY_TTL).exec();
      return p;
    },
    async unlink(playerId) { await redis.del(mkey(playerId)); },
  };
}

function attachPvp(ctx) {
  const { bus, send, localByPlayer, posCache, notify } = ctx;
  const parties = bus.redis ? redisParties(bus.redis) : memoryParties();
  const zoneMembers = new Map();  // zone -> Set<conn> (nguoi cua gateway nay trong vung)
  const zoneHandlers = new Map(); // zone -> handler topic zt:<zone>
  const structCache = new Map();  // id -> structure (vi tri de kiem tam)
  const bagCache = new Map();     // id -> bag
  const guildCache = new Map();   // playerId -> { id, at }
  const lastAttackNotify = new Map(); // structureId -> ms
  const deathTimers = new Map();  // playerId -> timeout

  const zoneTopic = (zone) => `zt:${zone}`;
  const channelTopic = (conn) => ctx.topicOf(conn.zone, conn.channel);
  const err = (conn, msg) => send(conn, { t: "error", msg });
  const now = () => Date.now();
  const near = (conn, x, z, range) => rules.dist2d(conn.pos[0], conn.pos[2], x, z) <= range;
  const safeAt = (x, z) => rules.inSafeZone(catalog.markets, x, z);
  // Dang cho hoi sinh (mat giuong) -> khong lam gi duoc trong the gioi.
  const waiting = (conn) => (conn.respawnAt || 0) > now();

  async function power(conn) {
    if (conn.power && now() - conn.power.at < POWER_TTL_MS) return conn.power;
    const p = await progressService.duelPower(conn.playerId);
    conn.power = { ...p, at: now() };
    return conn.power;
  }

  async function guildOf(playerId) {
    const c = guildCache.get(playerId);
    if (c && now() - c.at < GUILD_TTL_MS) return c.id;
    const g = await socialService.memberOf(playerId);
    guildCache.set(playerId, { id: g?.id || 0, at: now() });
    return g?.id || 0;
  }

  // Chu cong trinh, cung bang hoi hoac cung to doi voi chu -> duoc sua/mo ruong, KHONG duoc pha.
  async function isAlly(conn, ownerId) {
    if (ownerId === conn.playerId) return true;
    if (conn.party && conn.party.members.includes(ownerId)) return true;
    return !!conn.guildId && (await guildOf(ownerId)) === conn.guildId;
  }

  // ==== Vung (topic zt:<zone>) ====
  async function onJoined(conn) {
    let set = zoneMembers.get(conn.zone);
    if (!set) {
      set = new Set();
      zoneMembers.set(conn.zone, set);
      const zone = conn.zone;
      const h = (msg) => onZoneMessage(zone, msg);
      zoneHandlers.set(zone, h);
      await bus.subscribe(zoneTopic(zone), h);
    }
    set.add(conn);
    conn.structQueryPos = null;
    await sendNearby(conn).catch((e) => logger.warn({ err: e }, "Loi tai cong trinh/tui do quanh nguoi choi"));
  }

  async function onLeft(conn, zone) {
    const set = zoneMembers.get(zone);
    if (!set) return;
    set.delete(conn);
    if (set.size) return;
    zoneMembers.delete(zone);
    const h = zoneHandlers.get(zone);
    zoneHandlers.delete(zone);
    await bus.unsubscribe(zoneTopic(zone), h);
  }

  async function sendNearby(conn) {
    if (!conn.zone) return;
    const [x, , z] = conn.pos;
    conn.structQueryPos = [x, z];
    const [structs, bags] = await Promise.all([
      pvpService.structuresNear(conn.zone, x, z, STRUCT_VIEW_RADIUS),
      pvpService.bagsNear(conn.zone, conn.channel, x, z, BAG_VIEW_RADIUS),
    ]);
    for (const s of structs) structCache.set(s.id, s);
    for (const b of bags) bagCache.set(b.id, b);
    send(conn, { t: "structs", list: structs });
    send(conn, { t: "bags", list: bags });
  }

  function onZoneMessage(zone, msg) {
    const set = zoneMembers.get(zone);
    if (!set) return;
    if (msg.k === "struct") {
      structCache.set(msg.s.id, msg.s);
      for (const c of set) if (near(c, msg.s.x, msg.s.z, STRUCT_VIEW_RADIUS)) send(c, { t: "struct", s: msg.s });
    } else if (msg.k === "struct_gone") {
      structCache.delete(msg.id);
      for (const c of set) send(c, { t: "struct_gone", id: msg.id });
    }
  }

  // Tin tren topic kenh (gateway goi tu onChannelMessage) - tui do va thanh mau.
  function onChannelMessage(members, msg) {
    if (msg.k === "bag") {
      bagCache.set(msg.bag.id, msg.bag);
      for (const c of members) send(c, { t: "bag", bag: msg.bag });
      return true;
    }
    if (msg.k === "bag_gone") {
      bagCache.delete(msg.id);
      for (const c of members) send(c, { t: "bag_gone", id: msg.id });
      return true;
    }
    if (msg.k === "shot") {
      const raw = JSON.stringify({ t: "shot", id: msg.id, tx: msg.tx, ty: msg.ty, tz: msg.tz });
      for (const c of members) if (c.playerId !== msg.id) send(c, raw);
      return true;
    }
    if (msg.k === "hpbar") {
      const raw = JSON.stringify({ t: "hpbar", id: msg.id, hp: msg.hp, max: msg.max });
      for (const c of members) if (c.playerId !== msg.id) send(c, raw);
      return true;
    }
    return false;
  }

  // ==== Su kien ca nhan (topic user:<id>) - xu ly TRUOC khi chuyen cho client ====
  // Tra ve true neu da xu ly xong (khong chuyen tiep nguyen ban).
  async function onUserEvent(conn, e) {
    switch (e.type) {
      case "pvp_damage": await receiveDamage(conn, e); return true;
      case "healed":
        if (conn.dead || conn.hp == null) return true;
        conn.hp = Math.min(conn.maxHp, conn.hp + e.amount);
        send(conn, { t: "event", e: { type: "healed", byName: e.byName, amount: e.amount, hp: conn.hp } });
        publishHpBar(conn);
        return true;
      case "party_changed":
        conn.party = e.party && e.party.members.includes(conn.playerId) ? e.party : null;
        await republishSelf(conn);
        return false;
      default: return false;
    }
  }

  function publishHpBar(conn) {
    if (conn.zone) bus.publish(channelTopic(conn), { k: "hpbar", id: conn.playerId, hp: conn.hp, max: conn.maxHp }).catch(() => {});
  }

  async function receiveDamage(conn, e) {
    if (!conn.zone || conn.zone !== e.zone || conn.channel !== e.channel) return;
    if (conn.dead || (conn.protectedUntil || 0) > now()) return;
    if (e.party && conn.party && conn.party.id === e.party) return;
    if (e.guild && conn.guildId && e.guild === conn.guildId) return;
    if (conn.duelId) return; // dang dau truong: chi sat thuong dau truong tinh
    if (conn.hp != null && conn.hp <= 0) return; // da guc vi ly do khac (quai...) - khong roi do PvP
    const def = (await power(conn)).defense;
    const mh = rules.mountedHit({ attackerMounted: !!e.mounted, attackerSpeed: e.speed || 0, ranged: !!e.ranged, fin: !!e.fin, targetMounted: !!conn.mounted });
    const dmg = Math.max(1, Math.round(rules.pvpDamage(e.attack, def, !!e.fin) * mh.mult));
    if (conn.hp == null) { conn.maxHp = conn.maxHp || 100; conn.hp = conn.maxHp; }
    conn.hp = Math.max(0, conn.hp - dmg);
    conn.lastHitBy = { id: e.by, name: e.byName };
    // Giap dang mac (tot nhat con dung duoc) mon 1 diem moi don - server tu tinh, ghi DB theo dot.
    const armorId = (await power(conn)).armorId;
    if (armorId) (conn.wearPending ??= {})[armorId] = (conn.wearPending[armorId] || 0) + 1;
    send(conn, { t: "event", e: { type: "pvp_hurt", dmg, byName: e.byName, by: e.by, hp: conn.hp, ranged: !!e.ranged, charge: mh.charge, knock: mh.knock } });
    publishHpBar(conn);
    if (conn.hp <= 0) startDeath(conn);
  }

  // Bat dau chet: xin client danh sach tui (client day op kinh te truoc roi moi gui death_ack).
  function startDeath(conn) {
    conn.dead = true;
    conn.manifestAtDeath = null;
    send(conn, { t: "event", e: { type: "pvp_dead_pending", byName: conn.lastHitBy?.name || "" } });
    deathTimers.set(conn.playerId, setTimeout(() => finalizeDeath(conn).catch((err2) => logger.warn({ err: err2 }, "Loi chot do roi")), rules.DEATH_ACK_TIMEOUT_MS));
  }

  async function finalizeDeath(conn) {
    const t = deathTimers.get(conn.playerId);
    if (t === undefined) return; // da chot roi
    clearTimeout(t);
    deathTimers.delete(conn.playerId);
    const killer = conn.lastHitBy || { id: null, name: "" };
    let result = null;
    if (conn.zone) {
      result = await pvpService.dropDeathBag({
        loserId: conn.playerId, loserName: conn.profile.displayName, killerId: killer.id,
        manifest: conn.manifestAtDeath || conn.manifest, zone: conn.zone, channel: conn.channel, x: conn.pos[0], z: conn.pos[2],
      });
    }
    if (result?.bag && conn.zone) await bus.publish(channelTopic(conn), { k: "bag", bag: result.bag });
    const respawn = await decideRespawn(conn, conn.pos[0], conn.pos[2]);
    // Truy na: ha ke bi truy na -> lanh thuong; ha nguoi vo toi -> ke ha bi treo thuong.
    const bounty = killer.id ? await bountyService.onKill(killer.id, conn.playerId).catch((e) => { logger.warn({ err: e }, "Loi truy na"); return null; }) : null;
    send(conn, { t: "event", e: { type: "pvp_dead", killerName: killer.name, lost: result?.items || {}, respawn, bountyLost: bounty?.claimed || 0 } });
    if (killer.id) {
      await notify(killer.id, {
        type: "pvp_kill", victimName: conn.profile.displayName, bagId: result?.bag?.id || 0,
        bountyClaimed: bounty?.claimed || 0, bountyAdded: bounty?.added || 0, bountyNow: bounty?.claimed ? bounty.killerBounty : (bounty?.killerBounty || 0),
      });
      if (bounty?.claimed) await notify(killer.id, { type: "econ_sync" });
    }
    conn.dead = false;
    conn.hp = conn.maxHp || 100;
    conn.manifest = {};
    conn.protectedUntil = now() + rules.SPAWN_PROTECT_MS + 3000; // + thoi gian hoat canh nga/hoi sinh
    publishHpBar(conn);
  }

  // Quyet dinh noi/thoi diem hoi sinh (giuong / nong trai / doi 1 gio neu vua mat giuong).
  async function decideRespawn(conn, x, z) {
    const d = await pvpService.decideRespawn(conn.playerId, x, z).catch((e) => { logger.warn({ err: e }, "Loi quyet dinh hoi sinh"); return { mode: "farm", readyAt: now() }; });
    conn.respawnAt = d.mode === "wait" ? d.readyAt : 0;
    return d;
  }

  async function onWelcome(conn) {
    // Khoi phuc to doi (sau rot mang / gateway khoi dong lai).
    const party = await parties.partyOf(conn.playerId).catch((e) => { logger.warn({ err: e }, "Loi doc to doi"); return null; });
    if (party) {
      conn.party = party;
      send(conn, { t: "event", e: { type: "party_changed", party } });
    }
    const st = await pvpService.respawnState(conn.playerId).catch(() => null);
    if (!st) return;
    conn.respawnAt = st.readyAt;
    send(conn, { t: "event", e: { type: "respawn", respawn: st } });
  }

  async function republishSelf(conn) {
    if (!conn.zone) return;
    await ctx.republish(conn);
  }

  // ==== Xu ly tin client ====
  async function handle(conn, msg) {
    switch (msg.t) {
      case "hp": {
        if (!conn.limits.hp() || conn.dead) return true;
        const r = rules.acceptHp(conn.hp, msg.hp, msg.max);
        conn.hp = r.hp;
        conn.maxHp = r.max;
        return true;
      }
      case "bag":
        if (conn.limits.bag()) conn.manifest = msg.items && typeof msg.items === "object" ? msg.items : {};
        return true;
      case "death_ack":
        if (deathTimers.has(conn.playerId)) {
          conn.manifestAtDeath = msg.items && typeof msg.items === "object" ? msg.items : {};
          await finalizeDeath(conn);
        }
        return true;
      case "died": {
        // Chet vi quai/roi/... (client bao) - chet PvP da co pvp_dead kem quyet dinh hoi sinh.
        if (!conn.limits.build() || deathTimers.has(conn.playerId)) return true;
        const respawn = await decideRespawn(conn, conn.pos[0], conn.pos[2]);
        send(conn, { t: "event", e: { type: "respawn", respawn } });
        return true;
      }
      case "shot": return shot(conn, msg).then(() => true);
      case "pvp_hit": return pvpHit(conn, msg).then(() => true);
      case "bag_loot": return lootBag(conn, msg).then(() => true);
      case "struct_place": return structPlace(conn, msg).then(() => true);
      case "struct_hit": return structHit(conn, msg).then(() => true);
      case "struct_repair": return structRepair(conn, msg).then(() => true);
      case "struct_remove": return structRemove(conn, msg).then(() => true);
      case "chest_open": return chestOpen(conn, msg).then(() => true);
      case "chest_move": return chestMove(conn, msg).then(() => true);
      case "party_invite": return partyInvite(conn, msg).then(() => true);
      case "party_accept": return partyAccept(conn, msg).then(() => true);
      case "party_leave": return partyLeave(conn).then(() => true);
      case "heal_ally": return healAlly(conn, msg).then(() => true);
      case "structs_mine":
        send(conn, { t: "structs_mine", list: await pvpService.myStructures(conn.playerId) });
        return true;
      default: return false;
    }
  }

  // Loi nghiep vu (AppError) -> bao client; loi khac -> nem tiep (gateway ghi log).
  async function guarded(conn, fn) {
    try { return await fn(); } catch (e) {
      if (e.isOperational) { err(conn, e.message); return null; }
      throw e;
    }
  }

  // Hieu ung mui ten bay (de nguoi cung kenh thay ai dang ban ai) - chi hinh anh, sat thuong o pvp_hit.
  async function shot(conn, msg) {
    if (!conn.zone || conn.dead || waiting(conn) || !conn.limits.shot()) return;
    const tx = Number(msg.tx), ty = Number(msg.ty), tz = Number(msg.tz);
    if (![tx, ty, tz].every(Number.isFinite) || !near(conn, tx, tz, 1400)) return;
    reveal(conn);
    await bus.publish(channelTopic(conn), { k: "shot", id: conn.playerId, tx, ty, tz });
  }

  async function pvpHit(conn, msg) {
    if (!conn.zone || conn.dead || waiting(conn) || !Number.isInteger(msg.target) || msg.target === conn.playerId) return;
    const ranged = !!msg.ranged;
    if (now() - (conn.lastPvpHit || 0) < (ranged ? rules.SHOT_COOLDOWN_MS : rules.HIT_COOLDOWN_MS)) return;
    // Chi danh nguoi CUNG kenh (posCache ghi kem kenh) va trong tam theo vi tri server biet.
    const tp = posCache.get(msg.target);
    if (!tp || tp[2] !== `${conn.zone}:${conn.channel}`) return;
    if (conn.party && conn.party.members.includes(msg.target)) return;
    if (safeAt(conn.pos[0], conn.pos[2]) || safeAt(tp[0], tp[1])) return err(conn, "Khu cho la vung an toan - khong the tan cong.");
    const p = await power(conn);
    // Cung: phai co cung + con mui ten (so huu that tren server), tam theo loai cung.
    if (ranged && (p.rangedAttack <= 0 || p.arrows <= 0)) return;
    const reach = ranged ? p.rangedRange + rules.RANGED_SLACK : rules.PVP_RANGE + 30 + (conn.mounted ? rules.MOUNTED_REACH_BONUS : 0);
    if (!near(conn, tp[0], tp[1], reach)) return;
    conn.lastPvpHit = now();
    conn.protectedUntil = 0; // tu danh nguoi -> mat bao ve hoi sinh
    reveal(conn);
    await notify(msg.target, {
      type: "pvp_damage", by: conn.playerId, byName: conn.profile.displayName, attack: ranged ? p.rangedAttack : p.attack, fin: !ranged && !!msg.fin, ranged,
      guild: conn.guildId || 0, party: conn.party?.id || null, zone: conn.zone, channel: conn.channel,
      mounted: !!conn.mounted, speed: conn.speed || 0,
    });
  }

  async function lootBag(conn, msg) {
    const id = Number(msg.id);
    if (!conn.zone || conn.dead || !Number.isInteger(id)) return;
    const cached = bagCache.get(id);
    if (cached && !near(conn, cached.x, cached.z, rules.INTERACT_RANGE)) return err(conn, "Lai gan tui do hon.");
    const r = await guarded(conn, () => pvpService.lootBag(id, conn.playerId));
    if (!r) return;
    if (!near(conn, r.bag.x, r.bag.z, rules.INTERACT_RANGE + 60)) logger.warn({ playerId: conn.playerId, id }, "Nhat tui o xa");
    bagCache.delete(id);
    send(conn, { t: "event", e: { type: "bag_looted", items: r.items, gold: r.gold, ownerName: r.bag.ownerName } });
    await bus.publish(channelTopic(conn), { k: "bag_gone", id });
  }

  async function structPlace(conn, msg) {
    if (!conn.zone || conn.dead || waiting(conn) || !conn.limits.build()) return;
    const x = Number(msg.x), z = Number(msg.z);
    if (!Number.isFinite(x) || !Number.isFinite(z) || !near(conn, x, z, rules.PLACE_RANGE)) return err(conn, "Dat qua xa.");
    if (safeAt(x, z)) return err(conn, "Khong the xay trong khu cho.");
    const item = typeof msg.item === "string" ? msg.item : "";
    const s = await guarded(conn, () => pvpService.placeStructure(conn.playerId, item, conn.zone, x, z, Number(msg.rot) || 0));
    if (!s) return;
    s.ownerName = conn.profile.displayName;
    send(conn, { t: "event", e: { type: "econ_sync" } });
    await bus.publish(zoneTopic(conn.zone), { k: "struct", s });
  }

  async function findStructure(id) {
    return structCache.get(id) || (await pvpService.getStructure(id));
  }

  async function structHit(conn, msg) {
    const id = Number(msg.id);
    if (!conn.zone || conn.dead || !Number.isInteger(id)) return;
    if (now() - (conn.lastStructHit || 0) < rules.STRUCT_HIT_COOLDOWN_MS) return;
    const s = await findStructure(id);
    if (!s || waiting(conn) || !near(conn, s.x, s.z, rules.INTERACT_RANGE + 30)) return;
    if (await isAlly(conn, s.ownerId)) return err(conn, "Khong the pha cong trinh cua minh/dong doi.");
    if (safeAt(s.x, s.z)) return;
    reveal(conn);
    conn.lastStructHit = now();
    const dmg = rules.structureDamage((await power(conn)).attack);
    const r = await guarded(conn, () => pvpService.damageStructure(id, conn.playerId, dmg, conn.channel));
    if (!r) return;
    r.structure.ownerName = s.ownerName;
    if (r.destroyed) {
      await bus.publish(zoneTopic(conn.zone), { k: "struct_gone", id });
      if (r.bag) await bus.publish(channelTopic(conn), { k: "bag", bag: r.bag });
      await notify(s.ownerId, { type: "base_destroyed", kind: s.kind, x: s.x, z: s.z, byName: conn.profile.displayName });
    } else {
      await bus.publish(zoneTopic(conn.zone), { k: "struct", s: r.structure });
      if (now() - (lastAttackNotify.get(id) || 0) > ATTACK_NOTIFY_MS) {
        lastAttackNotify.set(id, now());
        await notify(s.ownerId, { type: "base_attacked", kind: s.kind, x: s.x, z: s.z, byName: conn.profile.displayName });
      }
    }
  }

  async function structRepair(conn, msg) {
    const id = Number(msg.id);
    const s = Number.isInteger(id) ? await findStructure(id) : null;
    if (!s || !conn.zone || !near(conn, s.x, s.z, rules.INTERACT_RANGE + 30)) return;
    if (!(await isAlly(conn, s.ownerId))) return err(conn, "Chi chu va dong doi moi sua duoc.");
    const r = await guarded(conn, () => pvpService.repairStructure(id, conn.playerId));
    if (!r) return;
    r.ownerName = s.ownerName;
    send(conn, { t: "event", e: { type: "econ_sync" } });
    await bus.publish(zoneTopic(conn.zone), { k: "struct", s: r });
  }

  async function structRemove(conn, msg) {
    const id = Number(msg.id);
    const s = Number.isInteger(id) ? await findStructure(id) : null;
    if (!s || !conn.zone || !near(conn, s.x, s.z, rules.INTERACT_RANGE + 30)) return;
    const r = await guarded(conn, () => pvpService.removeStructure(id, conn.playerId));
    if (!r) return;
    send(conn, { t: "event", e: { type: "econ_sync" } });
    await bus.publish(zoneTopic(conn.zone), { k: "struct_gone", id });
  }

  async function chestAccess(conn, id) {
    const s = Number.isInteger(id) ? await findStructure(id) : null;
    if (!s || s.kind !== "chest" || !conn.zone || !near(conn, s.x, s.z, rules.INTERACT_RANGE + 30)) return null;
    if (!(await isAlly(conn, s.ownerId))) { err(conn, "Ruong cua nguoi khac - pha ruong de lay do."); return null; }
    return s;
  }

  async function chestOpen(conn, msg) {
    const id = Number(msg.id);
    if (!(await chestAccess(conn, id))) return;
    send(conn, { t: "chest", id, items: await pvpService.chestContents(id) });
  }

  async function chestMove(conn, msg) {
    const id = Number(msg.id);
    if (!conn.limits.build() || !(await chestAccess(conn, id))) return;
    const items = await guarded(conn, () => pvpService.chestMove(id, conn.playerId, String(msg.item || ""), Number(msg.n)));
    if (!items) return;
    send(conn, { t: "chest", id, items });
    send(conn, { t: "event", e: { type: "econ_sync" } });
  }

  // ==== An nap ====
  // Client bao "dang an" (h=1) moi goi vi tri; server CHI chap nhan khi that su dung trong bui co cao
  // (ham tat dinh pvpRules.inBush, giong het client) hoac trong leu, va khong vua tan cong.
  function updateHidden(conn, want) {
    let hidden = false;
    if (want && conn.zone && !rules.HIDE_ZONES_EXCLUDED(conn.zone) && now() >= (conn.revealUntil || 0) && !conn.dead) {
      const [x, , z] = conn.pos;
      hidden = rules.inBush(x, z) || [...structCache.values()].some((st) => st.kind === "tent" && st.zone === conn.zone && rules.dist2d(st.x, st.z, x, z) < 34);
    }
    if (hidden !== !!conn.hidden) { conn.hidden = hidden; conn.moved = true; }
    return hidden;
  }

  function reveal(conn) {
    conn.revealUntil = now() + rules.REVEAL_AFTER_ATTACK_MS;
    if (conn.hidden) { conn.hidden = false; conn.moved = true; }
  }

  // ==== To doi ====
  async function broadcastParty(p) {
    for (const id of p.members) await notify(id, { type: "party_changed", party: p });
  }

  async function partyInvite(conn, msg) {
    if (typeof msg.to !== "string") return;
    const target = await characterService.findByName(msg.to.slice(0, 20));
    if (!target || target.playerId === conn.playerId) return err(conn, "Khong tim thay nguoi choi.");
    let p = conn.party ? await parties.get(conn.party.id) : null;
    if (!p) { p = await parties.create(conn.playerId); conn.party = p; await broadcastParty(p); }
    if (p.members.length >= rules.PARTY_MAX) return err(conn, `To doi toi da ${rules.PARTY_MAX} nguoi.`);
    if (p.members.includes(target.playerId)) return err(conn, "Nguoi nay da o trong to doi.");
    await notify(target.playerId, { type: "party_invite", partyId: p.id, fromName: conn.profile.displayName });
    send(conn, { t: "event", e: { type: "party_invited", toName: target.displayName } });
  }

  async function partyAccept(conn, msg) {
    const p = typeof msg.partyId === "string" ? await parties.get(msg.partyId) : null;
    if (!p) return err(conn, "To doi khong con.");
    if (p.members.length >= rules.PARTY_MAX) return err(conn, "To doi da day.");
    if (conn.party && conn.party.id !== p.id) await partyLeave(conn);
    if (!p.members.includes(conn.playerId)) p.members.push(conn.playerId);
    await parties.save(p);
    conn.party = p;
    await broadcastParty(p);
  }

  async function partyLeave(conn) {
    if (!conn.party) return;
    const p = await parties.get(conn.party.id);
    conn.party = null;
    if (!p) return;
    p.members = p.members.filter((id) => id !== conn.playerId);
    if (p.leader === conn.playerId) p.leader = p.members[0] || 0;
    await parties.save(p);
    await parties.unlink(conn.playerId);
    await notify(conn.playerId, { type: "party_changed", party: null });
    // Con 1 nguoi -> giai tan.
    if (p.members.length === 1) {
      const last = p.members[0];
      p.members = [];
      await parties.save(p);
      await parties.unlink(last);
      await notify(last, { type: "party_changed", party: null });
    }
    else await broadcastParty(p);
  }

  async function healAlly(conn, msg) {
    if (!conn.zone || conn.dead || waiting(conn) || !Number.isInteger(msg.target) || msg.target === conn.playerId) return;
    if (now() - (conn.lastHeal || 0) < rules.HEAL_COOLDOWN_MS) return err(conn, "Cho hoi chieu ho tro.");
    const tp = posCache.get(msg.target);
    if (!tp || tp[2] !== `${conn.zone}:${conn.channel}` || !near(conn, tp[0], tp[1], rules.HEAL_RANGE)) return err(conn, "Lai gan nguoi can ho tro hon.");
    conn.lastHeal = now();
    if (!(await guarded(conn, () => pvpService.consumeHealPotion(conn.playerId)))) return;
    await notify(msg.target, { type: "healed", by: conn.playerId, byName: conn.profile.displayName, amount: rules.HEAL_AMOUNT });
    send(conn, { t: "event", e: { type: "heal_done", amount: rules.HEAL_AMOUNT } });
    send(conn, { t: "event", e: { type: "econ_sync" } });
  }

  // ==== Nhip dinh ky ====
  // Vi tri to doi (moi giay, bat ke vung/kenh) + tai lai cong trinh/tui khi di xa.
  const partyTick = setInterval(() => {
    for (const c of localByPlayer.values()) {
      if (!c.party || c.party.members.length < 2) continue;
      const e = { type: "party_pos", id: c.playerId, name: c.profile.displayName, x: c.pos[0], z: c.pos[2], zone: c.zone || "", hp: c.hp ?? 0, max: c.maxHp || 100 };
      for (const id of c.party.members) if (id !== c.playerId) notify(id, e).catch(() => {});
    }
  }, 1000);

  const areaTick = setInterval(() => {
    for (const c of localByPlayer.values()) {
      if (!c.zone || !c.structQueryPos) continue;
      if (rules.dist2d(c.pos[0], c.pos[2], c.structQueryPos[0], c.structQueryPos[1]) > STRUCT_REQUERY_MOVE)
        sendNearby(c).catch((e) => logger.warn({ err: e }, "Loi tai cong trinh"));
    }
  }, 2000);

  // Ghi hao mon do ben da gom (10s/lan + luc ngat ket noi) - khong ghi DB moi don danh.
  async function flushWear(c) {
    if (!c.wearPending) return;
    const w = c.wearPending;
    c.wearPending = null;
    await pvpService.wearItems(c.playerId, w).catch((e) => logger.warn({ err: e }, "Loi ghi hao mon"));
    c.power = null;
  }
  const wearTick = setInterval(() => { for (const c of localByPlayer.values()) flushWear(c); }, 10000);

  const purgeTick = setInterval(() => {
    pvpService.purgeBags().then((gone) => {
      for (const b of gone) {
        bagCache.delete(Number(b.id));
        bus.publish(ctx.topicOf(b.zone, b.channel), { k: "bag_gone", id: Number(b.id) }).catch(() => {});
      }
    }).catch(() => {});
  }, 30000);

  async function onDisconnect(conn) {
    // Roi mang giua luc dang chet -> van chot do roi (khong the thoat game de giu do).
    if (deathTimers.has(conn.playerId)) await finalizeDeath(conn).catch(() => {});
    await flushWear(conn);
    // KHONG roi to doi khi mat ket noi - to doi nam trong Redis, dang nhap lai se duoc tra ve.
  }

  function close() {
    clearInterval(partyTick);
    clearInterval(areaTick);
    clearInterval(purgeTick);
    clearInterval(wearTick);
    for (const t of deathTimers.values()) clearTimeout(t);
  }

  return {
    handle, onJoined, onLeft, onChannelMessage, onUserEvent, onDisconnect, onWelcome, updateHidden, close,
    publicExtra: (conn) => ({ gid: conn.guildId || 0, pid: conn.party?.id || "", hd: conn.hidden ? 1 : 0 }),
  };
}

module.exports = { attachPvp, memoryParties };
