-- Migration 005: hoi sinh bang giuong.
--  player_respawn.bed_lost : giuong CUOI CUNG cua nguoi choi vua bi pha -> lan chet ke tiep phai doi
--                            1 gio moi hoi sinh duoc o nong trai (xoa co khi dung giuong moi / da doi xong).
--  player_respawn.ready_at : dang cho hoi sinh toi thoi diem nay (NULL = khong cho). Luu DB de thoat
--                            game/dang nhap lai van phai doi tiep, khong lach duoc.

CREATE TABLE IF NOT EXISTS player_respawn (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  bed_lost BOOLEAN NOT NULL DEFAULT false,
  ready_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS base_structures_owner_kind ON base_structures (owner_id, kind);
