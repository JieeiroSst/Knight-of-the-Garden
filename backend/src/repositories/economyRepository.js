// SQL cho kinh te phia server. Cac ham nhan "client" (ket noi dang trong 1 transaction) de
// economyService/tradeService/marketService ghep nhieu buoc thanh 1 giao dich nguyen tu.
const { pool } = require("../db/pool");

async function withTransaction(fn) {
  const client = await pool.connect();
  try {
    await client.query("BEGIN");
    const result = await fn(client);
    await client.query("COMMIT");
    return result;
  } catch (err) {
    await client.query("ROLLBACK");
    throw err;
  } finally {
    client.release();
  }
}

// Khoa dong vi (SELECT ... FOR UPDATE) - moi thay doi kinh te cua 1 nguoi choi duoc tuan tu hoa,
// tranh 2 request song song cung tieu 1 dong vang.
async function lockWallet(client, playerId) {
  const r = await client.query("SELECT gold, house_level, last_batch_seq FROM wallets WHERE player_id = $1 FOR UPDATE", [playerId]);
  return r.rows[0] || null;
}

// Khoa nhieu vi theo THU TU id tang dan - tranh deadlock khi 2 nguoi giao dich cheo nhau.
async function lockWallets(client, ids) {
  const sorted = [...new Set(ids)].sort((a, b) => a - b);
  const r = await client.query("SELECT player_id, gold, house_level FROM wallets WHERE player_id = ANY($1) ORDER BY player_id FOR UPDATE", [sorted]);
  return new Map(r.rows.map((row) => [row.player_id, row]));
}

async function createWallet(client, playerId, gold, houseLevel, items) {
  await client.query(
    "INSERT INTO wallets (player_id, gold, house_level) VALUES ($1, $2, $3) ON CONFLICT (player_id) DO NOTHING",
    [playerId, gold, houseLevel]
  );
  await writeItems(client, playerId, new Map(), items);
}

async function readItems(client, playerId) {
  const r = await client.query("SELECT item_id, count FROM inventory_items WHERE player_id = $1", [playerId]);
  return new Map(r.rows.map((row) => [row.item_id, row.count]));
}

// Chi ghi cac dong THAY DOI (so voi trang thai luc nap) - giam tai ghi DB.
async function writeItems(client, playerId, before, after) {
  const upserts = [];
  const deletes = [];
  for (const [id, n] of after) if (before.get(id) !== n) upserts.push([id, n]);
  for (const id of before.keys()) if (!after.has(id)) deletes.push(id);
  if (upserts.length) {
    await client.query(
      `INSERT INTO inventory_items (player_id, item_id, count)
       SELECT $1, u.item_id, u.count FROM unnest($2::text[], $3::int[]) AS u(item_id, count)
       ON CONFLICT (player_id, item_id) DO UPDATE SET count = EXCLUDED.count`,
      [playerId, upserts.map((u) => u[0]), upserts.map((u) => u[1])]
    );
  }
  if (deletes.length) {
    await client.query("DELETE FROM inventory_items WHERE player_id = $1 AND item_id = ANY($2)", [playerId, deletes]);
  }
}

async function updateWallet(client, playerId, gold, houseLevel, seq) {
  await client.query(
    "UPDATE wallets SET gold = $2, house_level = $3, last_batch_seq = GREATEST(last_batch_seq, $4), updated_at = now() WHERE player_id = $1",
    [playerId, gold, houseLevel, seq]
  );
}

async function addGold(client, playerId, delta) {
  await client.query("UPDATE wallets SET gold = gold + $2, updated_at = now() WHERE player_id = $1", [playerId, delta]);
}

async function readCounters(client, playerId, bucket) {
  const r = await client.query("SELECT source, amount FROM economy_counters WHERE player_id = $1 AND bucket = $2", [playerId, bucket]);
  return new Map(r.rows.map((row) => [row.source, Number(row.amount)]));
}

async function writeCounters(client, playerId, bucket, before, after) {
  const changed = [...after].filter(([k, v]) => before.get(k) !== v);
  if (!changed.length) return;
  await client.query(
    `INSERT INTO economy_counters (player_id, bucket, source, amount)
     SELECT $1, $2, u.source, u.amount FROM unnest($3::text[], $4::bigint[]) AS u(source, amount)
     ON CONFLICT (player_id, bucket, source) DO UPDATE SET amount = EXCLUDED.amount`,
    [playerId, bucket, changed.map((c) => c[0]), changed.map((c) => c[1])]
  );
}

async function readClaims(client, playerId, keys) {
  if (!keys.length) return new Set();
  const r = await client.query("SELECT claim_key FROM reward_claims WHERE player_id = $1 AND claim_key = ANY($2)", [playerId, keys]);
  return new Set(r.rows.map((row) => row.claim_key));
}

async function insertClaims(client, playerId, keys) {
  if (!keys.length) return;
  await client.query(
    "INSERT INTO reward_claims (player_id, claim_key) SELECT $1, k FROM unnest($2::text[]) AS k ON CONFLICT DO NOTHING",
    [playerId, keys]
  );
}

async function readBin(client, playerId) {
  const r = await client.query("SELECT day, item_id, count, unit_price FROM shipping_bin WHERE player_id = $1", [playerId]);
  return r.rows.map((row) => ({ day: row.day, item: row.item_id, count: row.count, unitPrice: row.unit_price }));
}

async function writeBin(client, playerId, bin) {
  await client.query("DELETE FROM shipping_bin WHERE player_id = $1", [playerId]);
  if (!bin.length) return;
  await client.query(
    `INSERT INTO shipping_bin (player_id, day, item_id, count, unit_price)
     SELECT $1, u.day, u.item_id, u.count, u.unit_price
     FROM unnest($2::int[], $3::text[], $4::int[], $5::int[]) AS u(day, item_id, count, unit_price)`,
    [playerId, bin.map((b) => b.day), bin.map((b) => b.item), bin.map((b) => b.count), bin.map((b) => b.unitPrice)]
  );
}

async function readSaveData(client, playerId) {
  const r = await client.query("SELECT data FROM saves WHERE player_id = $1", [playerId]);
  return r.rows[0]?.data || null;
}

// Them/bot 1 vat pham (dung cho giao dich/cho) - tra false neu khong du de bot.
async function adjustItem(client, playerId, itemId, delta) {
  if (delta > 0) {
    await client.query(
      `INSERT INTO inventory_items (player_id, item_id, count) VALUES ($1, $2, $3)
       ON CONFLICT (player_id, item_id) DO UPDATE SET count = inventory_items.count + EXCLUDED.count`,
      [playerId, itemId, delta]
    );
    return true;
  }
  const need = -delta;
  // Lay het ca chong -> xoa dong (CHECK count > 0 khong cho phep cap nhat ve 0).
  const del = await client.query("DELETE FROM inventory_items WHERE player_id = $1 AND item_id = $2 AND count = $3 RETURNING 1", [playerId, itemId, need]);
  if (del.rows.length) return true;
  const r = await client.query(
    "UPDATE inventory_items SET count = count - $3 WHERE player_id = $1 AND item_id = $2 AND count > $3 RETURNING count",
    [playerId, itemId, need]
  );
  if (!r.rows.length) return false;
  return true;
}

module.exports = {
  withTransaction, lockWallet, lockWallets, createWallet, readItems, writeItems, updateWallet, addGold,
  readCounters, writeCounters, readClaims, insertClaims, readBin, writeBin, readSaveData, adjustItem,
};
