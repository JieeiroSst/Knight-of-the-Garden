const { createCalendar } = require("../game/calendar");
const { env } = require("../config/env");

const calendar = createCalendar({ epoch: env.GAME_EPOCH, tzOffsetMin: env.GAME_TZ_OFFSET_MIN });

module.exports = { calendar, now: () => calendar.info(Date.now()) };
