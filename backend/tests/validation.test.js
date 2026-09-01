// Unit test cho zod schema - KHONG can database, chay tuc thi.
process.env.NODE_ENV = process.env.NODE_ENV || "test";
process.env.JWT_SECRET = process.env.JWT_SECRET || "test-secret-du-16-ky-tu-tro-len";
process.env.DATABASE_URL = process.env.DATABASE_URL || "postgres://test:test@localhost:5432/test";

const test = require("node:test");
const assert = require("node:assert/strict");
const { credentialsSchema, saveSchema } = require("../src/validation/schemas");

test("credentialsSchema: chap nhan username/password hop le", () => {
  const result = credentialsSchema.safeParse({ username: "nongdan01", password: "matkhau123" });
  assert.equal(result.success, true);
});

test("credentialsSchema: tu dong .trim() username", () => {
  const result = credentialsSchema.safeParse({ username: "  nongdan01  ", password: "matkhau123" });
  assert.equal(result.success, true);
  assert.equal(result.data.username, "nongdan01");
});

test("credentialsSchema: tu choi username qua ngan", () => {
  const result = credentialsSchema.safeParse({ username: "ab", password: "matkhau123" });
  assert.equal(result.success, false);
});

test("credentialsSchema: tu choi password qua ngan", () => {
  const result = credentialsSchema.safeParse({ username: "nongdan01", password: "12345" });
  assert.equal(result.success, false);
});

test("credentialsSchema: tu choi thieu field", () => {
  const result = credentialsSchema.safeParse({ username: "nongdan01" });
  assert.equal(result.success, false);
});

test("saveSchema: chap nhan du lieu bat ky mien khong null/undefined", () => {
  assert.equal(saveSchema.safeParse({ data: { Gold: 100 } }).success, true);
  assert.equal(saveSchema.safeParse({ data: [] }).success, true);
  assert.equal(saveSchema.safeParse({ data: 0 }).success, true);
});

test("saveSchema: tu choi thieu data / data null", () => {
  assert.equal(saveSchema.safeParse({}).success, false);
  assert.equal(saveSchema.safeParse({ data: null }).success, false);
});
