// Ban be, bang hoi, tham nong trai.
const repo = require("../repositories/socialRepository");
const econ = require("../repositories/economyRepository");
const saveRepo = require("../repositories/saveRepository");
const characters = require("./characterService");
const { ensureWallet } = require("./economyService");
const { tradable } = require("./marketService");
const { onlineSet } = require("../realtime/presence");
const { notifyPlayer } = require("../realtime/bus");
const { AppError } = require("../utils/AppError");
const time = require("./timeService");
const cache = require("../cache/cache");

// Bang hoi cua 1 nguoi (gateway hoi lien tuc khi kiem dong doi PvP) - cache, xoa khi doi thanh vien.
const guildKey = (id) => `guild:${id}`;
const memberOfCached = (id) => cache.cached(guildKey(id), 120, () => repo.memberOf(id));

const GUILD_COST = 1000;
const GUILD_MAX = 50;
const MAX_HELPS_PER_DAY = 40;

async function target(name, me) {
  const t = await characters.findByName(name);
  if (!t) throw new AppError(404, "Khong tim thay nguoi choi.");
  if (t.playerId === me) throw new AppError(400, "Khong the lam viec nay voi chinh minh.");
  return t;
}

// ==== Ban be ====
async function addFriend(me, name) {
  const t = await target(name, me);
  const f = await repo.getFriendship(me, t.playerId);
  if (f?.status === "accepted") throw new AppError(409, "Da la ban be.");
  // Ben kia da gui loi moi truoc -> chap nhan luon.
  if (f?.status === "pending" && f.requested_by === t.playerId) return acceptFriend(me, t.playerId);
  await repo.requestFriend(me, t.playerId);
  const meChar = await characters.get(me);
  notifyPlayer(t.playerId, { type: "friend_request", fromName: meChar?.displayName, fromId: me });
  return { status: "pending" };
}

async function acceptFriend(me, otherId) {
  if (!(await repo.acceptFriend(me, otherId))) throw new AppError(404, "Khong co loi moi ket ban.");
  const meChar = await characters.get(me);
  notifyPlayer(otherId, { type: "friend_accepted", fromName: meChar?.displayName, fromId: me });
  return { status: "accepted" };
}

async function removeFriend(me, otherId) {
  await repo.removeFriend(me, otherId);
  return { ok: true };
}

async function listFriends(me) {
  const rows = await repo.listFriends(me);
  const online = await onlineSet(rows.map((r) => r.other_id));
  return rows.map((r) => ({
    playerId: r.other_id, displayName: r.display_name, gender: r.gender, modelId: r.model_id,
    status: r.status, incoming: r.status === "pending" && r.requested_by !== me, online: online.has(r.other_id),
  }));
}

// ==== Bang hoi ====
async function myGuild(me) {
  const g = await repo.memberOf(me);
  if (!g) return { guild: null, invites: await repo.invitesFor(me) };
  const mem = await repo.members(g.id);
  const online = await onlineSet(mem.map((m) => m.player_id));
  return {
    guild: { id: g.id, name: g.name, tag: g.tag, leaderId: g.leader_id, myRole: g.role },
    members: mem.map((m) => ({ playerId: m.player_id, displayName: m.display_name, role: m.role, online: online.has(m.player_id) })),
    storage: (await repo.storage(g.id)).map((s) => ({ itemId: s.item_id, count: s.count })),
    invites: [],
  };
}

// Lap bang: ton GUILD_COST vang (tru nguyen tu trong vi).
async function createGuild(me, name, tag) {
  if (await repo.memberOf(me)) throw new AppError(400, "Ban dang o trong 1 bang hoi.");
  try {
    await econ.withTransaction(async (client) => {
      const w = await ensureWallet(client, me);
      if (Number(w.gold) < GUILD_COST) throw new AppError(400, `Can ${GUILD_COST} vang de lap bang.`);
      await econ.addGold(client, me, -GUILD_COST);
      await repo.createGuild(client, name, tag.toUpperCase(), me);
    });
  } catch (err) {
    if (err.code === "23505") throw new AppError(409, "Ten bang hoi da ton tai.");
    throw err;
  }
  await cache.invalidate(guildKey(me));
  notifyPlayer(me, { type: "guild_changed" });
  return myGuild(me);
}

async function requireRole(me, roles) {
  const g = await repo.memberOf(me);
  if (!g) throw new AppError(400, "Ban chua vao bang hoi.");
  if (roles && !roles.includes(g.role)) throw new AppError(403, "Ban khong co quyen nay.");
  return g;
}

async function inviteToGuild(me, name) {
  const g = await requireRole(me, ["leader", "officer"]);
  const t = await target(name, me);
  if (await repo.memberOf(t.playerId)) throw new AppError(400, "Nguoi nay da co bang hoi.");
  if ((await repo.countMembers(g.id)) >= GUILD_MAX) throw new AppError(400, `Bang hoi toi da ${GUILD_MAX} nguoi.`);
  await repo.invite(g.id, t.playerId);
  notifyPlayer(t.playerId, { type: "guild_invite", guildName: g.name });
  return { ok: true };
}

async function acceptGuild(me, guildId) {
  if (await repo.memberOf(me)) throw new AppError(400, "Ban dang o trong 1 bang hoi.");
  const ok = await econ.withTransaction((client) => repo.joinGuild(client, guildId, me));
  if (!ok) throw new AppError(404, "Khong co loi moi vao bang nay.");
  await cache.invalidate(guildKey(me));
  notifyPlayer(me, { type: "guild_changed" });
  return myGuild(me);
}

// Roi bang: bang chu roi thi nhuong cho pho bang/thanh vien lau nhat; het nguoi thi giai tan.
async function leaveGuild(me) {
  const g = await requireRole(me);
  let next = null;
  await econ.withTransaction(async (client) => {
    await repo.leaveGuild(client, me);
    if (g.role === "leader") {
      next = await repo.nextLeader(client, g.id);
      if (next) await repo.setLeader(client, g.id, next);
      else await repo.deleteGuild(client, g.id);
    }
  });
  await cache.invalidate(guildKey(me), next && guildKey(next));
  notifyPlayer(me, { type: "guild_changed" });
  return { ok: true };
}

async function kick(me, otherId) {
  const g = await requireRole(me, ["leader", "officer"]);
  const other = await repo.memberOf(otherId);
  if (!other || other.id !== g.id) throw new AppError(404, "Khong phai thanh vien bang.");
  if (other.role === "leader" || (other.role === "officer" && g.role !== "leader")) throw new AppError(403, "Khong the moi nguoi nay ra.");
  await econ.withTransaction((client) => repo.leaveGuild(client, otherId));
  await cache.invalidate(guildKey(otherId));
  notifyPlayer(otherId, { type: "guild_changed" });
  return { ok: true };
}

async function promote(me, otherId, role) {
  const g = await requireRole(me, ["leader"]);
  const other = await repo.memberOf(otherId);
  if (!other || other.id !== g.id || other.role === "leader") throw new AppError(404, "Khong phai thanh vien bang.");
  await repo.setRole(g.id, otherId, role === "officer" ? "officer" : "member");
  await cache.invalidate(guildKey(otherId));
  return { ok: true };
}

// Kho chung: gui/lay nguyen tu giua vi ca nhan va kho bang.
async function deposit(me, itemId, count) {
  const g = await requireRole(me);
  if (!tradable(itemId)) throw new AppError(400, "Vat pham khong the gui.");
  await econ.withTransaction(async (client) => {
    await ensureWallet(client, me);
    const carry = await econ.adjustItem(client, me, itemId, -count);
    if (!carry) throw new AppError(400, "Khong du vat pham.");
    await repo.adjustStorage(client, g.id, itemId, count, carry);
  });
  return myGuild(me);
}

async function withdraw(me, itemId, count) {
  const g = await requireRole(me);
  await econ.withTransaction(async (client) => {
    await ensureWallet(client, me);
    const carry = await repo.adjustStorage(client, g.id, itemId, -count);
    if (!carry) throw new AppError(400, "Kho bang khong du vat pham.");
    await econ.adjustItem(client, me, itemId, count, carry);
  });
  return myGuild(me);
}

// ==== Tham nong trai (chi ban be) ====
async function friendFarm(me, hostId) {
  if (!(await repo.areFriends(me, hostId))) throw new AppError(403, "Chi ban be moi tham nong trai duoc.");
  const save = await saveRepo.get(hostId);
  const host = await characters.get(hostId);
  const farm = (save?.data?.Farm || []).map((t) => ({
    x: t.X, y: t.Y, freeform: !!t.Freeform, posX: t.PosX, posZ: t.PosZ, cropId: t.CropId,
    growStage: t.GrowStage, growDays: t.GrowDays, watered: !!t.Watered, isGiant: !!t.IsGiant, pest: !!t.PestAfflicted,
  }));
  return { hostId, hostName: host?.displayName, farm };
}

async function helpWater(me, hostId, plotKeys) {
  if (!(await repo.areFriends(me, hostId))) throw new AppError(403, "Chi ban be moi giup duoc.");
  const day = time.now().day;
  const used = await repo.helpsToday(me, day);
  const keys = [...new Set(plotKeys)].slice(0, Math.max(0, MAX_HELPS_PER_DAY - used));
  const added = await repo.addHelps(hostId, me, day, keys);
  if (added > 0) {
    const meChar = await characters.get(me);
    notifyPlayer(hostId, { type: "farm_helped", fromName: meChar?.displayName, count: added });
  }
  return { watered: added, remainingToday: Math.max(0, MAX_HELPS_PER_DAY - used - added) };
}

// Chu nong trai lay danh sach o duoc ban tuoi ho (hom nay) roi xac nhan da ap dung.
async function takeHelps(me) {
  const day = time.now().day;
  const rows = await repo.pendingHelps(me, day);
  await repo.ackHelps(me);
  return rows.filter((r) => r.day === day).map((r) => ({ plotKey: r.plot_key, helper: r.display_name }));
}

module.exports = {
  addFriend, acceptFriend, removeFriend, listFriends,
  myGuild, createGuild, inviteToGuild, acceptGuild, leaveGuild, kick, promote, deposit, withdraw,
  friendFarm, helpWater, takeHelps, GUILD_COST, areFriends: repo.areFriends, memberOf: memberOfCached, guildKey,
};
