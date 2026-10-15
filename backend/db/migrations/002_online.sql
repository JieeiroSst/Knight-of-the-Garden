-- Migration 002: game online nhieu nguoi choi.
--  - characters: nhan vat tu tao (ten hien thi, gioi tinh, ngoai hinh) - nguoi khac nhin thay.
--  - wallets + inventory_items: KINH TE DO SERVER QUAN LY (vang + vat pham la nguon su that tren
--    server; client chi gui "y dinh" qua /api/economy/ops, server kiem tra roi moi ap dung).
--  - shipping_bin: thung giao hang phia server (tu ban khi sang ngay game moi).
--  - economy_counters / reward_claims: gioi han toc do nhan do theo nguon + phan thuong 1 lan.
--  - trades / market_listings: trao doi truc tiep va cho mua ban giua nguoi choi.
-- Moi bang du lieu nguoi choi deu co player_id dung dau khoa chinh -> san sang chia shard theo
-- player_id (vd Citus distribution column) khi len quy mo lon (xem README backend).

CREATE TABLE IF NOT EXISTS characters (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  display_name TEXT NOT NULL,
  gender TEXT NOT NULL CHECK (gender IN ('male', 'female')),
  model_id TEXT NOT NULL,
  skin_color TEXT NOT NULL,
  hair_color TEXT NOT NULL,
  outfit_color TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS characters_display_name_lower ON characters (lower(display_name));

CREATE TABLE IF NOT EXISTS wallets (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  gold BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
  house_level INTEGER NOT NULL DEFAULT 0,
  last_batch_seq BIGINT NOT NULL DEFAULT 0,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS inventory_items (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  count INTEGER NOT NULL CHECK (count > 0),
  PRIMARY KEY (player_id, item_id)
);

CREATE TABLE IF NOT EXISTS shipping_bin (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  day INTEGER NOT NULL,
  item_id TEXT NOT NULL,
  count INTEGER NOT NULL CHECK (count > 0),
  unit_price INTEGER NOT NULL CHECK (unit_price >= 0),
  PRIMARY KEY (player_id, day, item_id)
);

CREATE TABLE IF NOT EXISTS economy_counters (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  bucket BIGINT NOT NULL,          -- gio (epoch ms / 3600000)
  source TEXT NOT NULL,
  amount BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (player_id, bucket, source)
);

CREATE TABLE IF NOT EXISTS reward_claims (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  claim_key TEXT NOT NULL,
  claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (player_id, claim_key)
);

CREATE TABLE IF NOT EXISTS trades (
  id BIGSERIAL PRIMARY KEY,
  a_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  b_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'open', 'done', 'cancelled')),
  a_offer JSONB NOT NULL DEFAULT '{"gold":0,"items":{}}',
  b_offer JSONB NOT NULL DEFAULT '{"gold":0,"items":{}}',
  a_locked BOOLEAN NOT NULL DEFAULT false,
  b_locked BOOLEAN NOT NULL DEFAULT false,
  a_confirmed BOOLEAN NOT NULL DEFAULT false,
  b_confirmed BOOLEAN NOT NULL DEFAULT false,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS trades_a_active ON trades (a_id) WHERE status IN ('pending', 'open');
CREATE INDEX IF NOT EXISTS trades_b_active ON trades (b_id) WHERE status IN ('pending', 'open');

CREATE TABLE IF NOT EXISTS market_listings (
  id BIGSERIAL PRIMARY KEY,
  seller_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  count INTEGER NOT NULL CHECK (count > 0),
  price BIGINT NOT NULL CHECK (price > 0),       -- tong gia cho ca lo
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'sold', 'cancelled', 'expired')),
  buyer_id INTEGER REFERENCES players(id) ON DELETE SET NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL,
  closed_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS market_active_item ON market_listings (item_id, price) WHERE status = 'active';
CREATE INDEX IF NOT EXISTS market_active_recent ON market_listings (created_at DESC) WHERE status = 'active';
CREATE INDEX IF NOT EXISTS market_seller ON market_listings (seller_id, status);
