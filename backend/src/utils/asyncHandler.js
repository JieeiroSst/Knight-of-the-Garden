// Express 4 KHONG tu bat Promise bi reject trong route handler async - neu khong wrap, 1 loi
// (vd query DB that bai) se lam request "treo" mai khong bao gio tra response. Boc handler bang
// ham nay de moi loi tu dong duoc chuyen toi next(err) -> errorHandler.js.
const asyncHandler = (fn) => (req, res, next) => Promise.resolve(fn(req, res, next)).catch(next);

module.exports = { asyncHandler };
