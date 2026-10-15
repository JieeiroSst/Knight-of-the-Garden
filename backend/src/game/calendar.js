// Lich game THEO THOI GIAN THUC, DUNG CHUNG cho moi nguoi choi: 1 ngay game = 1 ngay that, tinh
// theo mui gio cua game (GAME_TZ_OFFSET_MIN, mac dinh UTC+7). Ngay 1 = GAME_EPOCH. Server la nguon
// su that ve thoi gian - client dong bo qua GET /api/time, KHONG dung dong ho may nguoi choi (doi
// gio may de "tua" cay lon nhanh se khong co tac dung).
const DAY_MS = 86400000;
const SEASONS = ["Spring", "Summer", "Fall", "Winter"];
const DAYS_PER_SEASON = 28;

function createCalendar({ epoch = "2026-01-01", tzOffsetMin = 420 } = {}) {
  // Moc 00:00 ngay epoch THEO MUI GIO GAME, quy ra mili-giay UTC.
  const epochMs = Date.parse(`${epoch}T00:00:00Z`) - tzOffsetMin * 60000;

  function dayAt(nowMs) {
    return Math.floor((nowMs - epochMs) / DAY_MS) + 1;
  }

  function info(nowMs = Date.now()) {
    const day = dayAt(nowMs);
    const idx = Math.max(0, day - 1);
    const seasonIndex = Math.floor(idx / DAYS_PER_SEASON) % 4;
    return {
      serverNow: nowMs,
      tzOffsetMin,
      day,
      year: Math.floor(idx / (DAYS_PER_SEASON * 4)) + 1,
      season: SEASONS[seasonIndex],
      dayInSeason: (idx % DAYS_PER_SEASON) + 1,
      // Thoi diem (ms UTC) bat dau ngay game KE TIEP - client dung de biet chinh xac luc qua ngay.
      nextDayAt: epochMs + day * DAY_MS,
    };
  }

  return { dayAt, info, epochMs };
}

module.exports = { createCalendar, SEASONS, DAYS_PER_SEASON, DAY_MS };
