const { logger } = require("../utils/logger");

// Log 1 dong CO CAU TRUC cho MOI request sau khi tra response xong (method/path/status/thoi
// gian xu ly) - de doi chieu voi log loi khi can dieu tra su co.
function requestLogger(req, res, next) {
  const startedAt = Date.now();
  res.on("finish", () => {
    logger.info(
      { method: req.method, path: req.path, status: res.statusCode, ms: Date.now() - startedAt },
      "request"
    );
  });
  next();
}

module.exports = { requestLogger };
