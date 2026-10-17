// Luat chien dau dau truong + cuong hoa - ham THUAN.

// Cuong hoa +N: ti le thanh cong, vat lieu, vang. That bai tu +6 tro len tut 1 cap.
const ENHANCE = [
  { chance: 1.0, gold: 100, mats: [["thoi_sat", 1]] },
  { chance: 0.95, gold: 200, mats: [["thoi_sat", 2]] },
  { chance: 0.9, gold: 350, mats: [["thoi_sat", 3]] },
  { chance: 0.8, gold: 500, mats: [["thoi_bac", 1]] },
  { chance: 0.7, gold: 800, mats: [["thoi_bac", 2]] },
  { chance: 0.6, gold: 1200, mats: [["thoi_bac", 3]] },
  { chance: 0.5, gold: 1800, mats: [["thoi_vang", 1]] },
  { chance: 0.4, gold: 2600, mats: [["thoi_vang", 2]] },
  { chance: 0.3, gold: 3600, mats: [["thoi_vang", 3], ["kim_cuong", 1]] },
  { chance: 0.2, gold: 5000, mats: [["thoi_vang", 4], ["kim_cuong", 2]] },
];
const MAX_ENHANCE = ENHANCE.length;

// Moi cap +10% chi so goc.
const enhanceMult = (level) => 1 + 0.1 * (level || 0);

// Mui ten dung cho cung (xem items.json "Ammo").
const ARROW_ID = "mui_ten";

// Suc manh dau truong tu do NGUOI CHOI SO HUU THAT (vi tren server) - lay vu khi/giap manh nhat.
// Cung (Ranged) tinh RIENG: rangedAttack/rangedRange (tam ban), khong cong vao don can chien; arrows =
// so mui ten dang co (counts: Map item -> so luong, tuy chon).
// usable(id): vu khi/giap con du do ben (>= 40%, xem itemState) - do hong khong tinh vao PvP.
function duelPower(ownedIds, enh, catalog, counts = null, usable = () => true) {
  let attack = 5, defense = 0, rangedAttack = 0, rangedRange = 0, armorId = null, weaponId = null;
  for (const id of ownedIds) {
    const d = catalog.items.get(id);
    if (!d || !usable(id)) continue;
    const mult = enhanceMult(enh.get(id));
    if (d.Type === "Weapon" && d.Ranged) {
      const a = Math.round((d.Damage || 0) * mult);
      if (a > rangedAttack) { rangedAttack = a; rangedRange = d.Range || 900; }
    } else if (d.Type === "Weapon") {
      const a = Math.round((d.Damage || 0) * mult);
      if (a > attack) { attack = a; weaponId = id; }
    }
    if (d.Type === "Armor") {
      const df = Math.round((d.Defense || 0) * mult);
      if (df > defense) { defense = df; armorId = id; }
    }
  }
  return { attack, defense, rangedAttack, rangedRange, arrows: counts ? counts.get(ARROW_ID) || 0 : 0, armorId, weaponId };
}

function duelDamage(attacker, defender, rand = Math.random) {
  const base = Math.max(5, attacker.attack - defender.defense * 0.5);
  return Math.max(1, Math.round(base * (0.9 + rand() * 0.2)));
}

// Elo K=32.
function elo(winner, loser, k = 32) {
  const expected = 1 / (1 + 10 ** ((loser - winner) / 400));
  const delta = Math.max(1, Math.round(k * (1 - expected)));
  return { winner: winner + delta, loser: Math.max(100, loser - delta), delta };
}

module.exports = { ARROW_ID, ENHANCE, MAX_ENHANCE, enhanceMult, duelPower, duelDamage, elo };
