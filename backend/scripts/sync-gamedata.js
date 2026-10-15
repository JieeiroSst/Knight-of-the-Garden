// Sao chep du lieu game DUNG CHUNG (data/*.json o goc repo - nguon su that, Godot cung doc) vao
// backend/gamedata/ de server kiem tra kinh te (gia mua/ban, cong thuc, bang roi do...). Docker
// build context chi la thu muc backend/ nen phai co ban sao ben trong. Chay lai moi khi sua data/:
//   npm run sync-gamedata
// Test "gamedata khop data/" (tests/gamedata.test.js) se bao loi neu quen chay.
const fs = require("fs");
const path = require("path");

const FILES = ["items.json", "recipes.json", "cooking.json", "enemies.json", "achievements.json", "house.json", "starter.json", "fish.json", "forage.json"];
const SRC = path.join(__dirname, "..", "..", "data");
const DST = path.join(__dirname, "..", "gamedata");

// Chi sao chep khi chay truc tiep (npm run sync-gamedata) - test chi doc FILES/SRC/DST de so sanh.
if (require.main === module) {
  fs.mkdirSync(DST, { recursive: true });
  for (const f of FILES) {
    fs.copyFileSync(path.join(SRC, f), path.join(DST, f));
    console.log(`synced ${f}`);
  }
}
module.exports = { FILES, SRC, DST };
