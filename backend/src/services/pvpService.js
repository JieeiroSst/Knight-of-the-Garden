// PvP the gioi mo + can cu. MOI thay doi do/vang chay trong 1 transaction co khoa vi (giong
// trade/market) -> khong the nhan ban do du 2 nguoi bam nhat cung luc hay mang chap chon.
const econ = require("../repositories/economyRepository");
const repo = require("../repositories/pvpRepository");
const { ensureWallet, ensureWallets } = require("./economyService");
const rules = require("../game/pvpRules");
const { catalog } = require("../game/catalog");
const { AppError } = require("../utils/AppError");
const cache = require("../cache/cache");
const itemState = require("../game/itemState");

// ==== Khoa cache (xem cache/cache.js - DB commit TRUOC, xoa cache SAU) ====
const STRUCT_CELL_TTL = 120;
const BAG_CELL_TTL = 60;
const sCellKey = (zone, x, z) => `scell:${zone}:${cache.cellOf(x)}:${cache.cellOf(z)}`;
const bCellKey = (zone, channel, x, z) => `bcell:${zone}:${channel}:${cache.cellOf(x)}:${cache.cellOf(z)}`;
const myKey = (ownerId) => `mystruct:${ownerId}`;
const respawnKey = (id) => `respawn:${id}`;

const fail = (msg) => { throw new AppError(400, msg); };

const publicBag = (b) => ({
  id: Number(b.id), x: b.x, z: b.z, ownerId: b.owner_id, killerId: b.killer_id, ownerName: b.owner_name,
  priorityUntil: new Date(b.priority_until).getTime(), expiresAt: new Date(b.expires_at).getTime(),
});

const publicStructure = (s) => ({
  id: Number(s.id), ownerId: s.owner_id, ownerName: s.owner_name || "", kind: s.kind, x: s.x, z: s.z, rot: s.rot, hp: s.hp, maxHp: s.max_hp,
});

// Nguoi choi bi ha: lay DO TRONG TUI CHINH (danh sach client bao, cat theo so huu that) -> tui roi.
async function dropDeathBag({ loserId, loserName, killerId, manifest, zone, channel, x, z }) {
  const clean = rules.cleanManifest(manifest, catalog);
  return econ.withTransaction(async (client) => {
    await ensureWallet(client, loserId);
    const owned = await econ.readItems(client, loserId);
    const loss = rules.lossFromManifest(clean, owned);
    if (!loss.size) return null;
    const states = {};
    for (const [id, n] of loss) {
      const carry = await econ.adjustItem(client, loserId, id, -n);
      if (!carry) fail("Khong tru duoc do.");
      if (carry !== true && Object.keys(carry).length) states[id] = carry; // han dung/do ben di theo vao tui roi
    }
    const bag = await repo.insertBag(client, {
      zone, channel, x, z, ownerId: loserId, killerId, ownerName: loserName, items: Object.fromEntries(loss), gold: 0, states,
      priorityMs: rules.BAG_PRIORITY_MS, lifetimeMs: rules.BAG_LIFETIME_MS,
    });
    return { bag: publicBag(bag), items: Object.fromEntries(loss) };
  }).then(async (r) => { if (r) await cache.invalidate(bCellKey(zone, channel, x, z)); return r; });
}

async function lootBag(bagId, playerId, now = Date.now()) {
  return econ.withTransaction(async (client) => {
    const bag = await repo.lockBag(client, bagId);
    if (!bag) fail("Tui do khong ton tai.");
    const why = rules.canLootBag(bag, playerId, now);
    if (why) fail(why);
    await ensureWallet(client, playerId);
    for (const [id, n] of Object.entries(bag.items || {})) await econ.adjustItem(client, playerId, id, n, bag.states?.[id] || null);
    if (bag.gold > 0) await econ.addGold(client, playerId, bag.gold);
    await repo.markLooted(client, bagId, playerId);
    return { bag: publicBag(bag), items: bag.items || {}, gold: bag.gold, zone: bag.zone, channel: bag.channel };
  }).then(async (r) => { await cache.invalidate(bCellKey(r.zone, r.channel, r.bag.x, r.bag.z)); return r; });
}

// Tui quanh vi tri: doc tung o luoi qua cache (nhieu nguoi cung vung -> 1 lan doc DB moi o/TTL).
async function bagsNear(zone, channel, x, z, radius) {
  const now = Date.now();
  const out = [];
  for (const [cx, cz] of cache.cellsInRadius(x, z, radius)) {
    const rows = await cache.cached(`bcell:${zone}:${channel}:${cx}:${cz}`, BAG_CELL_TTL,
      async () => (await repo.bagsInCell(zone, channel, cx, cz, cache.CELL)).map(publicBag));
    for (const b of rows) if (b.expiresAt > now && Math.abs(b.x - x) <= radius && Math.abs(b.z - z) <= radius) out.push(b);
  }
  return out;
}

async function purgeBags() {
  const gone = await repo.purgeBags();
  await cache.invalidate([...new Set(gone.map((b) => bCellKey(b.zone, b.channel, b.x, b.z)))]);
  return gone;
}

// ==== Can cu ====
async function placeStructure(ownerId, itemId, zone, x, z, rot) {
  const def = rules.STRUCTURES[itemId];
  if (!def) fail("Vat pham nay khong xay duoc.");
  if (!rules.canBuildIn(zone)) fail("Khong the xay trong khu vuc nay.");
  return econ.withTransaction(async (client) => {
    await ensureWallet(client, ownerId);
    if ((await repo.countStructures(client, ownerId)) >= rules.MAX_STRUCTURES_PER_PLAYER)
      fail(`Toi da ${rules.MAX_STRUCTURES_PER_PLAYER} cong trinh moi nguoi.`);
    if (!(await econ.adjustItem(client, ownerId, itemId, -1))) fail("Ban khong co bo xay dung nay.");
    const s = await repo.insertStructure(client, { ownerId, kind: def.kind, zone, x, z, rot: Number(rot) || 0, hp: def.hp });
    // Dung giuong moi -> het bi phat "mat giuong".
    if (def.kind === "bed") {
      const st = await repo.getRespawn(client, ownerId);
      if (st?.bed_lost) await repo.setRespawn(client, ownerId, { bedLost: false, readyAt: st.ready_at });
    }
    return publicStructure(s);
  }).then(async (s) => { await cache.invalidate(sCellKey(zone, x, z), myKey(ownerId), respawnKey(ownerId)); return s; });
}

// Danh cong trinh. Pha ruong -> do ben trong roi thanh tui (nguoi pha duoc uu tien).
async function damageStructure(id, attackerId, dmg, channel) {
  return econ.withTransaction(async (client) => {
    const s = await repo.lockStructure(client, id);
    if (!s) fail("Cong trinh khong con.");
    if (s.owner_id === attackerId) fail("Khong the danh cong trinh cua chinh minh (hay go bo).");
    const hp = Math.max(0, s.hp - dmg);
    if (hp > 0) {
      await repo.setStructureHp(client, id, hp);
      return { structure: publicStructure({ ...s, hp }), destroyed: false, bag: null, raw: s };
    }
    let bag = null;
    if (s.kind === "chest") {
      const items = await repo.chestItems(client, id);
      if (items.size)
        bag = publicBag(await repo.insertBag(client, {
          zone: s.zone, channel, x: s.x, z: s.z, ownerId: s.owner_id, killerId: attackerId, ownerName: "",
          items: Object.fromEntries(items), gold: 0, states: await repo.chestStates(client, id), priorityMs: rules.BAG_PRIORITY_MS, lifetimeMs: rules.BAG_LIFETIME_MS,
        }));
    }
    await repo.deleteStructure(client, id);
    // Bi PHA (khong khoi phuc - chu phai tu xay lai). Giuong cuoi cung mat -> bi phat lan chet sau.
    let bedLost = false;
    if (s.kind === "bed" && (await repo.bedsOf(client, s.owner_id)).length === 0) {
      const st = await repo.getRespawn(client, s.owner_id);
      await repo.setRespawn(client, s.owner_id, { bedLost: true, readyAt: st?.ready_at || null });
      bedLost = true;
    }
    return { structure: publicStructure({ ...s, hp: 0 }), destroyed: true, bag, bedLost, raw: s };
  }).then(async (r) => {
    const s = r.raw;
    delete r.raw;
    await cache.invalidate(sCellKey(s.zone, s.x, s.z), r.destroyed && myKey(s.owner_id), r.bedLost && respawnKey(s.owner_id),
      r.bag && bCellKey(s.zone, channel, s.x, s.z));
    return r;
  });
}

async function repairStructure(id, playerId) {
  return econ.withTransaction(async (client) => {
    const s = await repo.lockStructure(client, id);
    if (!s) fail("Cong trinh khong con.");
    if (s.hp >= s.max_hp) fail("Cong trinh con nguyen ven.");
    await ensureWallet(client, playerId);
    for (const [item, n] of rules.REPAIR_COST)
      if (!(await econ.adjustItem(client, playerId, item, -n))) fail(`Can ${n} ${catalog.items.get(item)?.Name || item} de sua.`);
    const hp = Math.min(s.max_hp, s.hp + rules.REPAIR_AMOUNT);
    await repo.setStructureHp(client, id, hp);
    return publicStructure({ ...s, hp, zone: s.zone });
  }).then(async (r) => { await cache.invalidate(sCellKey(r.zone, r.x, r.z)); delete r.zone; return r; });
}

// Chu go bo: tra lai bo xay dung neu con >= 50% mau, do trong ruong ve vi chu.
async function removeStructure(id, ownerId) {
  return econ.withTransaction(async (client) => {
    const s = await repo.lockStructure(client, id);
    if (!s) fail("Cong trinh khong con.");
    if (s.owner_id !== ownerId) fail("Chi chu cong trinh moi go bo duoc.");
    await ensureWallet(client, ownerId);
    const items = s.kind === "chest" ? await repo.chestItems(client, id) : new Map();
    const chestSt = s.kind === "chest" ? await repo.chestStates(client, id) : {};
    for (const [item, n] of items) await econ.adjustItem(client, ownerId, item, n, chestSt[item] || null);
    const refund = s.hp * 2 >= s.max_hp ? rules.KIND_TO_ITEM[s.kind] : null;
    if (refund) await econ.adjustItem(client, ownerId, refund, 1);
    await repo.deleteStructure(client, id);
    return { structure: publicStructure(s), refund, items: Object.fromEntries(items), zone: s.zone };
  }).then(async (r) => { await cache.invalidate(sCellKey(r.zone, r.structure.x, r.structure.z), myKey(ownerId)); return r; });
}

async function chestContents(id) {
  return Object.fromEntries(await repo.chestItems(null, id));
}

// delta > 0: cat tu vi nguoi choi vao ruong; < 0: lay ra.
async function chestMove(id, playerId, itemId, delta) {
  const n = Math.floor(Math.abs(Number(delta)));
  if (!catalog.items.has(itemId) || !(n > 0) || n > rules.CHEST_MAX_PER_KIND) fail("So luong khong hop le.");
  return econ.withTransaction(async (client) => {
    const s = await repo.lockStructure(client, id);
    if (!s || s.kind !== "chest") fail("Ruong khong con.");
    await ensureWallets(client, [playerId]);
    const items = await repo.chestItems(client, id);
    if (delta > 0) {
      if (!items.has(itemId) && items.size >= rules.CHEST_MAX_KINDS) fail(`Ruong chua toi da ${rules.CHEST_MAX_KINDS} loai do.`);
      if ((items.get(itemId) || 0) + n > rules.CHEST_MAX_PER_KIND) fail("Ruong day cho loai do nay.");
      const carry = await econ.adjustItem(client, playerId, itemId, -n);
      if (!carry) fail("Ban khong du do de cat.");
      await repo.adjustChest(client, id, itemId, n, carry);
    } else {
      const carry = await repo.adjustChest(client, id, itemId, -n);
      if (!carry) fail("Ruong khong du do.");
      await econ.adjustItem(client, playerId, itemId, n, carry);
    }
    return Object.fromEntries(await repo.chestItems(client, id));
  });
}

// Cong trinh quanh vi tri: tung o luoi qua cache. Cong trinh it thay doi (chi khi xay/danh/sua/go)
// nen phan lon lan vao vung / di chuyen deu doc tu Redis thay vi Postgres.
async function structuresNear(zone, x, z, radius) {
  const out = [];
  for (const [cx, cz] of cache.cellsInRadius(x, z, radius)) {
    const rows = await cache.cached(`scell:${zone}:${cx}:${cz}`, STRUCT_CELL_TTL,
      async () => (await repo.structuresInCell(zone, cx, cz, cache.CELL)).map(publicStructure));
    for (const s of rows) if (Math.abs(s.x - x) <= radius && Math.abs(s.z - z) <= radius) out.push(s);
  }
  return out;
}
const getStructure = async (id) => { const s = await repo.getStructure(id); return s ? publicStructure(s) : null; };
const myStructures = (ownerId) => cache.cached(myKey(ownerId), 300, async () => (await repo.myStructures(ownerId)).map(publicStructure));

// ==== Hoi sinh ====
// Dang phai cho hoi sinh? -> { mode: "wait", readyAt } hoac null.
async function respawnState(playerId) {
  const st = await cache.cached(respawnKey(playerId), 60, async () => {
    const r = await repo.getRespawn(null, playerId);
    return r ? { bedLost: r.bed_lost, readyAt: r.ready_at ? new Date(r.ready_at).getTime() : 0 } : null;
  });
  return st && st.readyAt > Date.now() ? { mode: "wait", readyAt: st.readyAt } : null;
}

// Nguoi choi vua chet tai (x, z) -> hoi sinh o dau / bao gio. Dang trong thoi gian cho (chet lien
// tiep, dang nhap lai) -> giu nguyen han cu, khong cong them.
async function decideRespawn(playerId, x, z) {
  const now = Date.now();
  return econ.withTransaction(async (client) => {
    const st = await repo.getRespawn(client, playerId);
    const waitingUntil = st?.ready_at ? new Date(st.ready_at).getTime() : 0;
    if (waitingUntil > now) return { mode: "wait", readyAt: waitingUntil };
    const beds = await repo.bedsOf(client, playerId);
    const d = rules.respawnDecision({ beds, bedLost: !!st?.bed_lost, deathX: x, deathZ: z, now });
    // Bi phat 1 lan: bat dau dem gio, xoa co mat giuong (doi xong la het).
    if (d.mode === "wait") await repo.setRespawn(client, playerId, { bedLost: false, readyAt: d.readyAt });
    return d;
  }).then(async (d) => { await cache.invalidate(respawnKey(playerId)); return d; });
}

// Hao mon do ben do SERVER tinh (vd giap trung don PvP) - gom nhieu don roi ghi 1 lan.
async function wearItems(playerId, wear) {
  const entries = Object.entries(wear).filter(([id, n]) => n > 0 && itemState.maxDur(catalog.items.get(id)));
  if (!entries.length) return;
  await econ.withTransaction(async (client) => {
    await ensureWallet(client, playerId);
    const counts = await econ.readItems(client, playerId);
    for (const [id, n] of entries) {
      const have = counts.get(id) || 0;
      if (!have) continue;
      const r = await client.query("SELECT state FROM item_states WHERE player_id = $1 AND item_id = $2 FOR UPDATE", [playerId, id]);
      const st = itemState.normalize(r.rows[0]?.state || null, have, catalog.items.get(id), Date.now());
      await econ.writeState(client, playerId, id, itemState.wear(st, n));
    }
  });
  await cache.invalidate(`power:${playerId}`);
}

// Ho tro dong doi: dung 1 Thuoc Hoi Mau cua minh de hoi mau cho nguoi khac.
async function consumeHealPotion(healerId) {
  return econ.withTransaction(async (client) => {
    await ensureWallet(client, healerId);
    if (!(await econ.adjustItem(client, healerId, "potion", -1))) fail("Can 1 Thuoc Hoi Mau de ho tro.");
    return true;
  });
}

module.exports = {
  dropDeathBag, lootBag, bagsNear, purgeBags,
  placeStructure, damageStructure, repairStructure, removeStructure, chestContents, chestMove,
  structuresNear, getStructure, myStructures, consumeHealPotion, publicBag, respawnState, decideRespawn, wearItems,
};
