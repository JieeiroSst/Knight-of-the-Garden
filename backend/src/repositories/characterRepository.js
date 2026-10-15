const { pool } = require("../db/pool");

const COLS = "player_id, display_name, gender, model_id, skin_color, hair_color, outfit_color";

async function get(playerId) {
  const r = await pool.query(`SELECT ${COLS} FROM characters WHERE player_id = $1`, [playerId]);
  return r.rows[0] || null;
}

async function getMany(ids) {
  const r = await pool.query(`SELECT ${COLS} FROM characters WHERE player_id = ANY($1)`, [ids]);
  return r.rows;
}

async function findByName(name) {
  const r = await pool.query(`SELECT ${COLS} FROM characters WHERE lower(display_name) = lower($1)`, [name]);
  return r.rows[0] || null;
}

async function upsert(playerId, c) {
  const r = await pool.query(
    `INSERT INTO characters (player_id, display_name, gender, model_id, skin_color, hair_color, outfit_color)
     VALUES ($1, $2, $3, $4, $5, $6, $7)
     ON CONFLICT (player_id) DO UPDATE SET display_name = EXCLUDED.display_name, gender = EXCLUDED.gender,
       model_id = EXCLUDED.model_id, skin_color = EXCLUDED.skin_color, hair_color = EXCLUDED.hair_color,
       outfit_color = EXCLUDED.outfit_color, updated_at = now()
     RETURNING ${COLS}`,
    [playerId, c.displayName, c.gender, c.modelId, c.skinColor, c.hairColor, c.outfitColor]
  );
  return r.rows[0];
}

module.exports = { get, getMany, findByName, upsert };
