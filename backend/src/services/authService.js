// Logic NGHIEP VU dang ky/dang nhap (bam mat khau, ky JWT) - tach khoi controller (lop HTTP) va
// repository (lop SQL), de test duoc doc lap va tai su dung neu sau nay them duong vao khac
// (vd dang nhap qua OAuth) ma van dung chung logic tao token.
const bcrypt = require("bcryptjs");
const playerRepository = require("../repositories/playerRepository");
const { signToken } = require("../utils/jwt");
const { AppError } = require("../utils/AppError");
const { logger } = require("../utils/logger");

const BCRYPT_ROUNDS = 10;

async function register(username, password) {
  const passwordHash = await bcrypt.hash(password, BCRYPT_ROUNDS);

  let playerId;
  try {
    playerId = await playerRepository.create(username, passwordHash);
  } catch (err) {
    if (err.code === "23505") throw new AppError(409, "Username da ton tai.");
    throw err;
  }

  logger.info({ playerId }, "Nguoi choi moi dang ky");
  return signToken(playerId);
}

async function login(username, password) {
  const player = await playerRepository.findByUsername(username);
  if (!player) throw new AppError(401, "Sai username hoac password.");

  const match = await bcrypt.compare(password, player.password_hash);
  if (!match) throw new AppError(401, "Sai username hoac password.");

  return signToken(player.id);
}

module.exports = { register, login };
