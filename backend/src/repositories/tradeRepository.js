const { pool } = require("../db/pool");

async function findActiveBetween(a, b) {
  const r = await pool.query(
    `SELECT * FROM trades WHERE status IN ('pending','open') AND ((a_id = $1 AND b_id = $2) OR (a_id = $2 AND b_id = $1)) LIMIT 1`,
    [a, b]
  );
  return r.rows[0] || null;
}

async function create(a, b) {
  const r = await pool.query("INSERT INTO trades (a_id, b_id) VALUES ($1, $2) RETURNING *", [a, b]);
  return r.rows[0];
}

async function get(id) {
  const r = await pool.query("SELECT * FROM trades WHERE id = $1", [id]);
  return r.rows[0] || null;
}

async function lock(client, id) {
  const r = await client.query("SELECT * FROM trades WHERE id = $1 FOR UPDATE", [id]);
  return r.rows[0] || null;
}

async function update(client, id, fields) {
  const keys = Object.keys(fields);
  const sets = keys.map((k, i) => `${k} = $${i + 2}`).join(", ");
  const r = await client.query(`UPDATE trades SET ${sets}, updated_at = now() WHERE id = $1 RETURNING *`, [id, ...keys.map((k) => fields[k])]);
  return r.rows[0];
}

module.exports = { findActiveBetween, create, get, lock, update };
