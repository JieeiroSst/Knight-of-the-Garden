// Luat PvP the gioi mo + can cu - ham THUAN (khong DB, khong mang) de test de dang.

// Bo xay dung (vat pham Type Tool trong data/items.json, che tao qua recipes.json) -> cong trinh.
//  chest: ruong cat do (pha -> do roi ra). tent: diem hoi sinh cua chu. campfire: hoi mau dong doi.
const STRUCTURES = {
  bo_tuong_go: { kind: "wall", hp: 800 },
  bo_leu_trai: { kind: "tent", hp: 600 },
  bo_ruong_can_cu: { kind: "chest", hp: 500 },
  bo_lua_trai: { kind: "campfire", hp: 300 },
  bo_giuong: { kind: "bed", hp: 400 },
  // Tram che tao theo cap (xem STATION_TIER).
  bo_ban_che_tac: { kind: "workbench", hp: 400 },
  bo_ban_moc: { kind: "carpentry", hp: 600 },
  bo_lo_ren: { kind: "forge", hp: 900 },
  bo_xuong: { kind: "workshop", hp: 1200 },
};

// Cap tram che tao: tram cap cao dung duoc cong thuc cap thap hon. Ban Che Tac (1) va Lo Ren (3) nong
// trai nao cung co san -> luon dung duoc; Ban Moc (2) va Xuong (4) PHAI tu xay o can cu.
const STATION_TIER = { workbench: 1, carpentry: 2, forge: 3, workshop: 4 };
const STATIONS_AT_FARM = new Set(["workbench", "forge", "tailor"]);
function stationOk(required, ownedKinds) {
  if (!required || STATIONS_AT_FARM.has(required)) return true;
  const need = STATION_TIER[required];
  if (!need) return false;
  for (const k of ownedKinds || []) if ((STATION_TIER[k] || 0) >= need) return true;
  return false;
}
const KIND_TO_ITEM = Object.fromEntries(Object.entries(STRUCTURES).map(([item, s]) => [s.kind, item]));

const MAX_STRUCTURES_PER_PLAYER = 60;
const PLACE_RANGE = 160;        // dat cach nguoi toi da
const INTERACT_RANGE = 120;     // danh/sua/mo ruong
const PVP_RANGE = 95;           // tam danh can chien (don vi the gioi, nguoi cao ~40)
const HEAL_RANGE = 200;
const HIT_COOLDOWN_MS = 400;
const STRUCT_HIT_COOLDOWN_MS = 450;
const HEAL_COOLDOWN_MS = 4000;
const HEAL_AMOUNT = 60;
const SPAWN_PROTECT_MS = 10000;   // mien sat thuong sau khi hoi sinh
const DEATH_ACK_TIMEOUT_MS = 4000; // cho client gui danh sach tui truoc khi chot do roi
const BAG_PRIORITY_MS = 60000;
const BAG_LIFETIME_MS = 10 * 60000;
const CHEST_MAX_KINDS = 20;
const CHEST_MAX_PER_KIND = 999;
const REPAIR_COST = [["wood", 3]];
const REPAIR_AMOUNT = 150;
const PARTY_MAX = 4;

// ==== Hoi sinh (giuong) ====
// Co giuong -> hoi sinh ngay tai giuong gan cho chet nhat. MAT giuong (giuong cuoi cung bi pha) ->
// lan chet tiep theo phai doi BED_LOST_WAIT_MS moi hoi sinh duoc o nong trai. Chua tung co giuong
// (nguoi moi) -> hoi sinh ngay o nong trai nhu binh thuong.
const BED_LOST_WAIT_MS = 60 * 60 * 1000;

function respawnDecision({ beds, bedLost, deathX, deathZ, now }) {
  if (beds.length) {
    let best = beds[0], bestD = Infinity;
    for (const b of beds) { const d = Math.hypot(b.x - deathX, b.z - deathZ); if (d < bestD) { bestD = d; best = b; } }
    return { mode: "bed", x: best.x, z: best.z, readyAt: now };
  }
  if (bedLost) return { mode: "wait", readyAt: now + BED_LOST_WAIT_MS };
  return { mode: "farm", readyAt: now };
}

// ==== Cung ten (tam xa) ====
const RANGED_SLACK = 60;
const SHOT_COOLDOWN_MS = 650;

// ==== Chien dau tren lung ngua ====
// Xung phong: dang cuoi ngua va toc do (server tu uoc luong tu cac goi vi tri) >= CHARGE_SPEED ->
// don can chien x CHARGE_MULT va CHAC CHAN hat nguoi dang cuoi ngua xuong. Don ket (finisher) thuong
// co KNOCK_CHANCE_FIN hat nguoi cuoi xuong. Nguoi tren ngua bi nguoi di bo chem chi an MOUNTED_DEF.
const CHARGE_SPEED = 180;
const CHARGE_MULT = 1.4;
const MOUNTED_DEF = 0.85;
const MOUNTED_REACH_BONUS = 40;
const KNOCK_CHANCE_FIN = 0.3;

function mountedHit({ attackerMounted, attackerSpeed, ranged, fin, targetMounted }, rand = Math.random) {
  const charge = !ranged && attackerMounted && attackerSpeed >= CHARGE_SPEED;
  let mult = charge ? CHARGE_MULT : 1;
  if (targetMounted && !attackerMounted && !ranged) mult *= MOUNTED_DEF;
  const knock = targetMounted && (charge || (fin && rand() < KNOCK_CHANCE_FIN));
  return { charge, mult, knock };
}

// Toc do uoc luong (don vi/giay) tu 2 goi vi tri lien tiep - lam muot de khong nhay vot vi goi den tre.
function smoothSpeed(prev, dist, dtMs) {
  if (!(dtMs > 40)) return prev || 0;
  const inst = Math.min(2000, (dist / dtMs) * 1000);
  return (prev || 0) * 0.5 + inst * 0.5;
}

// ==== Khu cho = vung an toan (khong danh nguoi, khong pha/xay) ====
function inSafeZone(markets, x, z) {
  for (const m of markets) if (Math.hypot(x - m.X, z - m.Z) <= (m.SafeRadius || 350)) return m;
  return null;
}

// ==== Cho an nap (bui co cao) - HAM TAT DINH, CHEP Y HET scripts/core/HideSpots.cs ====
// Luoi o HIDE_CELL don vi; moi o co ~HIDE_CHANCE/1024 kha nang co 1 bui (tam + ban kinh tinh tu hash).
// Server dung de KIEM TRA loi bao "dang an" cua client (chi an duoc khi that su dung trong bui/leu).
const HIDE_CELL = 300;
const HIDE_CHANCE = 300;
const HIDE_SEED = 77;
const REVEAL_DIST = 110;          // dung gan hon khoang nay thi van thay nguoi dang an
const REVEAL_AFTER_ATTACK_MS = 3000;
const HIDE_ZONES_EXCLUDED = (zone) => zone === "town" || (typeof zone === "string" && zone.startsWith("farm_"));

function hash3(x, y, seed) {
  let h = (Math.imul(x | 0, 0x8da6b343) ^ Math.imul(y | 0, 0xd8163841) ^ Math.imul(seed, 0xcb1ab31f)) >>> 0;
  h = (h ^ (h >>> 13)) >>> 0;
  h = Math.imul(h, 0x85ebca6b) >>> 0;
  h = (h ^ (h >>> 16)) >>> 0;
  return h;
}

// Bui trong o (cx, cz) hoac null: { x, z, r }.
function bushInCell(cx, cz) {
  const h = hash3(cx, cz, HIDE_SEED);
  if ((h & 1023) >= HIDE_CHANCE) return null;
  const margin = 50;
  const span = HIDE_CELL - 2 * margin;
  const ox = margin + (((h >>> 10) & 255) / 255) * span;
  const oz = margin + (((h >>> 18) & 255) / 255) * span;
  return { x: cx * HIDE_CELL + ox, z: cz * HIDE_CELL + oz, r: 30 + ((h >>> 26) & 15) };
}

function inBush(x, z) {
  const cx = Math.floor(x / HIDE_CELL), cz = Math.floor(z / HIDE_CELL);
  for (let dx = -1; dx <= 1; dx++)
    for (let dz = -1; dz <= 1; dz++) {
      const b = bushInCell(cx + dx, cz + dz);
      if (b && Math.hypot(x - b.x, z - b.z) <= b.r) return true;
    }
  return false;
}
// Vung KHONG duoc xay (khu rieng tu cua nong trai).
const canBuildIn = (zone) => typeof zone === "string" && !zone.startsWith("farm_");

// Sat thuong 1 don danh nguoi: giong dau truong (vu khi/giap THAT tren server), don ket x1.5.
function pvpDamage(attack, defense, finisher, rand = Math.random) {
  const base = Math.max(5, attack - defense * 0.5) * (finisher ? 1.5 : 1);
  return Math.max(1, Math.round(base * (0.9 + rand() * 0.2)));
}

// Sat thuong len cong trinh: theo suc tan cong, toi thieu 10 (tay khong van pha duoc nhung rat cham).
const structureDamage = (attack) => Math.max(10, Math.round(attack));

// Chuan hoa mau client bao len: max hop ly, mau KHONG duoc tang qua nhanh (chong hoi mau ao giua tran).
//  - giam: luon nhan (quai danh, roi...).
//  - tang: toi da 25% max moi lan bao (thuoc/an), client bi gioi han so lan bao/giay o gateway.
function acceptHp(current, reportedHp, reportedMax) {
  const max = Math.max(50, Math.min(20000, Math.round(Number(reportedMax) || 100)));
  let hp = Math.max(0, Math.min(max, Math.round(Number(reportedHp) || 0)));
  if (current == null) return { hp: Math.max(1, hp), max };
  if (hp > current) hp = Math.min(hp, current + Math.ceil(max * 0.25));
  return { hp, max };
}

// Danh sach tui client bao -> chi giu vat pham hop le, so nguyen duong, gioi han kich thuoc.
function cleanManifest(raw, catalog) {
  const out = new Map();
  if (!raw || typeof raw !== "object") return out;
  for (const [id, n] of Object.entries(raw).slice(0, 48)) {
    const count = Math.floor(Number(n));
    if (!catalog.items.has(id) || !(count > 0)) continue;
    out.set(id, Math.min(count, 9999));
  }
  return out;
}

// Do that su bi mat = min(tui bao, so huu tren server) - khong bao gio lay qua so dang co.
function lossFromManifest(manifest, owned) {
  const loss = new Map();
  for (const [id, n] of manifest) {
    const take = Math.min(n, owned.get(id) || 0);
    if (take > 0) loss.set(id, take);
  }
  return loss;
}

function canLootBag(bag, playerId, now) {
  if (bag.looted_at) return "Tui do da bi nhat.";
  if (new Date(bag.expires_at).getTime() <= now) return "Tui do da bien mat.";
  if (bag.killer_id && bag.killer_id !== playerId && new Date(bag.priority_until).getTime() > now)
    return "Nguoi ha duoc uu tien nhat tui nay trong 1 phut.";
  return null;
}

const dist2d = (ax, az, bx, bz) => Math.hypot(ax - bx, az - bz);

module.exports = {
  STRUCTURES, KIND_TO_ITEM, MAX_STRUCTURES_PER_PLAYER, PLACE_RANGE, INTERACT_RANGE, PVP_RANGE, HEAL_RANGE,
  HIT_COOLDOWN_MS, STRUCT_HIT_COOLDOWN_MS, HEAL_COOLDOWN_MS, HEAL_AMOUNT, SPAWN_PROTECT_MS, DEATH_ACK_TIMEOUT_MS,
  BAG_PRIORITY_MS, BAG_LIFETIME_MS, CHEST_MAX_KINDS, CHEST_MAX_PER_KIND, REPAIR_COST, REPAIR_AMOUNT, PARTY_MAX,
  STATION_TIER, stationOk,
  CHARGE_SPEED, CHARGE_MULT, MOUNTED_DEF, MOUNTED_REACH_BONUS, KNOCK_CHANCE_FIN, mountedHit, smoothSpeed,
  BED_LOST_WAIT_MS, respawnDecision, RANGED_SLACK, SHOT_COOLDOWN_MS, inSafeZone,
  HIDE_CELL, HIDE_CHANCE, HIDE_SEED, REVEAL_DIST, REVEAL_AFTER_ATTACK_MS, HIDE_ZONES_EXCLUDED, hash3, bushInCell, inBush,
  canBuildIn, pvpDamage, structureDamage, acceptHp, cleanManifest, lossFromManifest, canLootBag, dist2d,
};
