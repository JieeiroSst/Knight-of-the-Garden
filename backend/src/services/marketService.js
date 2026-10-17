// Cho mua ban giua nguoi choi: dang ban = vat pham bi giu (ky quy) khoi tui nguoi ban ngay luc dang;
// mua = 1 transaction nguyen tu (tru vang nguoi mua, cong vang nguoi ban tru thue, chuyen vat pham);
// het han/huy = hoan vat pham. Khong the ban do khong co, khong the mua 2 lan 1 tin.
const econ = require("../repositories/economyRepository");
const market = require("../repositories/marketRepository");
const characters = require("./characterService");
const { ensureWallet, ensureWallets } = require("./economyService");
const { catalog } = require("../game/catalog");
const { AppError } = require("../utils/AppError");
const { env } = require("../config/env");
const { notifyPlayer } = require("../realtime/bus");

const MAX_ACTIVE_LISTINGS = 20;
const UNTRADABLE_TYPES = new Set(["Quest"]);

function tradable(itemId) {
  const def = catalog.items.get(itemId);
  return def && !UNTRADABLE_TYPES.has(def.Type);
}

function toPublic(row) {
  return {
    id: Number(row.id), sellerId: row.seller_id, sellerName: row.seller_name ?? null, itemId: row.item_id,
    count: row.count, price: Number(row.price), status: row.status, createdAt: row.created_at, expiresAt: row.expires_at,
  };
}

async function createListing(sellerId, { itemId, count, price }) {
  if (!tradable(itemId)) throw new AppError(400, "Vat pham khong the rao ban.");
  if (!(await characters.get(sellerId))) throw new AppError(400, "Hay tao nhan vat truoc.");
  return econ.withTransaction(async (client) => {
    await ensureWallet(client, sellerId);
    if ((await market.countActive(client, sellerId)) >= MAX_ACTIVE_LISTINGS) throw new AppError(400, `Toi da ${MAX_ACTIVE_LISTINGS} tin dang cung luc.`);
    const carry = await econ.adjustItem(client, sellerId, itemId, -count);
    if (!carry) throw new AppError(400, "Khong du vat pham de rao ban.");
    return toPublic(await market.insert(client, sellerId, itemId, count, price, env.MARKET_LISTING_DAYS, carry));
  });
}

async function buy(buyerId, listingId) {
  const result = await econ.withTransaction(async (client) => {
    const l = await market.lockById(client, listingId);
    if (!l || l.status !== "active" || new Date(l.expires_at) <= new Date()) throw new AppError(404, "Tin rao khong con.");
    if (l.seller_id === buyerId) throw new AppError(400, "Khong the mua hang cua chinh minh.");
    const wallets = await ensureWallets(client, [buyerId, l.seller_id]);
    const price = Number(l.price);
    if (Number(wallets.get(buyerId).gold) < price) throw new AppError(400, "Khong du vang.");
    const tax = Math.floor((price * env.MARKET_TAX_PERCENT) / 100);
    await econ.addGold(client, buyerId, -price);
    await econ.addGold(client, l.seller_id, price - tax);
    await econ.adjustItem(client, buyerId, l.item_id, l.count, l.item_state);
    await market.close(client, l.id, "sold", buyerId);
    return { listing: l, earned: price - tax };
  });
  notifyPlayer(result.listing.seller_id, { type: "market_sold", itemId: result.listing.item_id, count: result.listing.count, gold: result.earned });
  return { ok: true, itemId: result.listing.item_id, count: result.listing.count };
}

async function cancel(sellerId, listingId) {
  return econ.withTransaction(async (client) => {
    const l = await market.lockById(client, listingId);
    if (!l || l.seller_id !== sellerId) throw new AppError(404, "Khong tim thay tin rao.");
    if (l.status !== "active") throw new AppError(400, "Tin rao da dong.");
    await ensureWallet(client, sellerId);
    await econ.adjustItem(client, sellerId, l.item_id, l.count, l.item_state);
    await market.close(client, l.id, "cancelled");
    return { ok: true };
  });
}

// q: tim theo ten vat pham (khong dau/co dau deu khop vi so sanh chu thuong tren ten goc + id).
async function search({ itemId, q, sort, page }) {
  let itemIds = null;
  if (itemId) itemIds = [itemId];
  else if (q) {
    const needle = q.toLowerCase();
    itemIds = [...catalog.items.values()].filter((d) => d.Id.includes(needle) || (d.Name || "").toLowerCase().includes(needle)).map((d) => d.Id);
    if (!itemIds.length) return [];
  }
  const limit = 30;
  return (await market.search({ itemIds, sort, limit, offset: Math.max(0, page || 0) * limit })).map(toPublic);
}

async function mine(sellerId) {
  return (await market.listMine(sellerId)).map(toPublic);
}

module.exports = { createListing, buy, cancel, search, mine, tradable, MAX_ACTIVE_LISTINGS };
