const rateLimit = require("express-rate-limit");
const { env } = require("../config/env");

// Gioi han chung: chan spam/DDoS co ban o tang ung dung cho toan bo /api (KHONG thay the DDoS
// protection that su o tang mang/CDN - xem README).
const generalLimiter = rateLimit({
  windowMs: 60 * 1000,
  max: env.RATE_LIMIT_GENERAL,
  standardHeaders: true,
  legacyHeaders: false,
});

// Gioi han RIENG, CHAT HON cho dang nhap/dang ky - muc tieu brute-force pho bien nhat.
const authLimiter = rateLimit({
  windowMs: 15 * 60 * 1000,
  max: env.RATE_LIMIT_AUTH,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: "Qua nhieu lan thu, vui long doi roi thu lai." },
});

module.exports = { generalLimiter, authLimiter };
