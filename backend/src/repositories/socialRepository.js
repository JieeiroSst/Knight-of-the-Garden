// SQL cho ban be / bang hoi / tham nong trai.
const { pool } = require("../db/pool");
const { adjustContainer } = require("./containerState");

const pair = (x, y) => (x < y ? [x, y] : [y, x]);

// ---- Ban be ----
async function getFriendship(x, y) {
  const [a, b] = pair(x, y);
  const r = await pool.query("SELECT * FROM friendships WHERE a_id = $1 AND b_id = $2", [a, b]);
  return r.rows[0] || null;
}

async function requestFriend(from, to) {
  const [a, b] = pair(from, to);
  await pool.query(
    `INSERT INTO friendships (a_id, b_id, status, requested_by) VALUES ($1, $2, 'pending', $3)
     ON CONFLICT (a_id, b_id) DO NOTHING`, [a, b, from]);
}

async function acceptFriend(x, y) {
  const [a, b] = pair(x, y);
  const r = await pool.query(
    "UPDATE friendships SET status = 'accepted' WHERE a_id = $1 AND b_id = $2 AND status = 'pending' AND requested_by <> $3 RETURNING 1",
    [a, b, x]);
  return r.rowCount > 0;
}

async function removeFriend(x, y) {
  const [a, b] = pair(x, y);
  await pool.query("DELETE FROM friendships WHERE a_id = $1 AND b_id = $2", [a, b]);
}

async function listFriends(me) {
  const r = await pool.query(
    `SELECT f.status, f.requested_by, CASE WHEN f.a_id = $1 THEN f.b_id ELSE f.a_id END AS other_id, c.display_name, c.gender, c.model_id
     FROM friendships f JOIN characters c ON c.player_id = CASE WHEN f.a_id = $1 THEN f.b_id ELSE f.a_id END
     WHERE f.a_id = $1 OR f.b_id = $1 ORDER BY c.display_name LIMIT 300`, [me]);
  return r.rows;
}

async function areFriends(x, y) {
  const f = await getFriendship(x, y);
  return !!f && f.status === "accepted";
}

// ---- Bang hoi ----
async function memberOf(playerId) {
  const r = await pool.query(
    `SELECT g.id, g.name, g.tag, g.leader_id, m.role FROM guild_members m JOIN guilds g ON g.id = m.guild_id WHERE m.player_id = $1`,
    [playerId]);
  return r.rows[0] || null;
}

async function createGuild(client, name, tag, leaderId) {
  const r = await client.query("INSERT INTO guilds (name, tag, leader_id) VALUES ($1, $2, $3) RETURNING id", [name, tag, leaderId]);
  await client.query("INSERT INTO guild_members (player_id, guild_id, role) VALUES ($1, $2, 'leader')", [leaderId, r.rows[0].id]);
  return r.rows[0].id;
}

async function members(guildId) {
  const r = await pool.query(
    `SELECT m.player_id, m.role, c.display_name FROM guild_members m LEFT JOIN characters c ON c.player_id = m.player_id
     WHERE m.guild_id = $1 ORDER BY m.role, c.display_name`, [guildId]);
  return r.rows;
}

async function countMembers(guildId) {
  const r = await pool.query("SELECT count(*)::int AS n FROM guild_members WHERE guild_id = $1", [guildId]);
  return r.rows[0].n;
}

async function invite(guildId, playerId) {
  await pool.query("INSERT INTO guild_invites (guild_id, player_id) VALUES ($1, $2) ON CONFLICT DO NOTHING", [guildId, playerId]);
}

async function invitesFor(playerId) {
  const r = await pool.query(
    `SELECT g.id, g.name, g.tag FROM guild_invites i JOIN guilds g ON g.id = i.guild_id WHERE i.player_id = $1`, [playerId]);
  return r.rows;
}

async function joinGuild(client, guildId, playerId) {
  const del = await client.query("DELETE FROM guild_invites WHERE guild_id = $1 AND player_id = $2 RETURNING 1", [guildId, playerId]);
  if (!del.rowCount) return false;
  await client.query("INSERT INTO guild_members (player_id, guild_id, role) VALUES ($1, $2, 'member')", [playerId, guildId]);
  await client.query("DELETE FROM guild_invites WHERE player_id = $1", [playerId]);
  return true;
}

async function leaveGuild(client, playerId) {
  await client.query("DELETE FROM guild_members WHERE player_id = $1", [playerId]);
}

async function setRole(guildId, playerId, role) {
  await pool.query("UPDATE guild_members SET role = $3 WHERE guild_id = $1 AND player_id = $2", [guildId, playerId, role]);
}

async function deleteGuild(client, guildId) {
  await client.query("DELETE FROM guilds WHERE id = $1", [guildId]);
}

async function nextLeader(client, guildId) {
  const r = await client.query(
    `SELECT player_id FROM guild_members WHERE guild_id = $1 ORDER BY CASE role WHEN 'officer' THEN 0 ELSE 1 END, joined_at LIMIT 1`, [guildId]);
  return r.rows[0]?.player_id || null;
}

async function setLeader(client, guildId, playerId) {
  await client.query("UPDATE guilds SET leader_id = $2 WHERE id = $1", [guildId, playerId]);
  await client.query("UPDATE guild_members SET role = 'leader' WHERE player_id = $1", [playerId]);
}

async function storage(guildId) {
  const r = await pool.query("SELECT item_id, count FROM guild_storage WHERE guild_id = $1 ORDER BY item_id", [guildId]);
  return r.rows;
}

// Tra ve true (them) / trang thai phan lay ra (lay) / false (khong du) - xem containerState.js.
async function adjustStorage(client, guildId, itemId, delta, carry = null) {
  return adjustContainer(client, "guild_storage", ["guild_id"], [guildId], itemId, delta, carry);
}

// ---- Tham nong trai ----
async function addHelps(hostId, helperId, day, keys) {
  if (!keys.length) return 0;
  const r = await pool.query(
    `INSERT INTO farm_helps (host_id, plot_key, day, helper_id) SELECT $1, k, $2, $3 FROM unnest($4::text[]) k
     ON CONFLICT DO NOTHING`, [hostId, day, helperId, keys]);
  return r.rowCount;
}

async function helpsToday(helperId, day) {
  const r = await pool.query("SELECT count(*)::int AS n FROM farm_helps WHERE helper_id = $1 AND day = $2", [helperId, day]);
  return r.rows[0].n;
}

async function pendingHelps(hostId, minDay) {
  const r = await pool.query(
    `SELECT h.plot_key, h.day, c.display_name FROM farm_helps h LEFT JOIN characters c ON c.player_id = h.helper_id
     WHERE h.host_id = $1 AND h.applied = false AND h.day >= $2`, [hostId, minDay]);
  return r.rows;
}

async function ackHelps(hostId) {
  await pool.query("UPDATE farm_helps SET applied = true WHERE host_id = $1 AND applied = false", [hostId]);
}

module.exports = {
  getFriendship, requestFriend, acceptFriend, removeFriend, listFriends, areFriends,
  memberOf, createGuild, members, countMembers, invite, invitesFor, joinGuild, leaveGuild, setRole, deleteGuild, nextLeader, setLeader,
  storage, adjustStorage, addHelps, helpsToday, pendingHelps, ackHelps,
};
