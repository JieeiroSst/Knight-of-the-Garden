const express = require("express");
const bcrypt = require("bcryptjs");
const { pool } = require("../db");
const { signToken } = require("../auth");

const router = express.Router();

function validateCredentials(username, password) {
  if (typeof username !== "string" || typeof password !== "string") return "Thieu username/password.";
  if (username.trim().length < 3) return "Username phai co it nhat 3 ky tu.";
  if (password.length < 6) return "Password phai co it nhat 6 ky tu.";
  return null;
}

// POST /api/register {username, password} -> tao tai khoan moi, tra JWT.
router.post("/register", async (req, res) => {
  const { username, password } = req.body || {};
  const invalidReason = validateCredentials(username, password);
  if (invalidReason) return res.status(400).json({ error: invalidReason });

  try {
    const passwordHash = await bcrypt.hash(password, 10);
    const result = await pool.query(
      "INSERT INTO players (username, password_hash) VALUES ($1, $2) RETURNING id",
      [username.trim(), passwordHash]
    );
    const playerId = result.rows[0].id;
    return res.status(201).json({ token: signToken(playerId) });
  } catch (err) {
    if (err.code === "23505") return res.status(409).json({ error: "Username da ton tai." });
    console.error(err);
    return res.status(500).json({ error: "Loi may chu, thu lai sau." });
  }
});

// POST /api/login {username, password} -> so sanh bam, tra JWT.
router.post("/login", async (req, res) => {
  const { username, password } = req.body || {};
  const invalidReason = validateCredentials(username, password);
  if (invalidReason) return res.status(400).json({ error: invalidReason });

  try {
    const result = await pool.query(
      "SELECT id, password_hash FROM players WHERE username = $1",
      [username.trim()]
    );
    if (result.rows.length === 0) return res.status(401).json({ error: "Sai username hoac password." });

    const player = result.rows[0];
    const match = await bcrypt.compare(password, player.password_hash);
    if (!match) return res.status(401).json({ error: "Sai username hoac password." });

    return res.json({ token: signToken(player.id) });
  } catch (err) {
    console.error(err);
    return res.status(500).json({ error: "Loi may chu, thu lai sau." });
  }
});

module.exports = router;
