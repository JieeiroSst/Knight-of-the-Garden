-- Migration 001: schema khoi tao.
-- Luu CA khoi SaveData (vang/tui do/nhiem vu/hop dong/trang thai nong trai) thanh 1 cot JSONB
-- DUY NHAT thay vi tach nhieu bang quan he - game don nguoi choi, khong can truy van cheo giua
-- nhieu nguoi choi tren tung muc du lieu con, nen giu nguyen cau truc JSON hien co (tu
-- SaveSystem.SaveData ben Godot) la lua chon don gian/an toan nhat khi chuyen tu file sang
-- database that.

CREATE TABLE IF NOT EXISTS players (
  id SERIAL PRIMARY KEY,
  username TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS saves (
  player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
  data JSONB NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
