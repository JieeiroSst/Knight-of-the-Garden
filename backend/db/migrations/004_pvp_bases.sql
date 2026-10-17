-- Migration 004: PvP the gioi mo + can cu (kieu game sinh ton).
--  loot_bags            : tui do roi ra khi bi nguoi choi khac ha (hoac khi ruong can cu bi pha) -
--                         nguoi ha duoc uu tien nhat trong vai chuc giay, sau do ai cung nhat duoc.
--  base_structures      : cong trinh nguoi choi xay o the gioi mo (tuong, leu, ruong, lua trai) -
--                         moi nguoi trong vung deu thay, nguoi khac danh pha duoc (mau luu o day).
--  base_chest_items     : do cat trong ruong can cu (pha ruong -> do roi thanh tui).

CREATE TABLE IF NOT EXISTS loot_bags (
  id BIGSERIAL PRIMARY KEY,
  zone TEXT NOT NULL,
  channel INTEGER NOT NULL,
  x REAL NOT NULL,
  z REAL NOT NULL,
  owner_id INTEGER REFERENCES players(id) ON DELETE SET NULL,   -- nguoi bi mat do
  killer_id INTEGER REFERENCES players(id) ON DELETE SET NULL,  -- nguoi duoc uu tien nhat
  owner_name TEXT NOT NULL DEFAULT '',
  items JSONB NOT NULL DEFAULT '{}'::jsonb,
  gold INTEGER NOT NULL DEFAULT 0 CHECK (gold >= 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  priority_until TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL,
  looted_by INTEGER REFERENCES players(id) ON DELETE SET NULL,
  looted_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS loot_bags_active ON loot_bags (zone, channel, expires_at) WHERE looted_at IS NULL;

CREATE TABLE IF NOT EXISTS base_structures (
  id BIGSERIAL PRIMARY KEY,
  owner_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  kind TEXT NOT NULL,
  zone TEXT NOT NULL,
  x REAL NOT NULL,
  z REAL NOT NULL,
  rot REAL NOT NULL DEFAULT 0,
  hp INTEGER NOT NULL CHECK (hp >= 0),
  max_hp INTEGER NOT NULL CHECK (max_hp > 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS base_structures_area ON base_structures (zone, x, z);
CREATE INDEX IF NOT EXISTS base_structures_owner ON base_structures (owner_id);

CREATE TABLE IF NOT EXISTS base_chest_items (
  structure_id BIGINT NOT NULL REFERENCES base_structures(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  count INTEGER NOT NULL CHECK (count > 0),
  PRIMARY KEY (structure_id, item_id)
);
