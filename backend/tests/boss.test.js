// Trum the gioi (mau chung, chia thuong theo dong gop) + trum ham nguc to doi + luat truy na.
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://khong-dung@localhost:1/x";
delete process.env.REDIS_URL;

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const WebSocket = require("ws");

const characterService = require("../src/services/characterService");
const socialService = require("../src/services/socialService");
const pvpService = require("../src/services/pvpService");
const worldService = require("../src/services/worldEventService");
characterService.get = async (playerId) => ({ playerId, displayName: `P${playerId}`, gender: "male", modelId: "Farmer", skinColor: "#fff", hairColor: "#000", outfitColor: "#00f" });
socialService.memberOf = async () => null;
pvpService.structuresNear = async () => [];
pvpService.bagsNear = async () => [];
pvpService.respawnState = async () => null;
const granted = [];
worldService.grantLoot = async (id, items, gold, counter) => { granted.push({ id, items, gold, counter }); return { items, gold }; };

const boss = require("../src/game/bossRules");
const { catalog } = require("../src/game/catalog");
const { attachGateway } = require("../src/realtime/gateway");
const { signToken } = require("../src/utils/jwt");

let server, gateway, wsUrl;
test.before(async () => {
  server = http.createServer();
  gateway = attachGateway(server, { tickMs: 40, capacity: 50, world: { tickMs: 999999 } });
  await new Promise((r) => server.listen(0, r));
  wsUrl = `ws://127.0.0.1:${server.address().port}/ws`;
});
test.after(async () => { await gateway?.close(); await new Promise((r) => server.close(r)); });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
function connect(playerId) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    const json = [];
    ws.on("message", (raw, bin) => { if (!bin) json.push(JSON.parse(raw)); });
    ws.on("error", reject);
    ws.on("open", () => {
      ws.send(JSON.stringify({ t: "auth", token: signToken(playerId) }));
      resolve({ ws, json, send: (m) => ws.send(JSON.stringify(m)),
        async waitFor(pred, ms = 4000) { const end = Date.now() + ms; while (Date.now() < end) { const h = json.find(pred); if (h) return h; await sleep(10); } throw new Error("het gio"); } });
    });
  });
}
async function joinAt(c, x, z, y = 0) {
  await c.waitFor((m) => m.t === "welcome");
  c.send({ t: "join", zone: "wild" });
  await c.waitFor((m) => m.t === "joined");
  c.send({ t: "pos", x, y, z, r: 0, a: 0 });
}

test("luat: chia thuong theo dong gop, bo nguoi < 1%, top 3 co kim cuong; mau ham nguc theo so nguoi; truy na tang dan", () => {
  const def = catalog.enemies.get("boss_cave");
  const r = boss.worldBossRewards({ 1: 6000, 2: 3000, 3: 900, 4: 50 }, 10000, def, () => 0);
  assert.deepEqual(r.map((x) => x.playerId), [1, 2, 3], "nguoi gay 0.5% mau khong co thuong");
  assert.ok(r[0].gold > r[1].gold && r[1].gold > r[2].gold);
  assert.equal(r[0].items.kim_cuong >= 3, true);
  assert.ok(boss.dungeonHp(4) > boss.dungeonHp(1) && boss.dungeonGold(4) > boss.dungeonGold(1));
  assert.equal(boss.clampHit(99999), boss.MAX_HIT);
  assert.equal(boss.clampHit(-5), 0);
  assert.equal(boss.clampHit("abc"), 0);
  assert.equal(boss.bountyForKill(0), 100);
  assert.ok(boss.bountyForKill(3) > boss.bountyForKill(1));
  assert.equal(boss.bountyForKill(50), boss.bountyForKill(10), "co tran");
});

test("trum the gioi: mau chung, phai dung gan, ha xong chia thuong cho ca 2 nguoi + bao ket thuc", async () => {
  const a = await connect(9701), b = await connect(9702), far = await connect(9703);
  await joinAt(a, 5000, 5000);
  await joinAt(b, 5100, 5000);
  await joinAt(far, 0, 0);
  await sleep(100);
  const w = gateway.world();
  await w._force({ boss: { id: "b1", kind: "boss_cave", x: 5000, z: 5000, maxHp: 600, wakeAt: Date.now() - 1, until: Date.now() + 600000 }, nextBossAt: Date.now() + 1e9, nextDropAt: Date.now() + 1e9, nextCaravanAt: Date.now() + 1e9 });
  const st = await far.waitFor((m) => m.t === "world" && m.state.boss);
  assert.equal(st.state.boss.hp, 600);
  far.send({ t: "boss_hit", id: "b1", dmg: 200 });
  a.send({ t: "boss_hit", id: "b1", dmg: 99999 }); // bi kep con MAX_HIT
  await far.waitFor((m) => m.t === "boss_hp" && m.hp === 600 - boss.MAX_HIT);
  await sleep(boss.HIT_COOLDOWN_MS + 30);
  b.send({ t: "boss_hit", id: "b1", dmg: 200 });
  await sleep(boss.HIT_COOLDOWN_MS + 30);
  a.send({ t: "boss_hit", id: "b1", dmg: 200 });
  const end = await far.waitFor((m) => m.t === "boss_end" && m.id === "b1");
  assert.equal(end.killed, true);
  assert.equal(end.top[0].name, "P9701");
  const ra = await a.waitFor((m) => m.t === "event" && m.e.type === "boss_reward");
  assert.equal(ra.e.rank, 1);
  await b.waitFor((m) => m.t === "event" && m.e.type === "boss_reward");
  assert.equal(granted.filter((g) => g.counter === "act:boss").length, 2, "nguoi o xa khong duoc tinh");
  assert.ok(!granted.some((g) => g.id === 9703));
  for (const c of [a, b, far]) c.ws.close();
});

test("trum ham nguc: chi vao duoc trong ham, ha xong nhan thuong 1 lan/ngay", async () => {
  const a = await connect(9711);
  await joinAt(a, 0, 0);
  await sleep(60);
  a.send({ t: "dboss_join" });
  await a.waitFor((m) => m.t === "error" && /Ham Nguc/.test(m.msg));
  a.send({ t: "pos", x: boss.DUNGEON_X, y: 4100, z: boss.DUNGEON_Z - 150, r: 0, a: 0 });
  await sleep(60);
  a.send({ t: "dboss_join" });
  const j = await a.waitFor((m) => m.t === "dboss" && m.state === "fight");
  assert.equal(j.maxHp, boss.dungeonHp(1));
  const hits = Math.ceil(j.maxHp / boss.MAX_HIT);
  for (let i = 0; i < hits; i++) { a.send({ t: "dboss_hit", id: j.id, dmg: 999 }); await sleep(boss.HIT_COOLDOWN_MS + 15); }
  const end = await a.waitFor((m) => m.t === "event" && m.e.type === "dboss_end");
  assert.equal(end.e.rewarded, true);
  assert.equal(end.e.gold, boss.dungeonGold(1));
  assert.ok(granted.some((g) => g.id === 9711 && g.counter === "act:dungeon"));
  a.json.length = 0;
  a.send({ t: "dboss_join" });
  await a.waitFor((m) => m.t === "dboss" && m.state === "done");
  a.ws.close();
});
