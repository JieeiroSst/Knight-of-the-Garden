using System;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Nho khoi quang nao DA DAO va ngay game no moc lai (theo PersistKey cua OreNode) - de bai quang
    // trong chunk hoang da (tao/xoa dong theo vi tri nguoi choi) va ham mo khong "hoi sinh" khi tai
    // lai chunk/thoat game. Luu trong SaveSystem (OreRegrowDay). Tu don cac muc da toi han.
    public static class OreRegistry
    {
        private static readonly Dictionary<string, int> _regrowDay = new();

        public static event Action Changed;

        public static int DaysLeft(string key)
        {
            if (!_regrowDay.TryGetValue(key, out var day)) return 0;
            int left = day - GameManager.Instance.Day;
            if (left <= 0) { _regrowDay.Remove(key); return 0; }
            return left;
        }

        public static void MarkDepleted(string key, int regrowDays) => _regrowDay[key] = GameManager.Instance.Day + Math.Max(1, regrowDays);

        public static Dictionary<string, int> Export()
        {
            int today = GameManager.Instance.Day;
            return _regrowDay.Where(kv => kv.Value > today).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public static void Import(Dictionary<string, int> data)
        {
            _regrowDay.Clear();
            if (data != null) foreach (var kv in data) _regrowDay[kv.Key] = kv.Value;
            Changed?.Invoke();
        }
    }
}
