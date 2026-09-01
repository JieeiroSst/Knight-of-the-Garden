require("dotenv").config();
const express = require("express");
const cors = require("cors");
const { pool } = require("./db");

const authRoutes = require("./routes/auth");
const saveRoutes = require("./routes/save");

const app = express();
app.use(cors());
app.use(express.json({ limit: "2mb" })); // save co the kha lon (nhieu o dat/tui do), noi rong gioi han mac dinh 100kb

app.get("/health", (_req, res) => res.json({ ok: true }));
app.use("/api", authRoutes);
app.use("/api/save", saveRoutes);

const port = process.env.PORT || 3000;

async function start() {
  // Tu tao bang neu chua co (doc db/schema.sql) - de "docker compose up" chay ngay lan dau,
  // khong can nguoi dung tu chay migration thu cong.
  const fs = require("fs");
  const path = require("path");
  const schema = fs.readFileSync(path.join(__dirname, "..", "db", "schema.sql"), "utf8");
  await pool.query(schema);

  app.listen(port, () => {
    console.log(`Backend Hiep Si Ve Vuon dang chay tai http://localhost:${port}`);
  });
}

start().catch((err) => {
  console.error("Khong khoi dong duoc backend:", err);
  process.exit(1);
});
