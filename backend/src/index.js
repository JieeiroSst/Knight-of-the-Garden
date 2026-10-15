// Entrypoint MONG: khoi dong theo vai tro (ROLE) + tat mem.
//  - ROLE=api      : REST API (dang nhap/luu/kinh te/giao dich/cho) - stateless, nhan ban thoai mai.
//  - ROLE=gateway  : WebSocket /ws (hien dien/vi tri/chat) - can REDIS_URL khi chay nhieu instance.
//  - ROLE=all      : ca hai trong 1 process (dev/may nho; khong can Redis).
const http = require("http");
const { env } = require("./config/env");
const { createApp } = require("./app");
const { pool } = require("./db/pool");
const { runMigrations } = require("./db/migrate");
const { logger } = require("./utils/logger");
const { attachGateway } = require("./realtime/gateway");
const { getBus } = require("./realtime/bus");

let server;
let gateway;

async function start() {
  if (env.ROLE !== "gateway") await runMigrations();

  const app = createApp({ apiEnabled: env.ROLE !== "gateway" });
  server = http.createServer(app);
  if (env.ROLE !== "api") gateway = attachGateway(server);
  // Hang doi ket noi cho xu ly lon (kernel van gioi han boi net.core.somaxconn - tren may chu Linux
  // nen dat >= 4096 de chiu duoc luc nhieu nguoi vao cung luc).
  server.listen({ port: env.PORT, backlog: 4096 }, () => {
    logger.info({ port: env.PORT, env: env.NODE_ENV, role: env.ROLE }, "Backend Hiep Si Ve Vuon da khoi dong");
  });
}

// Tat mem (graceful shutdown): khi orchestrator gui SIGTERM (trien khai ban moi/giam instance),
// dong WebSocket (client tu ket noi lai gateway khac), doi request dang xu ly xong roi moi thoat.
function shutdown(signal) {
  logger.info({ signal }, "Nhan tin hieu tat, dang tat mem...");
  if (!server) {
    process.exit(0);
    return;
  }
  Promise.resolve(gateway?.close()).finally(() => {
    server.close(async () => {
      await pool.end();
      await getBus().close();
      logger.info("Da dong het ket noi, thoat.");
      process.exit(0);
    });
  });
  setTimeout(() => process.exit(1), 10000).unref();
}
process.on("SIGTERM", () => shutdown("SIGTERM"));
process.on("SIGINT", () => shutdown("SIGINT"));

start().catch((err) => {
  logger.error({ err }, "Khong khoi dong duoc backend");
  process.exit(1);
});
