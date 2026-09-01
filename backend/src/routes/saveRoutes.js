const express = require("express");
const saveController = require("../controllers/saveController");
const { requireAuth } = require("../middleware/auth");
const { validateBody } = require("../middleware/validate");
const { saveSchema } = require("../validation/schemas");

const router = express.Router();
router.use(requireAuth);

router.get("/", saveController.getSave);
router.put("/", validateBody(saveSchema), saveController.putSave);

module.exports = router;
