// Integration test pvpService voi Postgres THAT (giong online.test.js): chot do roi theo tui (khong lay
// qua so huu), uu tien nhat tui, khong nhat 2 lan; xay can cu tru bo xay dung, pha ruong -> do roi
// thanh tui, cat/lay ruong, sua ton go, go bo hoan bo.
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://hsvv:hsvv_password@localhost:5432/hsvv";
delete process.env.REDIS_URL;

const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("http");
const { createApp } = require("../src/app");
const { runMigrations } = require("../src/db/migrate");
const { pool } = require("../src/db/pool");
const pvp = require("../src/services/pvpService");
const econ = require("../src/repositories/economyRepository");
const rules = require("../src/game/pvpRules");

let server, baseUrl;
test.before(async () => {
  await runMigrations();
  server = http.createServer(createApp());
  await new Promise((r) => server.listen(0, r));
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});
test.after(async () => {
  await new Promise((r) => server.close(r));
  await pool.end();
});

const uid = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
async function api(token, method, path, body) {
  const res = await fetch(`${baseUrl}${path}`, {
    method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  return { status: res.status, body: text ? JSON.parse(text) : null };
}
async function newPlayer() {
  const reg = await api(null, "POST", "/api/register", { username: `u_${uid()}`, password: "matkhau123" });
  const token = reg.body.token;
  const c = await api(token, "PUT", "/api/character", {
    displayName: `NV${uid()}`.slice(0, 18), gender: "male", modelId: "Farmer", skinColor: "#f0c8a0", hairColor: "#402010", outfitColor: "#3060c0",
  });
  assert.equal(c.status, 200, JSON.stringify(c.body));
  const me = await api(token, "GET", "/api/character");
  await api(token, "GET", "/api/economy"); // tao vi
  return { token, id: me.body.playerId, name: me.body.displayName };
}
const items = async (id) => econ.withTransaction((c) => econ.readItems(c, id));
async function setItem(id, item, n) {
  await pool.query("DELETE FROM inventory_items WHERE player_id = $1 AND item_id = $2", [id, item]);
  if (n > 0) await pool.query("INSERT INTO inventory_items (player_id, item_id, count) VALUES ($1,$2,$3)", [id, item, n]);
}
const expectFail = async (p, re) => assert.match((await p.then(() => ({ message: "KHONG LOI" }), (e) => e)).message, re);

test("chet PvP: chi mat min(tui bao, so huu), nguoi ha uu tien, khong nhat 2 lan", async () => {
  const loser = await newPlayer(), killer = await newPlayer(), other = await newPlayer();
  await setItem(loser.id, "wood", 10);
  await setItem(loser.id, "potion", 2);
  const r = await pvp.dropDeathBag({
    loserId: loser.id, loserName: loser.name, killerId: killer.id,
    manifest: { wood: 50, potion: 1, khong_ton_tai: 5 }, zone: "wild", channel: 1, x: 100, z: -50,
  });
  assert.deepEqual(r.items, { wood: 10, potion: 1 });
  const left = await items(loser.id);
  assert.equal(left.get("wood") || 0, 0);
  assert.equal(left.get("potion"), 1);

  await expectFail(pvp.lootBag(r.bag.id, other.id), /uu tien/);
  const before = (await items(killer.id)).get("wood") || 0;
  const got = await pvp.lootBag(r.bag.id, killer.id);
  assert.deepEqual(got.items, { wood: 10, potion: 1 });
  assert.equal((await items(killer.id)).get("wood"), before + 10);
  await expectFail(pvp.lootBag(r.bag.id, killer.id), /da bi nhat/);

  // Het uu tien -> nguoi khac nhat duoc tui moi.
  await setItem(loser.id, "da", 3);
  const r2 = await pvp.dropDeathBag({ loserId: loser.id, loserName: loser.name, killerId: killer.id, manifest: { da: 3 }, zone: "wild", channel: 1, x: 0, z: 0 });
  assert.ok((await pvp.bagsNear("wild", 1, 0, 0, 500)).some((b) => b.id === r2.bag.id));
  await expectFail(pvp.lootBag(r2.bag.id, other.id), /uu tien/);
  const got2 = await pvp.lootBag(r2.bag.id, other.id, Date.now() + rules.BAG_PRIORITY_MS + 1000);
  assert.deepEqual(got2.items, { da: 3 });

  // Tui trong (khong co gi de mat) -> khong tao tui.
  assert.equal(await pvp.dropDeathBag({ loserId: loser.id, loserName: "", killerId: killer.id, manifest: { wood: 5 }, zone: "wild", channel: 1, x: 0, z: 0 }), null);
});

test("can cu: xay tru bo, ruong, pha ruong roi do, sua, go bo hoan bo", async () => {
  const owner = await newPlayer(), raider = await newPlayer();
  await expectFail(pvp.placeStructure(owner.id, "bo_ruong_can_cu", "wild", 0, 0, 0), /khong co bo/);
  await setItem(owner.id, "bo_ruong_can_cu", 1);
  await expectFail(pvp.placeStructure(owner.id, "bo_ruong_can_cu", `farm_${owner.id}`, 0, 0, 0), /Khong the xay/);
  const chest = await pvp.placeStructure(owner.id, "bo_ruong_can_cu", "wild", 500, 500, 90);
  assert.equal(chest.kind, "chest");
  assert.equal(chest.hp, 500);
  assert.equal((await items(owner.id)).get("bo_ruong_can_cu") || 0, 0);
  assert.ok((await pvp.structuresNear("wild", 500, 500, 100)).some((s) => s.id === chest.id && s.ownerName === owner.name));

  await setItem(owner.id, "wood", 20);
  let contents = await pvp.chestMove(chest.id, owner.id, "wood", 15);
  assert.deepEqual(contents, { wood: 15 });
  assert.equal((await items(owner.id)).get("wood"), 5);
  await expectFail(pvp.chestMove(chest.id, owner.id, "wood", 99), /khong du do/);
  contents = await pvp.chestMove(chest.id, owner.id, "wood", -3);
  assert.deepEqual(contents, { wood: 12 });

  await expectFail(pvp.damageStructure(chest.id, owner.id, 50, 1), /chinh minh/);
  const hit = await pvp.damageStructure(chest.id, raider.id, 200, 1);
  assert.equal(hit.destroyed, false);
  assert.equal(hit.structure.hp, 300);

  // Sua: ton 3 go, +150 mau.
  const repaired = await pvp.repairStructure(chest.id, owner.id);
  assert.equal(repaired.hp, 450);
  assert.equal((await items(owner.id)).get("wood"), 5);

  const kill = await pvp.damageStructure(chest.id, raider.id, 1000, 1);
  assert.equal(kill.destroyed, true);
  assert.deepEqual(kill.bag && (await pvp.lootBag(kill.bag.id, raider.id)).items, { wood: 12 });
  assert.equal((await pvp.structuresNear("wild", 500, 500, 100)).some((s) => s.id === chest.id), false);

  // Go bo tuong con nguyen -> hoan bo.
  await setItem(owner.id, "bo_tuong_go", 1);
  const wall = await pvp.placeStructure(owner.id, "bo_tuong_go", "wild", 0, 0, 0);
  await expectFail(pvp.removeStructure(wall.id, raider.id), /Chi chu/);
  const rm = await pvp.removeStructure(wall.id, owner.id);
  assert.equal(rm.refund, "bo_tuong_go");
  assert.equal((await items(owner.id)).get("bo_tuong_go"), 1);
  assert.equal((await pvp.myStructures(owner.id)).length, 0);
});

test("giuong: hoi sinh tai giuong; giuong cuoi bi pha -> lan chet sau doi 1 gio (luu DB, khong lach duoc); dung giuong moi het phat", async () => {
  const p = await newPlayer(), raider = await newPlayer();
  // Nguoi moi (chua tung co giuong) -> ve nong trai ngay.
  assert.equal((await pvp.decideRespawn(p.id, 0, 0)).mode, "farm");

  await setItem(p.id, "bo_giuong", 2);
  const bedA = await pvp.placeStructure(p.id, "bo_giuong", "wild", 1000, 0, 0);
  const bedB = await pvp.placeStructure(p.id, "bo_giuong", "wild", 5000, 0, 0);
  const d1 = await pvp.decideRespawn(p.id, 4800, 0);
  assert.deepEqual([d1.mode, d1.x], ["bed", 5000]);

  // Pha 1 giuong: van con giuong kia -> chua bi phat.
  assert.equal((await pvp.damageStructure(bedB.id, raider.id, 9999, 1)).bedLost, false);
  assert.equal((await pvp.decideRespawn(p.id, 4800, 0)).x, 1000);
  // Pha giuong cuoi -> mat giuong.
  assert.equal((await pvp.damageStructure(bedA.id, raider.id, 9999, 1)).bedLost, true);
  const wait = await pvp.decideRespawn(p.id, 0, 0);
  assert.equal(wait.mode, "wait");
  assert.ok(wait.readyAt - Date.now() > rules.BED_LOST_WAIT_MS - 5000);
  // Dang nhap lai / chet tiep trong luc cho -> giu nguyen han, khong reset.
  assert.deepEqual(await pvp.respawnState(p.id), { mode: "wait", readyAt: wait.readyAt });
  assert.equal((await pvp.decideRespawn(p.id, 0, 0)).readyAt, wait.readyAt);
  // Het han (gia lap) -> lan chet sau ve nong trai binh thuong (phat chi 1 lan).
  await pool.query("UPDATE player_respawn SET ready_at = now() - interval '1 second' WHERE player_id = $1", [p.id]);
  await require("../src/cache/cache").invalidate(`respawn:${p.id}`);
  assert.equal(await pvp.respawnState(p.id), null);
  assert.equal((await pvp.decideRespawn(p.id, 0, 0)).mode, "farm");
});

test("cache: cong trinh moi/bi pha thay ngay o truy van quanh vi tri (xoa cache sau khi DB commit)", async () => {
  const p = await newPlayer(), raider = await newPlayer();
  const cache = require("../src/cache/cache");
  assert.deepEqual(await pvp.structuresNear("cachez", 0, 0, 500), []);
  const before = { ...cache.stats };
  assert.deepEqual(await pvp.structuresNear("cachez", 0, 0, 500), []);
  assert.ok(cache.stats.hits > before.hits, "lan doc thu 2 phai lay tu cache");
  await setItem(p.id, "bo_tuong_go", 1);
  const w = await pvp.placeStructure(p.id, "bo_tuong_go", "cachez", 10, 10, 0);
  assert.deepEqual((await pvp.structuresNear("cachez", 0, 0, 500)).map((s) => s.id), [w.id]);
  await pvp.damageStructure(w.id, raider.id, 100, 1);
  assert.equal((await pvp.structuresNear("cachez", 0, 0, 500))[0].hp, 700);
  await pvp.damageStructure(w.id, raider.id, 9999, 1);
  assert.deepEqual(await pvp.structuresNear("cachez", 0, 0, 500), [], "bi pha la mat han, khong khoi phuc");
});

test("han dung (DB): thit qua han thanh Do Oi Thiu o lan dong bo ke tiep; cat ruong khong tuoi lai", async () => {
  const p = await newPlayer();
  await setItem(p.id, "thit_bo", 4);
  let snap = (await api(p.token, "GET", "/api/economy")).body;
  assert.equal(snap.states.thit_bo.fresh[0][1], 4, "lo moi cho thit chua co trang thai");
  // Gia lap da qua 13 gio.
  await pool.query("UPDATE item_states SET state = jsonb_set(state, '{fresh,0,0}', to_jsonb((extract(epoch from now()) * 1000 - 1000)::bigint)) WHERE player_id = $1 AND item_id = 'thit_bo'", [p.id]);
  snap = (await api(p.token, "GET", "/api/economy")).body;
  assert.equal(snap.items.thit_bo, undefined);
  assert.equal(snap.items.do_thoi, 4);
  assert.ok(snap.events.some((e) => e.type === "spoiled" && e.item === "thit_bo" && e.count === 4));

  // Cat thit gan het han vao ruong roi lay ra: han giu nguyen.
  await setItem(p.id, "thit_heo", 2);
  await api(p.token, "GET", "/api/economy");
  const exp0 = (await pool.query("SELECT state FROM item_states WHERE player_id = $1 AND item_id = 'thit_heo'", [p.id])).rows[0].state.fresh[0][0];
  await setItem(p.id, "bo_ruong_can_cu", 1);
  const chest = await pvp.placeStructure(p.id, "bo_ruong_can_cu", "wild", 3000, 3000, 0);
  await pvp.chestMove(chest.id, p.id, "thit_heo", 2);
  await pvp.chestMove(chest.id, p.id, "thit_heo", -2);
  const exp1 = (await pool.query("SELECT state FROM item_states WHERE player_id = $1 AND item_id = 'thit_heo'", [p.id])).rows[0].state.fresh[0][0];
  assert.equal(Number(exp1), Number(exp0));
});

test("do ben (DB): op wear qua API, kiem duoi 40% khong tinh trong PvP, sua xong tinh lai", async () => {
  const progress = require("../src/services/progressService");
  const cache = require("../src/cache/cache");
  const p = await newPlayer();
  const before = (await progress.duelPower(p.id)).attack; // vu khi khoi dau (neu co)
  await setItem(p.id, "kiem_sat", 1);
  await cache.invalidate(`power:${p.id}`);
  const def = require("../src/game/catalog").catalog.items.get("kiem_sat");
  const base = (await progress.duelPower(p.id)).attack;
  assert.ok(base >= def.Damage && base > before);
  let r = await api(p.token, "POST", "/api/economy/ops", { seq: 1, ops: [{ t: "wear", item: "kiem_sat", n: def.MaxDurability - 10 }] });
  assert.equal(r.status, 200, JSON.stringify(r.body));
  assert.equal(r.body.states.kiem_sat.dur[0], 10);
  await cache.invalidate(`power:${p.id}`);
  assert.equal((await progress.duelPower(p.id)).attack, before, "kiem hong -> khong tinh, dung vu khi con lanh");
  const cost = require("../src/game/itemState").repairCost(def);
  await pool.query("UPDATE wallets SET gold = gold + $2 WHERE player_id = $1", [p.id, cost.gold]);
  for (const [id, n] of cost.mats) await setItem(p.id, id, n);
  r = await api(p.token, "POST", "/api/economy/ops", { seq: 2, ops: [{ t: "repair", item: "kiem_sat" }] });
  assert.equal(r.body.rejected.length, 0, JSON.stringify(r.body.rejected));
  assert.equal(r.body.states.kiem_sat.dur[0], def.MaxDurability);
  await cache.invalidate(`power:${p.id}`);
  assert.equal((await progress.duelPower(p.id)).attack, base);
});
