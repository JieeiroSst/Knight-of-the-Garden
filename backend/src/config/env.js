// Doc + KIEM TRA toan bo bien moi truong 1 LAN DUY NHAT luc khoi dong - neu thieu/sai (vd
// JWT_SECRET qua ngan hoac thieu han) thi DUNG NGAY voi thong bao ro rang, thay vi de loi am
// tham lan sau (vd ky JWT bang "undefined", chi phat hien khi co nguoi dang nhap that).
require("dotenv").config();
const { z } = require("zod");

const envSchema = z.object({
  NODE_ENV: z.enum(["development", "production", "test"]).default("development"),
  PORT: z.coerce.number().int().positive().default(3000),
  DATABASE_URL: z.string().min(1, "DATABASE_URL la bat buoc."),
  JWT_SECRET: z.string().min(16, "JWT_SECRET phai it nhat 16 ky tu (dung openssl rand -hex 32 de tao)."),
  JWT_EXPIRES_IN: z.string().default("30d"),
  // Xem README "San sang cho quy mo lon hon" - gioi han so ket noi Postgres/toc do request.
  PG_POOL_MAX: z.coerce.number().int().positive().default(10),
  PG_IDLE_TIMEOUT_MS: z.coerce.number().int().positive().default(30000),
  PG_CONN_TIMEOUT_MS: z.coerce.number().int().positive().default(5000),
  RATE_LIMIT_GENERAL: z.coerce.number().int().positive().default(120),
  RATE_LIMIT_AUTH: z.coerce.number().int().positive().default(20),
});

const parsed = envSchema.safeParse(process.env);
if (!parsed.success) {
  console.error("Bien moi truong khong hop le, dung khoi dong:");
  for (const issue of parsed.error.issues) {
    console.error(`  - ${issue.path.join(".")}: ${issue.message}`);
  }
  process.exit(1);
}

module.exports = { env: parsed.data };
