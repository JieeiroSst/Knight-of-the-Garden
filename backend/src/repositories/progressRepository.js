// SQL cho diem danh / nhiem vu ngay-tuan / chi so xep hang / Elo PvP / cuong hoa trang bi.
const { pool } = require("../db/pool");

async function getLogin(client, playerId) {
  const r = await client.query("SELECT last_day, streak FROM daily_logins WHERE player_id = $1 FOR UPDATE", [playerId]);
  return r.rows[0] || null;
}
async function setLogin(client, playerId, day, streak) {
  await client.query(
    `INSERT INTO daily_logins (player_id, last_day, streak) VALUES ($1, $2, $3)
     ON CONFLICT (player_id) DO UPDATE SET last_day = EXCLUDED.last_day, streak = EXCLUDED.streak`, [playerId, day, streak]);
}
async function peekLogin(playerId) {
  const r = await pool.query("SELECT last_day, streak FROM daily_logins WHERE player_id = $1", [playerId]);
  return r.rows[0] || null;
}

// Tong bo dem kinh te (economy_counters) trong khoang gio [fromBucket, toBucket).
async function countersBetween(playerId, fromBucket, toBucket) {
  const r = await pool.query(
    "SELECT source, sum(amount)::bigint AS n FROM economy_counters WHERE player_id = $1 AND bucket >= $2 AND bucket < $3 GROUP BY source",
    [playerId, fromBucket, toBucket]);
  return new Map(r.rows.map((x) => [x.source, Number(x.n)]));
}

async function claims(playerId, periodKey) {
  const r = await pool.query("SELECT idx FROM quest_claims WHERE player_id = $1 AND period_key = $2", [playerId, periodKey]);
  return new Set(r.rows.map((x) => x.idx));
}
async function insertClaim(client, playerId, periodKey, idx) {
  const r = await client.query("INSERT INTO quest_claims (player_id, period_key, idx) VALUES ($1, $2, $3) ON CONFLICT DO NOTHING RETURNING 1", [playerId, periodKey, idx]);
  return r.rowCount > 0;
}

// Chi so xep hang client bao: chi nhan tang, toi da `cap` moi ngay game.
async function reportStat(playerId, stat, value, day, cap) {
  await pool.query(
    `INSERT INTO player_stats (player_id, stat, value, updated_day, day_gain) VALUES ($1, $2, LEAST($3::bigint, $5::bigint), $4, LEAST($3::bigint, $5::bigint))
     ON CONFLICT (player_id, stat) DO UPDATE SET
       value = player_stats.value + LEAST(GREATEST($3 - player_stats.value, 0),
               GREATEST($5 - CASE WHEN player_stats.updated_day = $4 THEN player_stats.day_gain ELSE 0 END, 0)),
       day_gain = CASE WHEN player_stats.updated_day = $4 THEN player_stats.day_gain ELSE 0 END
               + LEAST(GREATEST($3 - player_stats.value, 0),
                       GREATEST($5 - CASE WHEN player_stats.updated_day = $4 THEN player_stats.day_gain ELSE 0 END, 0)),
       updated_day = $4`,
    [playerId, stat, value, day, cap]);
}

async function topStat(stat, limit) {
  const r = await pool.query(
    `SELECT s.player_id, s.value, c.display_name FROM player_stats s JOIN characters c ON c.player_id = s.player_id
     WHERE s.stat = $1 ORDER BY s.value DESC LIMIT $2`, [stat, limit]);
  return r.rows;
}
async function myStat(stat, playerId) {
  const r = await pool.query(
    `SELECT value, (SELECT count(*)::int + 1 FROM player_stats o WHERE o.stat = $1 AND o.value > s.value) AS rank
     FROM player_stats s WHERE s.stat = $1 AND s.player_id = $2`, [stat, playerId]);
  return r.rows[0] || null;
}
async function topGold(limit) {
  const r = await pool.query(
    `SELECT w.player_id, w.gold AS value, c.display_name FROM wallets w JOIN characters c ON c.player_id = w.player_id
     ORDER BY w.gold DESC LIMIT $1`, [limit]);
  return r.rows;
}
async function myGold(playerId) {
  const r = await pool.query(
    `SELECT gold AS value, (SELECT count(*)::int + 1 FROM wallets o WHERE o.gold > w.gold) AS rank FROM wallets w WHERE player_id = $1`, [playerId]);
  return r.rows[0] || null;
}
async function topPvp(limit) {
  const r = await pool.query(
    `SELECT p.player_id, p.rating AS value, p.wins, p.losses, c.display_name FROM pvp_ratings p JOIN characters c ON c.player_id = p.player_id
     ORDER BY p.rating DESC LIMIT $1`, [limit]);
  return r.rows;
}
async function myPvp(playerId) {
  const r = await pool.query(
    `SELECT rating AS value, wins, losses, (SELECT count(*)::int + 1 FROM pvp_ratings o WHERE o.rating > p.rating) AS rank FROM pvp_ratings p WHERE player_id = $1`, [playerId]);
  return r.rows[0] || null;
}

async function lockRatings(client, a, b) {
  for (const id of [a, b].sort((x, y) => x - y))
    await client.query("INSERT INTO pvp_ratings (player_id) VALUES ($1) ON CONFLICT DO NOTHING", [id]);
  const r = await client.query("SELECT player_id, rating FROM pvp_ratings WHERE player_id = ANY($1) ORDER BY player_id FOR UPDATE", [[a, b]]);
  return new Map(r.rows.map((x) => [x.player_id, x.rating]));
}
async function setRating(client, id, rating, won) {
  await client.query(
    `UPDATE pvp_ratings SET rating = $2, wins = wins + $3, losses = losses + $4 WHERE player_id = $1`, [id, rating, won ? 1 : 0, won ? 0 : 1]);
}

async function enhancements(playerId) {
  const r = await pool.query("SELECT item_id, level FROM item_enhancements WHERE player_id = $1", [playerId]);
  return new Map(r.rows.map((x) => [x.item_id, x.level]));
}
async function setEnhancement(client, playerId, itemId, level) {
  await client.query(
    `INSERT INTO item_enhancements (player_id, item_id, level) VALUES ($1, $2, $3)
     ON CONFLICT (player_id, item_id) DO UPDATE SET level = EXCLUDED.level`, [playerId, itemId, level]);
}
async function ownedItemCounts(playerId) {
  const r = await pool.query("SELECT item_id, count FROM inventory_items WHERE player_id = $1", [playerId]);
  return new Map(r.rows.map((x) => [x.item_id, x.count]));
}
async function itemStates(playerId) {
  const r = await pool.query("SELECT item_id, state FROM item_states WHERE player_id = $1", [playerId]);
  return new Map(r.rows.map((x) => [x.item_id, x.state]));
}
async function ownedItems(playerId) {
  const r = await pool.query("SELECT item_id FROM inventory_items WHERE player_id = $1", [playerId]);
  return r.rows.map((x) => x.item_id);
}

module.exports = {
  getLogin, setLogin, peekLogin, countersBetween, claims, insertClaim, reportStat, topStat, myStat, topGold, myGold, topPvp, myPvp,
  lockRatings, setRating, enhancements, setEnhancement, ownedItems, ownedItemCounts, itemStates,
};
