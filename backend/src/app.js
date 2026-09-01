// Lap rap Express app (middleware + route + xu ly loi) TACH RIENG khoi index.js (chi lo khoi
// dong/tat mem) - de tests/ co the tao 1 app THAT (khong mock) ma khong can thuc su goi
// app.listen(), tranh xung dot cong khi chay nhieu test song song.
const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const routes = require("./routes");
const { generalLimiter } = require("./middleware/rateLimiters");
const { requestLogger } = require("./middleware/requestLogger");
const { notFound } = require("./middleware/notFound");
const { errorHandler } = require("./middleware/errorHandler");

function createApp() {
  const app = express();

  // Sau reverse proxy/load balancer khi chay that (xem README) - neu khong bat, req.ip luon la
  // IP cua proxy, lam rate-limit vo hieu (moi nguoi choi bi tinh CHUNG 1 IP).
  app.set("trust proxy", 1);

  app.use(helmet());
  app.use(cors());
  app.use(express.json({ limit: "2mb" })); // save co the kha lon (nhieu o dat/tui do), noi rong gioi han mac dinh 100kb
  app.use(requestLogger);
  app.use("/api", generalLimiter);

  app.use(routes);

  app.use(notFound);
  app.use(errorHandler); // PHAI dat SAU CUNG - Express nhan dien middleware xu ly loi qua so tham so (err, req, res, next)

  return app;
}

module.exports = { createApp };
