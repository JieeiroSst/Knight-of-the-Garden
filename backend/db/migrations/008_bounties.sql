-- Migration 008: LENH TRUY NA. Ai ha nguoi choi vo toi (khong bi truy na) bi he thong treo thuong (tang
-- dan theo so lan); nguoi choi khac co the gop vang treo them. Ha duoc ke bi truy na -> nhan het tien thuong.
CREATE TABLE IF NOT EXISTS bounties (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  amount BIGINT NOT NULL DEFAULT 0 CHECK (amount >= 0),
  kills INTEGER NOT NULL DEFAULT 0,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS bounties_amount ON bounties (amount DESC) WHERE amount > 0;
