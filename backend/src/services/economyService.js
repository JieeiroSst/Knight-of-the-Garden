// Kinh te do SERVER quan ly: vang + vat pham cua moi nguoi choi nam trong wallets/inventory_items.
// Client gui lo "op" (y dinh) -> server kiem tra bang economyRules trong 1 transaction (khoa vi)
// -> tra ve anh chup (snapshot) chinh thuc de client dong bo lai.
const repo = require("../repositories/economyRepository");
const rules = require("../game/economyRules");
const { catalog } = require("../game/catalog");
const time = require("./timeService");
const market = require("../repositories/marketRepository");
const itemState = require("../game/itemState");

// Tao vi lan dau: nguoi choi cu (da co ban luu) -> nhap 1 lan tu ban luu; nguoi moi -> bo khoi dau.
async function ensureWallet(client, playerId) {
  let w = await repo.lockWallet(client, playerId);
  if (w) return w;
  const save = await repo.readSaveData(client, playerId);
  const init = save ? rules.importLegacy(save, catalog) : { ...rules.starterState(catalog), houseLevel: 0 };
  await repo.createWallet(client, playerId, init.gold, init.houseLevel || 0, init.items);
  return repo.lockWallet(client, playerId);
}

// Tao/khoa vi cua NHIEU nguoi theo thu tu id tang dan - moi transaction dung 2 vi tro len deu khoa
// cung 1 thu tu, tranh deadlock giua giao dich va mua ban cho dien ra cung luc.
async function ensureWallets(client, ids) {
  for (const id of [...new Set(ids)].sort((a, b) => a - b)) await ensureWallet(client, id);
  return repo.lockWallets(client, ids);
}

function toSnapshot(state, seq, events = [], rejected = []) {
  return {
    // Han dung / do ben theo loai vat pham dang co (client hien thanh do ben, dem gio hong).
    states: Object.fromEntries(state.itemStates || []),
    gold: state.gold,
    houseLevel: state.houseLevel,
    items: Object.fromEntries(state.items),
    bin: state.bin.map((b) => ({ day: b.day, item: b.item, count: b.count, unitPrice: b.unitPrice })),
    seq,
    events,
    rejected,
    time: time.now(),
  };
}

// seq: so thu tu lo tang dan phia client - gui lai lo cu (mat mang, retry) se KHONG ap dung 2 lan.
async function applyBatch(playerId, seq, ops) {
  const info = time.now();
  return repo.withTransaction(async (client) => {
    const w = await ensureWallet(client, playerId);
    const itemsBefore = await repo.readItems(client, playerId);
    // Tin rao het han -> hoan vat pham cho nguoi ban (tinh vao trang thai moi, ghi lai ben duoi).
    const expired = await market.expireForSeller(client, playerId);
    const statesBefore = await repo.readStates(client, playerId);
    const state = {
      gold: Number(w.gold),
      houseLevel: w.house_level,
      items: new Map(itemsBefore),
      bin: await repo.readBin(client, playerId),
      counters: new Map(),
      claimed: new Set(),
      itemStates: new Map(statesBefore),
    };
    const nowMs = Date.now();
    const lastSeq = Number(w.last_batch_seq);
    const events = [];
    for (const e of expired) {
      const have = state.items.get(e.item_id) || 0;
      state.itemStates.set(e.item_id, itemState.give(state.itemStates.get(e.item_id), have, e.count, e.item_state, catalog.items.get(e.item_id), nowMs));
      state.items.set(e.item_id, have + e.count);
      events.push({ type: "market_expired", item: e.item_id, count: e.count });
    }

    // Thit/nong san/mon an qua han -> thoi rua thanh "do_thoi" (khong an/nau/ban duoc nua).
    spoilAll(state, nowMs, events);

    const payout = rules.settleShipping(state, info.day);
    if (payout) events.push(payout);

    let rejected = [];
    let newClaims = [];
    const duplicate = Number.isInteger(seq) && seq > 0 && seq <= lastSeq;
    if (!duplicate && Array.isArray(ops) && ops.length) {
      const bucket = rules.hourBucket(info.serverNow);
      const countersBefore = await repo.readCounters(client, playerId, bucket);
      state.counters = new Map(countersBefore);
      const keys = ops.map((o) => o?.key).filter((k) => typeof k === "string");
      keys.push(...ops.filter((o) => o?.t === "achievement").map((o) => `ach_${o.id}`));
      state.claimed = await repo.readClaims(client, playerId, keys);
      // Tram che tao da xay (chi can khi lo co lenh che tao).
      const stations = ops.some((o) => o?.t === "craft")
        ? (await client.query("SELECT DISTINCT kind FROM base_structures WHERE owner_id = $1", [playerId])).rows.map((r) => r.kind)
        : [];
      const result = rules.applyOps(state, ops, { day: info.day, nowMs: info.serverNow, catalog, stations });
      rejected = result.rejected;
      newClaims = result.newClaims;
      await repo.writeCounters(client, playerId, bucket, countersBefore, state.counters);
      await repo.insertClaims(client, playerId, newClaims);
    }

    // Dong bo trang thai voi so luong cuoi (an/ban -> bo lo cu/mon te; thu hoach/mua -> lo moi/mon moi).
    const statesAfter = new Map();
    for (const [id, n] of state.items) {
      const st = itemState.normalize(state.itemStates.get(id), n, catalog.items.get(id), nowMs);
      if (st) statesAfter.set(id, st);
    }
    state.itemStates = statesAfter;
    await repo.writeItems(client, playerId, itemsBefore, state.items);
    await repo.writeStates(client, playerId, statesBefore, statesAfter);
    await repo.writeBin(client, playerId, state.bin);
    await repo.updateWallet(client, playerId, state.gold, state.houseLevel, duplicate ? lastSeq : Math.max(lastSeq, seq || 0));
    return toSnapshot(state, duplicate ? lastSeq : Math.max(lastSeq, seq || 0), events, rejected);
  });
}

const getSnapshot = (playerId) => applyBatch(playerId, 0, []);

function spoilAll(state, nowMs, events) {
  for (const [id, n] of [...state.items]) {
    const def = catalog.items.get(id);
    if (!itemState.shelfMs(def)) continue;
    const { state: st, spoiled } = itemState.spoil(itemState.normalize(state.itemStates.get(id), n, def, nowMs), nowMs);
    if (!spoiled) continue;
    const left = n - spoiled;
    if (left > 0) state.items.set(id, left); else state.items.delete(id);
    state.itemStates.set(id, left > 0 ? st : null);
    if (catalog.items.has(itemState.ROTTEN_ID))
      state.items.set(itemState.ROTTEN_ID, (state.items.get(itemState.ROTTEN_ID) || 0) + spoiled);
    events.push({ type: "spoiled", item: id, count: spoiled });
  }
}

module.exports = { applyBatch, getSnapshot, ensureWallet, ensureWallets };
