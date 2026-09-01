// Lop DUY NHAT truc tiep dung SQL cho bang "saves".
const { pool } = require("../db/pool");

async function get(playerId) {
  const result = await pool.query(
    "SELECT data, updated_at FROM saves WHERE player_id = $1",
    [playerId]
  );
  return result.rows[0] || null;
}

async function upsert(playerId, data) {
  await pool.query(
    `INSERT INTO saves (player_id, data, updated_at) VALUES ($1, $2, now())
     ON CONFLICT (player_id) DO UPDATE SET data = EXCLUDED.data, updated_at = now()`,
    [playerId, data]
  );
}

module.exports = { get, upsert };
