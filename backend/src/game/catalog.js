// Du lieu game (gamedata/*.json - ban sao cua data/ o goc repo, xem scripts/sync-gamedata.js) nap
// 1 lan luc khoi dong va danh chi muc san - economyRules dung de kiem tra gia, cong thuc, nguon roi
// do... Server KHONG tin gia/cong thuc client gui len, luon tra cuu tu day.
const fs = require("fs");
const path = require("path");

const DIR = path.join(__dirname, "..", "..", "gamedata");
const read = (f) => JSON.parse(fs.readFileSync(path.join(DIR, f), "utf8"));

function build() {
  const items = new Map(read("items.json").map((i) => [i.Id, i]));
  const recipes = new Map(read("recipes.json").map((r) => [r.Id, r]));
  const cooking = new Map(read("cooking.json").map((r) => [r.Id, r]));
  const enemies = new Map(read("enemies.json").map((e) => [e.Id, e]));
  const achievements = new Map(read("achievements.json").map((a) => [a.Id, a]));
  const house = read("house.json").sort((a, b) => a.Level - b.Level);
  const starter = read("starter.json");
  const fish = new Set(read("fish.json").map((f) => f.ItemId));
  const forage = new Set(read("forage.json").map((f) => f.ItemId));
  // Khu cho (vung an toan: khong danh nguoi/pha/xay) - toa do the gioi, dung chung voi Godot.
  const markets = read("markets.json");

  // Moi item co the roi tu quai nao + so vang toi da 1 con quai cho (kiem tra nguon "loot"/"kill").
  const lootItems = new Set();
  let maxEnemyGold = 0;
  for (const e of enemies.values()) {
    for (const l of e.Loot || []) lootItems.add(l.ItemId);
    maxEnemyGold = Math.max(maxEnemyGold, e.GoldReward || 0);
  }
  const recipeOutputs = new Set([...recipes.values(), ...cooking.values()].map((r) => r.OutputId));

  return { items, recipes, cooking, enemies, achievements, house, starter, fish, forage, lootItems, maxEnemyGold, recipeOutputs, markets };
}

const catalog = build();

module.exports = { catalog, build };
