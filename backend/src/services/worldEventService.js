// Giao dich cho su kien the gioi (ruong tiep te, doan thuong nhan) - transaction co khoa vi nhu moi
// thay doi kinh te khac.
const econ = require("../repositories/economyRepository");
const { ensureWallet } = require("./economyService");
const { catalog } = require("../game/catalog");
const { AppError } = require("../utils/AppError");

// counter: bo dem nhiem vu ngay/tuan cong them (vd "act:boss", "act:dungeon").
async function grantLoot(playerId, items, gold, counter = null) {
  return econ.withTransaction(async (client) => {
    await ensureWallet(client, playerId);
    const given = {};
    for (const [id, n] of Object.entries(items)) if (catalog.items.has(id) && n > 0) { await econ.adjustItem(client, playerId, id, n); given[id] = n; }
    if (gold > 0) await econ.addGold(client, playerId, gold);
    if (counter) await econ.bumpCounter(client, playerId, counter, 1);
    return { items: given, gold };
  });
}

async function buyFromCaravan(playerId, itemId, n, unitPrice) {
  if (!catalog.items.has(itemId) || !(n > 0)) throw new AppError(400, "Vat pham khong hop le.");
  const cost = unitPrice * n;
  return econ.withTransaction(async (client) => {
    const w = await ensureWallet(client, playerId);
    if (Number(w.gold) < cost) throw new AppError(400, "Khong du vang.");
    await econ.addGold(client, playerId, -cost);
    await econ.adjustItem(client, playerId, itemId, n);
    return { itemId, n, cost };
  });
}

module.exports = { grantLoot, buyFromCaravan };
