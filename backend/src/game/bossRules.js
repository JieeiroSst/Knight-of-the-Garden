// Trum the gioi + trum ham nguc to doi + truy na - ham THUAN (unit test duoc).
//  - Trum the gioi: xuat hien dinh ky o vanh hoang da quanh thi tran, MAU CHUNG tren server (moi nguoi
//    cung danh 1 con), phan thuong chia theo sat thuong dong gop.
//  - Trum ham nguc (Rong Co Dai tang 5): moi TO DOI 1 con, mau tang theo so thanh vien, ca doi cung nhan
//    thuong (moi nguoi 1 lan/ngay).
//  - Truy na: ha nguoi vo toi -> bi treo thuong (tang dan); ha ke bi truy na -> lanh thuong.

// ==== Trum the gioi ====
const WORLD_BOSS_KINDS = ["boss_cave", "boss_ruins", "boss_cemetery", "boss_mountain", "boss_swamp", "boss_forest"];
const BOSS_EVERY_MS = [60 * 60000, 90 * 60000];
const BOSS_FIRST_MS = 10 * 60000;    // lan dau sau khi server chay
const BOSS_STAY_MS = 25 * 60000;
const BOSS_WARN_MS = 3 * 60000;       // bao truoc toa do 3 phut roi moi thuc day
const WORLD_BOSS_HP = 14000;
const BOSS_GOLD_POOL = 5000;
const MIN_SHARE = 0.01;               // dong gop < 1% mau -> khong co thuong (chong "cham 1 cai an thuong")

// ==== Danh trum (ca 2 loai) ====
const MAX_HIT = 260;                  // sat thuong toi da 1 don (vu khi tot nhat + cuong hoa + chi mang)
const HIT_COOLDOWN_MS = 300;
const HIT_RANGE = 450;                // tinh tu tam trum (trum to + cung ban xa)

// ==== Ham nguc to doi ====
const DUNGEON_BOSS = "boss_dungeon";
const DUNGEON_BASE_HP = 2000;
const DUNGEON_HP_PER_MEMBER = 0.7;    // +70% mau moi thanh vien them
const DUNGEON_STAY_MS = 30 * 60000;
const DUNGEON_GOLD = 600;
const DUNGEON_PARTY_BONUS = 0.15;     // +15% thuong moi thanh vien them (khuyen khich di doi)
const DUNGEON_JOIN_RANGE = 700;       // phai o trong ham nguc (quanh cong vao, tren cao) moi tham gia
// Cong Ham Nguc Co (Godot: Main.DungeonEntrancePos = RuinsRegionCenter + (0,0,560)); cac tang nam ngay
// tren cong (Y >= 500) - xem MainAdventure.BuildDungeon.
const DUNGEON_X = 6700, DUNGEON_Z = -2090, DUNGEON_MIN_Y = 400;

// ==== Truy na ====
const BOUNTY_BASE = 100;
const BOUNTY_STEP = 50;
const BOUNTY_MAX_STEPS = 10;
const BOUNTY_WANTED = 100;            // >= muc nay hien "TRUY NA" tren dau + bang truy na
const BOUNTY_PLACE_MIN = 100;
const BOUNTY_PLACE_MAX = 100000;

const randIn = (rand, [a, b]) => a + rand() * (b - a);

function dungeonHp(members) {
  const n = Math.max(1, Math.min(8, members | 0));
  return Math.round(DUNGEON_BASE_HP * (1 + DUNGEON_HP_PER_MEMBER * (n - 1)));
}

function dungeonGold(members) {
  const n = Math.max(1, Math.min(8, members | 0));
  return Math.round(DUNGEON_GOLD * (1 + DUNGEON_PARTY_BONUS * (n - 1)));
}

// Gioi han 1 don: so nguyen duong, khong vuot MAX_HIT.
function clampHit(dmg) {
  const d = Math.floor(Number(dmg));
  if (!Number.isFinite(d) || d <= 0) return 0;
  return Math.min(d, MAX_HIT);
}

// Loot cua trum lay tu chinh bang loot quai (catalog.enemies) - moi nguoi du dieu kien 1 luot.
function rollBossLoot(def, rand = Math.random, bonus = false) {
  const items = {};
  for (const l of def?.Loot || []) {
    const chance = Math.min(1, (l.Chance ?? 1) * (bonus ? 1.5 : 1));
    if (rand() > chance) continue;
    const n = (l.Min ?? 1) + Math.floor(rand() * ((l.Max ?? 1) - (l.Min ?? 1) + 1));
    if (n > 0) items[l.ItemId] = (items[l.ItemId] || 0) + n;
  }
  return items;
}

// Chia thuong trum the gioi theo sat thuong: dmg = { playerId: so mau da gay }.
// Top 3 nhan them 1 luot loot "may man" (+50% ty le) + kim cuong.
function worldBossRewards(dmg, maxHp, def, rand = Math.random) {
  const rows = Object.entries(dmg)
    .map(([id, d]) => ({ playerId: Number(id), dmg: Number(d) }))
    .filter((r) => Number.isInteger(r.playerId) && r.dmg > 0)
    .sort((a, b) => b.dmg - a.dmg);
  const total = rows.reduce((s, r) => s + r.dmg, 0) || 1;
  const out = [];
  rows.forEach((r, rank) => {
    const share = r.dmg / Math.max(total, maxHp);
    if (share < MIN_SHARE) return;
    const items = rollBossLoot(def, rand, rank < 3);
    if (rank < 3) items.kim_cuong = (items.kim_cuong || 0) + (3 - rank);
    out.push({ playerId: r.playerId, rank: rank + 1, share, gold: Math.max(100, Math.round(BOSS_GOLD_POOL * (r.dmg / total))), items });
  });
  return out;
}

// Tien treo thuong he thong cong them cho ke ha nguoi vo toi lan thu (kills+1).
function bountyForKill(kills) {
  return BOUNTY_BASE + BOUNTY_STEP * Math.min(Math.max(0, kills | 0), BOUNTY_MAX_STEPS);
}

module.exports = {
  WORLD_BOSS_KINDS, BOSS_EVERY_MS, BOSS_FIRST_MS, BOSS_STAY_MS, BOSS_WARN_MS, WORLD_BOSS_HP, BOSS_GOLD_POOL, MIN_SHARE,
  MAX_HIT, HIT_COOLDOWN_MS, HIT_RANGE,
  DUNGEON_BOSS, DUNGEON_BASE_HP, DUNGEON_STAY_MS, DUNGEON_JOIN_RANGE, DUNGEON_X, DUNGEON_Z, DUNGEON_MIN_Y, dungeonHp, dungeonGold,
  BOUNTY_WANTED, BOUNTY_PLACE_MIN, BOUNTY_PLACE_MAX, bountyForKill,
  randIn, clampHit, rollBossLoot, worldBossRewards,
};
