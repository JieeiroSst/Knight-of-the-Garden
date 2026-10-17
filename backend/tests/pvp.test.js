// PvP the gioi mo qua gateway THAT (WebSocket that), thay DB bang ban gia: danh trong/ngoai tam, dong
// doi khong danh nhau, chet -> chot do theo danh sach tui -> tui roi + bao nguoi ha, bao ve hoi sinh,
// ho tro hoi mau. Cong them unit test luat thuan (pvpRules).
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
const progressService = require("../src/services/progressService");
const pvpService = require("../src/services/pvpService");
const rules = require("../src/game/pvpRules");

const names = new Map();
characterService.get = async (playerId) => ({
  playerId, displayName: `P${playerId}`, gender: "male", modelId: "Farmer", skinColor: "#fff", hairColor: "#000", outfitColor: "#00f",
});
characterService.findByName = async (name) => { const id = Number(String(name).slice(1)); return id ? { playerId: id, displayName: name } : null; };
socialService.memberOf = async () => null;
socialService.areFriends = async () => false;
// 9500-9599: co cung + 3 mui ten; nguoi khac: khong co cung.
progressService.duelPower = async (id) => (id >= 9500 && id < 9600
  ? { attack: 10, defense: 0, rangedAttack: 30, rangedRange: 900, arrows: 3 }
  : { attack: 40, defense: 0, rangedAttack: 0, rangedRange: 0, arrows: 0 });
pvpService.structuresNear = async () => [];
pvpService.bagsNear = async () => [];
pvpService.consumeHealPotion = async () => true;
pvpService.wearItems = async () => {};
pvpService.respawnState = async () => null;
pvpService.decideRespawn = async () => ({ mode: "farm", readyAt: Date.now() });
const drops = [];
pvpService.dropDeathBag = async (args) => {
  drops.push(args);
  const items = Object.fromEntries(Object.entries(args.manifest || {}).map(([k, v]) => [k, Number(v)]));
  return { bag: { id: 77, x: args.x, z: args.z, ownerId: args.loserId, killerId: args.killerId, ownerName: args.loserName }, items };
};

const { attachGateway } = require("../src/realtime/gateway");
const { signToken } = require("../src/utils/jwt");

let server, gateway, wsUrl;
test.before(async () => {
  server = http.createServer();
  gateway = attachGateway(server, { tickMs: 40, capacity: 50 });
  await new Promise((r) => server.listen(0, r));
  wsUrl = `ws://127.0.0.1:${server.address().port}/ws`;
});
test.after(async () => {
  await gateway?.close();
  await new Promise((r) => server.close(r));
});

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const ev = (type) => (m) => m.t === "event" && m.e.type === type;

function connect(playerId) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    const json = [];
    ws.on("message", (raw, isBinary) => { if (!isBinary) json.push(JSON.parse(raw)); });
    ws.on("error", reject);
    ws.on("open", () => {
      ws.send(JSON.stringify({ t: "auth", token: signToken(playerId) }));
      resolve({
        ws, json, id: playerId,
        send: (m) => ws.send(JSON.stringify(m)),
        async waitFor(pred, ms = 2500) {
          const end = Date.now() + ms;
          while (Date.now() < end) { const hit = json.find(pred); if (hit) return hit; await sleep(10); }
          throw new Error("het gio cho tin");
        },
        count: (pred) => json.filter(pred).length,
      });
    });
  });
}

async function joinAt(c, zone, x, z, hp = 100) {
  await c.waitFor((m) => m.t === "welcome");
  c.send({ t: "join", zone });
  await c.waitFor((m) => m.t === "joined");
  c.send({ t: "pos", x, y: 0, z, r: 0, a: 0 });
  c.send({ t: "hp", hp, max: 100 });
}

test("luat: mau chi tang cham, do mat = min(tui bao, so huu), uu tien nhat tui", () => {
  assert.deepEqual(rules.acceptHp(40, 100, 100), { hp: 65, max: 100 });
  assert.deepEqual(rules.acceptHp(40, 10, 100), { hp: 10, max: 100 });
  assert.equal(rules.acceptHp(null, 80, 999999).max, 20000);
  const catalog = { items: new Map([["wood", {}], ["potion", {}]]) };
  const m = rules.cleanManifest({ wood: 50, potion: "3", hack: 9, potion2: -1 }, catalog);
  assert.deepEqual([...m], [["wood", 50], ["potion", 3]]);
  assert.deepEqual([...rules.lossFromManifest(m, new Map([["wood", 20]]))], [["wood", 20]]);
  const now = Date.now();
  const bag = { killer_id: 5, priority_until: new Date(now + 1000), expires_at: new Date(now + 5000) };
  assert.ok(rules.canLootBag(bag, 6, now));
  assert.equal(rules.canLootBag(bag, 5, now), null);
  assert.equal(rules.canLootBag(bag, 6, now + 2000), null);
  assert.ok(rules.canLootBag({ ...bag, looted_at: new Date() }, 5, now));
  assert.ok(rules.pvpDamage(40, 0, true) > rules.pvpDamage(40, 0, false) * 1.2);
});

test("PvP: danh trong tam tru mau, ngoai tam / khac kenh khong tinh", async () => {
  const a = await connect(9101), b = await connect(9102), far = await connect(9103);
  await joinAt(a, "pvpz", 0, 0);
  await joinAt(b, "pvpz", 50, 0);
  await joinAt(far, "pvpz", 3000, 0);
  await sleep(150);
  a.send({ t: "pvp_hit", target: 9102 });
  const hurt = await b.waitFor(ev("pvp_hurt"));
  assert.equal(hurt.e.byName, "P9101");
  assert.ok(hurt.e.dmg >= 36 && hurt.e.dmg <= 44, `dmg ${hurt.e.dmg}`);
  assert.equal(hurt.e.hp, 100 - hurt.e.dmg);
  await a.waitFor((m) => m.t === "hpbar" && m.id === 9102);

  await sleep(450);
  a.send({ t: "pvp_hit", target: 9103 });
  await sleep(200);
  assert.equal(far.count(ev("pvp_hurt")), 0, "ngoai tam khong trung");
  for (const c of [a, b, far]) c.ws.close();
});

test("PvP: chet -> chot do theo tui -> tui roi cho ca kenh, nguoi ha duoc bao, bao ve hoi sinh", async () => {
  const killer = await connect(9201), victim = await connect(9202), watcher = await connect(9203);
  await joinAt(killer, "pvpd", 0, 0);
  await joinAt(victim, "pvpd", 40, 0, 30);
  await joinAt(watcher, "pvpd", 80, 0);
  await sleep(150);
  victim.send({ t: "bag", items: { wood: 12, potion: 2 } });
  killer.send({ t: "pvp_hit", target: 9202, fin: true });
  await victim.waitFor(ev("pvp_dead_pending"));
  victim.send({ t: "death_ack", items: { wood: 12, potion: 2 } });
  const dead = await victim.waitFor(ev("pvp_dead"));
  assert.equal(dead.e.killerName, "P9201");
  assert.equal(dead.e.respawn.mode, "farm");
  assert.deepEqual(dead.e.lost, { wood: 12, potion: 2 });
  const d = drops.at(-1);
  assert.equal(d.loserId, 9202);
  assert.equal(d.killerId, 9201);
  assert.equal(d.zone, "pvpd");
  const bagMsg = await watcher.waitFor((m) => m.t === "bag");
  assert.equal(bagMsg.bag.id, 77);
  await killer.waitFor(ev("pvp_kill"));

  // Bao ve hoi sinh: danh tiep ngay khong tru mau.
  const before = victim.count(ev("pvp_hurt"));
  await sleep(450);
  killer.send({ t: "pvp_hit", target: 9202 });
  await sleep(200);
  assert.equal(victim.count(ev("pvp_hurt")), before);
  for (const c of [killer, victim, watcher]) c.ws.close();
});

test("To doi: khong danh nhau duoc + ho tro hoi mau", async () => {
  const a = await connect(9301), b = await connect(9302);
  await joinAt(a, "pvpp", 0, 0);
  await joinAt(b, "pvpp", 60, 0, 40);
  await sleep(150);
  a.send({ t: "party_invite", to: "P9302" });
  const inv = await b.waitFor(ev("party_invite"));
  b.send({ t: "party_accept", partyId: inv.e.partyId });
  await a.waitFor((m) => m.t === "event" && m.e.type === "party_changed" && m.e.party?.members.length === 2);
  await sleep(100);
  a.send({ t: "pvp_hit", target: 9302 });
  await sleep(200);
  assert.equal(b.count(ev("pvp_hurt")), 0, "dong doi khong tru mau");

  a.send({ t: "heal_ally", target: 9302 });
  const healed = await b.waitFor(ev("healed"));
  assert.equal(healed.e.amount, rules.HEAL_AMOUNT);
  assert.equal(healed.e.hp, 100);
  await a.waitFor(ev("heal_done"));
  // Vi tri dong doi gui moi giay.
  const pos = await a.waitFor(ev("party_pos"), 2500);
  assert.equal(pos.e.id, 9302);
  for (const c of [a, b]) c.ws.close();
});

test("luat: hoi sinh giuong gan nhat / doi 1 gio khi mat giuong / nguoi moi ve nong trai; cho an toan; bui co tat dinh", () => {
  const now = 1000;
  const beds = [{ x: 0, z: 0 }, { x: 500, z: 0 }];
  assert.deepEqual(rules.respawnDecision({ beds, bedLost: true, deathX: 450, deathZ: 0, now }), { mode: "bed", x: 500, z: 0, readyAt: now });
  assert.deepEqual(rules.respawnDecision({ beds: [], bedLost: true, deathX: 0, deathZ: 0, now }), { mode: "wait", readyAt: now + rules.BED_LOST_WAIT_MS });
  assert.equal(rules.respawnDecision({ beds: [], bedLost: false, deathX: 0, deathZ: 0, now }).mode, "farm");
  const markets = [{ X: 100, Z: 100, SafeRadius: 350 }];
  assert.ok(rules.inSafeZone(markets, 300, 100));
  assert.equal(rules.inSafeZone(markets, 600, 100), null);
  // Gia tri chot: PHAI trung voi HideSpots.cs ben Godot (test C# dung cung so).
  assert.equal(rules.hash3(-5, 7, 77), 3310952574);
  const b = findBush();
  assert.ok(rules.inBush(b.x, b.z));
  assert.equal(rules.inBush(b.x + b.r + 200, b.z + b.r + 200) && false, false);
});

function findBush() {
  for (let cx = 0; cx < 100; cx++) { const b = rules.bushInCell(cx, 3); if (b) return b; }
  throw new Error("khong co bui");
}

test("Cho: khong danh duoc trong vung an toan cua khu cho", async () => {
  const { catalog } = require("../src/game/catalog");
  const m = catalog.markets[1];
  const a = await connect(9401), b = await connect(9402);
  await joinAt(a, "wild", m.X, m.Z);
  await joinAt(b, "wild", m.X + 40, m.Z);
  await sleep(150);
  a.send({ t: "pvp_hit", target: 9402 });
  await a.waitFor((x) => x.t === "error" && /an toan/.test(x.msg));
  await sleep(100);
  assert.equal(b.count(ev("pvp_hurt")), 0);
  for (const c of [a, b]) c.ws.close();
});

test("Cung: ban xa trong tam cung, can cung + mui ten that; nguoi khong co cung khong ban xa duoc", async () => {
  const archer = await connect(9501), target = await connect(9502), noBow = await connect(9601);
  await joinAt(archer, "arch", 0, 0);
  await joinAt(target, "arch", 700, 0);
  await joinAt(noBow, "arch", 0, 50);
  await sleep(150);
  archer.send({ t: "shot", tx: 700, ty: 20, tz: 0 });
  archer.send({ t: "pvp_hit", target: 9502, ranged: true });
  const hurt = await target.waitFor(ev("pvp_hurt"));
  assert.equal(hurt.e.ranged, true);
  assert.ok(hurt.e.dmg >= 27 && hurt.e.dmg <= 33);
  await noBow.waitFor((x) => x.t === "shot" && x.id === 9501);
  noBow.send({ t: "pvp_hit", target: 9502, ranged: true });
  await sleep(250);
  assert.equal(target.count(ev("pvp_hurt")), 1, "khong co cung -> khong tinh");
  for (const c of [archer, target, noBow]) c.ws.close();
});

test("An nap: trong bui co -> nguoi o xa nhan vanish, khong nhan vi tri; nguoi gan van thay; lo dien khi ra", async () => {
  const b = findBush();
  const hider = await connect(9701), far = await connect(9702), close = await connect(9703);
  await joinAt(hider, "hide", b.x, b.z);
  await joinAt(far, "hide", b.x + 600, b.z);
  await joinAt(close, "hide", b.x + 60, b.z);
  await sleep(150);
  hider.send({ t: "pos", x: b.x + 1, y: 0, z: b.z, r: 0, a: 0, h: 1 });
  await far.waitFor((m) => m.t === "vanish" && m.id === 9701);
  far.json.length = 0;
  hider.send({ t: "pos", x: b.x + 2, y: 0, z: b.z, r: 0, a: 0, h: 1 });
  await sleep(300);
  const rowsFar = far.json.filter((m) => m.t === "moves").flatMap((m) => m.m).filter((r) => r[0] === 9701);
  assert.equal(rowsFar.length, 0, "nguoi xa khong duoc biet vi tri nguoi dang an");
  const rowsClose = close.json.filter((m) => m.t === "moves").flatMap((m) => m.m).filter((r) => r[0] === 9701);
  assert.ok(rowsClose.length >= 1, "nguoi dung gan van thay");
  assert.equal(close.count((m) => m.t === "vanish"), 0);

  // Bao an o ngoai bui -> server khong chap nhan.
  hider.send({ t: "pos", x: b.x + b.r + 500, y: 0, z: b.z + b.r + 500, r: 0, a: 0, h: 1 });
  await far.waitFor((m) => m.t === "appear" && m.id === 9701);
  for (const c of [hider, far, close]) c.ws.close();
});

test("luat ngua: xung phong x1.4 + hat nguoi cuoi xuong; nguoi di bo chem nguoi tren ngua bi giam; don ket co the hat", () => {
  const charge = rules.mountedHit({ attackerMounted: true, attackerSpeed: 250, ranged: false, fin: false, targetMounted: true });
  assert.deepEqual(charge, { charge: true, mult: rules.CHARGE_MULT, knock: true });
  const slow = rules.mountedHit({ attackerMounted: true, attackerSpeed: 50, ranged: false, fin: false, targetMounted: true });
  assert.equal(slow.charge, false);
  assert.equal(slow.knock, false);
  const foot = rules.mountedHit({ attackerMounted: false, attackerSpeed: 0, ranged: false, fin: true, targetMounted: true }, () => 0.1);
  assert.deepEqual(foot, { charge: false, mult: rules.MOUNTED_DEF, knock: true });
  assert.equal(rules.mountedHit({ attackerMounted: true, attackerSpeed: 300, ranged: true, fin: false, targetMounted: true }).charge, false, "ban cung khong tinh xung phong");
  assert.ok(rules.smoothSpeed(0, 100, 500) > 90);
});

test("Ngua: server uoc luong toc do tu goi vi tri -> xung phong hat doi thu dang cuoi xuong ngua", async () => {
  const rider = await connect(9801), target = await connect(9802);
  await joinAt(rider, "horse", 0, 0);
  await joinAt(target, "horse", 400, 0);
  target.send({ t: "pos", x: 400, y: 0, z: 0, r: 0, a: 32 }); // dang cuoi ngua
  await sleep(100);
  // Phi nuoc dai ~260 don vi/giay ve phia muc tieu.
  for (let i = 1; i <= 6; i++) { rider.send({ t: "pos", x: i * 52, y: 0, z: 0, r: 0, a: 32 + 2 }); await sleep(200); }
  rider.send({ t: "pvp_hit", target: 9802 });
  const hurt = await target.waitFor(ev("pvp_hurt"));
  assert.equal(hurt.e.charge, true);
  assert.equal(hurt.e.knock, true);
  for (const c of [rider, target]) c.ws.close();
});

test("To doi: rot mang / dang nhap lai van con trong to doi (khong bi xoa), van khong danh nhau duoc", async () => {
  const a = await connect(9901);
  let b = await connect(9902);
  await joinAt(a, "partyz", 0, 0);
  await joinAt(b, "partyz", 50, 0);
  await sleep(100);
  a.send({ t: "party_invite", to: "P9902" });
  const inv = await b.waitFor(ev("party_invite"));
  b.send({ t: "party_accept", partyId: inv.e.partyId });
  await a.waitFor((m) => m.t === "event" && m.e.type === "party_changed" && m.e.party?.members.length === 2);
  b.ws.close();
  await sleep(200);
  b = await connect(9902);
  const restored = await b.waitFor((m) => m.t === "event" && m.e.type === "party_changed" && m.e.party);
  assert.deepEqual(restored.e.party.members.sort(), [9901, 9902]);
  await b.waitFor((m) => m.t === "welcome");
  b.send({ t: "join", zone: "partyz" });
  await b.waitFor((m) => m.t === "joined");
  b.send({ t: "pos", x: 50, y: 0, z: 0, r: 0, a: 0 });
  b.send({ t: "hp", hp: 100, max: 100 });
  await sleep(150);
  b.send({ t: "pvp_hit", target: 9901 });
  await sleep(200);
  assert.equal(a.count(ev("pvp_hurt")), 0, "van la dong doi sau khi dang nhap lai");
  for (const c of [a, b]) c.ws.close();
});
