// Integration test (Postgres + WebSocket that): ban be + online, bang hoi + kho + chat bang, tham nong
// trai + tuoi ho, diem danh + nhiem vu (tien do tu bo dem kinh te), xep hang, cuong hoa, dau truong.
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

let server, gateway, base, wsUrl;

test.before(async () => {
  await runMigrations();
  server = http.createServer(createApp());
  gateway = attachGateway(server, { tickMs: 50, capacity: 80 });
  await new Promise((r) => server.listen(0, r));
  base = `http://127.0.0.1:${server.address().port}`;
  wsUrl = `ws://127.0.0.1:${server.address().port}/ws`;
});

test.after(async () => {
  await gateway?.close();
  await new Promise((r) => server.close(r));
  await pool.end();
});

const uid = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;

async function api(token, method, path, body) {
  const res = await fetch(`${base}${path}`, {
    method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  return { status: res.status, body: text ? JSON.parse(text) : null };
}

async function player() {
  const reg = await api(null, "POST", "/api/register", { username: `s_${uid()}`, password: "matkhau123" });
  const token = reg.body.token;
  const name = `P${uid()}`.slice(0, 18);
  await api(token, "PUT", "/api/character", { displayName: name, gender: "male", modelId: "Farmer", skinColor: "#f0c8a0", hairColor: "#402010", outfitColor: "#3060c0" });
  const id = (await api(token, "GET", "/api/character")).body.playerId;
  await api(token, "GET", "/api/economy");
  return { token, name, id };
}

async function grant(id, gold, items = {}) {
  await pool.query("UPDATE wallets SET gold = gold + $2 WHERE player_id = $1", [id, gold]);
  for (const [item, n] of Object.entries(items))
    await pool.query(`INSERT INTO inventory_items (player_id, item_id, count) VALUES ($1,$2,$3)
      ON CONFLICT (player_id, item_id) DO UPDATE SET count = inventory_items.count + EXCLUDED.count`, [id, item, n]);
}

function connect(token) {
  return new Promise((resolve) => {
    const ws = new WebSocket(wsUrl);
    const box = [];
    ws.on("message", (raw) => box.push(JSON.parse(raw)));
    ws.waitFor = async (pred, ms = 4000) => {
      const end = Date.now() + ms;
      while (Date.now() < end) {
        const i = box.findIndex(pred);
        if (i >= 0) return box.splice(i, 1)[0];
        await new Promise((r) => setTimeout(r, 15));
      }
      throw new Error("het thoi gian cho tin WS");
    };
    ws.sendJson = (m) => ws.send(JSON.stringify(m));
    ws.on("open", async () => { ws.sendJson({ t: "auth", token }); await ws.waitFor((m) => m.t === "welcome"); resolve(ws); });
  });
}

const makeFriends = async (a, b) => {
  await api(a.token, "POST", "/api/friends", { name: b.name });
  await api(b.token, "POST", `/api/friends/${a.id}/accept`);
};

test("ban be: moi -> thong bao -> chap nhan -> thay online", async () => {
  const a = await player();
  const b = await player();
  const wb = await connect(b.token);
  assert.equal((await api(a.token, "POST", "/api/friends", { name: b.name })).body.status, "pending");
  await wb.waitFor((m) => m.t === "event" && m.e.type === "friend_request");
  const listB = (await api(b.token, "GET", "/api/friends")).body;
  assert.equal(listB[0].incoming, true);
  assert.equal((await api(b.token, "POST", `/api/friends/${a.id}/accept`)).body.status, "accepted");
  const listA = (await api(a.token, "GET", "/api/friends")).body;
  assert.equal(listA[0].status, "accepted");
  assert.equal(listA[0].online, true, "B dang ket noi gateway");
  wb.close();
});

test("bang hoi: lap (ton vang), moi, vao, kho chung, chat bang, roi bang nhuong bang chu", async () => {
  const lead = await player();
  const mem = await player();
  const tooPoor = await api(lead.token, "POST", "/api/guild", { name: `B${uid()}`.slice(0, 20), tag: "HSV" });
  assert.equal(tooPoor.status, 400);
  await grant(lead.id, 1000, { wheat: 10 });
  const gname = `Bang ${uid()}`.slice(0, 20);
  const g = (await api(lead.token, "POST", "/api/guild", { name: gname, tag: "hsv" })).body;
  assert.equal(g.guild.tag, "HSV");
  assert.equal(g.guild.myRole, "leader");
  await api(lead.token, "POST", "/api/guild/invite", { name: mem.name });
  const invites = (await api(mem.token, "GET", "/api/guild")).body.invites;
  assert.equal(invites[0].name, gname);
  const joined = (await api(mem.token, "POST", `/api/guild/${g.guild.id}/join`)).body;
  assert.equal(joined.members.length, 2);

  await api(lead.token, "POST", "/api/guild/storage/deposit", { itemId: "wheat", count: 6 });
  const afterW = (await api(mem.token, "POST", "/api/guild/storage/withdraw", { itemId: "wheat", count: 4 })).body;
  assert.equal(afterW.storage.find((s) => s.itemId === "wheat").count, 2);
  assert.equal((await api(mem.token, "GET", "/api/economy")).body.items.wheat, 4);

  const wl = await connect(lead.token);
  const wm = await connect(mem.token);
  wm.sendJson({ t: "gchat", text: "chao ca bang" });
  assert.equal((await wl.waitFor((m) => m.t === "chat" && m.ch === "guild")).text, "chao ca bang");

  await api(lead.token, "POST", "/api/guild/leave");
  const now = (await api(mem.token, "GET", "/api/guild")).body;
  assert.equal(now.guild.myRole, "leader");
  wl.close(); wm.close();
});

test("tham nong trai: chi ban be tuoi ho, chu nhan duoc 1 lan; ai cung vao duoc zone farm_", async () => {
  const host = await player();
  const guest = await player();
  const stranger = await player();
  await api(host.token, "PUT", "/api/save", { data: { Farm: [{ X: 1, Y: 2, CropId: "tomato", GrowStage: 1, GrowDays: 3, Watered: false }] } });
  assert.equal((await api(guest.token, "GET", `/api/visits/${host.id}/farm`)).status, 403);
  await makeFriends(host, guest);
  const farm = (await api(guest.token, "GET", `/api/visits/${host.id}/farm`)).body;
  assert.equal(farm.farm[0].cropId, "tomato");
  assert.equal((await api(guest.token, "POST", `/api/visits/${host.id}/water`, { plots: ["g1_2", "g1_2"] })).body.watered, 1);
  const helps = (await api(host.token, "POST", "/api/visits/helps/take")).body;
  assert.equal(helps[0].plotKey, "g1_2");
  assert.equal((await api(host.token, "POST", "/api/visits/helps/take")).body.length, 0);

  // Nong trai nam o toa do rieng tren ban do chung -> nguoi la di toi cung vao duoc vung nong trai.
  const ws = await connect(stranger.token);
  ws.sendJson({ t: "join", zone: `farm_${host.id}` });
  assert.equal((await ws.waitFor((m) => m.t === "joined")).zone, `farm_${host.id}`);
  const wg = await connect(guest.token);
  wg.sendJson({ t: "join", zone: `farm_${host.id}` });
  assert.equal((await wg.waitFor((m) => m.t === "joined")).zone, `farm_${host.id}`);
  ws.close(); wg.close();
});

test("diem danh + nhiem vu ngay: tien do tu bo dem kinh te, nhan 1 lan", async () => {
  const p = await player();
  const c1 = await api(p.token, "POST", "/api/daily/checkin");
  assert.equal(c1.body.streak, 1);
  assert.equal((await api(p.token, "POST", "/api/daily/checkin")).status, 409);
  const d = (await api(p.token, "GET", "/api/daily")).body;
  assert.equal(d.daily.length, 3);
  assert.equal(d.checkin.claimedToday, true);
  // Hoan thanh nhiem vu dau tien bang dung loai op kinh te ma no dem.
  const q = d.daily[0];
  const opFor = {
    harvest: { t: "gain", item: "pumpkin", src: "harvest" }, fish: { t: "gain", item: "ca", src: "fish" },
    mine: { t: "gain", item: "da", src: "mine" }, loot: { t: "gain", item: "mud_core", src: "loot" },
    animal: { t: "gain", item: "egg", src: "animal" }, forage: { t: "gain", item: "hanh_rung", src: "forage" },
  };
  if (q.id === "kill_gold") await api(p.token, "POST", "/api/economy/ops", { seq: 1, ops: Array.from({ length: Math.ceil(q.target / 30) }, () => ({ t: "gold+", n: 30, src: "kill" })) });
  else if (opFor[q.id]) await api(p.token, "POST", "/api/economy/ops", { seq: 1, ops: [{ ...opFor[q.id], n: q.target }] });
  else {
    await grant(p.id, 0, { wheat: 999, rong: 99, wood: 99, carrot: 30, cabbage: 30, pumpkin: 50 });
    const op = q.id === "cook" ? { t: "cook", recipe: "c_sup_rau" } : q.id === "craft" ? { t: "craft", recipe: "r_fertilizer" } : { t: "ship", item: "pumpkin", n: 1, mult: 1 };
    await api(p.token, "POST", "/api/economy/ops", { seq: 1, ops: Array.from({ length: q.target }, () => op) });
  }
  assert.equal((await api(p.token, "GET", "/api/daily")).body.daily[0].progress, q.target);
  const claim = await api(p.token, "POST", "/api/daily/claim", { period: "d", idx: 0 });
  assert.equal(claim.body.gold, q.gold);
  assert.equal((await api(p.token, "POST", "/api/daily/claim", { period: "d", idx: 0 })).status, 409);
  assert.equal((await api(p.token, "POST", "/api/daily/claim", { period: "d", idx: 1 })).status, 400);
});

test("xep hang: vang tu vi server, chi so client bi chan tang/ngay", async () => {
  const p = await player();
  await grant(p.id, 5_000_000);
  const g = (await api(p.token, "GET", "/api/leaderboard/gold")).body;
  assert.equal(g.me.rank, 1);
  await api(p.token, "POST", "/api/stats", { stats: { fish_caught: 999999 } });
  const f = (await api(p.token, "GET", "/api/leaderboard/fish_caught")).body;
  assert.equal(f.me.value, 300, "toi da 300/ngay");
  assert.equal((await api(p.token, "GET", "/api/leaderboard/hack")).status, 404);
});

test("cuong hoa: +1 (100%) tru vang + thoi sat; khong so huu thi tu choi", async () => {
  const p = await player();
  assert.equal((await api(p.token, "POST", "/api/forge/enhance", { itemId: "shield" })).status, 400);
  await grant(p.id, 1000, { thoi_sat: 1 });
  const r = (await api(p.token, "POST", "/api/forge/enhance", { itemId: "sword" })).body;
  assert.deepEqual([r.success, r.level], [true, 1]);
  assert.equal((await api(p.token, "GET", "/api/forge")).body.sword, 1);
  const eco = (await api(p.token, "GET", "/api/economy")).body;
  assert.equal(eco.items.thoi_sat, undefined);
});

test("dau truong: thach dau, server tinh sat thuong theo trang bi, ket thuc cap nhat Elo", async () => {
  const zone = `ar${uid()}`.slice(0, 20);
  const a = await player();
  const b = await player();
  const wa = await connect(a.token);
  const wb = await connect(b.token);
  wa.sendJson({ t: "join", zone }); await wa.waitFor((m) => m.t === "joined");
  wb.sendJson({ t: "join", zone }); await wb.waitFor((m) => m.t === "joined");
  wa.sendJson({ t: "pos", x: 0, y: 0, z: 0, r: 0, a: 0 });
  wb.sendJson({ t: "pos", x: 30, y: 0, z: 0, r: 0, a: 0 });
  wa.sendJson({ t: "duel_invite", to: b.name });
  const inv = await wb.waitFor((m) => m.t === "event" && m.e.type === "duel_invite");
  wb.sendJson({ t: "duel_accept", duelId: inv.e.duelId });
  const start = await wa.waitFor((m) => m.t === "event" && m.e.type === "duel_start");
  assert.equal(start.e.hp, 400);
  // A co kiem (sat thuong 25) - B tay khong; danh lien tuc cho toi khi B guc.
  let end = null;
  for (let i = 0; i < 40 && !end; i++) {
    wa.sendJson({ t: "duel_hit", duelId: start.e.duelId });
    await new Promise((r) => setTimeout(r, 470));
    end = await wb.waitFor((m) => m.t === "event" && m.e.type === "duel_end", 10).catch(() => null);
  }
  assert.ok(end, "tran dau phai ket thuc");
  assert.equal(end.e.winnerId, a.id);
  assert.equal(end.e.delta, 16);
  const board = (await api(a.token, "GET", "/api/leaderboard/pvp")).body;
  assert.equal(board.me.value, 1016);
  wa.close(); wb.close();
});

test("trang phuc: chi mac duoc do so huu, nguoi cung kenh thay doi qua spawn", async () => {
  const zone = `tp${uid()}`.slice(0, 20);
  const a = await player();
  const b = await player();
  const wa = await connect(a.token);
  const wb = await connect(b.token);
  wa.sendJson({ t: "join", zone }); await wa.waitFor((m) => m.t === "joined");
  wb.sendJson({ t: "join", zone }); await wb.waitFor((m) => m.t === "joined");
  // Chua co bo giap -> bi tu choi, khong ai thay doi.
  wa.sendJson({ t: "outfit", id: "giap_thap_tu" });
  assert.match((await wa.waitFor((m) => m.t === "error")).msg, /chua co/);
  // Khong phai trang phuc (vat lieu) -> tu choi.
  await grant(a.id, 0, { vai: 1, giap_thap_tu: 1, do_yem_nong_dan: 1 });
  await new Promise((r) => setTimeout(r, 300));
  wa.sendJson({ t: "outfit", id: "vai" });
  assert.match((await wa.waitFor((m) => m.t === "error")).msg, /trang phuc/);
  await new Promise((r) => setTimeout(r, 300));
  wa.sendJson({ t: "outfit", id: "giap_thap_tu" });
  const seen = await wb.waitFor((m) => m.t === "spawn" && m.p.id === a.id);
  assert.equal(seen.p.app.f, "giap_thap_tu");
  // Nguoi vao kenh sau van thay trang phuc (roster luu kem app.f).
  const c = await player();
  const wc = await connect(c.token);
  wc.sendJson({ t: "join", zone });
  const joined = await wc.waitFor((m) => m.t === "joined");
  assert.equal(joined.players.find((p) => p.id === a.id).app.f, "giap_thap_tu");
  // Coi do (id rong) -> ve trang phuc mac dinh.
  await new Promise((r) => setTimeout(r, 300));
  wa.sendJson({ t: "outfit", id: "" });
  const off = await wb.waitFor((m) => m.t === "spawn" && m.p.id === a.id);
  assert.equal(off.p.app.f, null);
  wa.close(); wb.close(); wc.close();
});
