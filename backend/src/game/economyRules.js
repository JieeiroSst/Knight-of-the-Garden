// LUAT KINH TE PHIA SERVER - ham THUAN (khong cham DB), ap 1 lo "op" client gui len vao trang thai
// trong bo nho. economyService nap trang thai tu DB (khoa dong vi), goi applyOps(), roi ghi lai.
// Tach rieng de unit test day du ma khong can Postgres.
//
// Nguyen tac:
//  - Mua/ban/che tao/nau an/giao hang/nang cap nha: server TU tinh gia & nguyen lieu tu catalog,
//    khong tin so lieu client.
//  - Nhan do/vang tu the gioi (thu hoach, cau ca, danh quai...): server khong mo phong lai the gioi,
//    nen kiem tra (1) item co hop voi nguon khong, (2) gioi han so luong/gio theo nguon, (3) phan
//    thuong 1-lan phai kem "key" duy nhat (ruong, thanh tuu, le hoi...) - nhan lai se bi tu choi.
//  - Moi op loi chi bi bo qua (kem ly do), cac op khac trong lo van ap dung.
const { catalog: defaultCatalog } = require("./catalog");

const MAX_PRICE_MULT = 2.2;     // mua Thu +30% + le hoi +20% + ky nang (toi da ~+33%) + vat nuoi vui +10%
const MAX_OPS_PER_BATCH = 200;
const MAX_STACK_OP = 999;

const ORES = new Set([
  "dong_tho", "sat_tho", "da_quy", "quang_nui", "quang_hang", "da", "thach_anh", "hong_ngoc", "ngoc_luc_bao", "kim_cuong", "co_vat",
  // 3 ham mo moi (Than/Pha Le/Vang - xem MainMines.cs ben Godot).
  "than_da", "bac_tho", "vang_tho", "pha_le", "da_hac_dien",
]);
const ANIMAL = new Set(["milk", "egg", "wool", "honey", "mat_ong", "beeswax"]);
const PROCESSED_FIXED = new Set(["pho_mai", "cheese", "mayonnaise", "vai", "wine", "mut_trai_cay", "nuoc_ep"]);
// San pham giet thit/san ban (nguon "hunt").
const HUNT = new Set(["thit_bo", "thit_heo", "thit_cuu", "thit_ga", "thit_de", "thit_rung", "da_thu", "long_thu", "long_vu", "wool"]);
const itemState = require("./itemState");
const { stationOk } = require("./pvpRules");
const FORBIDDEN_WORLD_TYPES = new Set(["Tool", "Weapon", "Armor", "Outfit"]);

// Nguon nhan VAT PHAM: dieu kien item + gioi han so luong / gio + co bat buoc key duy nhat khong.
function itemSources(cat) {
  const isType = (t) => (id) => cat.items.get(id)?.Type === t;
  return {
    harvest: { ok: (id) => isType("Crop")(id) || id === "wheat", perHour: 600 },
    forage: { ok: (id) => cat.forage.has(id) || id === "thao_duoc_rung" || id === "thao_duoc_dam_lay" || id === "sen" || id === "rong", perHour: 300 },
    fish: { ok: (id) => cat.fish.has(id), perHour: 150 },
    mine: { ok: (id) => ORES.has(id), perHour: 400 },
    tree: { ok: (id) => id === "wood", perHour: 400 },
    loot: { ok: (id) => cat.lootItems.has(id), perHour: 400 },
    animal: { ok: (id) => ANIMAL.has(id), perHour: 300 },
    // Giet thit vat nuoi / san thu rung: thit, da, long (xem Huntable.cs ben Godot).
    hunt: { ok: (id) => HUNT.has(id), perHour: 240 },
    // Lay nuoc sach o ho/song/gieng.
    water: { ok: (id) => id === "nuoc_sach", perHour: 60 },
    process: { ok: (id) => id.startsWith("mut_") || id.startsWith("ruou_") || PROCESSED_FIXED.has(id), perHour: 200 },
    compost: { ok: (id) => id === "fertilizer_basic", perHour: 80 },
    festival: { ok: (id) => id === "trung_le_hoi", perHour: 40 },
    // Bay cua dat canh nuoc (moi bay toi da vai mon/ngay).
    trap: { ok: (id) => id === "cua" || id === "rong" || id === "ca", perHour: 60 },
    // Nhat lai do trang tri da dat (dat = tieu thu, nhat lai = nhan lai dung mon do).
    decor: { ok: (id) => id.startsWith("decor_"), perHour: 60 },
    chest: { ok: (id) => !FORBIDDEN_WORLD_TYPES.has(cat.items.get(id)?.Type), perHour: 80, needsKey: true },
    boss: { ok: (id) => cat.lootItems.has(id), perHour: 60, needsKey: true },
    reward: { ok: () => true, perHour: 60, needsKey: true },
    misc: { ok: (id) => !FORBIDDEN_WORLD_TYPES.has(cat.items.get(id)?.Type), perHour: 40 },
  };
}

// Nguon nhan VANG: gioi han moi op + moi gio.
function goldSources(cat) {
  return {
    kill: { perOp: Math.max(50, cat.maxEnemyGold * 3), perHour: 30000 },
    chest: { perOp: 2000, perHour: 20000, needsKey: true },
    reward: { perOp: 10000, perHour: 40000, needsKey: true },
    misc: { perOp: 500, perHour: 2000 },
  };
}

function hourBucket(nowMs) {
  return Math.floor(nowMs / 3600000);
}

// state: { gold, houseLevel, items: Map<id,count>, counters: Map<source,amount> (gio hien tai),
//          claimed: Set<key>, bin: Array<{day,item,count,unitPrice}> }
// ctx: { day, nowMs, catalog }
// Tra ve { applied, rejected: [{index, reason}], newClaims: [key], events: [] } va SUA state tai cho.
function applyOps(state, ops, ctx) {
  const cat = ctx.catalog || defaultCatalog;
  const srcItems = itemSources(cat);
  const srcGold = goldSources(cat);
  const rejected = [];
  const newClaims = [];
  let applied = 0;

  const count = (id) => state.items.get(id) || 0;
  const add = (id, n) => state.items.set(id, count(id) + n);
  const take = (id, n) => {
    const left = count(id) - n;
    if (left > 0) state.items.set(id, left);
    else state.items.delete(id);
  };
  const has = (needs) => needs.every(([id, n]) => count(id) >= n);
  // Trang thai han dung/do ben (state.itemStates: Map item -> state) - xem itemState.js.
  const states = state.itemStates || (state.itemStates = new Map());
  const getSt = (id) => itemState.normalize(states.get(id), count(id), cat.items.get(id), ctx.nowMs || Date.now());
  const setSt = (id, st) => states.set(id, st);
  const counter = (key) => state.counters.get(key) || 0;
  const bump = (key, n) => state.counters.set(key, counter(key) + n);
  const claimKey = (key) => {
    if (typeof key !== "string" || key.length === 0 || key.length > 120) return "key khong hop le";
    if (state.claimed.has(key)) return "da nhan phan thuong nay";
    return null;
  };
  const markClaimed = (key) => {
    state.claimed.add(key);
    newClaims.push(key);
  };
  const intN = (v) => Number.isInteger(v) && v > 0 && v <= MAX_STACK_OP;
  const mult = (m) => Math.min(MAX_PRICE_MULT, Math.max(1, Number.isFinite(m) ? m : 1));

  if (!Array.isArray(ops)) return { applied, rejected: [{ index: -1, reason: "ops phai la mang" }], newClaims, events: [] };
  if (ops.length > MAX_OPS_PER_BATCH) return { applied, rejected: [{ index: -1, reason: "qua nhieu op trong 1 lo" }], newClaims, events: [] };

  ops.forEach((op, index) => {
    const reason = applyOne(op);
    if (reason) rejected.push({ index, reason });
    else applied++;
  });

  return { applied, rejected, newClaims, events: [] };

  function applyOne(op) {
    if (!op || typeof op !== "object") return "op khong hop le";
    const item = op.item ? cat.items.get(op.item) : null;
    switch (op.t) {
      case "gain": {
        if (!item) return "vat pham khong ton tai";
        if (!intN(op.n)) return "so luong khong hop le";
        const src = srcItems[op.src] ? op.src : "misc";
        const rule = srcItems[src];
        if (!rule.ok(op.item)) return `nguon ${src} khong the cho ${op.item}`;
        if (rule.needsKey) {
          const err = claimKey(op.key);
          if (err) return err;
        }
        const ck = `item:${src}`;
        if (counter(ck) + op.n > rule.perHour) return `vuot gioi han ${src}/gio`;
        bump(ck, op.n);
        if (rule.needsKey) markClaimed(op.key);
        add(op.item, op.n);
        return null;
      }
      // Hao mon do ben (client bao sau moi lan vung/dung/bi danh) - chi tru, khong bao gio tang.
      case "wear": {
        if (!item || !itemState.maxDur(item)) return "vat pham khong co do ben";
        if (!Number.isInteger(op.n) || op.n <= 0 || op.n > 500) return "so luong khong hop le";
        if (count(op.item) <= 0) return "khong so huu vat pham";
        setSt(op.item, itemState.wear(getSt(op.item), op.n));
        return null;
      }
      // Sua do: ton vang + nguyen lieu theo items.json (RepairGold/RepairCost) -> mon tot nhat chua day ve toi da.
      case "repair": {
        if (!item || !itemState.maxDur(item)) return "vat pham khong sua duoc";
        if (count(op.item) <= 0) return "khong so huu vat pham";
        const fixed = itemState.repair(getSt(op.item), item);
        if (!fixed) return "do con nguyen ven";
        const cost = itemState.repairCost(item);
        if (state.gold < cost.gold) return "khong du vang de sua";
        if (!has(cost.mats)) return "thieu nguyen lieu de sua";
        state.gold -= cost.gold;
        for (const [id, n] of cost.mats) take(id, n);
        setSt(op.item, fixed);
        return null;
      }
      case "consume": {
        if (!item) return "vat pham khong ton tai";
        if (!intN(op.n)) return "so luong khong hop le";
        if (count(op.item) < op.n) return "khong du vat pham";
        take(op.item, op.n);
        return null;
      }
      case "gold+": {
        if (!Number.isInteger(op.n) || op.n <= 0) return "so vang khong hop le";
        const src = srcGold[op.src] ? op.src : "misc";
        const rule = srcGold[src];
        if (op.n > rule.perOp) return `vuot gioi han vang moi lan (${src})`;
        if (rule.needsKey) {
          const err = claimKey(op.key);
          if (err) return err;
        }
        const ck = `gold:${src}`;
        if (counter(ck) + op.n > rule.perHour) return `vuot gioi han vang ${src}/gio`;
        bump(ck, op.n);
        if (rule.needsKey) markClaimed(op.key);
        state.gold += op.n;
        return null;
      }
      case "gold-": {
        if (!Number.isInteger(op.n) || op.n <= 0) return "so vang khong hop le";
        if (state.gold < op.n) return "khong du vang";
        state.gold -= op.n;
        return null;
      }
      case "buy": {
        if (!item || !(item.BuyPrice > 0)) return "vat pham khong ban";
        if (!intN(op.n)) return "so luong khong hop le";
        const cost = item.BuyPrice * op.n;
        if (state.gold < cost) return "khong du vang";
        state.gold -= cost;
        add(op.item, op.n);
        return null;
      }
      case "sell": {
        if (!item || !(item.SellPrice > 0)) return "vat pham khong mua lai";
        if (!intN(op.n)) return "so luong khong hop le";
        if (count(op.item) < op.n) return "khong du vat pham";
        take(op.item, op.n);
        state.gold += Math.round(item.SellPrice * mult(op.mult)) * op.n;
        return null;
      }
      case "craft":
      case "cook": {
        const recipe = (op.t === "craft" ? cat.recipes : cat.cooking).get(op.recipe);
        if (!recipe) return "cong thuc khong ton tai";
        // Tram che tao theo cap: Ban Moc/Xuong phai tu xay o can cu (ctx.stations = loai tram dang so huu).
        if (op.t === "craft" && !stationOk(recipe.Station, ctx.stations)) return `can tram che tao: ${recipe.Station}`;
        const needs = recipe.Ingredients.map((i) => [i.ItemId, i.Count]);
        if (!has(needs)) return "thieu nguyen lieu";
        for (const [id, n] of needs) take(id, n);
        add(recipe.OutputId, recipe.OutputCount || 1);
        bump(`act:${op.t}`, 1); // dem cho nhiem vu ngay (xem dailyService)
        return null;
      }
      case "ship": {
        if (!item || !(item.SellPrice > 0) || FORBIDDEN_WORLD_TYPES.has(item.Type) || item.Type === "Quest") return "vat pham khong giao duoc";
        if (!intN(op.n)) return "so luong khong hop le";
        if (count(op.item) < op.n) return "khong du vat pham";
        take(op.item, op.n);
        const unitPrice = Math.round(item.SellPrice * mult(op.mult));
        const row = state.bin.find((b) => b.day === ctx.day && b.item === op.item);
        if (row) {
          row.count += op.n;
          row.unitPrice = Math.max(row.unitPrice, unitPrice);
        } else state.bin.push({ day: ctx.day, item: op.item, count: op.n, unitPrice });
        bump("act:ship", op.n);
        return null;
      }
      case "unship": {
        const row = state.bin.find((b) => b.day === ctx.day && b.item === op.item);
        if (!row) return "khong co trong thung";
        state.bin.splice(state.bin.indexOf(row), 1);
        add(row.item, row.count);
        return null;
      }
      case "house": {
        const next = cat.house.find((h) => h.Level === state.houseLevel + 1);
        if (!next) return "nha da toi cap toi da";
        const needs = next.Materials.map((m) => [m.ItemId, m.Count]);
        if (state.gold < next.Gold || !has(needs)) return "chua du chi phi nang cap";
        state.gold -= next.Gold;
        for (const [id, n] of needs) take(id, n);
        state.houseLevel = next.Level;
        return null;
      }
      case "achievement": {
        const ach = cat.achievements.get(op.id);
        if (!ach) return "thanh tuu khong ton tai";
        const key = `ach_${ach.Id}`;
        const err = claimKey(key);
        if (err) return err;
        markClaimed(key);
        state.gold += ach.RewardGold || 0;
        return null;
      }
      default:
        return "loai op khong ho tro";
    }
  }
}

// Sang ngay game moi: ban het hang trong thung giao hang cua cac ngay TRUOC (chi hang dat vao
// ngay hom nay moi con lai) -> cong vang + su kien bao cao cho client.
function settleShipping(state, day) {
  const due = state.bin.filter((b) => b.day < day);
  if (due.length === 0) return null;
  let gold = 0;
  const lines = due.map((b) => {
    const sub = b.count * b.unitPrice;
    gold += sub;
    return { item: b.item, count: b.count, gold: sub };
  });
  state.bin = state.bin.filter((b) => b.day >= day);
  state.gold += gold;
  return { type: "ship_payout", gold, lines };
}

// Bo khoi dau cho nguoi choi moi (data/starter.json).
function starterState(cat = defaultCatalog) {
  const items = new Map();
  for (const it of cat.starter.Items) items.set(it.ItemId, (items.get(it.ItemId) || 0) + it.Count);
  return { gold: cat.starter.Gold || 0, items };
}

// Nhap 1 LAN tu ban luu cu (truoc khi kinh te len server) - chi lay vat pham co that trong catalog,
// so luong gioi han hop ly de tranh nhap du lieu ao.
function importLegacy(saveData, cat = defaultCatalog) {
  const items = new Map();
  const lists = [saveData?.Inventory, saveData?.Backpack, saveData?.HomeChest];
  for (const list of lists) {
    if (!Array.isArray(list)) continue;
    for (const s of list) {
      const id = s?.Id;
      const n = Math.min(9999, Math.max(0, Math.floor(Number(s?.Count) || 0)));
      if (!id || !cat.items.has(id) || n <= 0) continue;
      items.set(id, (items.get(id) || 0) + n);
    }
  }
  const gold = Math.min(10_000_000, Math.max(0, Math.floor(Number(saveData?.Gold) || 0)));
  const houseLevel = Math.min(3, Math.max(0, Math.floor(Number(saveData?.HouseLevel) || 0)));
  return { gold, items, houseLevel };
}

module.exports = { applyOps, settleShipping, starterState, importLegacy, hourBucket, MAX_PRICE_MULT, MAX_OPS_PER_BATCH };
