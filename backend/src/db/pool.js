// Ket noi PostgreSQL dung chung (connection pool) - moi repository import module nay thay vi tu
// tao ket noi rieng. Gioi han so ket noi (PG_POOL_MAX) quan trong khi chay NHIEU INSTANCE sau
// load balancer - xem README "San sang cho quy mo lon hon".
const { Pool } = require("pg");
const { env } = require("../config/env");
const { logger } = require("../utils/logger");

const pool = new Pool({
  connectionString: env.DATABASE_URL,
  max: env.PG_POOL_MAX,
  idleTimeoutMillis: env.PG_IDLE_TIMEOUT_MS,
  connectionTimeoutMillis: env.PG_CONN_TIMEOUT_MS,
});

// Loi ket noi "ngoai luong" (vd Postgres tu dong dong 1 ket noi ranh) khong duoc bat boi 1
// request cu the nao - neu khong lang nghe, Node se coi day la loi CHUA XU LY va crash toan bo
// process (dang phuc vu hang tram request khac cung sap theo).
pool.on("error", (err) => {
  logger.error({ err }, "Loi ngoai luong tu Postgres pool");
});

module.exports = { pool };
