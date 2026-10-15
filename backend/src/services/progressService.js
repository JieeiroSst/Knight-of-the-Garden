// Diem danh/nhiem vu ngay-tuan, bang xep hang, cuong hoa trang bi, ket qua dau truong.
const prog = require("../repositories/progressRepository");
const econ = require("../repositories/economyRepository");
const { ensureWallet } = require("./economyService");
const daily = require("../game/dailyRules");
const combat = require("../game/combatRules");
const { catalog } = require("../game/catalog");
const { calendar } = require("./timeService");
const { AppError } = require("../utils/AppError");

const HOUR = 3600000;
const dayBuckets = (day) => {
  const start = calendar.epochMs + (day - 1) * 86400000;
  return [Math.floor(start / HOUR), Math.floor((start + 86400000) / HOUR)];
};
const weekOf = (day) => Math.floor((day - 1) / 7);
const weekBuckets = (week) => [dayBuckets(week * 7 + 1)[0], dayBuckets(week * 7 + 7)[1]];

async function questView(playerId, kind, info) {
  const isDay = kind === "d";
  const periodNo = isDay ? info.day : weekOf(info.day);
  const key = `${kind}${periodNo}`;
  const [from, to] = isDay ? dayBuckets(info.day) : weekBuckets(periodNo);
  const counters = await prog.countersBetween(playerId, from, to);
  const claimed = await prog.claims(playerId, key);
  return daily.pickQuests(playerId, periodNo, isDay ? 1 : 2).map((q, idx) => {
    const target = isDay ? q.d : q.w;
    return {
      idx, id: q.id, target, progress: Math.min(target, counters.get(q.counter) || 0),
      gold: isDay ? q.gold : q.gold * 4, claimed: claimed.has(idx),
    };
  });
}

async function getDaily(playerId) {
  const info = calendar.info(Date.now());
  const login = await prog.peekLogin(playerId);
  return {
    day: info.day,
    checkin: {
      streak: login?.streak || 0,
      claimedToday: login?.last_day === info.day,
      nextStreak: daily.nextStreak(login, info.day),
      rewards: daily.CHECKIN_REWARDS,
    },
    daily: await questView(playerId, "d", info),
    weekly: await questView(playerId, "w", info),
  };
}

async function grant(client, playerId, gold, item, n) {
  if (gold) await econ.addGold(client, playerId, gold);
  if (item && n) await econ.adjustItem(client, playerId, item, n);
}

async function checkin(playerId) {
  const day = calendar.dayAt(Date.now());
  const result = await econ.withTransaction(async (client) => {
    await ensureWallet(client, playerId);
    const prev = await prog.getLogin(client, playerId);
    if (prev?.last_day === day) throw new AppError(409, "Hom nay da diem danh roi.");
    const streak = daily.nextStreak(prev, day);
    await prog.setLogin(client, playerId, day, streak);
    const r = daily.checkinReward(streak);
    await grant(client, playerId, r.gold, r.item, r.n);
    return { streak, reward: r };
  });
  return result;
}

async function claimQuest(playerId, kind, idx) {
  const info = calendar.info(Date.now());
  const quests = await questView(playerId, kind, info);
  const q = quests[idx];
  if (!q) throw new AppError(404, "Nhiem vu khong ton tai.");
  if (q.claimed) throw new AppError(409, "Da nhan thuong nhiem vu nay.");
  if (q.progress < q.target) throw new AppError(400, "Chua hoan thanh nhiem vu.");
  const key = `${kind}${kind === "d" ? info.day : weekOf(info.day)}`;
  await econ.withTransaction(async (client) => {
    await ensureWallet(client, playerId);
    if (!(await prog.insertClaim(client, playerId, key, idx))) throw new AppError(409, "Da nhan thuong nhiem vu nay.");
    await grant(client, playerId, q.gold);
  });
  return { gold: q.gold };
}

// ==== Bang xep hang ====
// Chi so client bao (tang toi da/ngay de chan gian lan bang tay); gold/pvp lay thang tu server.
const REPORTED = { level: 3, fish_caught: 300, crops_harvested: 2000, bosses_defeated: 20, monsters_killed: 1000, ores_mined: 1500 };

async function reportStats(playerId, stats) {
  const day = calendar.dayAt(Date.now());
  for (const [stat, cap] of Object.entries(REPORTED)) {
    const v = Number(stats?.[stat]);
    if (Number.isInteger(v) && v >= 0) await prog.reportStat(playerId, stat, v, day, cap);
  }
  return { ok: true };
}

const BOARDS = ["gold", "pvp", ...Object.keys(REPORTED)];

async function leaderboard(stat, playerId) {
  if (!BOARDS.includes(stat)) throw new AppError(404, "Bang xep hang khong ton tai.");
  const limit = 50;
  const [top, mine] =
    stat === "gold" ? [await prog.topGold(limit), await prog.myGold(playerId)]
    : stat === "pvp" ? [await prog.topPvp(limit), await prog.myPvp(playerId)]
    : [await prog.topStat(stat, limit), await prog.myStat(stat, playerId)];
  return {
    stat,
    top: top.map((r, i) => ({ rank: i + 1, playerId: r.player_id, name: r.display_name, value: Number(r.value), wins: r.wins, losses: r.losses })),
    me: mine ? { rank: mine.rank, value: Number(mine.value) } : null,
  };
}

// ==== Cuong hoa ====
async function getEnhancements(playerId) {
  return Object.fromEntries(await prog.enhancements(playerId));
}

async function enhance(playerId, itemId, rand = Math.random) {
  const def = catalog.items.get(itemId);
  if (!def || !["Weapon", "Armor", "Tool"].includes(def.Type)) throw new AppError(400, "Chi cuong hoa duoc vu khi/giap/cong cu.");
  return econ.withTransaction(async (client) => {
    const w = await ensureWallet(client, playerId);
    const owned = await econ.readItems(client, playerId);
    if (!owned.has(itemId)) throw new AppError(400, "Ban khong so huu vat pham nay.");
    const level = (await prog.enhancements(playerId)).get(itemId) || 0;
    if (level >= combat.MAX_ENHANCE) throw new AppError(400, "Da cuong hoa toi da.");
    const step = combat.ENHANCE[level];
    if (Number(w.gold) < step.gold) throw new AppError(400, `Can ${step.gold} vang.`);
    for (const [id, n] of step.mats)
      if ((owned.get(id) || 0) < n) throw new AppError(400, "Thieu vat lieu cuong hoa.");
    await econ.addGold(client, playerId, -step.gold);
    for (const [id, n] of step.mats) await econ.adjustItem(client, playerId, id, -n);
    const success = rand() < step.chance;
    const newLevel = success ? level + 1 : level >= 6 ? level - 1 : level;
    await prog.setEnhancement(client, playerId, itemId, newLevel);
    return { itemId, success, level: newLevel };
  });
}

// ==== Dau truong ====
async function duelPower(playerId) {
  const owned = await prog.ownedItems(playerId);
  return combat.duelPower(owned, await prog.enhancements(playerId), catalog);
}

async function recordDuel(winnerId, loserId) {
  return econ.withTransaction(async (client) => {
    const r = await prog.lockRatings(client, winnerId, loserId);
    const res = combat.elo(r.get(winnerId), r.get(loserId));
    await prog.setRating(client, winnerId, res.winner, true);
    await prog.setRating(client, loserId, res.loser, false);
    return res;
  });
}

module.exports = { getDaily, checkin, claimQuest, reportStats, leaderboard, BOARDS, getEnhancements, enhance, duelPower, recordDuel };
