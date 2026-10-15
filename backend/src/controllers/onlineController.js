// Lop HTTP mong cho cac tinh nang online (thoi gian/nhan vat/kinh te/giao dich/cho).
const time = require("../services/timeService");
const characterService = require("../services/characterService");
const economyService = require("../services/economyService");
const tradeService = require("../services/tradeService");
const marketService = require("../services/marketService");
const { asyncHandler } = require("../utils/asyncHandler");
const { AppError } = require("../utils/AppError");

const id = (req) => {
  const n = Number(req.params.id);
  if (!Number.isSafeInteger(n) || n <= 0) throw new AppError(400, "id khong hop le.");
  return n;
};

module.exports = {
  time: (_req, res) => res.json(time.now()),

  getCharacter: asyncHandler(async (req, res) => {
    const c = await characterService.get(req.playerId);
    if (!c) throw new AppError(404, "Chua tao nhan vat.");
    res.json(c);
  }),
  putCharacter: asyncHandler(async (req, res) => res.json(await characterService.save(req.playerId, req.body))),
  getProfile: asyncHandler(async (req, res) => {
    const c = await characterService.get(id(req));
    if (!c) throw new AppError(404, "Khong tim thay nguoi choi.");
    res.json(c);
  }),

  getEconomy: asyncHandler(async (req, res) => res.json(await economyService.getSnapshot(req.playerId))),
  postOps: asyncHandler(async (req, res) => res.json(await economyService.applyBatch(req.playerId, req.body.seq, req.body.ops))),

  tradeInvite: asyncHandler(async (req, res) => res.status(201).json(await tradeService.invite(req.playerId, req.body.toName))),
  tradeGet: asyncHandler(async (req, res) => res.json(await tradeService.get(req.playerId, id(req)))),
  tradeAccept: asyncHandler(async (req, res) => res.json(await tradeService.accept(req.playerId, id(req)))),
  tradeOffer: asyncHandler(async (req, res) => res.json(await tradeService.setOffer(req.playerId, id(req), req.body))),
  tradeLock: asyncHandler(async (req, res) => res.json(await tradeService.lock(req.playerId, id(req)))),
  tradeConfirm: asyncHandler(async (req, res) => res.json(await tradeService.confirm(req.playerId, id(req)))),
  tradeCancel: asyncHandler(async (req, res) => res.json(await tradeService.cancel(req.playerId, id(req)))),

  marketSearch: asyncHandler(async (req, res) => {
    const { itemId, q, sort } = req.query;
    res.json(await marketService.search({
      itemId: typeof itemId === "string" ? itemId.slice(0, 60) : undefined,
      q: typeof q === "string" ? q.slice(0, 40) : undefined,
      sort: sort === "recent" ? "recent" : "price",
      page: Math.min(50, Number(req.query.page) || 0),
    }));
  }),
  marketMine: asyncHandler(async (req, res) => res.json(await marketService.mine(req.playerId))),
  marketCreate: asyncHandler(async (req, res) => res.status(201).json(await marketService.createListing(req.playerId, req.body))),
  marketBuy: asyncHandler(async (req, res) => res.json(await marketService.buy(req.playerId, id(req)))),
  marketCancel: asyncHandler(async (req, res) => res.json(await marketService.cancel(req.playerId, id(req)))),
};
