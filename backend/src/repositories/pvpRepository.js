// SQL cho tui do roi (loot_bags) va can cu (base_structures/base_chest_items). Ham nhan "client" chay
// ben trong transaction cua pvpService.
const { pool } = require("../db/pool");
const { adjustContainer } = require("./containerState");

const BAG_COLS = "id, zone, channel, x, z, owner_id, killer_id, owner_name, items, states, gold, created_at, priority_until, expires_at, looted_at";
const STRUCT_COLS = "id, owner_id, kind, zone, x, z, rot, hp, max_hp";

async function insertBag(client, b) {
  const r = await client.query(
    `INSERT INTO loot_bags (zone, channel, x, z, owner_id, killer_id, owner_name, items, gold, priority_until, expires_at, states)
     VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9, now() + ($10 || ' milliseconds')::interval, now() + ($11 || ' milliseconds')::interval, $12)
     RETURNING ${BAG_COLS}`,
    [b.zone, b.channel, b.x, b.z, b.ownerId || null, b.killerId || null, b.ownerName || "", JSON.stringify(b.items), b.gold || 0,
      String(b.priorityMs), String(b.lifetimeMs), JSON.stringify(b.states || {})]
  );
  return r.rows[0];
}

async function lockBag(client, id) {
  const r = await client.query(`SELECT ${BAG_COLS} FROM loot_bags WHERE id = $1 FOR UPDATE`, [id]);
  return r.rows[0] || null;
}

async function markLooted(client, id, playerId) {
  await client.query("UPDATE loot_bags SET looted_by = $2, looted_at = now() WHERE id = $1", [id, playerId]);
}

async function activeBags(zone, channel, x, z, radius) {
  const r = await pool.query(
    `SELECT ${BAG_COLS} FROM loot_bags
     WHERE zone = $1 AND channel = $2 AND looted_at IS NULL AND expires_at > now()
       AND x BETWEEN $3::real - $5::real AND $3::real + $5::real AND z BETWEEN $4::real - $5::real AND $4::real + $5::real
     ORDER BY id DESC LIMIT 100`,
    [zone, channel, x, z, radius]
  );
  return r.rows;
}

// Tui het han (khong ai nhat) -> do mat han; tui da nhat giu 1 ngay de tra cuu roi xoa.
async function purgeBags() {
  const r = await pool.query(
    `DELETE FROM loot_bags WHERE (looted_at IS NULL AND expires_at < now()) OR looted_at < now() - interval '1 day'
     RETURNING id, zone, channel, x, z`
  );
  return r.rows;
}

async function countStructures(client, ownerId) {
  const r = await client.query("SELECT count(*)::int AS n FROM base_structures WHERE owner_id = $1", [ownerId]);
  return r.rows[0].n;
}

async function insertStructure(client, s) {
  const r = await client.query(
    `INSERT INTO base_structures (owner_id, kind, zone, x, z, rot, hp, max_hp) VALUES ($1,$2,$3,$4,$5,$6,$7,$7) RETURNING ${STRUCT_COLS}`,
    [s.ownerId, s.kind, s.zone, s.x, s.z, s.rot, s.hp]
  );
  return r.rows[0];
}

async function lockStructure(client, id) {
  const r = await client.query(`SELECT ${STRUCT_COLS} FROM base_structures WHERE id = $1 FOR UPDATE`, [id]);
  return r.rows[0] || null;
}

async function getStructure(id) {
  const r = await pool.query(`SELECT ${STRUCT_COLS} FROM base_structures WHERE id = $1`, [id]);
  return r.rows[0] || null;
}

async function setStructureHp(client, id, hp) {
  await client.query("UPDATE base_structures SET hp = $2 WHERE id = $1", [id, hp]);
}

async function deleteStructure(client, id) {
  await client.query("DELETE FROM base_structures WHERE id = $1", [id]);
}

async function structuresNear(zone, x, z, radius) {
  const r = await pool.query(
    `SELECT s.id, s.owner_id, s.kind, s.zone, s.x, s.z, s.rot, s.hp, s.max_hp, COALESCE(c.display_name, '') AS owner_name
     FROM base_structures s LEFT JOIN characters c ON c.player_id = s.owner_id
     WHERE s.zone = $1 AND s.x BETWEEN $2::real - $4::real AND $2::real + $4::real AND s.z BETWEEN $3::real - $4::real AND $3::real + $4::real
     LIMIT 400`,
    [zone, x, z, radius]
  );
  return r.rows;
}

// 1 o luoi (cx, cz) kich thuoc cell - dung cho cache theo o (xem cache.cellsInRadius).
async function structuresInCell(zone, cx, cz, cell) {
  const r = await pool.query(
    `SELECT s.id, s.owner_id, s.kind, s.zone, s.x, s.z, s.rot, s.hp, s.max_hp, COALESCE(c.display_name, '') AS owner_name
     FROM base_structures s LEFT JOIN characters c ON c.player_id = s.owner_id
     WHERE s.zone = $1 AND s.x >= $2::real AND s.x < $3::real AND s.z >= $4::real AND s.z < $5::real
     LIMIT 500`,
    [zone, cx * cell, (cx + 1) * cell, cz * cell, (cz + 1) * cell]
  );
  return r.rows;
}

async function bagsInCell(zone, channel, cx, cz, cell) {
  const r = await pool.query(
    `SELECT ${BAG_COLS} FROM loot_bags
     WHERE zone = $1 AND channel = $2 AND looted_at IS NULL AND expires_at > now()
       AND x >= $3::real AND x < $4::real AND z >= $5::real AND z < $6::real
     LIMIT 100`,
    [zone, channel, cx * cell, (cx + 1) * cell, cz * cell, (cz + 1) * cell]
  );
  return r.rows;
}

async function bedsOf(client, ownerId) {
  const r = await (client || pool).query("SELECT id, x, z FROM base_structures WHERE owner_id = $1 AND kind = 'bed'", [ownerId]);
  return r.rows;
}

async function getRespawn(client, playerId) {
  const r = await (client || pool).query("SELECT bed_lost, ready_at FROM player_respawn WHERE player_id = $1", [playerId]);
  return r.rows[0] || null;
}

async function setRespawn(client, playerId, { bedLost, readyAt }) {
  await client.query(
    `INSERT INTO player_respawn (player_id, bed_lost, ready_at, updated_at) VALUES ($1, $2, $3, now())
     ON CONFLICT (player_id) DO UPDATE SET bed_lost = EXCLUDED.bed_lost, ready_at = EXCLUDED.ready_at, updated_at = now()`,
    [playerId, !!bedLost, readyAt ? new Date(readyAt) : null]
  );
}

async function myStructures(ownerId) {
  const r = await pool.query(`SELECT ${STRUCT_COLS} FROM base_structures WHERE owner_id = $1 ORDER BY id`, [ownerId]);
  return r.rows;
}

async function chestItems(client, structureId) {
  const r = await (client || pool).query("SELECT item_id, count FROM base_chest_items WHERE structure_id = $1", [structureId]);
  return new Map(r.rows.map((row) => [row.item_id, row.count]));
}

async function adjustChest(client, structureId, itemId, delta, carry = null) {
  return adjustContainer(client, "base_chest_items", ["structure_id"], [structureId], itemId, delta, carry);
}

async function chestStates(client, structureId) {
  const r = await client.query("SELECT item_id, state FROM base_chest_items WHERE structure_id = $1 AND state IS NOT NULL", [structureId]);
  return Object.fromEntries(r.rows.map((row) => [row.item_id, row.state]));
}

module.exports = {
  insertBag, lockBag, markLooted, activeBags, purgeBags,
  countStructures, insertStructure, lockStructure, getStructure, setStructureHp, deleteStructure, structuresNear, myStructures,
  structuresInCell, bagsInCell, bedsOf, getRespawn, setRespawn,
  chestItems, adjustChest, chestStates,
};
