// Lop HTTP mong cho ban be/bang hoi/tham nong trai/hang ngay/xep hang/cuong hoa.
const social = require("../services/socialService");
const progress = require("../services/progressService");
const { asyncHandler } = require("../utils/asyncHandler");
const { AppError } = require("../utils/AppError");

const id = (req) => {
  const n = Number(req.params.id);
  if (!Number.isSafeInteger(n) || n <= 0) throw new AppError(400, "id khong hop le.");
  return n;
};
const h = (fn) => asyncHandler(async (req, res) => res.json(await fn(req)));

module.exports = {
  friends: h((req) => social.listFriends(req.playerId)),
  addFriend: h((req) => social.addFriend(req.playerId, req.body.name)),
  acceptFriend: h((req) => social.acceptFriend(req.playerId, id(req))),
  removeFriend: h((req) => social.removeFriend(req.playerId, id(req))),

  myGuild: h((req) => social.myGuild(req.playerId)),
  createGuild: h((req) => social.createGuild(req.playerId, req.body.name, req.body.tag)),
  inviteGuild: h((req) => social.inviteToGuild(req.playerId, req.body.name)),
  acceptGuild: h((req) => social.acceptGuild(req.playerId, id(req))),
  leaveGuild: h((req) => social.leaveGuild(req.playerId)),
  kick: h((req) => social.kick(req.playerId, id(req))),
  promote: h((req) => social.promote(req.playerId, id(req), req.body.role)),
  deposit: h((req) => social.deposit(req.playerId, req.body.itemId, req.body.count)),
  withdraw: h((req) => social.withdraw(req.playerId, req.body.itemId, req.body.count)),

  friendFarm: h((req) => social.friendFarm(req.playerId, id(req))),
  helpWater: h((req) => social.helpWater(req.playerId, id(req), req.body.plots)),
  takeHelps: h((req) => social.takeHelps(req.playerId)),

  daily: h((req) => progress.getDaily(req.playerId)),
  checkin: h((req) => progress.checkin(req.playerId)),
  claim: h((req) => progress.claimQuest(req.playerId, req.body.period, req.body.idx)),
  reportStats: h((req) => progress.reportStats(req.playerId, req.body.stats)),
  leaderboard: h((req) => progress.leaderboard(String(req.params.stat), req.playerId)),

  enhancements: h((req) => progress.getEnhancements(req.playerId)),
  enhance: h((req) => progress.enhance(req.playerId, req.body.itemId)),
};
