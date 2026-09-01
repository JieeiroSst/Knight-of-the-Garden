const { pool } = require("../db/pool");

// Liveness: process con song khong - dung cho orchestrator quyet dinh co restart container hay
// khong, PHAI luon nhanh/khong phu thuoc dich vu ngoai (khong cham DB o day).
const liveness = (_req, res) => res.json({ ok: true });

// Readiness: instance nay co san sang NHAN LUU LUONG khong - dung cho load balancer, co kiem tra
// ca ket noi Postgres (1 instance con song nhung mat ket noi DB thi khong nen nhan request moi).
const readiness = async (_req, res) => {
  try {
    await pool.query("SELECT 1");
    res.json({ ok: true });
  } catch (err) {
    res.status(503).json({ ok: false, error: "Khong ket noi duoc database." });
  }
};

module.exports = { liveness, readiness };
