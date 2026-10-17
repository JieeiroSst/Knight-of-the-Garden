// Lop HTTP mong cho cac tinh nang online (thoi gian/nhan vat/kinh te/giao dich/cho).
const time = require("../services/timeService");
const characterService = require("../services/characterService");
const economyService = require("../services/economyService");
const tradeService = require("../services/tradeService");
const marketService = require("../services/marketService");
const { asyncHandler } = require("../utils/asyncHandler");
const farmService = require("../services/farmService");
const bountyService = require("../services/bountyService");
const { pool } = require("../db/pool");
const { AppError } = require("../utils/AppError");

const id = (req) => {
  const n = Number(req.params.id);
  if (!Number.isSafeInteger(n) || n <= 0) throw new AppError(400, "id khong hop le.");
  return n;
};

module.exports = {
  bounties: asyncHandler(async (req, res) => res.json({ top: await bountyService.top(30), me: await bountyService.of(req.playerId) })),
  placeBounty: asyncHandler(async (req, res) => {
    let targetId = req.body.targetId;
    if (!targetId) {
      const r = await pool.query("SELECT player_id FROM characters WHERE lower(display_name) = lower($1) LIMIT 1", [req.body.targetName]);
      if (!r.rows.length) throw new AppError(404, "Khong tim thay nguoi choi.");
      targetId = r.rows[0].player_id;
    }
    res.json(await bountyService.place(req.playerId, targetId, req.body.amount));
  }),
  myFarm: asyncHandler(async (req, res) => res.json(await farmService.ensureFarm(req.playerId))),
  farmsNear: asyncHandler(async (req, res) => {
    const x = Number(req.query.x), z = Number(req.query.z), r = Number(req.query.r) || 200000;
    if (!Number.isFinite(x) || !Number.isFinite(z)) return res.status(400).json({ error: "Thieu toa do." });
    res.json(await farmService.farmsNear(x, z, r));
  }),
  farmView: asyncHandler(async (req, res) => {
    const id = Number(req.params.id);
    if (!Number.isInteger(id)) return res.status(400).json({ error: "id khong hop le." });
    res.json(await farmService.publicView(id));
  }),
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
