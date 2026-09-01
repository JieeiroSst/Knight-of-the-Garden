// Logging CO CAU TRUC (JSON, khong phai console.log van ban thuong) - de mot he thong thu thap
// log tap trung (vd Loki/Datadog/CloudWatch khi deploy that) doc/loc/canh bao duoc, thay vi chi
// la chuoi van ban roi rac. "silent" trong test de output test khong bi nhieu boi log request.
const pino = require("pino");
const { env } = require("../config/env");

const logger = pino({
  level: env.NODE_ENV === "test" ? "silent" : "info",
});

module.exports = { logger };
