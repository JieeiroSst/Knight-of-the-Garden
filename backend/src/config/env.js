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

  // ==== Game online ====
  // Lich game theo thoi gian thuc (xem game/calendar.js): ngay 1 = GAME_EPOCH, mui gio game.
  GAME_EPOCH: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).default("2026-01-01"),
  GAME_TZ_OFFSET_MIN: z.coerce.number().int().min(-720).max(840).default(420),
  // Vai tro process: "api" (REST), "gateway" (WebSocket), "all" (ca 2 - chay 1 may/dev).
  ROLE: z.enum(["api", "gateway", "all"]).default("all"),
  // Redis cho pub/sub giua cac gateway + bang dem kenh. Bo trong = bus trong bo nho (CHI 1 process).
  REDIS_URL: z.string().optional(),
  CHANNEL_CAPACITY: z.coerce.number().int().min(2).max(500).default(80),
  WS_TICK_MS: z.coerce.number().int().min(50).max(1000).default(200),
  WS_MAX_CONNECTIONS: z.coerce.number().int().positive().default(50000),
  MARKET_TAX_PERCENT: z.coerce.number().min(0).max(50).default(5),
  MARKET_LISTING_DAYS: z.coerce.number().int().positive().default(7),
});

const parsed = envSchema.safeParse(process.env);
// Production chay nhieu process (api/gateway rieng): BAT BUOC Redis - pub/sub giua gateway, to doi,
// kenh, cache. Thieu Redis thi moi process giu trang thai rieng -> mat to doi khi khoi dong lai.
if (parsed.success && parsed.data.NODE_ENV === "production" && parsed.data.ROLE !== "all" && !parsed.data.REDIS_URL) {
  parsed.success = false;
  parsed.error = { issues: [{ path: ["REDIS_URL"], message: "bat buoc khi NODE_ENV=production va ROLE=api/gateway (to doi/kenh/cache phai dung chung qua Redis)." }] };
}
if (!parsed.success) {
  console.error("Bien moi truong khong hop le, dung khoi dong:");
  for (const issue of parsed.error.issues) {
    console.error(`  - ${issue.path.join(".")}: ${issue.message}`);
  }
  process.exit(1);
}

module.exports = { env: parsed.data };
