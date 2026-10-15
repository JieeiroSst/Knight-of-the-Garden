const express = require("express");
const c = require("../controllers/socialController");
const { requireAuth } = require("../middleware/auth");
const { validateBody } = require("../middleware/validate");
const s = require("../validation/schemas");

const router = express.Router();
router.use(requireAuth);

router.get("/friends", c.friends);
router.post("/friends", validateBody(s.nameSchema), c.addFriend);
router.post("/friends/:id/accept", c.acceptFriend);
router.delete("/friends/:id", c.removeFriend);

router.get("/guild", c.myGuild);
router.post("/guild", validateBody(s.guildCreateSchema), c.createGuild);
router.post("/guild/invite", validateBody(s.nameSchema), c.inviteGuild);
router.post("/guild/:id/join", c.acceptGuild);
router.post("/guild/leave", c.leaveGuild);
router.post("/guild/members/:id/kick", c.kick);
router.post("/guild/members/:id/role", validateBody(s.roleSchema), c.promote);
router.post("/guild/storage/deposit", validateBody(s.itemCountSchema), c.deposit);
router.post("/guild/storage/withdraw", validateBody(s.itemCountSchema), c.withdraw);

router.get("/visits/:id/farm", c.friendFarm);
router.post("/visits/:id/water", validateBody(s.helpSchema), c.helpWater);
router.post("/visits/helps/take", c.takeHelps);

router.get("/daily", c.daily);
router.post("/daily/checkin", c.checkin);
router.post("/daily/claim", validateBody(s.claimSchema), c.claim);
router.post("/stats", validateBody(s.statsSchema), c.reportStats);
router.get("/leaderboard/:stat", c.leaderboard);

router.get("/forge", c.enhancements);
router.post("/forge/enhance", validateBody(s.enhanceSchema), c.enhance);

module.exports = router;
