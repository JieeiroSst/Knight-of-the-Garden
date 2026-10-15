// Nhan vat tu tao: ten hien thi (duy nhat), gioi tinh, mau nhan vat theo gioi tinh, mau da/toc/trang
// phuc. Nguoi choi khac nhin thay qua gateway (presence) va giao dich/cho.
const repo = require("../repositories/characterRepository");
const { AppError } = require("../utils/AppError");

// Phai khop CharacterCatalog.cs ben Godot.
// Boi canh Trung co: chi cac kieu nhan vat thoi Trung co (xem CharacterCatalog.cs ben Godot).
// Kieu cu (hien dai: Worker/Suit/Swat/Woman_*) khong con duoc chon khi luu, nhung nhan vat da tao
// truoc day van tai binh thuong.
const MODELS = {
  male: ["Farmer", "Adventurer", "Ranger", "Knight", "Lord"],
  female: ["HuntressF", "HoodedF", "PeasantF", "KnightF"],
};

function toPublic(row) {
  if (!row) return null;
  return {
    playerId: row.player_id, displayName: row.display_name, gender: row.gender, modelId: row.model_id,
    skinColor: row.skin_color, hairColor: row.hair_color, outfitColor: row.outfit_color,
  };
}

async function get(playerId) {
  return toPublic(await repo.get(playerId));
}

async function save(playerId, body) {
  if (!MODELS[body.gender]?.includes(body.modelId)) throw new AppError(400, "Mau nhan vat khong hop voi gioi tinh.");
  const existing = await repo.findByName(body.displayName);
  if (existing && existing.player_id !== playerId) throw new AppError(409, "Ten nhan vat da co nguoi dung.");
  try {
    return toPublic(await repo.upsert(playerId, body));
  } catch (err) {
    if (err.code === "23505") throw new AppError(409, "Ten nhan vat da co nguoi dung.");
    throw err;
  }
}

async function getMany(ids) {
  return (await repo.getMany(ids)).map(toPublic);
}

async function findByName(name) {
  return toPublic(await repo.findByName(name));
}

module.exports = { get, save, getMany, findByName, MODELS };
