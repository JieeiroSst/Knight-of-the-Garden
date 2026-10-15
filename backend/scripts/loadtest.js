// Kiem tai gateway WebSocket: tao N bot (chen thang vao DB + ky JWT, khong qua /api/register de
// khong vuong gioi han dang ky), moi bot ket noi, vao 1 vung ngau nhien, gui vi tri 5 lan/giay va
// thinh thoang chat. In ra so ket noi thanh cong, tin nhan/giay, bang thong, do tre vao kenh.
//
//   DATABASE_URL=... JWT_SECRET=... node scripts/loadtest.js --url ws://localhost:3000/ws --bots 2000 --zones 5 --seconds 30
//
// Chay tu NHIEU may cung luc de dat so luong lon (1 tien trinh Node ~ vai nghin ket noi).
const WebSocket = require("ws");
const { Pool } = require("pg");
const jwt = require("jsonwebtoken");

const arg = (name, def) => {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 ? process.argv[i + 1] : def;
};
const URL = arg("url", "ws://localhost:3000/ws");
const BOTS = Number(arg("bots", 500));
const ZONES = Number(arg("zones", 3));
const SECONDS = Number(arg("seconds", 20));
const PREFIX = arg("prefix", "bot");
const RAMP = Number(arg("ramp", 20));

async function seed() {
  const pool = new Pool({ connectionString: process.env.DATABASE_URL, max: 4 });
  const names = Array.from({ length: BOTS }, (_, i) => `${PREFIX}_${i}`);
  // 1 lan chen hang loat; mat khau gia (bot khong dang nhap qua mat khau).
  await pool.query(
    `INSERT INTO players (username, password_hash) SELECT u, 'x' FROM unnest($1::text[]) u ON CONFLICT (username) DO NOTHING`, [names]);
  const r = await pool.query("SELECT id, username FROM players WHERE username = ANY($1)", [names]);
  await pool.query(
    `INSERT INTO characters (player_id, display_name, gender, model_id, skin_color, hair_color, outfit_color)
     SELECT id, 'B' || id, CASE WHEN id % 2 = 0 THEN 'male' ELSE 'female' END,
            CASE WHEN id % 2 = 0 THEN 'Farmer' ELSE 'Woman_Casual' END, '#e0b090', '#302010', '#4070c0'
     FROM unnest($1::int[]) id ON CONFLICT (player_id) DO NOTHING`, [r.rows.map((x) => x.id)]);
  await pool.end();
  return r.rows.map((x) => jwt.sign({ playerId: x.id }, process.env.JWT_SECRET, { expiresIn: "1h" }));
}

async function main() {
  console.log(`Tao ${BOTS} bot...`);
  const tokens = await seed();
  const stats = { errors: {}, connected: 0, joined: 0, failed: 0, msgs: 0, bytes: 0, joinMs: [] };
  const sockets = [];

  await Promise.all(tokens.map((token, i) => new Promise((resolve) => {
    setTimeout(() => {
      const ws = new WebSocket(URL, { handshakeTimeout: 10000 });
      // Khong de 1 ket noi treo lam ca bai kiem tai cho mai.
      const giveUp = setTimeout(() => { if (!ws.joinedOk) { stats.failed++; stats.errors.timeout = (stats.errors.timeout || 0) + 1; ws.terminate(); resolve(); } }, 15000);
      let t0;
      let x = Math.random() * 500, z = Math.random() * 500;
      ws.on("open", () => {
        stats.connected++;
        ws.send(JSON.stringify({ t: "auth", token }));
      });
      ws.on("message", (raw) => {
        stats.msgs++;
        stats.bytes += raw.length;
        const m = JSON.parse(raw);
        if (m.t === "welcome") { t0 = Date.now(); ws.send(JSON.stringify({ t: "join", zone: `lt${i % ZONES}` })); }
        else if (m.t === "joined") {
          ws.joinedOk = true;
          clearTimeout(giveUp);
          stats.joined++;
          stats.joinMs.push(Date.now() - t0);
          ws.timer = setInterval(() => {
            x += Math.random() * 6 - 3; z += Math.random() * 6 - 3;
            ws.send(JSON.stringify({ t: "pos", x, y: 0, z, r: 0, a: 1 }));
            if (Math.random() < 0.004) ws.send(JSON.stringify({ t: "chat", text: "xin chao" }));
          }, 200);
          resolve();
        }
      });
      ws.on("error", (err) => { if (ws.joinedOk) return; clearTimeout(giveUp); stats.failed++; stats.errors[err.code || err.message] = (stats.errors[err.code || err.message] || 0) + 1; resolve(); });
      sockets.push(ws);
    }, Math.floor(i / RAMP) * 20); // RAMP ket noi moi 20ms
  })));

  const start = Date.now();
  const msg0 = stats.msgs, bytes0 = stats.bytes;
  await new Promise((r) => setTimeout(r, SECONDS * 1000));
  const secs = (Date.now() - start) / 1000;
  const sorted = stats.joinMs.sort((a, b) => a - b);
  const pct = (p) => sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))] || 0;
  console.log(JSON.stringify({
    bots: BOTS, connected: stats.connected, joined: stats.joined, failed: stats.failed, errors: stats.errors,
    msgsPerSec: Math.round((stats.msgs - msg0) / secs),
    kbPerSecTotal: Math.round((stats.bytes - bytes0) / secs / 1024),
    kbPerSecPerClient: +((stats.bytes - bytes0) / secs / 1024 / Math.max(1, stats.joined)).toFixed(2),
    joinLatencyMs: { p50: pct(0.5), p95: pct(0.95), p99: pct(0.99) },
  }, null, 2));
  for (const ws of sockets) { clearInterval(ws.timer); ws.close(); }
  setTimeout(() => process.exit(0), 500);
}

main().catch((err) => { console.error(err); process.exit(1); });
