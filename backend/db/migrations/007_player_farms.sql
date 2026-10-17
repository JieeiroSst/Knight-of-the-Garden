-- Migration 007: moi nguoi choi 1 nong trai tai TOA DO CO DINH rieng trong the gioi chung (xem farmService).
-- Luoi o cach nhau FARM_SPACING (5 km), cap theo vong xoan oc tu trung tam ra ngoai, bo o (0,0) (khu chung:
-- thi tran, 11 khu vuc, cho...). UNIQUE (gx, gz) -> 2 nguoi khong bao gio trung 1 o, ke ca dang ky cung luc.
CREATE TABLE IF NOT EXISTS player_farms (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  gx INTEGER NOT NULL,
  gz INTEGER NOT NULL,
  x REAL NOT NULL,
  z REAL NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (gx, gz)
);
CREATE INDEX IF NOT EXISTS player_farms_xz ON player_farms (x, z);
