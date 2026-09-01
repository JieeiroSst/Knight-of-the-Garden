// Noi DUY NHAT gan duong dan URL -> router con - giu nguyen dung 3 nhom URL nhu ban goc
// (GET /health, /health/ready, /api/register, /api/login, GET+PUT /api/save) de code Godot
// (BackendClient.cs) KHONG can sua gi khi backend duoc viet lai kien truc lop nay.
const express = require("express");
const authRoutes = require("./authRoutes");
const saveRoutes = require("./saveRoutes");
const healthRoutes = require("./healthRoutes");

const router = express.Router();

router.use("/health", healthRoutes);
router.use("/api", authRoutes);
router.use("/api/save", saveRoutes);

module.exports = router;
