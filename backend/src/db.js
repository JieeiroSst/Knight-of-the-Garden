// Ket noi PostgreSQL dung chung (connection pool) - moi route import module nay thay vi tu tao
// ket noi rieng.
const { Pool } = require("pg");

const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
});

module.exports = { pool };
