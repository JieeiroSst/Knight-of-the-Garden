// Lenh truy na (xem bossRules + migration 008). Moi thay doi trong transaction khoa hang bounties.
const { pool } = require("../db/pool");
const econ = require("../repositories/economyRepository");
const { ensureWallet } = require("./economyService");
const rules = require("../game/bossRules");
const { AppError } = require("../utils/AppError");
const cache = require("../cache/cache");

const TOP_KEY = "bounty:top";

async function lockRow(client, playerId) {
  await client.query("INSERT INTO bounties (player_id) VALUES ($1) ON CONFLICT DO NOTHING", [playerId]);
  const r = await client.query("SELECT amount, kills FROM bounties WHERE player_id = $1 FOR UPDATE", [playerId]);
  return { amount: Number(r.rows[0].amount), kills: r.rows[0].kills };
}

// Ha nguoi choi trong the gioi mo (khong tinh dau tay doi):
//  - nan nhan dang bi truy na -> ke ha nhan het tien thuong, xoa lenh truy na cua nan nhan.
//  - nan nhan vo toi -> ke ha bi treo thuong them (tang dan theo so lan).
async function onKill(killerId, victimId) {
  if (!killerId || !victimId || killerId === victimId) return null;
  const r = await econ.withTransaction(async (client) => {
    // Khoa theo thu tu id tang dan -> khong deadlock khi 2 nguoi ha nhau cung luc.
    const [a, b] = killerId < victimId ? [killerId, victimId] : [victimId, killerId];
    const rows = { [a]: await lockRow(client, a), [b]: await lockRow(client, b) };
    const victim = rows[victimId], killer = rows[killerId];
    if (victim.amount > 0) {
      await ensureWallet(client, killerId);
      await econ.addGold(client, killerId, victim.amount);
      await econ.bumpCounter(client, killerId, "act:bounty", 1);
      await client.query("UPDATE bounties SET amount = 0, kills = 0, updated_at = now() WHERE player_id = $1", [victimId]);
      return { claimed: victim.amount, killerBounty: killer.amount };
    }
    const add = rules.bountyForKill(killer.kills);
    await client.query("UPDATE bounties SET amount = amount + $2, kills = kills + 1, updated_at = now() WHERE player_id = $1", [killerId, add]);
    return { claimed: 0, added: add, killerBounty: killer.amount + add };
  });
  await cache.invalidate(TOP_KEY);
  return r;
}

// Nguoi choi gop vang treo thuong len dau nguoi khac.
async function place(playerId, targetId, amount) {
  const n = Math.floor(Number(amount));
  if (!Number.isInteger(targetId) || targetId === playerId) throw new AppError(400, "Khong the treo thuong nguoi nay.");
  if (!(n >= rules.BOUNTY_PLACE_MIN && n <= rules.BOUNTY_PLACE_MAX)) throw new AppError(400, `Tien treo thuong tu ${rules.BOUNTY_PLACE_MIN} den ${rules.BOUNTY_PLACE_MAX} vang.`);
  const exists = await pool.query("SELECT 1 FROM players WHERE id = $1", [targetId]);
  if (!exists.rows.length) throw new AppError(404, "Nguoi choi khong ton tai.");
  const r = await econ.withTransaction(async (client) => {
    const w = await ensureWallet(client, playerId);
    if (Number(w.gold) < n) throw new AppError(400, "Khong du vang.");
    await econ.addGold(client, playerId, -n);
    const row = await lockRow(client, targetId);
    await client.query("UPDATE bounties SET amount = amount + $2, updated_at = now() WHERE player_id = $1", [targetId, n]);
    return { targetId, amount: row.amount + n, paid: n };
  });
  await cache.invalidate(TOP_KEY);
  return r;
}

async function top(limit = 30) {
  return cache.cached(TOP_KEY, 30, async () => {
    const r = await pool.query(
      `SELECT b.player_id, b.amount, b.kills, COALESCE(c.display_name, '') AS name
       FROM bounties b LEFT JOIN characters c ON c.player_id = b.player_id
       WHERE b.amount >= $1 ORDER BY b.amount DESC LIMIT $2`, [rules.BOUNTY_WANTED, Math.min(limit, 100)]);
    return r.rows.map((x) => ({ playerId: x.player_id, name: x.name, amount: Number(x.amount), kills: x.kills }));
  });
}

async function of(playerId) {
  const r = await pool.query("SELECT amount, kills FROM bounties WHERE player_id = $1", [playerId]);
  return r.rows[0] ? { amount: Number(r.rows[0].amount), kills: r.rows[0].kills } : { amount: 0, kills: 0 };
}

module.exports = { onKill, place, top, of };
