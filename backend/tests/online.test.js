// Integration test THAT cho tinh nang online (Postgres that + WebSocket that, bus trong bo nho):
// nhan vat, kinh te server, giao dich 2 nguoi, cho, gateway (vao kenh/thay nhau/vi tri/chat/nhan
// rieng/thong bao giao dich).
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://hsvv:hsvv_password@localhost:5432/hsvv";
delete process.env.REDIS_URL;

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const WebSocket = require("ws");
const { createApp } = require("../src/app");
const { runMigrations } = require("../src/db/migrate");
const { pool } = require("../src/db/pool");
const { attachGateway } = require("../src/realtime/gateway");
const { catalog } = require("../src/game/catalog");

let server, gateway, baseUrl, wsUrl;

test.before(async () => {
  await runMigrations();
  server = http.createServer(createApp());
  gateway = attachGateway(server, { tickMs: 50, capacity: 2 });
  await new Promise((r) => server.listen(0, r));
  baseUrl = `http://127.0.0.1:${server.address().port}`;
  wsUrl = `ws://127.0.0.1:${server.address().port}/ws`;
});

test.after(async () => {
  await gateway?.close();
  await new Promise((r) => server.close(r));
  await pool.end();
});

const uid = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;

async function api(token, method, path, body) {
  const res = await fetch(`${baseUrl}${path}`, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  return { status: res.status, body: text ? JSON.parse(text) : null };
}

async function newPlayer(gender = "male") {
  const username = `u_${uid()}`;
  const reg = await api(null, "POST", "/api/register", { username, password: "matkhau123" });
  const token = reg.body.token;
  const name = `NV${uid()}`.slice(0, 18);
  const c = await api(token, "PUT", "/api/character", {
    displayName: name, gender, modelId: gender === "male" ? "Farmer" : "HuntressF",
    skinColor: "#f0c8a0", hairColor: "#402010", outfitColor: "#3060c0",
  });
  assert.equal(c.status, 200, JSON.stringify(c.body));
  const me = await api(token, "GET", "/api/character");
  return { token, name, id: me.body.playerId };
}

// Dua vang/vat pham vao vi bang SQL truc tiep (thay vi cay/danh quai trong test).
async function grant(id, gold, items = {}) {
  await api(null, "GET", "/api/time");
  await pool.query("UPDATE wallets SET gold = gold + $2 WHERE player_id = $1", [id, gold]);
  for (const [item, n] of Object.entries(items)) {
    await pool.query(
      `INSERT INTO inventory_items (player_id, item_id, count) VALUES ($1,$2,$3)
       ON CONFLICT (player_id, item_id) DO UPDATE SET count = inventory_items.count + EXCLUDED.count`, [id, item, n]);
  }
}

function connect(token) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    const inbox = [];
    const waiters = [];
    ws.on("message", (raw) => {
      const msg = JSON.parse(raw);
      const i = waiters.findIndex((w) => w.pred(msg));
      if (i >= 0) waiters.splice(i, 1)[0].resolve(msg);
      else inbox.push(msg);
    });
    ws.waitFor = (pred, ms = 3000) => {
      const i = inbox.findIndex(pred);
      if (i >= 0) return Promise.resolve(inbox.splice(i, 1)[0]);
      return new Promise((res, rej) => {
        const w = { pred, resolve: res };
        waiters.push(w);
        setTimeout(() => { const k = waiters.indexOf(w); if (k >= 0) { waiters.splice(k, 1); rej(new Error("het thoi gian cho tin WS")); } }, ms);
      });
    };
    ws.sendJson = (m) => ws.send(JSON.stringify(m));
    ws.on("open", () => { ws.sendJson({ t: "auth", token }); ws.waitFor((m) => m.t === "welcome").then(() => resolve(ws), reject); });
    ws.on("error", reject);
  });
}

test("thoi gian game tu server", async () => {
  const r = await api(null, "GET", "/api/time");
  assert.equal(r.status, 200);
  assert.ok(r.body.day >= 1 && ["Spring", "Summer", "Fall", "Winter"].includes(r.body.season));
});

test("nhan vat: ten trung bi chan, mau sai gioi tinh bi chan", async () => {
  const a = await newPlayer();
  const b = await newPlayer("female");
  const dup = await api(b.token, "PUT", "/api/character", {
    displayName: a.name.toUpperCase(), gender: "female", modelId: "HuntressF", skinColor: "#ffffff", hairColor: "#000000", outfitColor: "#ff0000",
  });
  assert.equal(dup.status, 409);
  const wrong = await api(b.token, "PUT", "/api/character", {
    displayName: b.name, gender: "female", modelId: "Farmer", skinColor: "#ffffff", hairColor: "#000000", outfitColor: "#ff0000",
  });
  assert.equal(wrong.status, 400);
});

test("kinh te: bo khoi dau, op hop le/khong hop le, lo gui lai khong ap dung 2 lan", async () => {
  const p = await newPlayer();
  const snap = await api(p.token, "GET", "/api/economy");
  assert.equal(snap.body.gold, catalog.starter.Gold);
  assert.equal(snap.body.items.hoe, 1);

  const batch = { seq: 1, ops: [
    { t: "gain", item: "pumpkin", n: 3, src: "harvest" },
    { t: "gain", item: "sword", n: 5, src: "harvest" },
    { t: "sell", item: "pumpkin", n: 1, mult: 1 },
  ] };
  const r1 = await api(p.token, "POST", "/api/economy/ops", batch);
  assert.equal(r1.status, 200);
  assert.deepEqual(r1.body.rejected.map((x) => x.index), [1]);
  assert.equal(r1.body.items.pumpkin, 2);
  const goldAfter = r1.body.gold;
  assert.equal(goldAfter, catalog.starter.Gold + catalog.items.get("pumpkin").SellPrice);

  const r2 = await api(p.token, "POST", "/api/economy/ops", batch); // gui lai (mat mang/retry)
  assert.equal(r2.body.items.pumpkin, 2);
  assert.equal(r2.body.gold, goldAfter);

  const bad = await api(p.token, "POST", "/api/economy/ops", { seq: 2, ops: [{ t: "hack", n: 1 }] });
  assert.equal(bad.status, 400);
});

test("giao dich 2 nguoi: moi, dat do, khoa, xac nhan -> chuyen nguyen tu + thong bao WS", async () => {
  const a = await newPlayer();
  const b = await newPlayer("female");
  await api(a.token, "GET", "/api/economy");
  await api(b.token, "GET", "/api/economy");
  await grant(a.id, 500, { egg: 10 });
  await grant(b.id, 0, { wool: 4 });
  const wsB = await connect(b.token);

  const inv = await api(a.token, "POST", "/api/trades", { toName: b.name });
  assert.equal(inv.status, 201);
  const tradeId = inv.body.id;
  const note = await wsB.waitFor((m) => m.t === "event" && m.e.type === "trade_invite");
  assert.equal(note.e.tradeId, tradeId);

  assert.equal((await api(b.token, "POST", `/api/trades/${tradeId}/accept`)).status, 200);
  await api(a.token, "PUT", `/api/trades/${tradeId}/offer`, { gold: 100, items: { egg: 4 } });
  await api(b.token, "PUT", `/api/trades/${tradeId}/offer`, { gold: 0, items: { wool: 4 } });
  // Xac nhan truoc khi khoa -> tu choi.
  assert.equal((await api(a.token, "POST", `/api/trades/${tradeId}/confirm`)).status, 400);
  await api(a.token, "POST", `/api/trades/${tradeId}/lock`);
  await api(b.token, "POST", `/api/trades/${tradeId}/lock`);
  await api(a.token, "POST", `/api/trades/${tradeId}/confirm`);
  const done = await api(b.token, "POST", `/api/trades/${tradeId}/confirm`);
  assert.equal(done.body.status, "done");
  await wsB.waitFor((m) => m.t === "event" && m.e.type === "trade_done");

  const sa = (await api(a.token, "GET", "/api/economy")).body;
  const sb = (await api(b.token, "GET", "/api/economy")).body;
  assert.equal(sa.items.egg, 6);
  assert.equal(sa.items.wool, 4);
  assert.equal(sb.items.egg, 4);
  assert.equal(sb.items.wool, undefined);
  assert.equal(sb.gold, catalog.starter.Gold + 100);
  wsB.close();
});

test("giao dich: do khong du luc xac nhan -> rollback toan bo", async () => {
  const a = await newPlayer();
  const b = await newPlayer();
  await api(a.token, "GET", "/api/economy");
  await api(b.token, "GET", "/api/economy");
  await grant(a.id, 0, { milk: 2 });
  const t = (await api(a.token, "POST", "/api/trades", { toName: b.name })).body.id;
  await api(b.token, "POST", `/api/trades/${t}/accept`);
  await api(a.token, "PUT", `/api/trades/${t}/offer`, { gold: 0, items: { milk: 2 } });
  await api(b.token, "PUT", `/api/trades/${t}/offer`, { gold: 10, items: {} });
  await api(a.token, "POST", `/api/trades/${t}/lock`);
  await api(b.token, "POST", `/api/trades/${t}/lock`);
  // A tieu mat sua truoc khi xac nhan.
  await api(a.token, "POST", "/api/economy/ops", { seq: 1, ops: [{ t: "consume", item: "milk", n: 2 }] });
  await api(a.token, "POST", `/api/trades/${t}/confirm`);
  const r = await api(b.token, "POST", `/api/trades/${t}/confirm`);
  assert.equal(r.status, 400);
  const sb = (await api(b.token, "GET", "/api/economy")).body;
  assert.equal(sb.gold, catalog.starter.Gold);
});

test("cho: dang ban giu do, mua chuyen vang tru thue, khong mua 2 lan, huy hoan do", async () => {
  const seller = await newPlayer();
  const buyer = await newPlayer("female");
  await api(seller.token, "GET", "/api/economy");
  await api(buyer.token, "GET", "/api/economy");
  await grant(seller.id, 0, { kim_cuong: 3 });
  await grant(buyer.id, 1000);
  const wsS = await connect(seller.token);

  const l = await api(seller.token, "POST", "/api/market/listings", { itemId: "kim_cuong", count: 2, price: 600 });
  assert.equal(l.status, 201);
  assert.equal((await api(seller.token, "GET", "/api/economy")).body.items.kim_cuong, 1);
  const tooMany = await api(seller.token, "POST", "/api/market/listings", { itemId: "kim_cuong", count: 5, price: 1 });
  assert.equal(tooMany.status, 400);

  const found = await api(buyer.token, "GET", "/api/market/listings?itemId=kim_cuong");
  assert.ok(found.body.some((x) => x.id === l.body.id && x.sellerName === seller.name));

  assert.equal((await api(seller.token, "POST", `/api/market/listings/${l.body.id}/buy`)).status, 400);
  assert.equal((await api(buyer.token, "POST", `/api/market/listings/${l.body.id}/buy`)).status, 200);
  assert.equal((await api(buyer.token, "POST", `/api/market/listings/${l.body.id}/buy`)).status, 404);
  const sold = await wsS.waitFor((m) => m.t === "event" && m.e.type === "market_sold");
  assert.equal(sold.e.gold, 570);

  const sb = (await api(buyer.token, "GET", "/api/economy")).body;
  assert.equal(sb.items.kim_cuong, 2);
  assert.equal(sb.gold, catalog.starter.Gold + 1000 - 600);
  const ss = (await api(seller.token, "GET", "/api/economy")).body;
  assert.equal(ss.gold, catalog.starter.Gold + 570);

  const l2 = await api(seller.token, "POST", "/api/market/listings", { itemId: "kim_cuong", count: 1, price: 100 });
  assert.equal((await api(seller.token, "DELETE", `/api/market/listings/${l2.body.id}`)).status, 200);
  assert.equal((await api(seller.token, "GET", "/api/economy")).body.items.kim_cuong, 1);
  wsS.close();
});

test("gateway: vao kenh thay nhau, vi tri, chat kenh, nhan rieng, chia kenh khi day", async () => {
  const zone = `z${uid()}`.slice(0, 20);
  const a = await newPlayer();
  const b = await newPlayer("female");
  const c = await newPlayer();
  const wa = await connect(a.token);
  const wb = await connect(b.token);
  const wc = await connect(c.token);

  wa.sendJson({ t: "join", zone });
  const ja = await wa.waitFor((m) => m.t === "joined");
  assert.equal(ja.channel, 1);
  assert.equal(ja.players.length, 0);

  wb.sendJson({ t: "join", zone });
  const jb = await wb.waitFor((m) => m.t === "joined");
  assert.equal(jb.channel, 1);
  assert.equal(jb.players[0].name, a.name);
  const spawn = await wa.waitFor((m) => m.t === "spawn");
  assert.equal(spawn.p.name, b.name);
  assert.equal(spawn.p.app.m, "HuntressF");

  // Kenh 1 da du (capacity=2 trong test) -> nguoi thu 3 vao kenh 2, khong thay A/B.
  wc.sendJson({ t: "join", zone });
  const jc = await wc.waitFor((m) => m.t === "joined");
  assert.equal(jc.channel, 2);

  wb.sendJson({ t: "pos", x: 10, y: 0, z: -5.56, r: 1.5, a: 1 });
  const mv = await wa.waitFor((m) => m.t === "moves");
  assert.deepEqual(mv.m[0], [b.id, 10, 0, -5.6, 1.5, 1]);

  wa.sendJson({ t: "chat", text: "xin chao vcl" });
  const chat = await wb.waitFor((m) => m.t === "chat" && m.ch === "channel");
  assert.equal(chat.from, a.name);
  assert.equal(chat.text, "xin chao ***");

  wc.sendJson({ t: "whisper", to: a.name, text: "alo" });
  const w = await wa.waitFor((m) => m.t === "chat" && m.ch === "whisper");
  assert.equal(w.from, c.name);

  wb.close();
  const gone = await wa.waitFor((m) => m.t === "despawn");
  assert.equal(gone.id, b.id);
  wa.close();
  wc.close();
});

test("gateway: chua tao nhan vat thi khong vao duoc", async () => {
  const reg = await api(null, "POST", "/api/register", { username: `u_${uid()}`, password: "matkhau123" });
  const ws = new WebSocket(wsUrl);
  await new Promise((r) => ws.on("open", r));
  ws.send(JSON.stringify({ t: "auth", token: reg.body.token }));
  const code = await new Promise((r) => ws.on("close", (c) => r(c)));
  assert.equal(code, 4002);
});
