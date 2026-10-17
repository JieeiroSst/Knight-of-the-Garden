// Nong trai rieng cua moi nguoi choi tai toa do co dinh (cap 1 lan, khong doi). Nguoi khac di toi se
// thay nong trai do (xem Godot FarmSpace/FarmReplica): danh sach nong trai gan + ruong cong khai.
const { pool } = require("../db/pool");
const rules = require("../game/farmRules");
const saveRepo = require("../repositories/saveRepository");
const characters = require("./characterService");
const cache = require("../cache/cache");

const farmKey = (id) => `farm:${id}`;

async function getFarm(playerId) {
  const r = await pool.query("SELECT gx, gz, x, z FROM player_farms WHERE player_id = $1", [playerId]);
  return r.rows[0] || null;
}

// Lay (hoac cap moi) o nong trai: thu tung o theo vong xoan, INSERT ... ON CONFLICT DO NOTHING de 2
// nguoi dang ky cung luc khong bao gio nhan trung o.
async function ensureFarm(playerId) {
  return cache.cached(farmKey(playerId), 3600, async () => {
    const have = await getFarm(playerId);
    if (have) return have;
    const taken = (await pool.query("SELECT count(*)::int AS n FROM player_farms")).rows[0].n;
    let i = 0;
    for (const [gx, gz] of rules.spiral()) {
      // Bo qua nhanh phan da chac chan bi chiem (co the con lo hong khi xoa tai khoan - van duoc thu lai).
      if (i++ < Math.max(0, taken - 64)) continue;
      const { x, z } = rules.slotToWorld(gx, gz);
      const r = await pool.query(
        `INSERT INTO player_farms (player_id, gx, gz, x, z) VALUES ($1, $2, $3, $4, $5)
         ON CONFLICT DO NOTHING RETURNING gx, gz, x, z`, [playerId, gx, gz, x, z]);
      if (r.rows.length) return r.rows[0];
      const mine = await getFarm(playerId); // da duoc cap o request song song
      if (mine) return mine;
    }
    return null;
  });
}

async function farmsNear(x, z, radius) {
  const r = Math.min(radius, 400000);
  const rows = await pool.query(
    `SELECT f.player_id, f.x, f.z, COALESCE(c.display_name, '') AS name
     FROM player_farms f LEFT JOIN characters c ON c.player_id = f.player_id
     WHERE f.x BETWEEN $1::real - $3::real AND $1::real + $3::real AND f.z BETWEEN $2::real - $3::real AND $2::real + $3::real
     LIMIT 50`, [x, z, r]);
  return rows.rows.map((f) => ({ playerId: f.player_id, name: f.name, x: f.x, z: f.z }));
}

// Ruong cua 1 nong trai cho MOI nguoi xem (nong trai hien cong khai o toa do rieng). Cache ngan.
async function publicView(hostId) {
  return cache.cached(`farmview:${hostId}`, 60, async () => {
    const save = await saveRepo.get(hostId);
    const host = await characters.get(hostId);
    const farm = (save?.data?.Farm || []).map((t) => ({
      x: t.X, y: t.Y, freeform: !!t.Freeform, posX: t.PosX, posZ: t.PosZ, cropId: t.CropId,
      growStage: t.GrowStage, growDays: t.GrowDays, watered: !!t.Watered, isGiant: !!t.IsGiant, pest: !!t.PestAfflicted,
    }));
    const loc = await getFarm(hostId);
    return { hostId, hostName: host?.displayName, x: loc?.x ?? 0, z: loc?.z ?? 0, farm, houseLevel: save?.data?.HouseLevel ?? 0 };
  });
}

module.exports = { ensureFarm, getFarm, farmsNear, publicView };
