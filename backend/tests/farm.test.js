// Nong trai rieng tai toa do co dinh: cap 1 lan, khong trung, cach nhau 5 km; nguoi khac xem duoc.
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://hsvv:hsvv_password@localhost:5432/hsvv";
delete process.env.REDIS_URL;

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const rules = require("../src/game/farmRules");

test("luat: vong xoan khong lap, bo o trung tam, o ke nhau cach dung 5 km", () => {
  const seen = new Set();
  const it = rules.spiral();
  for (let i = 0; i < 200; i++) {
    const [gx, gz] = it.next().value;
    assert.ok(!(gx === 0 && gz === 0));
    const k = `${gx},${gz}`;
    assert.ok(!seen.has(k), `lap o ${k}`);
    seen.add(k);
  }
  assert.equal(rules.FARM_SPACING, 100000);
  const a = rules.slotToWorld(1, 0), b = rules.slotToWorld(2, 0);
  assert.equal(Math.hypot(a.x - b.x, a.z - b.z) / rules.UNITS_PER_METER, 5000);
});

test("DB: moi nguoi 1 toa do rieng, goi lai khong doi, cap song song khong trung, xem duoc nong trai nguoi khac", async (t) => {
  const { createApp } = require("../src/app");
  const { runMigrations } = require("../src/db/migrate");
  const { pool } = require("../src/db/pool");
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
    await api(reg.body.token, "PUT", "/api/character", { displayName: `NV${uid()}`.slice(0, 18), gender: "male", modelId: "Farmer", skinColor: "#f0c8a0", hairColor: "#402010", outfitColor: "#3060c0" });
    const me = await api(reg.body.token, "GET", "/api/character");
    return { token: reg.body.token, id: me.body.playerId };
  }
  try {
    const players = await Promise.all([player(), player(), player(), player()]);
    const farms = await Promise.all(players.map((p) => api(p.token, "GET", "/api/farm")));
    const spots = farms.map((f) => `${f.body.gx},${f.body.gz}`);
    assert.equal(new Set(spots).size, 4, "4 nguoi 4 o khac nhau (cap song song)");
    for (const f of farms) assert.ok(!(f.body.gx === 0 && f.body.gz === 0));
    const again = await api(players[0].token, "GET", "/api/farm");
    assert.deepEqual(again.body, farms[0].body, "goi lai khong doi toa do");
    for (let i = 0; i < 4; i++)
      for (let j = i + 1; j < 4; j++)
        assert.ok(Math.hypot(farms[i].body.x - farms[j].body.x, farms[i].body.z - farms[j].body.z) >= rules.FARM_SPACING - 1, "cach nhau >= 5 km");
    const near = await api(players[1].token, "GET", `/api/farms/near?x=${farms[0].body.x}&z=${farms[0].body.z}&r=1000`);
    assert.ok(near.body.some((f) => f.playerId === players[0].id));
    await api(players[0].token, "PUT", "/api/save", { data: { Farm: [{ X: 3, Y: 4, CropId: "carrot", GrowStage: 2, GrowDays: 3 }] } });
    const view = await api(players[2].token, "GET", `/api/farms/${players[0].id}/view`);
    assert.equal(view.status, 200);
    assert.equal(view.body.farm[0].cropId, "carrot");
    assert.equal(view.body.x, farms[0].body.x);
  } finally {
    await new Promise((r) => server.close(r));
    await pool.end();
  }
});
