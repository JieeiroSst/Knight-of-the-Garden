// Truy na (DB that): ha nguoi vo toi -> bi treo thuong tang dan; ha ke bi truy na -> lanh tien; gop treo thuong.
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://khong-dung@localhost:1/x";
delete process.env.REDIS_URL;

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const boss = require("../src/game/bossRules");

test("DB: truy na tang dan, lanh thuong, treo thuong bang vang cua minh", async (t) => {
  const { createApp } = require("../src/app");
  const { runMigrations } = require("../src/db/migrate");
  const { pool } = require("../src/db/pool");
  const bounty = require("../src/services/bountyService");
  try { await runMigrations(); } catch { t.skip("can Postgres"); return; }
  const server = http.createServer(createApp());
  await new Promise((r) => server.listen(0, r));
  const base = `http://127.0.0.1:${server.address().port}`;
  const api = async (token, method, path, body) => {
    const res = await fetch(base + path, { method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined });
    const text = await res.text();
    return { status: res.status, body: text ? JSON.parse(text) : null };
  };
  const uid = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
  async function player() {
    const reg = await api(null, "POST", "/api/register", { username: `u_${uid()}`, password: "matkhau123" });
    const name = `NV${uid()}`.slice(0, 18);
    await api(reg.body.token, "PUT", "/api/character", { displayName: name, gender: "male", modelId: "Farmer", skinColor: "#f0c8a0", hairColor: "#402010", outfitColor: "#3060c0" });
    const me = await api(reg.body.token, "GET", "/api/character");
    return { token: reg.body.token, id: me.body.playerId, name };
  }
  const gold = async (p) => Number((await api(p.token, "GET", "/api/economy")).body.gold);
  try {
    const [robber, victim, hunter] = [await player(), await player(), await player()];
    const r1 = await bounty.onKill(robber.id, victim.id);
    assert.equal(r1.added, boss.bountyForKill(0));
    const r2 = await bounty.onKill(robber.id, victim.id);
    assert.equal(r2.killerBounty, boss.bountyForKill(0) + boss.bountyForKill(1), "tang dan");
    const list = await api(hunter.token, "GET", "/api/bounties");
    assert.ok(list.body.top.some((b) => b.playerId === robber.id && b.amount === r2.killerBounty));

    // Gop treo thuong theo ten.
    await gold(hunter); // tao vi
    await pool.query("UPDATE wallets SET gold = 1000 WHERE player_id = $1", [hunter.id]);
    const g0 = await gold(hunter);
    const placed = await api(hunter.token, "POST", "/api/bounties", { targetName: robber.name, amount: 150 });
    assert.equal(placed.status, 200, JSON.stringify(placed.body));
    assert.equal(await gold(hunter), g0 - 150);
    assert.equal((await api(hunter.token, "POST", "/api/bounties", { targetId: hunter.id, amount: 150 })).status, 400, "khong tu treo minh");

    // Ha ke bi truy na -> lanh toan bo, ke do het bi truy na; nguoi ha KHONG bi treo thuong.
    const before = await gold(victim);
    const r3 = await bounty.onKill(victim.id, robber.id);
    assert.equal(r3.claimed, r2.killerBounty + 150);
    assert.equal(await gold(victim), before + r3.claimed);
    assert.deepEqual(await bounty.of(robber.id), { amount: 0, kills: 0 });
    assert.equal((await bounty.of(victim.id)).amount, 0);
    const daily = await api(victim.token, "GET", "/api/daily");
    assert.equal(daily.status, 200);
  } finally {
    await new Promise((r) => server.close(r));
    await pool.end();
  }
});
