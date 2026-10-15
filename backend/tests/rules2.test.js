process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://test:test@localhost:5432/test";
const test = require("node:test");
const assert = require("node:assert/strict");
const daily = require("../src/game/dailyRules");
const combat = require("../src/game/combatRules");
const { catalog } = require("../src/game/catalog");

test("diem danh: chuoi lien tiep tang, bo ngay ve 1, cung ngay giu nguyen", () => {
  assert.equal(daily.nextStreak(null, 10), 1);
  assert.equal(daily.nextStreak({ last_day: 9, streak: 3 }, 10), 4);
  assert.equal(daily.nextStreak({ last_day: 7, streak: 3 }, 10), 1);
  assert.equal(daily.nextStreak({ last_day: 10, streak: 3 }, 10), 3);
  assert.equal(daily.checkinReward(8).gold, daily.CHECKIN_REWARDS[0].gold);
});

test("nhiem vu: 3 nhiem vu khac nhau, tat dinh theo nguoi choi + ky", () => {
  const a = daily.pickQuests(42, 100, 1).map((q) => q.id);
  assert.equal(new Set(a).size, 3);
  assert.deepEqual(daily.pickQuests(42, 100, 1).map((q) => q.id), a);
  assert.notDeepEqual(daily.pickQuests(43, 100, 1).map((q) => q.id).concat(daily.pickQuests(42, 101, 1).map((q) => q.id)), a.concat(a));
});

test("dau truong: suc manh tu do so huu + cuong hoa, sat thuong toi thieu 1", () => {
  const p = combat.duelPower(["sword", "shield", "egg"], new Map([["sword", 2]]), catalog);
  assert.equal(p.attack, Math.round(catalog.items.get("sword").Damage * 1.2));
  assert.equal(p.defense, catalog.items.get("shield").Defense);
  assert.ok(combat.duelDamage({ attack: 1 }, { defense: 999 }, () => 0.5) >= 1);
});

test("Elo: thang doi manh hon duoc nhieu diem hon", () => {
  const upset = combat.elo(1000, 1400).delta;
  const expected = combat.elo(1400, 1000).delta;
  assert.ok(upset > expected);
  assert.equal(combat.elo(1000, 1000).delta, 16);
});
