// Gateway WebSocket (/ws): ket noi thoi gian thuc cho nguoi choi - hien dien, vi tri, chat, thong bao
// giao dich/cho. KHONG xu ly kinh te (vang/vat pham luon di qua REST API co kiem tra).
//
// Giao thuc (JSON):
//  client -> server:
//    {t:"auth", token}                    (bat buoc trong 5s dau)
//    {t:"join", zone, channel?}           vao 1 vung (server chon kenh con cho)
//    {t:"leave"}                          roi vung (vd ve nong trai rieng)
//    {t:"pos", x, y, z, r, a}             vi tri/huong/hoat canh (toi da ~12 lan/giay)
//    {t:"chat", text}                     chat kenh
//    {t:"whisper", to, text}              nhan rieng theo ten nhan vat
//    {t:"channels"}                       danh sach kenh cua vung hien tai
//    {t:"ping"}
//    <nhi phan> [0x56][seq][adpcm...]     giong noi (voice chat theo khoang cach, ~25 goi/giay khi noi)
//  server -> client:
//    {t:"welcome", id, name}
//    {t:"joined", zone, channel, players:[...]}, {t:"spawn", p}, {t:"despawn", id}
//    {t:"moves", m:[[id,x,y,z,r,a], ...]}  (gom theo nhip WS_TICK_MS)
//    {t:"chat", ch:"channel"|"whisper"|"system", from, id, text}
//    {t:"event", e}   (trade_invite/trade_update/trade_done/market_sold... tu REST API)
//    {t:"channels", zone, list:[{channel,count}]}, {t:"error", msg}, {t:"pong"}
//    <nhi phan> [0x56][u32 speakerId LE][seq][adpcm...]  giong noi cua nguoi o gan (<= VOICE_RANGE)
//
// PvP the gioi mo / can cu / to doi / ho tro (hp, bag, pvp_hit, struct_*, chest_*, party_*, heal_ally...):
// xem pvp.js - gateway chuyen moi loai tin khong co o day sang pvp.handle().
//
// Vung quan tam (AOI): nguoi trong AOI_NEAR nhan vi tri cua nhau moi nhip; xa hon chi 1/AOI_FAR_EVERY
// nhip (client cung an model nguoi o xa) - kenh 80 nguoi khong con nhan 80x80 hang vi tri/nhip.
//
// Mo rong ngang: chay NHIEU gateway sau load balancer (WebSocket sticky theo ket noi). Moi kenh la 1
// topic pub/sub (Redis) - gateway nao co nguoi trong kenh thi dang ky topic do va chuyen tiep cho
// nguoi cua minh. Thong bao ca nhan di qua topic user:<id>.
const { WebSocketServer } = require("ws");
const crypto = require("crypto");
const { verifyToken } = require("../utils/jwt");
const { logger } = require("../utils/logger");
const { env } = require("../config/env");
const { getBus } = require("./bus");
const { memoryChannels, redisChannels } = require("./channels");
const characterService = require("../services/characterService");
const socialService = require("../services/socialService");
const progressService = require("../services/progressService");
const { duelDamage } = require("../game/combatRules");
const presence = require("./presence");
const { memoryDuels, redisDuels } = require("./duels");
const { attachPvp } = require("./pvp");
const { attachWorldEvents } = require("./worldEvents");
const pvpRules = require("../game/pvpRules");
const { catalog } = require("../game/catalog");
const progressRepo = require("../repositories/progressRepository");

// Vat pham mac len nguoi duoc (trang phuc nong trai / bo giap) - hinh dang xem OutfitCatalog ben Godot.
const OUTFIT_TYPES = new Set(["Outfit", "Armor"]);

const DUEL_HP = 400;
const DUEL_RANGE = 85;
const DUEL_HIT_COOLDOWN_MS = 450;
const DUEL_MAX_MS = 120000;

const ZONE_RE = /^[a-z0-9_]{1,24}$/;

// Giong noi: PHAI khop VoiceChat.VoiceRange ben Godot. Goi toi da ~ 2+3+320 byte (40ms ADPCM 16kHz).
const VOICE_TAG = 0x56;
const VOICE_RANGE = 900;
const VOICE_MAX_BYTES = 1200;
const AOI_NEAR = 2500;
const AOI_FAR_EVERY = 5;
const COORD_LIMIT = 200000;
const BAD_WORDS = ["dm", "dcm", "vcl", "vkl", "clm", "fuck", "shit", "dit"];

function cleanText(text) {
  if (typeof text !== "string") return "";
  // Bo ky tu dieu khien + cat do dai + che tu thô tuc (so khop nguyen tu, khong phan biet hoa thuong).
  let t = text.replace(/[\u0000-\u001f\u007f]/g, "").trim().slice(0, 200);
  for (const w of BAD_WORDS) t = t.replace(new RegExp(`\\b${w}\\b`, "gi"), "*".repeat(w.length));
  return t;
}

// Token bucket don gian moi ket noi moi loai tin.
function bucket(ratePerSec, burst) {
  let tokens = burst;
  let last = Date.now();
  return () => {
    const now = Date.now();
    tokens = Math.min(burst, tokens + ((now - last) / 1000) * ratePerSec);
    last = now;
    if (tokens < 1) return false;
    tokens -= 1;
    return true;
  };
}

const num = (v) => (Number.isFinite(v) ? Math.max(-COORD_LIMIT, Math.min(COORD_LIMIT, Math.round(v * 10) / 10)) : 0);

function attachGateway(server, opts = {}) {
  const bus = opts.bus || getBus();
  const capacity = opts.capacity || env.CHANNEL_CAPACITY;
  const channels = bus.redis ? redisChannels(bus.redis, capacity) : memoryChannels(capacity);
  const duels = bus.redis ? redisDuels(bus.redis) : memoryDuels();
  // Vi tri gan nhat cua nguoi choi trong cac kenh gateway nay dang nghe (de kiem tra tam danh dau truong).
  const posCache = new Map();
  // Hang vi tri moi nhat theo nguoi choi (gui bu cho nguoi xem o xa, xem AOI).
  const latestRow = new Map();
  const gwId = crypto.randomBytes(4).toString("hex");
  const tickMs = opts.tickMs || env.WS_TICK_MS;
  const maxConnections = opts.maxConnections || env.WS_MAX_CONNECTIONS;

  const wss = new WebSocketServer({ server, path: "/ws", maxPayload: 4096 });
  let pvp = null;
  let world = null;
  const localByPlayer = new Map();          // playerId -> conn
  const localByChannel = new Map();         // "zone:n" -> Set<conn>
  const channelHandlers = new Map();        // "zone:n" -> handler (dang ky topic)

  const topicOf = (zone, n) => `ch:${zone}:${n}`;
  const send = (conn, msg) => {
    if (conn.ws.readyState === conn.ws.OPEN) conn.ws.send(typeof msg === "string" ? msg : JSON.stringify(msg));
  };

  // Tin den tu topic kenh (tu bat ky gateway nao, ke ca chinh minh) -> chuyen cho nguoi cua minh.
  function onChannelMessage(key, msg) {
    const members = localByChannel.get(key);
    if (!members) return;
    if (msg.k === "moves") {
      // posCache ghi kem kenh -> PvP/ho tro chi nhan muc tieu CUNG kenh (xem pvp.js).
      for (const row of msg.m) { posCache.set(row[0], [row[1], row[3], key]); latestRow.set(row[0], row); }
      for (const c of members) {
        const [cx, , cz] = c.pos;
        const m = [];
        for (const row of msg.m) {
          if (row[0] === c.playerId) continue;
          if (hiddenFrom(c, row)) continue;
          if (Math.hypot(row[1] - cx, row[3] - cz) <= AOI_NEAR) { m.push(row); c.farPending?.delete(row[0]); }
          else (c.farPending ??= new Set()).add(row[0]);
        }
        if (m.length) send(c, { t: "moves", m });
      }
    } else if (msg.k === "voice") {
      // Chi chuyen cho nguoi ban kinh VOICE_RANGE (theo vi tri server biet, khong tin client) -
      // o xa khong the nghe len.
      let out = null;
      for (const c of members) {
        if (c.playerId === msg.id) continue;
        if (Math.hypot(c.pos[0] - msg.x, c.pos[2] - msg.z) > VOICE_RANGE) continue;
        if (!out) {
          const body = Buffer.from(msg.d, "base64");
          out = Buffer.allocUnsafe(5 + body.length);
          out[0] = VOICE_TAG;
          out.writeUInt32LE(msg.id >>> 0, 1);
          body.copy(out, 5);
        }
        if (c.ws.readyState === c.ws.OPEN && c.ws.bufferedAmount < 256 * 1024) c.ws.send(out, { binary: true });
      }
    } else if (msg.k === "spawn") {
      for (const c of members) if (c.playerId !== msg.p.id) send(c, { t: "spawn", p: msg.p });
    } else if (msg.k === "despawn") {
      for (const c of members) c.vanished?.delete(msg.id);
      latestRow.delete(msg.id);
      for (const c of members) c.farPending?.delete(msg.id);
      for (const c of members) if (c.playerId !== msg.id) send(c, { t: "despawn", id: msg.id });
    } else if (msg.k === "chat") {
      const raw = JSON.stringify({ t: "chat", ch: "channel", from: msg.from, id: msg.id, text: msg.text });
      for (const c of members) send(c, raw);
    } else {
      pvp.onChannelMessage(members, msg);
    }
  }

  // An nap (row[6] = 1, xem pvp.updateHidden): nguoi KHONG phai dong doi o xa hon REVEAL_DIST khong
  // nhan vi tri (chong hack nhin xuyen) - chi nhan 1 tin "vanish" luc bat dau an, "appear" luc lo dien.
  function hiddenFrom(c, row) {
    const id = row[0];
    const hide = row[6] === 1 && !(c.party && c.party.members.includes(id))
      && Math.hypot(row[1] - c.pos[0], row[3] - c.pos[2]) > pvpRules.REVEAL_DIST;
    if (hide) {
      if (!(c.vanished ??= new Set()).has(id)) { c.vanished.add(id); c.farPending?.delete(id); send(c, { t: "vanish", id }); }
      return true;
    }
    if (c.vanished?.delete(id)) send(c, { t: "appear", id });
    return false;
  }

  async function addToChannel(conn, zone, n) {
    const key = `${zone}:${n}`;
    let set = localByChannel.get(key);
    if (!set) {
      set = new Set();
      localByChannel.set(key, set);
      const handler = (msg) => onChannelMessage(key, msg);
      channelHandlers.set(key, handler);
      await bus.subscribe(topicOf(zone, n), handler);
    }
    set.add(conn);
  }

  async function removeFromChannel(conn) {
    if (!conn.zone) return;
    const { zone, channel } = conn;
    const key = `${zone}:${channel}`;
    conn.zone = null;
    conn.channel = null;
    await pvp.onLeft(conn, zone);
    const set = localByChannel.get(key);
    if (set) {
      set.delete(conn);
      if (set.size === 0) {
        localByChannel.delete(key);
        const h = channelHandlers.get(key);
        channelHandlers.delete(key);
        await bus.unsubscribe(topicOf(zone, channel), h);
      }
    }
    await channels.remove(zone, channel, conn.playerId);
    await bus.publish(topicOf(zone, channel), { k: "despawn", id: conn.playerId });
  }

  // gid/pid: bang hoi/to doi - client biet ai la dong doi (khong tu dong nham danh, khong hien thanh do).
  const publicState = (conn) => ({ id: conn.playerId, name: conn.profile.displayName, app: conn.app, x: conn.pos[0], y: conn.pos[1], z: conn.pos[2], r: conn.pos[3], a: conn.pos[4], ...pvp.publicExtra(conn) });

  // Phat lai "spawn" (doi trang phuc/to doi...) de nguoi cung kenh cap nhat.
  async function republish(conn) {
    if (!conn.zone) return;
    await channels.put(conn.zone, conn.channel, conn.playerId, publicState(conn));
    await bus.publish(topicOf(conn.zone, conn.channel), { k: "spawn", p: publicState(conn) });
  }

  async function join(conn, zone, wanted) {
    if (!ZONE_RE.test(zone || "")) return send(conn, { t: "error", msg: "zone khong hop le" });
    // Nong trai cua 1 nguoi (farm_<id>): nong trai gio nam o toa do RIENG tren ban do chung (xem
    // farmService) -> ai di toi cung vao duoc vung do (gap chu nha, thay nhau).
    if (zone.startsWith("farm_")) {
      const owner = Number(zone.slice(5));
      if (!Number.isInteger(owner) || owner <= 0) return send(conn, { t: "error", msg: "zone khong hop le" });
    }
    await removeFromChannel(conn);
    const n = await channels.assign(zone, Number.isInteger(wanted) && wanted > 0 ? wanted : null);
    conn.zone = zone;
    conn.channel = n;
    await addToChannel(conn, zone, n);
    const others = (await channels.roster(zone, n)).filter((p) => p.id !== conn.playerId);
    await channels.put(zone, n, conn.playerId, publicState(conn));
    send(conn, { t: "joined", zone, channel: n, players: others });
    await bus.publish(topicOf(zone, n), { k: "spawn", p: publicState(conn) });
    await pvp.onJoined(conn);
  }

  async function handle(conn, msg) {
    switch (msg.t) {
      case "join": return join(conn, msg.zone, msg.channel);
      case "leave": return removeFromChannel(conn);
      case "pos": {
        if (!conn.limits.pos()) return;
        const prevPos = conn.pos;
        // a: ma hoat canh (bit 32 = dang cuoi ngua, xem PlayerCombat.NetAnimRoles / RemotePlayer).
        conn.pos = [num(msg.x), num(msg.y), num(msg.z), num(msg.r), Number.isInteger(msg.a) ? Math.max(0, Math.min(63, msg.a)) : 0];
        conn.moved = true;
        conn.mounted = (conn.pos[4] & 32) !== 0;
        const t = Date.now();
        conn.speed = pvpRules.smoothSpeed(conn.speed, Math.hypot(conn.pos[0] - prevPos[0], conn.pos[2] - prevPos[2]), t - (conn.posAt || 0));
        conn.posAt = t;
        pvp.updateHidden(conn, msg.h === 1 || msg.h === true);
        return;
      }
      case "chat": {
        if (!conn.zone) return;
        if (!conn.limits.chat()) return send(conn, { t: "error", msg: "Ban chat qua nhanh." });
        const text = cleanText(msg.text);
        if (!text) return;
        return bus.publish(topicOf(conn.zone, conn.channel), { k: "chat", from: conn.profile.displayName, id: conn.playerId, text });
      }
      case "whisper": {
        if (!conn.limits.chat()) return send(conn, { t: "error", msg: "Ban chat qua nhanh." });
        const text = cleanText(msg.text);
        if (!text || typeof msg.to !== "string") return;
        const target = await characterService.findByName(msg.to.slice(0, 20));
        if (!target) return send(conn, { t: "error", msg: "Khong tim thay nguoi choi." });
        await bus.publish(`user:${target.playerId}`, { type: "whisper", from: conn.profile.displayName, id: conn.playerId, text });
        return send(conn, { t: "chat", ch: "whisper", from: `-> ${target.displayName}`, id: conn.playerId, text });
      }
      case "gchat": {
        if (!conn.guildId) return send(conn, { t: "error", msg: "Ban chua vao bang hoi." });
        if (!conn.limits.chat()) return send(conn, { t: "error", msg: "Ban chat qua nhanh." });
        const text = cleanText(msg.text);
        if (!text) return;
        return bus.publish(`guild:${conn.guildId}`, { from: conn.profile.displayName, id: conn.playerId, text });
      }
      case "outfit": return setOutfit(conn, msg);
      case "duel_invite": return duelInvite(conn, msg);
      case "duel_accept": return duelAccept(conn, msg);
      case "duel_hit": return duelHit(conn, msg);
      case "duel_forfeit": return conn.duelId && duelEnd(conn.duelId, null, conn.playerId);
      case "channels":
        if (conn.zone) send(conn, { t: "channels", zone: conn.zone, list: await channels.list(conn.zone) });
        return;
      case "ping":
        return send(conn, { t: "pong" });
      default:
        if (await pvp.handle(conn, msg)) return;
        if (await world.handle(conn, msg)) return;
        return send(conn, { t: "error", msg: "loai tin khong ho tro" });
    }
  }

  // Doi trang phuc: chi cho mac do THUC SU so huu (kho do tren server), roi phat lai "spawn" de
  // nguoi cung kenh dung lai hinh nhan vat.
  async function setOutfit(conn, msg) {
    if (!conn.playerId || !conn.limits.outfit()) return;
    const id = typeof msg.id === "string" && msg.id ? msg.id.slice(0, 40) : null;
    if ((conn.app.f || null) === id) return;
    if (id) {
      if (!OUTFIT_TYPES.has(catalog.items.get(id)?.Type)) return send(conn, { t: "error", msg: "Khong phai trang phuc." });
      const owned = await progressRepo.ownedItems(conn.playerId);
      if (!owned.includes(id)) return send(conn, { t: "error", msg: "Ban chua co trang phuc nay." });
    }
    conn.app = { ...conn.app, f: id };
    await republish(conn);
  }

  async function authenticate(conn, token) {
    let payload;
    try { payload = verifyToken(token); } catch { return send(conn, { t: "error", msg: "token khong hop le" }), conn.ws.close(4001); }
    const profile = await characterService.get(payload.playerId);
    if (!profile) return send(conn, { t: "error", msg: "Hay tao nhan vat truoc." }), conn.ws.close(4002);
    // Dang nhap o noi khac -> dong ket noi cu (1 nguoi = 1 ket noi).
    const old = localByPlayer.get(payload.playerId);
    if (old) old.ws.close(4003, "dang nhap noi khac");
    conn.playerId = payload.playerId;
    conn.profile = profile;
    conn.app = { g: profile.gender, m: profile.modelId, s: profile.skinColor, h: profile.hairColor, o: profile.outfitColor };
    localByPlayer.set(conn.playerId, conn);
    conn.userHandler = (e) => pvp.onUserEvent(conn, e).then((handled) => {
      if (handled) return;
      if (e.type === "whisper") send(conn, { t: "chat", ch: "whisper", from: e.from, id: e.id, text: e.text });
      else {
        if (e.type === "guild_changed") refreshGuild(conn).catch(() => {});
        if (e.type === "duel_start" || e.type === "duel_end") conn.duelId = e.type === "duel_start" ? e.duelId : null;
        send(conn, { t: "event", e });
      }
    }).catch((err) => logger.warn({ err }, "Loi xu ly su kien ca nhan"));
    await bus.subscribe(`user:${conn.playerId}`, conn.userHandler);
    await presence.setOnline(conn.playerId);
    await refreshGuild(conn);
    send(conn, { t: "welcome", id: conn.playerId, name: profile.displayName, gateway: gwId, guildId: conn.guildId || 0 });
    await pvp.onWelcome(conn);
    await world.onWelcome(conn);
  }

  // ==== Bang hoi: dang ky chat bang theo bang hien tai (doi khi vao/roi bang) ====
  async function refreshGuild(conn) {
    if (conn.guildId && conn.guildHandler) await bus.unsubscribe(`guild:${conn.guildId}`, conn.guildHandler);
    conn.guildId = null;
    const g = await socialService.memberOf(conn.playerId);
    if (!g) return;
    conn.guildId = g.id;
    conn.guildHandler = (m) => send(conn, { t: "chat", ch: "guild", from: m.from, id: m.id, text: m.text });
    await bus.subscribe(`guild:${g.id}`, conn.guildHandler);
  }

  // ==== Dau tay doi (dau truong) - server tinh sat thuong tu trang bi THAT + cuong hoa ====
  const lastHit = new Map(); // playerId -> ms
  const notify = (id, e) => bus.publish(`user:${id}`, e);
  const posOf = (id) => localByPlayer.get(id)?.pos ? [localByPlayer.get(id).pos[0], localByPlayer.get(id).pos[2]] : posCache.get(id);

  // PvP the gioi mo / can cu / to doi (xem pvp.js) - khai bao sau notify/republish ma no dung.
  pvp = attachPvp({ bus, send, localByPlayer, posCache, notify, topicOf, republish });
  // Su kien the gioi: ruong tiep te, doan thuong nhan, Dem Trang Mau (xem worldEvents.js).
  world = attachWorldEvents({ bus, send, localByPlayer, gwId, notify }, opts.world || {});

  async function duelInvite(conn, msg) {
    if (!conn.zone) return send(conn, { t: "error", msg: "Hay ra khu vuc chung (thi tran) de thach dau." });
    if (conn.duelId) return send(conn, { t: "error", msg: "Ban dang trong 1 tran dau." });
    const target = typeof msg.to === "string" ? await characterService.findByName(msg.to.slice(0, 20)) : null;
    if (!target || target.playerId === conn.playerId) return send(conn, { t: "error", msg: "Khong tim thay doi thu." });
    const duelId = await duels.create({ a: conn.playerId, b: target.playerId, status: "pending", zone: conn.zone, channel: conn.channel, aName: conn.profile.displayName, bName: target.displayName });
    await notify(target.playerId, { type: "duel_invite", duelId, fromName: conn.profile.displayName, fromId: conn.playerId });
    send(conn, { t: "event", e: { type: "duel_invited", duelId, toName: target.displayName } });
  }

  async function duelAccept(conn, msg) {
    const d = typeof msg.duelId === "string" ? await duels.get(msg.duelId) : null;
    if (!d || d.status !== "pending" || d.b !== conn.playerId) return send(conn, { t: "error", msg: "Loi thach dau khong con." });
    if (conn.zone !== d.zone || conn.channel !== d.channel) return send(conn, { t: "error", msg: "Phai o cung khu vuc/kenh voi doi thu." });
    const [pa, pb] = await Promise.all([progressService.duelPower(d.a), progressService.duelPower(d.b)]);
    await duels.update(d.id, {
      status: "active", started: Date.now(), [`hp_${d.a}`]: DUEL_HP, [`hp_${d.b}`]: DUEL_HP,
      [`atk_${d.a}`]: pa.attack, [`def_${d.a}`]: pa.defense, [`atk_${d.b}`]: pb.attack, [`def_${d.b}`]: pb.defense,
    });
    const start = { type: "duel_start", duelId: d.id, aId: d.a, bId: d.b, aName: d.aName, bName: d.bName, hp: DUEL_HP };
    await notify(d.a, start);
    await notify(d.b, start);
  }

  async function duelHit(conn, msg) {
    const d = typeof msg.duelId === "string" ? await duels.get(msg.duelId) : null;
    if (!d || d.status !== "active" || (d.a !== conn.playerId && d.b !== conn.playerId)) return;
    const me = conn.playerId;
    const foe = me === d.a ? d.b : d.a;
    if (Date.now() - d.started > DUEL_MAX_MS) return duelEnd(d.id, null, null);
    const now = Date.now();
    if (now - (lastHit.get(me) || 0) < DUEL_HIT_COOLDOWN_MS) return;
    const p1 = posOf(me), p2 = posOf(foe);
    if (!p1 || !p2 || Math.hypot(p1[0] - p2[0], p1[1] - p2[1]) > DUEL_RANGE) return;
    lastHit.set(me, now);
    const dmg = duelDamage({ attack: d[`atk_${me}`] }, { defense: d[`def_${foe}`] });
    const after = await duels.damage(d.id, foe, dmg);
    const hp = { [d.a]: Math.max(0, after[`hp_${d.a}`]), [d.b]: Math.max(0, after[`hp_${d.b}`]) };
    const e = { type: "duel_hp", duelId: d.id, hp, by: me, dmg };
    await notify(d.a, e);
    await notify(d.b, e);
    if (after[`hp_${foe}`] <= 0) await duelEnd(d.id, me, foe);
  }

  // winner/loser null = hoa (het gio). loserOnly (forfeit/roi mang): nguoi con lai thang.
  async function duelEnd(duelId, winner, loser) {
    const d = await duels.get(duelId);
    if (!d || d.status !== "active") return;
    await duels.update(duelId, { status: "done" });
    if (!winner && loser) winner = loser === d.a ? d.b : d.a;
    let delta = 0;
    if (winner && loser) delta = (await progressService.recordDuel(winner, loser)).delta;
    const e = { type: "duel_end", duelId, winnerId: winner || 0, delta };
    await notify(d.a, e);
    await notify(d.b, e);
    await duels.remove(duelId);
  }

  wss.on("connection", (ws) => {
    if (wss.clients.size > maxConnections) return ws.close(1013, "gateway day");
    const conn = {
      ws, queue: Promise.resolve(), playerId: null, profile: null, zone: null, channel: null, pos: [0, 0, 0, 0, 0], moved: false, alive: true,
      limits: { pos: bucket(12, 20), chat: bucket(1, 3), outfit: bucket(1, 4), any: bucket(30, 60), voice: bucket(30, 40),
        hp: bucket(4, 8), bag: bucket(1, 4), build: bucket(3, 6), shot: bucket(3, 5) },
    };
    const authTimer = setTimeout(() => { if (!conn.playerId) ws.close(4000, "chua xac thuc"); }, 5000);
    ws.on("pong", () => { conn.alive = true; });
    ws.on("message", async (raw, isBinary) => {
      if (isBinary) return onVoice(conn, raw);
      if (!conn.limits.any()) return;
      let msg;
      try { msg = JSON.parse(raw); } catch { return; }
      if (!msg || typeof msg !== "object") return;
      // Xu ly TUAN TU tung tin cua 1 ket noi (handler la async - tranh 2 lenh join chen nhau).
      conn.queue = conn.queue.then(async () => {
        if (!conn.playerId) {
          if (msg.t === "auth" && typeof msg.token === "string") await authenticate(conn, msg.token);
          return;
        }
        await handle(conn, msg);
      }).catch((err) => logger.warn({ err }, "Loi xu ly tin WebSocket"));
    });
    ws.on("close", async () => {
      clearTimeout(authTimer);
      if (!conn.playerId) return;
      if (localByPlayer.get(conn.playerId) === conn) localByPlayer.delete(conn.playerId);
      try {
        if (conn.duelId) await duelEnd(conn.duelId, null, conn.playerId); // roi mang giua tran = thua
        await pvp.onDisconnect(conn);
        await removeFromChannel(conn);
        if (conn.userHandler) await bus.unsubscribe(`user:${conn.playerId}`, conn.userHandler);
        if (conn.guildId && conn.guildHandler) await bus.unsubscribe(`guild:${conn.guildId}`, conn.guildHandler);
        if (localByPlayer.get(conn.playerId) == null) await presence.setOffline(conn.playerId);
      } catch (err) {
        logger.warn({ err }, "Loi don dep ket noi");
      }
    });
  });

  // Goi giong noi tu client: [0x56][seq][adpcm...] -> phat qua topic kenh kem vi tri nguoi noi (moi
  // gateway tu loc nguoi nghe gan). Gioi han ~30 goi/giay, chi khi da xac thuc va dang o 1 vung chung.
  function onVoice(conn, raw) {
    if (!conn.playerId || !conn.zone || !conn.limits.voice()) return;
    const buf = Buffer.isBuffer(raw) ? raw : Buffer.concat(Array.isArray(raw) ? raw : [Buffer.from(raw)]);
    if (buf.length < 5 || buf.length > VOICE_MAX_BYTES || buf[0] !== VOICE_TAG) return;
    bus.publish(topicOf(conn.zone, conn.channel), { k: "voice", id: conn.playerId, x: conn.pos[0], z: conn.pos[2], d: buf.subarray(1).toString("base64") }).catch(() => {});
  }

  // Nhip gui vi tri: gom vi tri da doi cua nguoi trong gateway nay theo kenh -> 1 tin / kenh / nhip.
  let tickCount = 0;
  const tick = setInterval(async () => {
    tickCount++;
    for (const [key, members] of localByChannel) {
      const rows = [];
      for (const c of members) if (c.moved) { c.moved = false; const row = [c.playerId, ...c.pos]; if (c.hidden) row.push(1); rows.push(row); }
      const [zone, n] = key.split(":");
      if (rows.length) bus.publish(topicOf(zone, Number(n)), { k: "moves", m: rows }).catch(() => {});
      // Nguoi o xa (ngoai AOI_NEAR): gui vi tri MOI NHAT da hoan moi AOI_FAR_EVERY nhip - chay o
      // day (khong doi tin "moves" moi) de ca nguoi vua DUNG LAI cung duoc cap nhat vi tri cuoi.
      if (tickCount % AOI_FAR_EVERY === 0)
        for (const c of members) {
          if (!c.farPending?.size) continue;
          const m = [];
          for (const id of c.farPending) { const r = latestRow.get(id); if (r && !hiddenFrom(c, r)) m.push(r); }
          c.farPending.clear();
          if (m.length) send(c, { t: "moves", m });
        }
      // Moi ~2 giay cap nhat roster (de nguoi moi vao kenh thay dung vi tri hien tai).
      if (tickCount % Math.max(1, Math.round(2000 / tickMs)) === 0)
        for (const c of members) channels.put(zone, Number(n), c.playerId, publicState(c)).catch(() => {});
    }
  }, tickMs);

  // Phat hien ket noi chet (mat mang dot ngot khong kip dong).
  const heartbeat = setInterval(() => {
    for (const c of localByPlayer.values()) {
      if (!c.alive) { c.ws.terminate(); continue; }
      c.alive = false;
      c.ws.ping();
      presence.setOnline(c.playerId).catch(() => {});
    }
  }, 25000);

  function close() {
    pvp.close();
    world.close();
    clearInterval(tick);
    clearInterval(heartbeat);
    for (const c of wss.clients) c.close(1001, "server tat");
    return new Promise((resolve) => wss.close(() => resolve()));
  }

  logger.info({ gwId, bus: bus.kind, capacity, tickMs }, "Gateway WebSocket san sang tai /ws");
  return { wss, close, gwId, world: () => world, stats: () => ({ connections: wss.clients.size, channels: localByChannel.size }) };
}

module.exports = { attachGateway, cleanText };
