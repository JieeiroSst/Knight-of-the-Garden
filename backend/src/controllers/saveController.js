const saveService = require("../services/saveService");
const { asyncHandler } = require("../utils/asyncHandler");

const getSave = asyncHandler(async (req, res) => {
  const result = await saveService.getSave(req.playerId);
  res.json(result);
});

const putSave = asyncHandler(async (req, res) => {
  await saveService.putSave(req.playerId, req.body.data);
  res.json({ ok: true });
});

module.exports = { getSave, putSave };
