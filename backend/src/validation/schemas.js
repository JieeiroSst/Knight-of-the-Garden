// Validate DU LIEU DAU VAO bang zod (thay vi kiem tra tay tung field trong controller) - loi
// validate tra ve THONG NHAT qua middleware/validate.js, thong bao ro RIENG tung truong sai.
const { z } = require("zod");

const credentialsSchema = z.object({
  username: z
    .string({ required_error: "Thieu username." })
    .trim()
    .min(3, "Username phai co it nhat 3 ky tu.")
    .max(32, "Username toi da 32 ky tu."),
  password: z
    .string({ required_error: "Thieu password." })
    .min(6, "Password phai co it nhat 6 ky tu.")
    .max(200, "Password toi da 200 ky tu."),
});

const saveSchema = z.object({
  data: z.unknown().refine((v) => v !== undefined && v !== null, "Thieu du lieu luu."),
});

module.exports = { credentialsSchema, saveSchema };
