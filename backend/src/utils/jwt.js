const jwt = require("jsonwebtoken");
const { env } = require("../config/env");

function signToken(playerId) {
  return jwt.sign({ playerId }, env.JWT_SECRET, { expiresIn: env.JWT_EXPIRES_IN });
}

function verifyToken(token) {
  return jwt.verify(token, env.JWT_SECRET);
}

module.exports = { signToken, verifyToken };
