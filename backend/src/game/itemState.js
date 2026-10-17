// Trang thai "song" cua vat pham (ham THUAN): HAN SU DUNG (thit/nong san/mon an) va DO BEN (vu khi/
// giap/nong cu). Server giu theo TUNG LOAI vat pham cua moi nguoi choi, luon dong bo voi SO LUONG:
//   { fresh: [[hetHanMs, soLuong], ...] (lo cu -> moi), dur: [doBen, ...] (tot -> te) }
// - Tang so luong (thu hoach/mua/che tao...): them lo moi het han now+ShelfHours / mon moi do ben toi da.
// - Giam so luong (an/ban/dung/mat): tru LO CU NHAT truoc (an do sap hong truoc) / mon TE NHAT truoc.
// - Chuyen giua nguoi/kho (giao dich, cho, ruong, tui roi): trang thai DI THEO mon do (carry) -> cat vao
//   ruong khong lam thit tuoi lai, dua kiem cun cho nick phu khong thanh kiem moi.
// - Het han -> chuyen thanh "do_thoi" (khong an/nau/ban duoc). Do ben < 40% -> khong dung duoc toi khi sua.

const ROTTEN_ID = "do_thoi";
const USABLE_RATIO = 0.4;
const LOT_MERGE_MS = 30 * 60 * 1000; // gop lo het han gan nhau (giu mang ngan)
const MAX_TRACKED_COPIES = 50;

const shelfMs = (def) => (def && def.ShelfHours > 0 ? def.ShelfHours * 3600000 : 0);
const maxDur = (def) => (def && def.MaxDurability > 0 ? def.MaxDurability : 0);
const tracked = (def) => shelfMs(def) > 0 || maxDur(def) > 0;
const usable = (dur, max) => !(max > 0) || dur >= Math.ceil(max * USABLE_RATIO);

function clone(st) {
  return { fresh: st?.fresh ? st.fresh.map((l) => [l[0], l[1]]) : undefined, dur: st?.dur ? [...st.dur] : undefined };
}

const freshCount = (st) => (st?.fresh || []).reduce((a, l) => a + l[1], 0);

function addLot(fresh, exp, n) {
  if (n <= 0) return;
  const near = fresh.find((l) => Math.abs(l[0] - exp) <= LOT_MERGE_MS);
  if (near) near[1] += n;
  else fresh.push([exp, n]);
  fresh.sort((a, b) => a[0] - b[0]);
}

// Lam trang thai KHOP so luong `count` (thieu -> them mac dinh, thua -> bo lo cu/mon te).
function normalize(st, count, def, now) {
  if (!tracked(def) || count <= 0) return null;
  const out = clone(st);
  if (shelfMs(def) > 0) {
    out.fresh = out.fresh || [];
    let have = freshCount(out);
    if (have < count) addLot(out.fresh, now + shelfMs(def), count - have);
    else if (have > count) takeLots(out.fresh, have - count);
  } else delete out.fresh;
  const max = maxDur(def);
  if (max > 0) {
    out.dur = (out.dur || []).map((d) => Math.max(0, Math.min(max, Math.round(d))));
    const want = Math.min(count, MAX_TRACKED_COPIES);
    while (out.dur.length < want) out.dur.push(max);
    out.dur.sort((a, b) => b - a);
    out.dur.length = want;
  } else delete out.dur;
  return out;
}

// Bo n don vi tu lo cu nhat; tra ve cac lo da bo (de mang theo).
function takeLots(fresh, n) {
  const removed = [];
  while (n > 0 && fresh.length) {
    const lot = fresh[0];
    const k = Math.min(n, lot[1]);
    removed.push([lot[0], k]);
    lot[1] -= k;
    n -= k;
    if (lot[1] <= 0) fresh.shift();
  }
  return removed;
}

// Lay n don vi ra khoi trang thai (con `count` truoc khi lay) -> { state, removed }.
function take(st, count, n, def, now) {
  const cur = normalize(st, count, def, now);
  if (!cur) return { state: null, removed: null };
  const removed = {};
  if (cur.fresh) removed.fresh = takeLots(cur.fresh, n);
  if (cur.dur) {
    const k = Math.min(n, cur.dur.length);
    removed.dur = cur.dur.splice(cur.dur.length - k, k); // mon te nhat
  }
  return { state: normalize(cur, count - n, def, now), removed };
}

// Them n don vi (con `count` truoc khi them); carry = trang thai mang theo tu noi khac (co the null).
function give(st, count, n, carry, def, now) {
  if (!tracked(def)) return null;
  const cur = normalize(st, count, def, now) || {};
  if (shelfMs(def) > 0) {
    cur.fresh = cur.fresh || [];
    let left = n;
    for (const [exp, k] of carry?.fresh || []) {
      const use = Math.min(left, k);
      addLot(cur.fresh, exp, use);
      left -= use;
      if (left <= 0) break;
    }
    if (left > 0) addLot(cur.fresh, now + shelfMs(def), left);
  }
  if (maxDur(def) > 0) {
    cur.dur = cur.dur || [];
    const carried = (carry?.dur || []).slice(0, n);
    cur.dur.push(...carried);
    for (let i = carried.length; i < n; i++) cur.dur.push(maxDur(def));
  }
  return normalize(cur, count + n, def, now);
}

// Gop 2 trang thai (vd do cat chung 1 ruong).
function merge(a, b) {
  if (!a) return b ? clone(b) : null;
  if (!b) return clone(a);
  const out = clone(a);
  if (b.fresh) { out.fresh = out.fresh || []; for (const [e, n] of b.fresh) addLot(out.fresh, e, n); }
  if (b.dur) out.dur = [...(out.dur || []), ...b.dur].sort((x, y) => y - x);
  return out;
}

// Lo het han -> { state, spoiled }.
function spoil(st, now) {
  if (!st?.fresh?.length) return { state: st, spoiled: 0 };
  let spoiled = 0;
  const fresh = [];
  for (const [exp, n] of st.fresh) { if (exp <= now) spoiled += n; else fresh.push([exp, n]); }
  return { state: { ...clone(st), fresh }, spoiled };
}

// Mon dang dung = mon con ben nhat (dau mang). Hao mon n diem.
function wear(st, n) {
  if (!st?.dur?.length) return st;
  const out = clone(st);
  out.dur[0] = Math.max(0, out.dur[0] - n);
  out.dur.sort((a, b) => b - a);
  return out;
}

const bestDur = (st) => (st?.dur?.length ? st.dur[0] : null);
const soonestExp = (st) => (st?.fresh?.length ? st.fresh[0][0] : null);

// Sua: mon TOT NHAT con chua day do ben -> toi da. Tra ve state moi hoac null neu khong can sua.
function repair(st, def) {
  const max = maxDur(def);
  if (!max || !st?.dur?.length) return null;
  const i = st.dur.findIndex((d) => d < max);
  if (i < 0) return null;
  const out = clone(st);
  out.dur[i] = max;
  out.dur.sort((a, b) => b - a);
  return out;
}

const repairCost = (def) => ({
  gold: def?.RepairGold ?? Math.max(20, Math.round((def?.SellPrice || 0) * 0.4)),
  mats: (def?.RepairCost || []).map((c) => [c.ItemId, c.Count]),
});

module.exports = {
  ROTTEN_ID, USABLE_RATIO, shelfMs, maxDur, tracked, usable, normalize, take, give, merge, spoil, wear, bestDur, soonestExp,
  repair, repairCost, freshCount,
};
