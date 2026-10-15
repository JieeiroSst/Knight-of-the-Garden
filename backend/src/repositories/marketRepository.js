const { pool } = require("../db/pool");

async function insert(client, sellerId, itemId, count, price, days) {
  const r = await client.query(
    `INSERT INTO market_listings (seller_id, item_id, count, price, expires_at)
     VALUES ($1, $2, $3, $4, now() + make_interval(days => $5)) RETURNING *`,
    [sellerId, itemId, count, price, days]
  );
  return r.rows[0];
}

async function countActive(client, sellerId) {
  const r = await client.query("SELECT count(*)::int AS n FROM market_listings WHERE seller_id = $1 AND status = 'active'", [sellerId]);
  return r.rows[0].n;
}

async function lockById(client, id) {
  const r = await client.query("SELECT * FROM market_listings WHERE id = $1 FOR UPDATE", [id]);
  return r.rows[0] || null;
}

async function close(client, id, status, buyerId = null) {
  await client.query("UPDATE market_listings SET status = $2, buyer_id = $3, closed_at = now() WHERE id = $1", [id, status, buyerId]);
}

// Tin het han cua 1 nguoi ban: danh dau expired va tra ve de hoan vat pham (goi trong transaction
// cua economyService - nguoi ban nhan lai do o lan dong bo ke tiep).
async function expireForSeller(client, sellerId) {
  const r = await client.query(
    `UPDATE market_listings SET status = 'expired', closed_at = now()
     WHERE seller_id = $1 AND status = 'active' AND expires_at <= now() RETURNING item_id, count`,
    [sellerId]
  );
  return r.rows;
}

async function search({ itemIds, sort, limit, offset }) {
  const order = sort === "recent" ? "l.created_at DESC" : "l.price::float / l.count ASC, l.created_at ASC";
  const params = [limit, offset];
  let where = "l.status = 'active' AND l.expires_at > now()";
  if (itemIds) {
    params.push(itemIds);
    where += ` AND l.item_id = ANY($${params.length})`;
  }
  const r = await pool.query(
    `SELECT l.id, l.seller_id, c.display_name AS seller_name, l.item_id, l.count, l.price, l.created_at, l.expires_at
     FROM market_listings l LEFT JOIN characters c ON c.player_id = l.seller_id
     WHERE ${where} ORDER BY ${order} LIMIT $1 OFFSET $2`,
    params
  );
  return r.rows;
}

async function listMine(sellerId) {
  const r = await pool.query(
    `SELECT id, item_id, count, price, status, created_at, expires_at, closed_at FROM market_listings
     WHERE seller_id = $1 AND (status = 'active' OR closed_at > now() - interval '7 days')
     ORDER BY created_at DESC LIMIT 100`,
    [sellerId]
  );
  return r.rows;
}

module.exports = { insert, countActive, lockById, close, expireForSeller, search, listMine };
