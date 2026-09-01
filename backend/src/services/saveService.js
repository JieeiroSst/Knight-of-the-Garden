const saveRepository = require("../repositories/saveRepository");
const { AppError } = require("../utils/AppError");

async function getSave(playerId) {
  const row = await saveRepository.get(playerId);
  if (!row) throw new AppError(404, "Chua co du lieu luu.");
  return { data: row.data, updatedAt: row.updated_at };
}

async function putSave(playerId, data) {
  await saveRepository.upsert(playerId, data);
}

module.exports = { getSave, putSave };
