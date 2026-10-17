-- Migration 006: han su dung (thit/nong san/mon an) + do ben (vu khi/giap/nong cu) - xem src/game/itemState.js.
--  item_states        : trang thai theo loai vat pham cua nguoi choi { fresh: [[hetHanMs, n]...], dur: [doBen...] }
--  *.state/item_state : trang thai DI THEO do khi nam trong kho/cho/ruong/tui roi (cat vao ruong khong lam
--                       thit tuoi lai, chuyen kiem cun cho nguoi khac khong thanh kiem moi).
CREATE TABLE IF NOT EXISTS item_states (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  state JSONB NOT NULL,
  PRIMARY KEY (player_id, item_id)
);

ALTER TABLE market_listings ADD COLUMN IF NOT EXISTS item_state JSONB;
ALTER TABLE guild_storage ADD COLUMN IF NOT EXISTS state JSONB;
ALTER TABLE base_chest_items ADD COLUMN IF NOT EXISTS state JSONB;
ALTER TABLE loot_bags ADD COLUMN IF NOT EXISTS states JSONB NOT NULL DEFAULT '{}'::jsonb;
