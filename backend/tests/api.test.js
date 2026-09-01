// Integration test THAT (khong mock DB) - can Postgres dang chay va truy cap duoc qua
// DATABASE_URL (mac dinh: Postgres cua docker-compose, expose ra localhost:5432). Dung
// `fetch` co san trong Node 18+ (khong can them thu vien HTTP client).
process.env.NODE_ENV = "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://hsvv:hsvv_password@localhost:5432/hsvv";

const test = require("node:test");
const assert = require("node:assert/strict");
const { createApp } = require("../src/app");
const { runMigrations } = require("../src/db/migrate");
const { pool } = require("../src/db/pool");

let server;
let baseUrl;

test.before(async () => {
  await runMigrations();
  const app = createApp();
  await new Promise((resolve) => {
    server = app.listen(0, () => {
      baseUrl = `http://127.0.0.1:${server.address().port}`;
      resolve();
    });
  });
});

test.after(async () => {
  await new Promise((resolve) => server.close(resolve));
  await pool.end();
});

function uniqueUsername() {
  return `test_${Date.now()}_${Math.floor(Math.random() * 100000)}`;
}

async function postJson(path, body) {
  return fetch(`${baseUrl}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

test("dang ky -> dang nhap -> chua co save (404) -> luu -> nap lai dung du lieu", async () => {
  const username = uniqueUsername();
  const password = "matkhau123";

  const regRes = await postJson("/api/register", { username, password });
  assert.equal(regRes.status, 201);
  const { token: regToken } = await regRes.json();
  assert.ok(regToken);

  const loginRes = await postJson("/api/login", { username, password });
  assert.equal(loginRes.status, 200);
  const { token } = await loginRes.json();
  assert.ok(token);

  const notFoundRes = await fetch(`${baseUrl}/api/save`, { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(notFoundRes.status, 404);

  const putRes = await fetch(`${baseUrl}/api/save`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ data: { Gold: 500, Day: 3 } }),
  });
  assert.equal(putRes.status, 200);

  const getRes = await fetch(`${baseUrl}/api/save`, { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(getRes.status, 200);
  const body = await getRes.json();
  assert.deepEqual(body.data, { Gold: 500, Day: 3 });
});

test("dang ky trung username -> 409", async () => {
  const username = uniqueUsername();
  const password = "matkhau123";
  await postJson("/api/register", { username, password });
  const res = await postJson("/api/register", { username, password });
  assert.equal(res.status, 409);
});

test("dang nhap sai mat khau -> 401", async () => {
  const username = uniqueUsername();
  await postJson("/api/register", { username, password: "matkhaudung" });
  const res = await postJson("/api/login", { username, password: "matkhausai" });
  assert.equal(res.status, 401);
});

test("dang ky username qua ngan -> 400 (validate truoc khi cham DB)", async () => {
  const res = await postJson("/api/register", { username: "ab", password: "matkhau123" });
  assert.equal(res.status, 400);
});

test("truy cap /api/save khong co token -> 401", async () => {
  const res = await fetch(`${baseUrl}/api/save`);
  assert.equal(res.status, 401);
});

test("health (liveness) va health/ready (readiness) tra ve ok", async () => {
  const health = await fetch(`${baseUrl}/health`);
  assert.equal(health.status, 200);
  const ready = await fetch(`${baseUrl}/health/ready`);
  assert.equal(ready.status, 200);
});

test("duong dan khong ton tai -> 404 thong nhat", async () => {
  const res = await fetch(`${baseUrl}/khong-ton-tai`);
  assert.equal(res.status, 404);
  const body = await res.json();
  assert.ok(body.error);
});
