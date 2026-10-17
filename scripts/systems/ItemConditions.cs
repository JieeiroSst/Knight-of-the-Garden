using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Phia client cua HAN SU DUNG + DO BEN (server: backend src/game/itemState.js - nguon su that).
    //  - Nhan trang thai tu anh chup kinh te ("states"): lo han dung { het han, so luong } va do ben tung mon.
    //  - Hao mon tai cho ngay (vung kiem/dung nong cu/ban cung/bi quai danh) roi gom gui op "wear" theo dot.
    //  - Mon dang dung = mon BEN NHAT; duoi 40% do ben toi da -> KHONG dung duoc (kiem = danh tay khong,
    //    nong cu khong lam duoc, giap = 0 phong thu) cho toi khi SUA (Lo Ren / khu cho) hoac che tao mon moi.
    //  - Thit/nong san/mon an qua han thanh "Do Oi Thiu" o lan dong bo server ke tiep (tu xin dong bo khi
    //    thay co lo vua het han).
    public static class ItemConditions
    {
        public const float UsableRatio = 0.4f;
        public const string Rotten = "do_thoi";

        private class State { public List<(long exp, int n)> Fresh = new(); public List<int> Dur = new(); }
        private static readonly Dictionary<string, State> _states = new();
        private static readonly Dictionary<string, int> _pendingWear = new();
        private static readonly HashSet<string> _warned = new();

        private static ItemDef Def(string id) => id == null ? null : ItemDatabase.Instance.GetItem(id);
        public static bool HasDurability(string id) => Def(id)?.MaxDurability > 0;
        public static bool Perishable(string id) => Def(id)?.ShelfHours > 0;

        // (do ben mon dang dung, toi da) - do chua co trang thai (moi tinh) = toi da.
        public static (int cur, int max) Durability(string id)
        {
            int max = Def(id)?.MaxDurability ?? 0;
            if (max <= 0) return (0, 0);
            return _states.TryGetValue(id, out var st) && st.Dur.Count > 0 ? (st.Dur[0], max) : (max, max);
        }

        public static bool IsUsable(string id)
        {
            var (cur, max) = Durability(id);
            return max <= 0 || cur >= Mathf.CeilToInt(max * UsableRatio);
        }

        // Lo sap het han nhat: (ms con lai theo gio server, so luong trong lo) - null neu khong hong/khong co.
        public static (long msLeft, int n)? SoonestSpoil(string id)
        {
            if (!_states.TryGetValue(id, out var st) || st.Fresh.Count == 0) return null;
            var (exp, n) = st.Fresh[0];
            return (exp - (long)GameManager.Instance.ServerNowMs, n);
        }

        public static float FreshRatio(string id)
        {
            var s = SoonestSpoil(id);
            float shelf = (Def(id)?.ShelfHours ?? 0f) * 3600000f;
            return s == null || shelf <= 0f ? 1f : Mathf.Clamp(s.Value.msLeft / shelf, 0f, 1f);
        }

        // ==== Anh chup tu server ====
        public static void ApplySnapshot(JsonElement root)
        {
            if (!root.TryGetProperty("states", out var states)) return;
            _states.Clear();
            foreach (var p in states.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Object) continue;
                var st = new State();
                if (p.Value.TryGetProperty("fresh", out var f) && f.ValueKind == JsonValueKind.Array)
                    foreach (var lot in f.EnumerateArray()) st.Fresh.Add((lot[0].GetInt64(), lot[1].GetInt32()));
                if (p.Value.TryGetProperty("dur", out var d) && d.ValueKind == JsonValueKind.Array)
                    foreach (var v in d.EnumerateArray()) st.Dur.Add(v.GetInt32());
                _states[p.Name] = st;
            }
            // Hao mon chua kip gui van tinh (server chua biet).
            foreach (var (id, n) in _pendingWear) ApplyWearLocal(id, n);
        }

        public static void Reset()
        {
            _states.Clear();
            _pendingWear.Clear();
            _warned.Clear();
        }

        // ==== Hao mon ====
        public static void Wear(string id, int n = 1)
        {
            if (!HasDurability(id) || n <= 0) return;
            bool wasUsable = IsUsable(id);
            ApplyWearLocal(id, n);
            _pendingWear[id] = (_pendingWear.TryGetValue(id, out var p) ? p : 0) + n;
            var (cur, max) = Durability(id);
            string name = ItemDatabase.Instance.GetDisplayName(id);
            if (wasUsable && !IsUsable(id)) Notify.Show(string.Format(Loc.T("dur.broken_fmt"), name));
            else if (cur * 2 < max && _warned.Add(id)) Notify.Show(string.Format(Loc.T("dur.low_fmt"), name, Mathf.RoundToInt(100f * cur / max)));
        }

        private static void ApplyWearLocal(string id, int n)
        {
            int max = Def(id)?.MaxDurability ?? 0;
            if (max <= 0) return;
            if (!_states.TryGetValue(id, out var st)) _states[id] = st = new State();
            if (st.Dur.Count == 0) st.Dur.Add(max);
            st.Dur[0] = Mathf.Max(0, st.Dur[0] - n);
            st.Dur.Sort((a, b) => b.CompareTo(a));
        }

        // Goi tu EconomyClient truoc moi lan gui lo: lay het hao mon da gom thanh op "wear".
        public static int DrainWear(string id)
        {
            if (!_pendingWear.Remove(id, out var n)) return 0;
            return n;
        }

        public static IEnumerable<(string id, int n)> DrainWear()
        {
            var list = _pendingWear.Select(kv => (kv.Key, kv.Value)).ToList();
            _pendingWear.Clear();
            return list;
        }

        // Co lo vua het han -> xin dong bo de server doi thanh Do Oi Thiu ngay.
        public static bool AnyExpired()
        {
            long now = (long)GameManager.Instance.ServerNowMs;
            return _states.Values.Any(s => s.Fresh.Count > 0 && s.Fresh[0].exp <= now);
        }

        // ==== Sua ====
        public static bool NearRepairStation(Node3D player)
        {
            if (player == null) return false;
            foreach (Node n in player.GetTree().GetNodesInGroup("forge_stations"))
                if (n is Node3D f && f.GlobalPosition.DistanceTo(player.GlobalPosition) < 220f) return true;
            foreach (Node n in player.GetTree().GetNodesInGroup("base_structures"))
                if (n is BaseStructure s && s.Info.Kind is "forge" or "workshop" && s.GlobalPosition.DistanceTo(player.GlobalPosition) < 220f) return true;
            return PvpClient.MarketAt(player.GlobalPosition) != null;
        }

        public static string RepairCostText(string id)
        {
            var def = Def(id);
            if (def == null) return "";
            var parts = new List<string> { $"{def.RepairGold}g" };
            parts.AddRange(def.RepairCost.Select(c => $"{c.Count} {ItemDatabase.Instance.GetDisplayName(c.ItemId)}"));
            return string.Join(" + ", parts);
        }

        public static bool CanAffordRepair(string id)
        {
            var def = Def(id);
            return def != null && GameManager.Instance.Gold >= def.RepairGold
                && def.RepairCost.All(c => Inventory.Instance.CountOf(c.ItemId) + Backpack.Instance.CountOf(c.ItemId) >= c.Count);
        }

        public static void MarkRepaired(string id)
        {
            int max = Def(id)?.MaxDurability ?? 0;
            if (max <= 0) return;
            if (!_states.TryGetValue(id, out var st)) _states[id] = st = new State();
            int i = st.Dur.FindIndex(d => d < max);
            if (i >= 0) st.Dur[i] = max; else if (st.Dur.Count == 0) st.Dur.Add(max);
            st.Dur.Sort((a, b) => b.CompareTo(a));
            _warned.Remove(id);
        }
    }
}
