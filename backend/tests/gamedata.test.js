// Ban sao gamedata/ (backend dung de kiem tra kinh te) phai khop data/ (Godot dung) - neu lech,
// server se kiem tra gia/cong thuc theo du lieu cu. Sua: npm run sync-gamedata
const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");
const { FILES, SRC, DST } = require("../scripts/sync-gamedata.js");

test("gamedata/ khop voi data/ o goc repo", { skip: !fs.existsSync(SRC) && "khong co data/ (build Docker)" }, () => {
  for (const f of FILES) {
    assert.equal(fs.readFileSync(path.join(DST, f), "utf8"), fs.readFileSync(path.join(SRC, f), "utf8"), `${f} lech - chay npm run sync-gamedata`);
  }
});
