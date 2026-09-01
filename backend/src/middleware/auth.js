// Middleware kiem tra JWT trong header "Authorization: Bearer <token>" - gan req.playerId neu
// hop le, chuyen loi toi errorHandler.js (qua next) neu thieu/het han/sai.
const { verifyToken } = require("../utils/jwt");
const { AppError } = require("../utils/AppError");

function requireAuth(req, _res, next) {
  const header = req.headers["authorization"] || "";
  const token = header.startsWith("Bearer ") ? header.slice(7) : null;
  if (!token) return next(new AppError(401, "Thieu token dang nhap."));

  try {
    const payload = verifyToken(token);
    req.playerId = payload.playerId;
    next();
  } catch (err) {
    next(new AppError(401, "Token khong hop le hoac da het han."));
  }
}

module.exports = { requireAuth };
