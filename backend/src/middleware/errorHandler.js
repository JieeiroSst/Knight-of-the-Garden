const { logger } = require("../utils/logger");

// Noi DUY NHAT xu ly loi cho toan bo app - controller/service CHI can throw (hoac next(err)),
// KHONG tu goi res.status().json() rieng le nua. Phan biet loi "da biet truoc" (AppError,
// isOperational=true, vd sai mat khau/thieu du lieu) - an toan de tra THANG message cho client -
// voi loi NGOAI DU KIEN (bug/mat ket noi DB...) - CHI tra thong bao chung, tranh lo chi tiet noi
// bo, nhung log DAY DU (ca stack trace) de con dieu tra.
// eslint-disable-next-line no-unused-vars
function errorHandler(err, req, res, _next) {
  const statusCode = err.statusCode || 500;
  const isOperational = err.isOperational === true;

  if (!isOperational || statusCode >= 500) {
    logger.error({ err, path: req.path, method: req.method }, "Loi khong mong doi");
  } else {
    logger.warn({ path: req.path, method: req.method, statusCode, message: err.message }, "Loi xu ly duoc");
  }

  res.status(statusCode).json({
    error: isOperational ? err.message : "Loi may chu, thu lai sau.",
    ...(isOperational && err.details ? { details: err.details } : {}),
  });
}

module.exports = { errorHandler };
