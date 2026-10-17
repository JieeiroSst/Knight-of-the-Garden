// Kho chua do GOP theo loai (kho bang hoi, ruong can cu): cot count + state (han dung/do ben - xem
// game/itemState.js). Them -> gop trang thai mang theo (carry); lay -> tra ve trang thai phan lay ra.
const itemState = require("../game/itemState");
const { catalog } = require("../game/catalog");

// table/cot khoa la HANG SO trong code (khong tu input nguoi dung).
async function adjustContainer(client, table, keyCols, keyVals, itemId, delta, carry = null) {
  const def = catalog.items.get(itemId);
  const tracked = itemState.tracked(def);
  const params = [...keyVals, itemId];
  const where = `${keyCols.map((c, i) => `${c} = $${i + 1}`).join(" AND ")} AND item_id = $${keyCols.length + 1}`;
  const cur = await client.query(`SELECT count, state FROM ${table} WHERE ${where} FOR UPDATE`, params);
  const before = cur.rows[0]?.count || 0;
  if (delta < 0 && before < -delta) return false;
  const now = Date.now();
  let st = cur.rows[0]?.state || null;
  let removed = {};
  if (tracked) {
    if (delta > 0) st = itemState.give(st, before, delta, carry, def, now);
    else { const r = itemState.take(st, before, -delta, def, now); st = r.state; removed = r.removed || {}; }
  }
  const after = before + delta;
  const stJson = tracked && st ? JSON.stringify(st) : null;
  const n = params.length;
  if (after <= 0) await client.query(`DELETE FROM ${table} WHERE ${where}`, params);
  else if (cur.rows.length) await client.query(`UPDATE ${table} SET count = $${n + 1}, state = $${n + 2} WHERE ${where}`, [...params, after, stJson]);
  else
    await client.query(
      `INSERT INTO ${table} (${keyCols.join(", ")}, item_id, count, state) VALUES (${params.map((_, i) => `$${i + 1}`).join(", ")}, $${n + 1}, $${n + 2})`,
      [...params, after, stJson]);
  return delta > 0 ? true : removed;
}

module.exports = { adjustContainer };
