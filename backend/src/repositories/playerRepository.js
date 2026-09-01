// Lop DUY NHAT truc tiep dung SQL cho bang "players" - service KHONG BAO GIO tu viet query, chi
// goi qua day (de doi database/ORM sau nay chi can sua 1 cho).
const { pool } = require("../db/pool");

async function create(username, passwordHash) {
  const result = await pool.query(
    "INSERT INTO players (username, password_hash) VALUES ($1, $2) RETURNING id",
    [username, passwordHash]
  );
  return result.rows[0].id;
}

async function findByUsername(username) {
  const result = await pool.query(
    "SELECT id, password_hash FROM players WHERE username = $1",
    [username]
  );
  return result.rows[0] || null;
}

module.exports = { create, findByUsername };
