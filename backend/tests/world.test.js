// Su kien the gioi qua gateway THAT (thay transaction kinh te bang ban gia) + luat thuan.
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
worldService.grantLoot = async (id, items, gold) => { granted.push({ id, items, gold }); return { items, gold }; };
const bought = [];
worldService.buyFromCaravan = async (id, item, n, price) => { bought.push({ id, item, n, price }); return { itemId: item, n, cost: n * price }; };

const rules = require("../src/game/worldEventRules");
const pvpRules = require("../src/game/pvpRules");
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
        async waitFor(pred, ms = 2500) { const end = Date.now() + ms; while (Date.now() < end) { const h = json.find(pred); if (h) return h; await sleep(10); } throw new Error("het gio"); } });
    });
  });
}
async function joinAt(c, x, z) {
  await c.waitFor((m) => m.t === "welcome");
  c.send({ t: "join", zone: "wild" });
  await c.waitFor((m) => m.t === "joined");
  c.send({ t: "pos", x, y: 0, z, r: 0, a: 0 });
}

test("luat: loot xin hon khi xa; diem roi tranh cho; Dem Trang Mau chi ban dem va tat dinh; tram che tao theo cap", () => {
  const near = rules.rollDropLoot(1000, 0, () => 0.1), far = rules.rollDropLoot(20000, 0, () => 0.1);
  assert.ok(far.gold > near.gold);
  const markets = [{ X: 3500, Z: 0, SafeRadius: 350 }];
  for (let i = 0; i < 50; i++) { const p = rules.pickSpot(Math.random, markets); assert.ok(Math.hypot(p.x - 3500, p.z) > 750); }
  const epoch = 0, H = 3600000;
  assert.equal(rules.bloodMoon(12 * H, epoch), false, "ban ngay khong co");
  const nights = [...Array(50).keys()].filter((d) => rules.bloodMoon(d * 24 * H + 21 * H, epoch)).length;
  assert.ok(nights >= 4 && nights <= 20, `~1/5 so dem (${nights}/50)`);
  assert.equal(rules.bloodMoon(3 * 24 * H + 21 * H, epoch), rules.bloodMoon(4 * 24 * H + 2 * H, epoch), "nua dem sau cung 1 dem");
  assert.ok(pvpRules.stationOk("workbench", []) && pvpRules.stationOk("forge", []));
  assert.equal(pvpRules.stationOk("carpentry", []), false);
  assert.ok(pvpRules.stationOk("carpentry", ["forge"]), "tram cap cao dung duoc cong thuc cap thap");
  assert.equal(pvpRules.stationOk("workshop", ["carpentry", "forge"]), false);
  assert.ok(pvpRules.stationOk("workshop", ["workshop"]));
});

test("ruong tiep te: phat cho moi nguoi, chi nguoi toi truoc mo duoc, phai dung gan", async () => {
  const a = await connect(9601), b = await connect(9602), far = await connect(9603);
  await joinAt(a, 5000, 5000);
  await joinAt(b, 5040, 5000);
  await joinAt(far, 0, 0);
  await sleep(100);
  const w = gateway.world();
  await w._force({ drops: [{ id: "d1", x: 5000, z: 5000, landsAt: Date.now() - 1000, expiresAt: Date.now() + 600000, looted: false, loot: { items: { mui_ten: 30 }, gold: 200 } }], nextDropAt: Date.now() + 1e9, nextCaravanAt: Date.now() + 1e9 });
  const st = await far.waitFor((m) => m.t === "world" && m.state.drops.length === 1);
  assert.equal(st.state.drops[0].loot, undefined, "khong lo noi dung ruong cho client");
  far.send({ t: "drop_open", id: "d1" });
  await far.waitFor((m) => m.t === "error" && /gan/.test(m.msg));
  a.send({ t: "drop_open", id: "d1" });
  const opened = await a.waitFor((m) => m.t === "event" && m.e.type === "drop_opened");
  assert.deepEqual(opened.e.items, { mui_ten: 30 });
  await b.waitFor((m) => m.t === "drop_looted" && m.id === "d1" && m.byName === "P9601");
  b.send({ t: "drop_open", id: "d1" });
  await b.waitFor((m) => m.t === "error" && /nguoi khac/.test(m.msg));
  assert.equal(granted.length, 1);
  for (const c of [a, b, far]) c.ws.close();
});

test("doan thuong nhan: mua tru dung ton kho, het hang thi tu choi, phai dung gan", async () => {
  const a = await connect(9611);
  await joinAt(a, 7000, 0);
  await sleep(100);
  const w = gateway.world();
  await w._force({ caravan: null, nextCaravanAt: 0, nextDropAt: Date.now() + 1e9 });
  const st = await a.waitFor((m) => m.t === "world" && m.state.caravan);
  const cv = st.state.caravan;
  a.send({ t: "pos", x: cv.x, y: 0, z: cv.z, r: 0, a: 0 });
  await sleep(80);
  const offer = cv.stock[0];
  a.send({ t: "caravan_buy", item: offer.item, n: offer.left });
  await a.waitFor((m) => m.t === "event" && m.e.type === "caravan_bought");
  await a.waitFor((m) => m.t === "caravan_stock" && m.item === offer.item && m.left === 0);
  a.send({ t: "caravan_buy", item: offer.item, n: 1 });
  await a.waitFor((m) => m.t === "error" && /het/.test(m.msg));
  assert.equal(bought.length, 1);
  assert.equal(bought[0].price, offer.price);
  a.ws.close();
});
