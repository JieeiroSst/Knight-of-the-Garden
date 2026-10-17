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

const hexColor = z.string().regex(/^#?[0-9a-fA-F]{6}$/, "Mau phai dang hex 6 ky tu.");

const characterSchema = z.object({
  displayName: z
    .string()
    .trim()
    .min(3, "Ten nhan vat it nhat 3 ky tu.")
    .max(20, "Ten nhan vat toi da 20 ky tu.")
    // Chu cai (ca tieng Viet co dau), so, khoang trang, gach duoi - chan ky tu dieu khien/HTML.
    .regex(/^[\p{L}\p{N} _]+$/u, "Ten chi gom chu, so, khoang trang va dau gach duoi."),
  gender: z.enum(["male", "female"]),
  modelId: z.string().min(1).max(40),
  skinColor: hexColor,
  hairColor: hexColor,
  outfitColor: hexColor,
});

// 1 op kinh te - chi kiem tra HINH DANG o day; luat that nam o game/economyRules.js.
const opSchema = z
  .object({
    t: z.enum(["gain", "consume", "gold+", "gold-", "buy", "sell", "craft", "cook", "ship", "unship", "house", "achievement", "wear", "repair"]),
    item: z.string().max(60).optional(),
    n: z.number().int().optional(),
    src: z.string().max(20).optional(),
    key: z.string().max(120).optional(),
    mult: z.number().optional(),
    recipe: z.string().max(60).optional(),
    id: z.string().max(60).optional(),
  })
  .passthrough();

const economyBatchSchema = z.object({
  seq: z.number().int().min(0),
  ops: z.array(opSchema).max(200),
});

const tradeInviteSchema = z.object({ toName: z.string().trim().min(1).max(20) });
const tradeOfferSchema = z.object({
  gold: z.number().int().min(0).max(1e9).default(0),
  items: z.record(z.string().max(60), z.number().int().positive().max(9999)).default({}),
});

const listingSchema = z.object({
  itemId: z.string().min(1).max(60),
  count: z.number().int().positive().max(999),
  price: z.number().int().positive().max(1e9),
});

const nameSchema = z.object({ name: z.string().trim().min(1).max(20) });
const guildCreateSchema = z.object({
  name: z.string().trim().min(3, "Ten bang it nhat 3 ky tu.").max(24).regex(/^[\p{L}\p{N} _]+$/u, "Ten bang chi gom chu, so, khoang trang."),
  tag: z.string().trim().min(2).max(4).regex(/^[A-Za-z0-9]+$/, "Ky hieu 2-4 chu cai/so."),
});
const itemCountSchema = z.object({ itemId: z.string().min(1).max(60), count: z.number().int().positive().max(9999) });
const roleSchema = z.object({ role: z.enum(["officer", "member"]) });
const helpSchema = z.object({ plots: z.array(z.string().max(40)).min(1).max(60) });
const statsSchema = z.object({ stats: z.record(z.string().max(40), z.number().int().min(0)) });
const claimSchema = z.object({ period: z.enum(["d", "w"]), idx: z.number().int().min(0).max(2) });
const enhanceSchema = z.object({ itemId: z.string().min(1).max(60) });
// Treo thuong: theo id hoac ten nhan vat.
const bountySchema = z.object({
  targetId: z.number().int().positive().optional(),
  targetName: z.string().trim().min(1).max(20).optional(),
  amount: z.number().int().positive().max(100000),
}).refine((v) => v.targetId || v.targetName, { message: "Thieu nguoi bi treo thuong." });

module.exports = {
  credentialsSchema, saveSchema, characterSchema, economyBatchSchema, tradeInviteSchema, tradeOfferSchema, listingSchema,
  nameSchema, guildCreateSchema, itemCountSchema, roleSchema, helpSchema, statsSchema, claimSchema, enhanceSchema, bountySchema,
};
