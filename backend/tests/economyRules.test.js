// Unit test luat kinh te (khong can DB).
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://test:test@localhost:5432/test";

const test = require("node:test");
const assert = require("node:assert/strict");
const { applyOps, settleShipping, starterState, importLegacy } = require("../src/game/economyRules");
const { catalog } = require("../src/game/catalog");

const state = (gold = 0, items = {}) => ({
  gold, houseLevel: 0, items: new Map(Object.entries(items)), counters: new Map(), claimed: new Set(), bin: [],
});
const ctx = { day: 10, nowMs: Date.now(), catalog };

test("mua: tru dung gia trong catalog, khong tin gia client", () => {
  const s = state(100);
  const price = catalog.items.get("tomato_seed").BuyPrice;
  const r = applyOps(s, [{ t: "buy", item: "tomato_seed", n: 2, price: 1 }], ctx);
  assert.equal(r.rejected.length, 0);
  assert.equal(s.gold, 100 - price * 2);
  assert.equal(s.items.get("tomato_seed"), 2);
});

test("mua: khong du vang -> tu choi, khong doi trang thai", () => {
  const s = state(1);
  const r = applyOps(s, [{ t: "buy", item: "sword", n: 1 }], ctx);
  assert.equal(r.rejected.length, 1);
  assert.equal(s.gold, 1);
  assert.equal(s.items.size, 0);
});

test("ban: he so gia bi kep o MAX_PRICE_MULT", () => {
  const s = state(0, { pumpkin: 1 });
  applyOps(s, [{ t: "sell", item: "pumpkin", n: 1, mult: 1000 }], ctx);
  assert.equal(s.gold, Math.round(catalog.items.get("pumpkin").SellPrice * 2.2));
});

test("che tao: dung nguyen lieu tu cong thuc server", () => {
  const s = state(0, { rong: 2, wood: 1 });
  const r = applyOps(s, [{ t: "craft", recipe: "r_fertilizer" }], ctx);
  assert.equal(r.rejected.length, 0);
  assert.equal(s.items.get("fertilizer_basic"), 2);
  assert.equal(s.items.has("rong"), false);
  const r2 = applyOps(s, [{ t: "craft", recipe: "r_fertilizer" }], ctx);
  assert.equal(r2.rejected[0].reason, "thieu nguyen lieu");
});

test("nhan do: nguon phai hop voi vat pham", () => {
  const s = state();
  const r = applyOps(s, [
    { t: "gain", item: "pumpkin", n: 1, src: "harvest" },
    { t: "gain", item: "kim_cuong", n: 1, src: "harvest" },
    { t: "gain", item: "sword", n: 1, src: "misc" },
  ], ctx);
  assert.deepEqual(r.rejected.map((x) => x.index), [1, 2]);
  assert.equal(s.items.get("pumpkin"), 1);
});

test("nhan do: vuot gioi han moi gio bi chan", () => {
  const s = state();
  const ops = Array.from({ length: 7 }, () => ({ t: "gain", item: "ca", n: 25, src: "fish" }));
  const r = applyOps(s, ops, ctx);
  assert.equal(s.items.get("ca"), 150);
  assert.equal(r.rejected.length, 1);
});

test("phan thuong 1 lan: cung key lan 2 bi tu choi", () => {
  const s = state();
  const r = applyOps(s, [
    { t: "gold+", n: 300, src: "chest", key: "chest_forest_0" },
    { t: "gold+", n: 300, src: "chest", key: "chest_forest_0" },
    { t: "gold+", n: 300, src: "chest" },
  ], ctx);
  assert.equal(s.gold, 300);
  assert.equal(r.rejected.length, 2);
  assert.deepEqual(r.newClaims, ["chest_forest_0"]);
});

test("thanh tuu: vang lay tu catalog, chi 1 lan", () => {
  const s = state();
  applyOps(s, [{ t: "achievement", id: "first_harvest" }, { t: "achievement", id: "first_harvest" }], ctx);
  assert.equal(s.gold, catalog.achievements.get("first_harvest").RewardGold);
});

test("thung giao hang: ban vao ngay sau, hang hom nay chua ban", () => {
  const s = state(0, { pumpkin: 3 });
  applyOps(s, [{ t: "ship", item: "pumpkin", n: 3, mult: 1 }], ctx);
  assert.equal(settleShipping(s, 10), null);
  const ev = settleShipping(s, 11);
  assert.equal(ev.gold, catalog.items.get("pumpkin").SellPrice * 3);
  assert.equal(s.bin.length, 0);
  assert.equal(s.gold, ev.gold);
});

test("nang cap nha: tru vang + vat lieu theo house.json", () => {
  const s = state(5000, { wood: 100, da: 50 });
  const r = applyOps(s, [{ t: "house" }], ctx);
  assert.equal(r.rejected.length, 0);
  assert.equal(s.houseLevel, 1);
  assert.equal(s.gold, 4000);
});

test("tieu thu khong du -> tu choi", () => {
  const s = state(0, { egg: 1 });
  const r = applyOps(s, [{ t: "consume", item: "egg", n: 2 }, { t: "gold-", n: 5 }], ctx);
  assert.equal(r.rejected.length, 2);
  assert.equal(s.items.get("egg"), 1);
});

test("bo khoi dau + nhap ban luu cu (bo item la, kep so luong)", () => {
  assert.ok(starterState().items.get("hoe") >= 1);
  const imp = importLegacy({ Gold: 123, Inventory: [{ Id: "egg", Count: 5 }, { Id: "khong_ton_tai", Count: 9 }], Backpack: [{ Id: "egg", Count: 1 }] });
  assert.equal(imp.gold, 123);
  assert.equal(imp.items.get("egg"), 6);
  assert.equal(imp.items.has("khong_ton_tai"), false);
});
