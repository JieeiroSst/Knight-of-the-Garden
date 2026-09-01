// Entrypoint MONG: chi lo khoi dong (chay migration + mo cong lang nghe) va tat mem - toan bo
// logic ung dung that su nam trong app.js/routes/controllers/services/repositories.
const { env } = require("./config/env");
const { createApp } = require("./app");
const { pool } = require("./db/pool");
const { runMigrations } = require("./db/migrate");
const { logger } = require("./utils/logger");

let server;

async function start() {
  await runMigrations();

  const app = createApp();
  server = app.listen(env.PORT, () => {
    logger.info({ port: env.PORT, env: env.NODE_ENV }, "Backend Hiep Si Ve Vuon da khoi dong");
  });
}

// Tat mem (graceful shutdown): khi chay sau orchestrator (Kubernetes/ECS...), moi lan trien khai
// ban moi hoac tu dong giam so instance (autoscaling) deu gui SIGTERM truoc khi giet container.
// Neu khong xu ly, cac request DANG XU LY DO DANG bi cat ngang giua chung (nguoi choi co the mat
// du lieu dang luu). Doi request hien tai xong roi moi dong ket noi DB + thoat.
function shutdown(signal) {
  logger.info({ signal }, "Nhan tin hieu tat, dang tat mem...");
  if (!server) {
    process.exit(0);
    return;
  }
  server.close(async () => {
    await pool.end();
    logger.info("Da dong het ket noi, thoat.");
    process.exit(0);
  });
  // Du phong: neu con request "treo" qua lau, buoc thoat sau 10s thay vi cho vo han.
  setTimeout(() => process.exit(1), 10000).unref();
}
process.on("SIGTERM", () => shutdown("SIGTERM"));
process.on("SIGINT", () => shutdown("SIGINT"));

start().catch((err) => {
  logger.error({ err }, "Khong khoi dong duoc backend");
  process.exit(1);
});
