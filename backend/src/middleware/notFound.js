const { AppError } = require("../utils/AppError");

// PHAI dat SAU tat ca route hop le - bat moi duong dan khong khop thanh 1 loi 404 THONG NHAT
// (di qua errorHandler.js) thay vi Express tra HTML mac dinh xau.
function notFound(req, _res, next) {
  next(new AppError(404, `Khong tim thay: ${req.method} ${req.path}`));
}

module.exports = { notFound };
