// Su kien the gioi (ham THUAN): ruong tiep te roi tu troi, doan thuong nhan luu dong, Dem Trang Mau.
// Server (realtime/worldEvents.js) chay lich + phat trang thai cho MOI nguoi choi; toa do ro rang de ai
// cung biet duong toi (va tranh nhau).
const { DAY_MS } = require("./calendar");

const DROP_FALL_MS = 120000;        // bao truoc 2 phut roi moi cham dat (de nguoi choi kip toi)
const DROP_LIFETIME_MS = 20 * 60000;
const DROP_EVERY_MS = [20 * 60000, 35 * 60000];
const MAX_ACTIVE_DROPS = 2;
const DROP_OPEN_RANGE = 120;

const CARAVAN_STAY_MS = 15 * 60000;
const CARAVAN_EVERY_MS = [45 * 60000, 75 * 60000];
const CARAVAN_RANGE = 220;

const randIn = (rand, [a, b]) => a + rand() * (b - a);
const randInt = (rand, a, b) => Math.floor(a + rand() * (b - a + 1));

// Loot theo khoang cach toi nong trai: xa hon = nguy hiem hon = xin hon.
const DROP_TIERS = [
  { minDist: 0, gold: [100, 250], rolls: 3, pool: [["mui_ten", 20, 40], ["potion", 2, 4], ["nuoc_sach", 3, 5], ["thoi_sat", 2, 4], ["bo_nuong", 2, 3], ["co_kho", 5, 10]] },
  { minDist: 9000, gold: [250, 500], rolls: 4, pool: [["mui_ten", 30, 60], ["potion", 3, 5], ["thoi_bac", 2, 4], ["thoi_sat", 4, 6], ["cung_san", 1, 1], ["giap_sat", 1, 1], ["bo_ban_moc", 1, 1]] },
  { minDist: 16000, gold: [500, 1000], rolls: 5, pool: [["thoi_vang", 2, 4], ["kim_cuong", 1, 2], ["thoi_bac", 4, 6], ["bo_lo_ren", 1, 1], ["bo_xuong", 1, 1], ["giap_bac", 1, 1], ["mui_ten", 40, 80]] },
];

function rollDropLoot(x, z, rand = Math.random) {
  const dist = Math.hypot(x, z);
  const tier = [...DROP_TIERS].reverse().find((t) => dist >= t.minDist);
  const items = {};
  for (let i = 0; i < tier.rolls; i++) {
    const [id, a, b] = tier.pool[Math.floor(rand() * tier.pool.length)];
    items[id] = (items[id] || 0) + randInt(rand, a, b);
  }
  return { items, gold: Math.round(randIn(rand, tier.gold)) };
}

// Diem roi: vanh 3-18 km quanh nong trai, khong roi vao vung an toan cua cho.
function pickSpot(rand, markets, minR = 3000, maxR = 18000) {
  for (let i = 0; i < 30; i++) {
    const a = rand() * Math.PI * 2, r = randIn(rand, [minR, maxR]);
    const x = Math.round(Math.cos(a) * r), z = Math.round(Math.sin(a) * r);
    if (!markets.some((m) => Math.hypot(x - m.X, z - m.Z) < (m.SafeRadius || 350) + 400)) return { x, z };
  }
  return { x: minR, z: 0 };
}

// Doan thuong nhan: ban do HIEM (khong co o cua hang thuong) voi gia co dinh, so luong co han.
const CARAVAN_POOL = [
  ["cung_san", 380, 2], ["giap_sat", 320, 2], ["giap_bac", 650, 1], ["bo_ban_moc", 220, 3], ["bo_lo_ren", 420, 2],
  ["bo_xuong", 900, 1], ["thoi_bac", 40, 10], ["thoi_vang", 95, 6], ["kim_cuong", 320, 2], ["ao_long_thu", 160, 2],
  ["co_kho", 3, 40], ["mui_ten", 2, 200], ["nuoc_sach", 4, 30],
];

function rollCaravan(rand = Math.random) {
  const pool = [...CARAVAN_POOL];
  const stock = [];
  for (let i = 0; i < 6 && pool.length; i++) {
    const [item, price, left] = pool.splice(Math.floor(rand() * pool.length), 1)[0];
    stock.push({ item, price, left });
  }
  return stock;
}

// Dem Trang Mau: 1/5 so dem (tat dinh theo ngay - moi gateway tinh giong nhau), tu 19h toi 5h sang.
function hourOf(nowMs, epochMs) {
  return Math.floor((((nowMs - epochMs) % DAY_MS) + DAY_MS) % DAY_MS / 3600000);
}

function bloodMoon(nowMs, epochMs) {
  const day = Math.floor((nowMs - epochMs) / DAY_MS) + 1;
  const hour = hourOf(nowMs, epochMs);
  if (!(hour >= 19 || hour < 5)) return false;
  const nightDay = hour < 5 ? day - 1 : day;
  let h = (Math.imul(nightDay | 0, 0x9e3779b1) >>> 0) ^ 0x5bd1e995;
  h = Math.imul(h ^ (h >>> 15), 0x2c1b3c6d) >>> 0;
  return h % 5 === 0;
}

module.exports = {
  DROP_FALL_MS, DROP_LIFETIME_MS, DROP_EVERY_MS, MAX_ACTIVE_DROPS, DROP_OPEN_RANGE, CARAVAN_STAY_MS, CARAVAN_EVERY_MS, CARAVAN_RANGE,
  randIn, rollDropLoot, pickSpot, rollCaravan, bloodMoon, hourOf, DROP_TIERS, CARAVAN_POOL,
};
