// Lop HTTP MONG: doc req (da duoc validate/chuan hoa boi middleware/validate.js), goi service,
// dinh dang response - KHONG chua logic nghiep vu (bam mat khau, kiem tra trung username...),
// nhung thu do thuoc ve services/authService.js.
const authService = require("../services/authService");
const { asyncHandler } = require("../utils/asyncHandler");

const register = asyncHandler(async (req, res) => {
  const { username, password } = req.body;
  const token = await authService.register(username, password);
  res.status(201).json({ token });
});

const login = asyncHandler(async (req, res) => {
  const { username, password } = req.body;
  const token = await authService.login(username, password);
  res.json({ token });
});

module.exports = { register, login };
