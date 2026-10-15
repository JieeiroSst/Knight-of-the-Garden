// Trao doi truc tiep giua 2 nguoi choi: moi -> chap nhan -> 2 ben dat do/vang -> 2 ben KHOA ->
// 2 ben XAC NHAN -> server chuyen do nguyen tu (kiem tra lai so huu ngay luc chuyen). Sua de nghi
// sau khi khoa se tu mo khoa ca 2 ben (chong lua "doi do phut chot").
const econ = require("../repositories/economyRepository");
const trades = require("../repositories/tradeRepository");
const characters = require("./characterService");
const { ensureWallets } = require("./economyService");
const { tradable } = require("./marketService");
const { AppError } = require("../utils/AppError");
const { notifyPlayer } = require("../realtime/bus");

const MAX_OFFER_KINDS = 12;

function sideOf(t, playerId) {
  if (t.a_id === playerId) return "a";
  if (t.b_id === playerId) return "b";
  throw new AppError(404, "Khong tim thay giao dich.");
}

function toPublic(t, me) {
  const mine = sideOf(t, me);
  const other = mine === "a" ? "b" : "a";
  return {
    id: Number(t.id), status: t.status, otherId: t[`${other}_id`], iAmInviter: mine === "a",
    myOffer: t[`${mine}_offer`], theirOffer: t[`${other}_offer`],
    myLocked: t[`${mine}_locked`], theirLocked: t[`${other}_locked`],
    myConfirmed: t[`${mine}_confirmed`], theirConfirmed: t[`${other}_confirmed`],
  };
}

function notifyOther(t, me, type) {
  const other = t.a_id === me ? t.b_id : t.a_id;
  notifyPlayer(other, { type, tradeId: Number(t.id) });
}

async function invite(me, targetName) {
  const meChar = await characters.get(me);
  if (!meChar) throw new AppError(400, "Hay tao nhan vat truoc.");
  const target = await characters.findByName(targetName);
  if (!target) throw new AppError(404, "Khong tim thay nguoi choi.");
  if (target.playerId === me) throw new AppError(400, "Khong the giao dich voi chinh minh.");
  const t = (await trades.findActiveBetween(me, target.playerId)) || (await trades.create(me, target.playerId));
  notifyPlayer(target.playerId, { type: "trade_invite", tradeId: Number(t.id), fromName: meChar.displayName });
  return toPublic(t, me);
}

async function get(me, id) {
  const t = await trades.get(id);
  if (!t) throw new AppError(404, "Khong tim thay giao dich.");
  return toPublic(t, me);
}

async function mutate(me, id, fn) {
  const t = await econ.withTransaction(async (client) => {
    const row = await trades.lock(client, id);
    if (!row) throw new AppError(404, "Khong tim thay giao dich.");
    sideOf(row, me);
    return fn(client, row);
  });
  return toPublic(t, me);
}

async function accept(me, id) {
  const r = await mutate(me, id, async (client, t) => {
    if (t.b_id !== me) throw new AppError(403, "Chi nguoi duoc moi moi chap nhan duoc.");
    if (t.status !== "pending") throw new AppError(400, "Giao dich khong o trang thai cho.");
    return trades.update(client, t.id, { status: "open" });
  });
  notifyOther(await trades.get(id), me, "trade_update");
  return r;
}

function validateOffer(offer) {
  const gold = Number(offer?.gold || 0);
  if (!Number.isInteger(gold) || gold < 0 || gold > 1e9) throw new AppError(400, "So vang khong hop le.");
  const items = {};
  const entries = Object.entries(offer?.items || {});
  if (entries.length > MAX_OFFER_KINDS) throw new AppError(400, `Toi da ${MAX_OFFER_KINDS} loai vat pham.`);
  for (const [id, n] of entries) {
    if (!tradable(id)) throw new AppError(400, `Vat pham ${id} khong the giao dich.`);
    if (!Number.isInteger(n) || n <= 0 || n > 9999) throw new AppError(400, "So luong khong hop le.");
    items[id] = n;
  }
  return { gold, items };
}

async function setOffer(me, id, offer) {
  const clean = validateOffer(offer);
  const r = await mutate(me, id, async (client, t) => {
    if (t.status !== "open") throw new AppError(400, "Giao dich chua mo.");
    const side = sideOf(t, me);
    return trades.update(client, t.id, {
      [`${side}_offer`]: clean, a_locked: false, b_locked: false, a_confirmed: false, b_confirmed: false,
    });
  });
  notifyOther(await trades.get(id), me, "trade_update");
  return r;
}

async function lock(me, id) {
  const r = await mutate(me, id, async (client, t) => {
    if (t.status !== "open") throw new AppError(400, "Giao dich chua mo.");
    return trades.update(client, t.id, { [`${sideOf(t, me)}_locked`]: true });
  });
  notifyOther(await trades.get(id), me, "trade_update");
  return r;
}

async function confirm(me, id) {
  let done = false;
  const r = await mutate(me, id, async (client, t) => {
    if (t.status !== "open") throw new AppError(400, "Giao dich chua mo.");
    if (!t.a_locked || !t.b_locked) throw new AppError(400, "Ca 2 ben phai khoa de nghi truoc.");
    const side = sideOf(t, me);
    let row = await trades.update(client, t.id, { [`${side}_confirmed`]: true });
    if (row.a_confirmed && row.b_confirmed) {
      await execute(client, row);
      row = await trades.update(client, t.id, { status: "done" });
      done = true;
    }
    return row;
  });
  const t = await trades.get(id);
  if (done) {
    notifyPlayer(t.a_id, { type: "trade_done", tradeId: Number(t.id) });
    notifyPlayer(t.b_id, { type: "trade_done", tradeId: Number(t.id) });
  } else notifyOther(t, me, "trade_update");
  return r;
}

// Chuyen do 2 chieu trong CUNG transaction - thieu bat cu thu gi -> rollback toan bo.
async function execute(client, t) {
  const wallets = await ensureWallets(client, [t.a_id, t.b_id]);
  const legs = [[t.a_id, t.b_id, t.a_offer], [t.b_id, t.a_id, t.b_offer]];
  for (const [from, , offer] of legs) {
    if (Number(wallets.get(from).gold) < offer.gold) throw new AppError(400, "Mot ben khong con du vang.");
  }
  for (const [from, to, offer] of legs) {
    if (offer.gold > 0) {
      await econ.addGold(client, from, -offer.gold);
      await econ.addGold(client, to, offer.gold);
    }
    for (const [id, n] of Object.entries(offer.items)) {
      if (!(await econ.adjustItem(client, from, id, -n))) throw new AppError(400, "Mot ben khong con du vat pham.");
      await econ.adjustItem(client, to, id, n);
    }
  }
}

async function cancel(me, id) {
  const r = await mutate(me, id, async (client, t) => {
    if (t.status === "done" || t.status === "cancelled") return t;
    return trades.update(client, t.id, { status: "cancelled" });
  });
  notifyOther(await trades.get(id), me, "trade_cancelled");
  return r;
}

module.exports = { invite, get, accept, setOffer, lock, confirm, cancel };
