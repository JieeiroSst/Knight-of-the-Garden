// Middleware kiem tra JWT trong header "Authorization: Bearer <token>" - gan req.playerId neu
// hop le, tra 401 neu thieu/het han/sai.
const jwt = require("jsonwebtoken");

function requireAuth(req, res, next) {
  const header = req.headers["authorization"] || "";
  const token = header.startsWith("Bearer ") ? header.slice(7) : null;
  if (!token) return res.status(401).json({ error: "Thieu token dang nhap." });

  try {
    const payload = jwt.verify(token, process.env.JWT_SECRET);
    req.playerId = payload.playerId;
    next();
  } catch (err) {
    return res.status(401).json({ error: "Token khong hop le hoac da het han." });
  }
}

function signToken(playerId) {
  return jwt.sign({ playerId }, process.env.JWT_SECRET, {
    expiresIn: process.env.JWT_EXPIRES_IN || "30d",
  });
}

module.exports = { requireAuth, signToken };
