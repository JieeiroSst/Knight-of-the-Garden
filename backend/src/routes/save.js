const express = require("express");
const { pool } = require("../db");
const { requireAuth } = require("../auth");

const router = express.Router();
router.use(requireAuth);

// GET /api/save -> tra du lieu luu cua nguoi choi dang dang nhap. 404 neu chua tung luu (nguoi
// choi moi, Godot se tu tao the gioi mac dinh).
router.get("/", async (req, res) => {
  try {
    const result = await pool.query("SELECT data, updated_at FROM saves WHERE player_id = $1", [req.playerId]);
    if (result.rows.length === 0) return res.status(404).json({ error: "Chua co du lieu luu." });
    return res.json({ data: result.rows[0].data, updatedAt: result.rows[0].updated_at });
  } catch (err) {
    console.error(err);
    return res.status(500).json({ error: "Loi may chu, thu lai sau." });
  }
});

// PUT /api/save {data: <toan bo SaveData>} -> ghi de (upsert) ban luu duy nhat cua nguoi choi.
router.put("/", async (req, res) => {
  const { data } = req.body || {};
  if (data === undefined || data === null) return res.status(400).json({ error: "Thieu du lieu luu." });

  try {
    await pool.query(
      `INSERT INTO saves (player_id, data, updated_at) VALUES ($1, $2, now())
       ON CONFLICT (player_id) DO UPDATE SET data = EXCLUDED.data, updated_at = now()`,
      [req.playerId, data]
    );
    return res.json({ ok: true });
  } catch (err) {
    console.error(err);
    return res.status(500).json({ error: "Loi may chu, thu lai sau." });
  }
});

module.exports = router;
