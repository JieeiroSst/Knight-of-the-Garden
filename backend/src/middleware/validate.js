const { AppError } = require("../utils/AppError");

// Factory: tra ve 1 middleware validate req.body theo 1 schema zod cu the (xem validation/schemas.js).
// Ghi de req.body bang du lieu DA CHUAN HOA (vd username da .trim()) de controller phia sau dung
// truc tiep, khong can tu xu ly lai.
function validateBody(schema) {
  return (req, _res, next) => {
    const result = schema.safeParse(req.body);
    if (!result.success) {
      const details = result.error.issues.map((issue) => `${issue.path.join(".") || "body"}: ${issue.message}`);
      return next(new AppError(400, "Du lieu khong hop le.", details));
    }
    req.body = result.data;
    next();
  };
}

module.exports = { validateBody };
