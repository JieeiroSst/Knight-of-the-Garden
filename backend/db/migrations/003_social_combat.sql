-- Migration 003: xa hoi + chien dau + hang ngay.
--  friendships       : ket ban (a_id < b_id, 1 dong/cap), trang thai cho/da la ban
--  guilds/...        : bang hoi, thanh vien, loi moi, kho chung
--  farm_helps        : ban be tuoi cay ho khi tham nong trai (chu nong trai nhan o lan dong bo sau)
--  daily_logins      : diem danh chuoi ngay
--  quest_claims      : da nhan thuong nhiem vu ngay/tuan nao
--  player_stats      : chi so cho bang xep hang (client bao, server chan tang qua nhanh)
--  pvp_ratings       : diem Elo dau truong
--  item_enhancements : cap cuong hoa trang bi (+0..+10) - server quyet dinh, dung trong PvP

CREATE TABLE IF NOT EXISTS friendships (
  a_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  b_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  status TEXT NOT NULL CHECK (status IN ('pending', 'accepted')),
  requested_by INTEGER NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (a_id, b_id),
  CHECK (a_id < b_id)
);
CREATE INDEX IF NOT EXISTS friendships_b ON friendships (b_id);

CREATE TABLE IF NOT EXISTS guilds (
  id SERIAL PRIMARY KEY,
  name TEXT NOT NULL,
  tag TEXT NOT NULL,
  leader_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS guilds_name_lower ON guilds (lower(name));

CREATE TABLE IF NOT EXISTS guild_members (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  guild_id INTEGER NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
  role TEXT NOT NULL CHECK (role IN ('leader', 'officer', 'member')),
  joined_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS guild_members_guild ON guild_members (guild_id);

CREATE TABLE IF NOT EXISTS guild_invites (
  guild_id INTEGER NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (player_id, guild_id)
);

CREATE TABLE IF NOT EXISTS guild_storage (
  guild_id INTEGER NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  count INTEGER NOT NULL CHECK (count > 0),
  PRIMARY KEY (guild_id, item_id)
);

CREATE TABLE IF NOT EXISTS farm_helps (
  host_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  plot_key TEXT NOT NULL,
  day INTEGER NOT NULL,
  helper_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  applied BOOLEAN NOT NULL DEFAULT false,
  PRIMARY KEY (host_id, day, plot_key)
);

CREATE TABLE IF NOT EXISTS daily_logins (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  last_day INTEGER NOT NULL,
  streak INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS quest_claims (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  period_key TEXT NOT NULL,     -- "d<ngay>" hoac "w<tuan>"
  idx INTEGER NOT NULL,
  PRIMARY KEY (player_id, period_key, idx)
);

CREATE TABLE IF NOT EXISTS player_stats (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  stat TEXT NOT NULL,
  value BIGINT NOT NULL DEFAULT 0,
  updated_day INTEGER NOT NULL DEFAULT 0,
  day_gain BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (player_id, stat)
);
CREATE INDEX IF NOT EXISTS player_stats_rank ON player_stats (stat, value DESC);

CREATE TABLE IF NOT EXISTS pvp_ratings (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  rating INTEGER NOT NULL DEFAULT 1000,
  wins INTEGER NOT NULL DEFAULT 0,
  losses INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS pvp_ratings_rank ON pvp_ratings (rating DESC);

CREATE TABLE IF NOT EXISTS item_enhancements (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  item_id TEXT NOT NULL,
  level INTEGER NOT NULL CHECK (level BETWEEN 0 AND 10),
  PRIMARY KEY (player_id, item_id)
);

CREATE INDEX IF NOT EXISTS wallets_gold_rank ON wallets (gold DESC);
