// Diem danh + nhiem vu ngay/tuan - ham THUAN (unit test duoc). Tien do lay tu economy_counters
// (server tu dem khi ap dung op kinh te: item:harvest, item:fish, gold:kill, act:cook...) nen KHONG
// gia mao duoc bang client.

// Thuong diem danh 7 ngay (lap lai), ngay 7 lon nhat.
const CHECKIN_REWARDS = [
  { gold: 100 }, { gold: 150, item: "potion", n: 2 }, { gold: 200 }, { gold: 250, item: "tra_thao_moc", n: 2 },
  { gold: 300 }, { gold: 400, item: "moi_cau", n: 10 }, { gold: 800, item: "thach_anh", n: 3 },
];

// counter: khoa trong economy_counters; d/w: muc tieu ngay/tuan; reward: vang ngay (tuan x4).
const QUEST_POOL = [
  { id: "harvest", counter: "item:harvest", d: 20, w: 150, gold: 200 },
  { id: "fish", counter: "item:fish", d: 5, w: 35, gold: 250 },
  { id: "mine", counter: "item:mine", d: 15, w: 120, gold: 250 },
  { id: "loot", counter: "item:loot", d: 10, w: 80, gold: 220 },
  { id: "kill_gold", counter: "gold:kill", d: 150, w: 1200, gold: 200 },
  { id: "cook", counter: "act:cook", d: 2, w: 15, gold: 180 },
  { id: "craft", counter: "act:craft", d: 3, w: 20, gold: 180 },
  { id: "ship", counter: "act:ship", d: 20, w: 150, gold: 200 },
  { id: "animal", counter: "item:animal", d: 8, w: 60, gold: 200 },
  { id: "forage", counter: "item:forage", d: 6, w: 40, gold: 180 },
];

// Chon 3 nhiem vu TAT DINH theo (nguoi choi, ky) - moi nguoi 1 bo khac nhau, cung 1 ky luon cung bo.
function pickQuests(playerId, periodNo, salt) {
  let h = (playerId * 2654435761 + periodNo * 40503 + salt * 97) >>> 0;
  const idxs = [...QUEST_POOL.keys()];
  const out = [];
  while (out.length < 3) {
    h = (Math.imul(h ^ (h >>> 15), 2246822519) + 3266489917) >>> 0;
    out.push(idxs.splice(h % idxs.length, 1)[0]);
  }
  return out.map((i) => QUEST_POOL[i]);
}

// Chuoi diem danh: lien tiep thi +1, bo 1 ngay tro len thi ve 1.
function nextStreak(prev, today) {
  if (!prev) return 1;
  if (prev.last_day === today) return prev.streak;
  return prev.last_day === today - 1 ? prev.streak + 1 : 1;
}

function checkinReward(streak) {
  return CHECKIN_REWARDS[(streak - 1) % CHECKIN_REWARDS.length];
}

module.exports = { CHECKIN_REWARDS, QUEST_POOL, pickQuests, nextStreak, checkinReward };
