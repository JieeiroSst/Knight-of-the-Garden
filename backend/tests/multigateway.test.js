// Kiem tra MO RONG NGANG that su: 2 gateway doc lap (2 server HTTP rieng) noi qua Redis - nguoi choi
// o 2 gateway khac nhau van thay nhau, nhan vi tri/chat va thong bao ca nhan. Chi chay khi co
// TEST_REDIS_URL (vd redis://localhost:56379).
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://hsvv:hsvv_password@localhost:5432/hsvv";

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const WebSocket = require("ws");
const { createApp } = require("../src/app");
const { runMigrations } = require("../src/db/migrate");
const { pool } = require("../src/db/pool");
const { attachGateway } = require("../src/realtime/gateway");
const { redisBus } = require("../src/realtime/bus");

const REDIS = process.env.TEST_REDIS_URL;

test("2 gateway qua Redis: thay nhau, vi tri, chat, nhan rieng", { skip: !REDIS && "can TEST_REDIS_URL" }, async () => {
  await runMigrations();
  const nodes = [];
  for (let i = 0; i < 2; i++) {
    const bus = redisBus(REDIS);
    const server = http.createServer(createApp());
    const gw = attachGateway(server, { bus, tickMs: 50, capacity: 80 });
    await new Promise((r) => server.listen(0, r));
    nodes.push({ bus, server, gw, port: server.address().port });
  }
  const base = `http://127.0.0.1:${nodes[0].port}`;
  const uid = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
  async function player() {
    const reg = await (await fetch(`${base}/api/register`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: `m_${uid()}`, password: "matkhau123" }) })).json();
    const name = `G${uid()}`.slice(0, 18);
    await fetch(`${base}/api/character`, { method: "PUT", headers: { "Content-Type": "application/json", Authorization: `Bearer ${reg.token}` },
      body: JSON.stringify({ displayName: name, gender: "male", modelId: "Farmer", skinColor: "#ffffff", hairColor: "#000000", outfitColor: "#00ff00" }) });
    return { token: reg.token, name };
  }
  function connect(port, token) {
    return new Promise((resolve) => {
      const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
      const inbox = [];
      ws.on("message", (raw) => inbox.push(JSON.parse(raw)));
      ws.waitFor = async (pred, ms = 4000) => {
        const end = Date.now() + ms;
        while (Date.now() < end) {
          const i = inbox.findIndex(pred);
          if (i >= 0) return inbox.splice(i, 1)[0];
          await new Promise((r) => setTimeout(r, 20));
        }
        throw new Error("het thoi gian cho tin WS");
      };
      ws.on("open", async () => { ws.send(JSON.stringify({ t: "auth", token })); await ws.waitFor((m) => m.t === "welcome"); resolve(ws); });
    });
  }

  const zone = `mg${uid()}`.slice(0, 20);
  const a = await player();
  const b = await player();
  const wa = await connect(nodes[0].port, a.token);
  const wb = await connect(nodes[1].port, b.token);
  wa.send(JSON.stringify({ t: "join", zone }));
  await wa.waitFor((m) => m.t === "joined");
  wb.send(JSON.stringify({ t: "join", zone }));
  const jb = await wb.waitFor((m) => m.t === "joined");
  assert.equal(jb.players[0].name, a.name, "nguoi vao sau (gateway 2) thay nguoi o gateway 1");
  assert.equal((await wa.waitFor((m) => m.t === "spawn")).p.name, b.name);

  wb.send(JSON.stringify({ t: "pos", x: 1, y: 2, z: 3, r: 0, a: 2 }));
  assert.equal((await wa.waitFor((m) => m.t === "moves")).m[0][1], 1);
  wa.send(JSON.stringify({ t: "chat", text: "chao gateway ben kia" }));
  assert.equal((await wb.waitFor((m) => m.t === "chat")).text, "chao gateway ben kia");
  wb.send(JSON.stringify({ t: "whisper", to: a.name, text: "rieng" }));
  assert.equal((await wa.waitFor((m) => m.t === "chat" && m.ch === "whisper")).text, "rieng");

  // Dau tay doi giua 2 gateway: mau/trang thai tran nam o Redis, vi tri doi thu lay tu tin "moves".
  wa.send(JSON.stringify({ t: "pos", x: 0, y: 0, z: 0, r: 0, a: 0 }));
  wb.send(JSON.stringify({ t: "pos", x: 20, y: 0, z: 0, r: 0, a: 0 }));
  await new Promise((r) => setTimeout(r, 300));
  wa.send(JSON.stringify({ t: "duel_invite", to: b.name }));
  const inv = await wb.waitFor((m) => m.t === "event" && m.e.type === "duel_invite");
  wb.send(JSON.stringify({ t: "duel_accept", duelId: inv.e.duelId }));
  const start = await wa.waitFor((m) => m.t === "event" && m.e.type === "duel_start");
  wb.send(JSON.stringify({ t: "duel_hit", duelId: start.e.duelId }));
  const hit = await wa.waitFor((m) => m.t === "event" && m.e.type === "duel_hp");
  assert.ok(hit.e.hp[Object.keys(hit.e.hp).find((k) => Number(k) !== hit.e.by)] < 400, "B (gateway 2) danh trung A (gateway 1)");
  wa.send(JSON.stringify({ t: "duel_forfeit" }));
  const end = await wb.waitFor((m) => m.t === "event" && m.e.type === "duel_end");
  assert.notEqual(end.e.winnerId, 0);

  wa.close();
  wb.close();
  for (const n of nodes) {
    await n.gw.close();
    await new Promise((r) => n.server.close(r));
    await n.bus.close();
  }
  await pool.end();
});
