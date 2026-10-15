process.env.NODE_ENV = "test";
const test = require("node:test");
const assert = require("node:assert/strict");
const { createCalendar } = require("../src/game/calendar");

test("lich: ngay 1 bat dau dung 00:00 gio game (UTC+7)", () => {
  const cal = createCalendar({ epoch: "2026-01-01", tzOffsetMin: 420 });
  assert.equal(cal.dayAt(Date.parse("2025-12-31T17:00:00Z")), 1);
  assert.equal(cal.dayAt(Date.parse("2025-12-31T16:59:59Z")), 0);
  assert.equal(cal.dayAt(Date.parse("2026-01-02T16:59:59Z")), 2);
});

test("lich: mua doi moi 28 ngay, nam 112 ngay", () => {
  const cal = createCalendar({ epoch: "2026-01-01", tzOffsetMin: 0 });
  const at = (d) => cal.info(cal.epochMs + (d - 1) * 86400000 + 1000);
  assert.equal(at(1).season, "Spring");
  assert.equal(at(28).dayInSeason, 28);
  assert.equal(at(29).season, "Summer");
  assert.equal(at(113).year, 2);
  assert.equal(at(5).nextDayAt, cal.epochMs + 5 * 86400000);
});
