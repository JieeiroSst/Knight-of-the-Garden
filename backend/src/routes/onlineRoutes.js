const express = require("express");
const c = require("../controllers/onlineController");
const { requireAuth } = require("../middleware/auth");
const { validateBody } = require("../middleware/validate");
const s = require("../validation/schemas");

const router = express.Router();

// Cong khai (khong can dang nhap) - client dong bo dong ho game ngay o man hinh dang nhap.
router.get("/time", c.time);

router.use(requireAuth);
router.get("/character", c.getCharacter);
router.put("/character", validateBody(s.characterSchema), c.putCharacter);
router.get("/players/:id/profile", c.getProfile);

router.get("/economy", c.getEconomy);
router.post("/economy/ops", validateBody(s.economyBatchSchema), c.postOps);

router.post("/trades", validateBody(s.tradeInviteSchema), c.tradeInvite);
router.get("/trades/:id", c.tradeGet);
router.post("/trades/:id/accept", c.tradeAccept);
router.put("/trades/:id/offer", validateBody(s.tradeOfferSchema), c.tradeOffer);
router.post("/trades/:id/lock", c.tradeLock);
router.post("/trades/:id/confirm", c.tradeConfirm);
router.post("/trades/:id/cancel", c.tradeCancel);

router.get("/market/listings", c.marketSearch);
router.get("/market/mine", c.marketMine);
router.post("/market/listings", validateBody(s.listingSchema), c.marketCreate);
router.post("/market/listings/:id/buy", c.marketBuy);
router.delete("/market/listings/:id", c.marketCancel);

module.exports = router;
