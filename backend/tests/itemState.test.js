// Han su dung + do ben (ham thuan) va op wear/repair trong economyRules.
const test = require("node:test");
const assert = require("node:assert/strict");
const s = require("../src/game/itemState");
const { applyOps } = require("../src/game/economyRules");
const { catalog } = require("../src/game/catalog");

const H = 3600000;
const meat = { ShelfHours: 12 };
const sword = { MaxDurability: 250 };

test("han dung: an lo cu truoc, het han thanh do thoi, lo moi them sau", () => {
  let st = s.give(null, 0, 5, null, meat, 0);
  st = s.give(st, 5, 3, null, meat, 6 * H);
  const t = s.take(st, 8, 6, meat, 7 * H);
  assert.deepEqual(t.removed.fresh, [[12 * H, 5], [18 * H, 1]]);
  assert.deepEqual(t.state.fresh, [[18 * H, 2]]);
  assert.equal(s.spoil(t.state, 13 * H).spoiled, 0);
  assert.equal(s.spoil(t.state, 19 * H).spoiled, 2);
});

test("trang thai DI THEO do: kiem cun chuyen sang nguoi khac van cun, thit cat ruong khong tuoi lai", () => {
  let d = s.give(null, 0, 1, null, sword, 0);
  d = s.wear(d, 180);
  const out = s.take(d, 1, 1, sword, 0);
  assert.deepEqual(s.give(null, 0, 1, out.removed, sword, 0).dur, [70]);
  const m = s.give(null, 0, 4, null, meat, 0);
  const moved = s.take(m, 4, 4, meat, 10 * H);
  const chest = s.give(null, 0, 4, moved.removed, meat, 10 * H);
  assert.deepEqual(chest.fresh, [[12 * H, 4]], "van het han luc 12h, khong reset");
});

test("do ben: duoi 40% khong dung duoc; sua ve toi da; mon tot nhat dung truoc", () => {
  assert.ok(s.usable(100, 250));
  assert.equal(s.usable(99, 250), false);
  let d = s.give(null, 0, 2, null, sword, 0);
  d = s.wear(d, 200);
  assert.deepEqual(d.dur, [250, 50], "mon con nguyen tu len dung");
  assert.deepEqual(s.repair(d, sword).dur, [250, 250]);
  assert.equal(s.repair({ dur: [250] }, sword), null);
});

test("op wear/repair: sua ton vang + nguyen lieu theo items.json, do nguyen ven thi tu choi", () => {
  const def = catalog.items.get("kiem_sat");
  assert.ok(def.MaxDurability > 0, "kiem sat co do ben");
  const cost = s.repairCost(def);
  const state = { gold: cost.gold + 5, houseLevel: 0, items: new Map([["kiem_sat", 1], ...cost.mats]), counters: new Map(), claimed: new Set(), bin: [] };
  const ctx = { day: 1, nowMs: 1000, catalog };
  let r = applyOps(state, [{ t: "repair", item: "kiem_sat" }], ctx);
  assert.equal(r.rejected[0].reason, "do con nguyen ven");
  r = applyOps(state, [{ t: "wear", item: "kiem_sat", n: 200 }, { t: "repair", item: "kiem_sat" }], ctx);
  assert.equal(r.rejected.length, 0, JSON.stringify(r.rejected));
  assert.equal(state.gold, 5);
  assert.deepEqual(state.itemStates.get("kiem_sat").dur, [def.MaxDurability]);
  for (const [id] of cost.mats) assert.equal(state.items.get(id) || 0, 0);
  r = applyOps(state, [{ t: "wear", item: "potion", n: 1 }, { t: "wear", item: "kiem_sat", n: 9999 }], ctx);
  assert.equal(r.rejected.length, 2);
});
