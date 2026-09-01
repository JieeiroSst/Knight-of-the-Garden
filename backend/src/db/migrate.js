// Migration THAT SU (thay vi chay lai TOAN BO schema.sql moi lan khoi dong nhu truoc) - moi file
// .sql trong db/migrations/ chi CHAY 1 LAN DUY NHAT (theo doi qua bang schema_migrations), theo
// dung thu tu ten file. Them tinh nang moi cho DB sau nay -> them file "002_ten_gi_do.sql" thay
// vi sua truc tiep 001_init.sql (giu lai lich su thay doi, an toan cho database DA CO du lieu
// that - sua truc tiep migration cu se khong ap dung lai cho DB da chay migration do roi).
const fs = require("fs");
const path = require("path");
const { pool } = require("./pool");
const { logger } = require("../utils/logger");

const MIGRATIONS_DIR = path.join(__dirname, "..", "..", "db", "migrations");

async function runMigrations() {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS schema_migrations (
      filename TEXT PRIMARY KEY,
      applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
    );
  `);

  const appliedResult = await pool.query("SELECT filename FROM schema_migrations");
  const applied = new Set(appliedResult.rows.map((r) => r.filename));

  const files = fs
    .readdirSync(MIGRATIONS_DIR)
    .filter((f) => f.endsWith(".sql"))
    .sort();

  for (const file of files) {
    if (applied.has(file)) continue;

    const sql = fs.readFileSync(path.join(MIGRATIONS_DIR, file), "utf8");
    const client = await pool.connect();
    try {
      await client.query("BEGIN");
      await client.query(sql);
      await client.query("INSERT INTO schema_migrations (filename) VALUES ($1)", [file]);
      await client.query("COMMIT");
      logger.info({ file }, "Da ap dung migration");
    } catch (err) {
      await client.query("ROLLBACK");
      throw err;
    } finally {
      client.release();
    }
  }
}

module.exports = { runMigrations };
