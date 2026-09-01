const express = require("express");
const healthController = require("../controllers/healthController");

const router = express.Router();
router.get("/", healthController.liveness);
router.get("/ready", healthController.readiness);

module.exports = router;
