// Test gateway KHONG can Postgres (thay characterService/socialService bang ban gia): voice chat theo
// khoang cach + vung quan tam (AOI) cua vi tri.
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
characterService.get = async (playerId) => ({
  playerId, displayName: `P${playerId}`, gender: "male", modelId: "Farmer", skinColor: "#fff", hairColor: "#000", outfitColor: "#00f",
});
socialService.memberOf = async () => null;
socialService.areFriends = async () => false;

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

function connect(playerId) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    const json = [];
    const binary = [];
    ws.on("message", (raw, isBinary) => (isBinary ? binary : json).push(isBinary ? Buffer.from(raw) : JSON.parse(raw)));
    ws.on("error", reject);
    ws.on("open", () => {
      ws.send(JSON.stringify({ t: "auth", token: signToken(playerId) }));
      const c = {
        ws, json, binary,
        send: (m) => ws.send(JSON.stringify(m)),
        async waitFor(pred, ms = 2000) {
          const end = Date.now() + ms;
          while (Date.now() < end) {
            const hit = json.find(pred);
            if (hit) return hit;
            await sleep(10);
          }
          throw new Error("het gio cho tin");
        },
      };
      resolve(c);
    });
  });
}

async function joinAt(c, x, z) {
  await c.waitFor((m) => m.t === "welcome");
  c.send({ t: "join", zone: "vtest" });
  await c.waitFor((m) => m.t === "joined");
  c.send({ t: "pos", x, y: 0, z, r: 0, a: 0 });
}

const voicePacket = (seq) => Buffer.from([0x56, seq, 0x10, 0x00, 0x05, 0xab, 0xcd, 0xef]);

test("voice: chi nguoi trong VOICE_RANGE nhan duoc, kem dung id nguoi noi", async () => {
  const a = await connect(9001), near = await connect(9002), far = await connect(9003);
  await joinAt(a, 0, 0);
  await joinAt(near, 300, 0);
  await joinAt(far, 5000, 0);
  await sleep(120); // cho nhip gateway cap nhat vi tri

  a.ws.send(voicePacket(7), { binary: true });
  await sleep(150);

  assert.equal(near.binary.length, 1, "nguoi o gan phai nghe");
  const pkt = near.binary[0];
  assert.equal(pkt[0], 0x56);
  assert.equal(pkt.readUInt32LE(1), 9001);
  assert.deepEqual([...pkt.subarray(5)], [...voicePacket(7).subarray(1)]);
  assert.equal(far.binary.length, 0, "nguoi o xa KHONG duoc nghe");
  assert.equal(a.binary.length, 0, "khong tu nghe lai giong minh");

  // Goi qua lon / sai tag bi bo qua.
  a.ws.send(Buffer.alloc(2000, 0x56), { binary: true });
  a.ws.send(Buffer.from([0x11, 1, 2, 3, 4, 5]), { binary: true });
  await sleep(120);
  assert.equal(near.binary.length, 1);

  for (const c of [a, near, far]) c.ws.close();
});

test("voice: chua vao vung chung thi khong phat", async () => {
  const a = await connect(9011), b = await connect(9012);
  await a.waitFor((m) => m.t === "welcome");
  await joinAt(b, 0, 0);
  a.ws.send(voicePacket(1), { binary: true });
  await sleep(120);
  assert.equal(b.binary.length, 0);
  a.ws.close(); b.ws.close();
});

test("AOI: nguoi o xa van nhan vi tri cuoi cung (gui bu), nguoi gan nhan moi nhip", async () => {
  const mover = await connect(9021), near = await connect(9022), far = await connect(9023);
  await joinAt(mover, 0, 0);
  await joinAt(near, 100, 0);
  await joinAt(far, 9000, 0);
  await sleep(100);
  near.json.length = 0;
  far.json.length = 0;

  for (let i = 1; i <= 6; i++) {
    mover.send({ t: "pos", x: i * 10, y: 0, z: 0, r: 0, a: 1 });
    await sleep(45);
  }
  await sleep(400); // du vai nhip "far" (moi AOI_FAR_EVERY nhip)

  const rowsOf = (c) => c.json.filter((m) => m.t === "moves").flatMap((m) => m.m).filter((r) => r[0] === 9021);
  const nearRows = rowsOf(near), farRows = rowsOf(far);
  assert.ok(nearRows.length >= 4, `gan phai nhan nhieu cap nhat (${nearRows.length})`);
  assert.ok(farRows.length >= 1 && farRows.length < nearRows.length, `xa nhan thua hon (${farRows.length})`);
  assert.equal(farRows.at(-1)[1], 60, "nguoi xa phai thay vi tri CUOI CUNG");
  for (const c of [mover, near, far]) c.ws.close();
});
