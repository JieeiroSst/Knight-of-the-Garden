const express = require("express");
const authController = require("../controllers/authController");
const { validateBody } = require("../middleware/validate");
const { credentialsSchema } = require("../validation/schemas");
const { authLimiter } = require("../middleware/rateLimiters");

const router = express.Router();

router.post("/register", authLimiter, validateBody(credentialsSchema), authController.register);
router.post("/login", authLimiter, validateBody(credentialsSchema), authController.login);

module.exports = router;
