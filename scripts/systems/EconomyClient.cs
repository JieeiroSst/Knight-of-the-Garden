using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Kinh te do SERVER quan ly (backend economyService): vang + vat pham chinh thuc nam
    // tren server. Client van cap nhat tui do NGAY (de choi muot), dong thoi ghi lai "y dinh" thanh
    // cac op gui len POST /api/economy/ops theo lo; server kiem tra (gia, cong thuc, nguon roi do,
    // gioi han/gio, phan thuong 1 lan) va tra ve anh chup chinh thuc -> client DONG BO LAI theo do.
    // Sua file/bo nho de tu tang vang/do se bi server bo qua o lan dong bo ke tiep.
    //
    // - Nhan/mat do le (thu hoach, an, trong cay...) di qua Inventory/Backpack/vang -> OnItemDelta/
    //   OnGoldDelta, cong don RONG theo vat pham trong cua so lo (chuyen do tui<->balo<->ruong tu
    //   triet tieu, khong gui gi).
    // - Hanh dong ghep (mua/ban/che tao/nau/giao hang/nang cap nha) goi ham rieng (Buy/Sell/...) de
    //   server kiem tra nguyen ven.
    public partial class EconomyClient : Node
    {
        public static EconomyClient Instance { get; private set; }

        // Trong Suppress(): thay doi tui do/vang KHONG tao op (nap save, dong bo theo server, chuyen do
        // giua cac ngan, hanh dong ghep da tu gui op rieng).
        private static int _suppress;
        public static bool Suppressed => _suppress > 0;
        public static IDisposable Suppress() { _suppress++; return new Scope(); }
        private sealed class Scope : IDisposable
        {
            private bool _done;
            public void Dispose() { if (!_done) { _done = true; _suppress--; } }
        }

        private class PendingOp
        {
            public Dictionary<string, object> Op;
            public Dictionary<string, int> Items = new();
            public long Gold;
        }

        private List<PendingOp> _queue = new();
        private List<PendingOp> _inflight;
        private long _inflightSeq;
        private long _seq;
        private bool _busy;
        private double _sinceFlush;
        private double _sinceFullSync;
        private bool _syncRequested;

        // Cong don nhan/mat do le (chua dong goi thanh op).
        private readonly Dictionary<string, Dictionary<string, int>> _gains = new();
        private readonly Dictionary<string, int> _consumes = new();
        private readonly Dictionary<string, long> _goldGains = new();
        private long _goldSpend;

        public const double FlushInterval = 4.0;
        public const double FullSyncInterval = 60.0;

        // Anh chup chinh thuc gan nhat tu server.
        public bool HasSnapshot { get; private set; }
        public int HouseLevel { get; private set; }
        public List<(int day, string item, int count, int unitPrice)> Bin { get; private set; } = new();

        [Signal] public delegate void SnapshotAppliedEventHandler();

        public override void _EnterTree() => Instance = this;

        // ==== Ghi nhan thay doi le ====
        public void OnItemDelta(string itemId, int delta, string source, string key)
        {
            if (Suppressed || delta == 0 || !Online) return;
            if (delta > 0 && !string.IsNullOrEmpty(key))
            {
                // Phan thuong 1 lan (ruong/thu/nhiem vu...) - giu nguyen key, khong cong don.
                Push(new() { ["t"] = "gain", ["item"] = itemId, ["n"] = delta, ["src"] = source ?? "reward", ["key"] = key },
                    new() { [itemId] = delta }, 0, flushSoon: false);
                return;
            }
            if (delta > 0)
            {
                if (!_gains.TryGetValue(itemId, out var bySrc)) _gains[itemId] = bySrc = new();
                string src = source ?? "misc";
                bySrc[src] = (bySrc.TryGetValue(src, out var v) ? v : 0) + delta;
            }
            else _consumes[itemId] = (_consumes.TryGetValue(itemId, out var c) ? c : 0) - delta;
        }

        public void OnGoldDelta(long delta, string source, string key)
        {
            if (Suppressed || delta == 0 || !Online) return;
            if (delta > 0 && !string.IsNullOrEmpty(key))
            {
                Push(new() { ["t"] = "gold+", ["n"] = delta, ["src"] = source ?? "reward", ["key"] = key }, new(), delta, flushSoon: false);
                return;
            }
            if (delta > 0)
            {
                string src = source ?? "misc";
                _goldGains[src] = (_goldGains.TryGetValue(src, out var v) ? v : 0) + delta;
            }
            else _goldSpend -= delta;
        }

        // Dong goi phan cong don thanh op (giu dung thu tu voi cac op ghep phia sau).
        private void Seal()
        {
            foreach (var id in _gains.Keys.Union(_consumes.Keys).ToList())
            {
                int consume = _consumes.TryGetValue(id, out var c) ? c : 0;
                var bySrc = _gains.TryGetValue(id, out var g) ? g : new Dictionary<string, int>();
                int gained = bySrc.Values.Sum();
                if (consume >= gained)
                {
                    if (consume > gained)
                        _queue.Add(new PendingOp { Op = new() { ["t"] = "consume", ["item"] = id, ["n"] = consume - gained }, Items = new() { [id] = -(consume - gained) } });
                    continue;
                }
                // Tru phan da tieu vao nguon "misc" truoc, roi cac nguon con lai.
                int left = consume;
                foreach (var src in bySrc.Keys.OrderBy(k => k == "misc" ? 0 : 1).ToList())
                {
                    int take = Math.Min(left, bySrc[src]);
                    bySrc[src] -= take;
                    left -= take;
                }
                foreach (var (src, n) in bySrc)
                    for (int rest = n; rest > 0; rest -= 999)
                    {
                        int part = Math.Min(999, rest);
                        _queue.Add(new PendingOp { Op = new() { ["t"] = "gain", ["item"] = id, ["n"] = part, ["src"] = src }, Items = new() { [id] = part } });
                    }
            }
            _gains.Clear();
            _consumes.Clear();

            long gainTotal = _goldGains.Values.Sum();
            long net = gainTotal - _goldSpend;
            if (net < 0) _queue.Add(new PendingOp { Op = new() { ["t"] = "gold-", ["n"] = -net }, Gold = net });
            else if (net > 0)
            {
                long spend = _goldSpend;
                foreach (var src in _goldGains.Keys.OrderBy(k => k == "misc" ? 0 : 1).ToList())
                {
                    long take = Math.Min(spend, _goldGains[src]);
                    _goldGains[src] -= take;
                    spend -= take;
                    if (_goldGains[src] > 0)
                        _queue.Add(new PendingOp { Op = new() { ["t"] = "gold+", ["n"] = _goldGains[src], ["src"] = src }, Gold = _goldGains[src] });
                }
            }
            _goldGains.Clear();
            _goldSpend = 0;
        }

        private void Push(Dictionary<string, object> op, Dictionary<string, int> items, long gold, bool flushSoon = true)
        {
            Seal();
            _queue.Add(new PendingOp { Op = op, Items = items, Gold = gold });
            if (flushSoon) _sinceFlush = FlushInterval - 0.3;
        }

        // ==== Hanh dong ghep (server tu tinh gia/cong thuc) ====
        public bool Buy(string itemId, int count)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null || def.BuyPrice <= 0) return false;
            long cost = (long)def.BuyPrice * count;
            if (GameManager.Instance.Gold < cost) return false;
            using (Suppress())
            {
                GameManager.Instance.SpendGold((int)cost);
                if (!Inventory.Instance.AddItem(itemId, count)) Backpack.Instance.AddItem(itemId, count);
            }
            Push(new() { ["t"] = "buy", ["item"] = itemId, ["n"] = count }, new() { [itemId] = count }, -cost);
            return true;
        }

        public const float MaxPriceMult = 2.2f; // khop backend economyRules.MAX_PRICE_MULT

        public static float ClampMult(float m) => Mathf.Clamp(m, 1f, MaxPriceMult);

        public bool Sell(string itemId, int count, float mult)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null || def.SellPrice <= 0 || Inventory.Instance.CountOf(itemId) < count) return false;
            mult = ClampMult(mult);
            long gold = (long)Mathf.RoundToInt(def.SellPrice * mult) * count;
            using (Suppress())
            {
                Inventory.Instance.RemoveItem(itemId, count);
                GameManager.Instance.AddGold((int)gold);
            }
            Push(new() { ["t"] = "sell", ["item"] = itemId, ["n"] = count, ["mult"] = mult }, new() { [itemId] = -count }, gold);
            return true;
        }

        // Che tao/nau an: tieu nguyen lieu + them thanh pham theo cong thuc (craft = recipes.json, cook = cooking.json).
        public bool Craft(Data.RecipeDef recipe, bool cooking)
        {
            if (recipe.Ingredients.Any(i => Inventory.Instance.CountOf(i.ItemId) < i.Count)) return false;
            var effects = new Dictionary<string, int>();
            using (Suppress())
            {
                foreach (var i in recipe.Ingredients)
                {
                    Inventory.Instance.RemoveItem(i.ItemId, i.Count);
                    effects[i.ItemId] = (effects.TryGetValue(i.ItemId, out var e) ? e : 0) - i.Count;
                }
                int before = Inventory.Instance.CountOf(recipe.OutputId);
                if (!Inventory.Instance.AddItem(recipe.OutputId, recipe.OutputCount))
                    Backpack.Instance.AddItem(recipe.OutputId, recipe.OutputCount - (Inventory.Instance.CountOf(recipe.OutputId) - before));
                effects[recipe.OutputId] = (effects.TryGetValue(recipe.OutputId, out var o) ? o : 0) + recipe.OutputCount;
            }
            Push(new() { ["t"] = cooking ? "cook" : "craft", ["recipe"] = recipe.Id }, effects, 0);
            return true;
        }

        // Thung giao hang (phia server): bo vao -> server giu, sang ngay game moi tu ban.
        public bool Ship(string itemId, int count, float mult)
        {
            if (Inventory.Instance.CountOf(itemId) < count) return false;
            mult = ClampMult(mult);
            using (Suppress()) Inventory.Instance.RemoveItem(itemId, count);
            var def = ItemDatabase.Instance.GetItem(itemId);
            int unit = Mathf.RoundToInt((def?.SellPrice ?? 0) * mult);
            AddToLocalBin(itemId, count, unit);
            Push(new() { ["t"] = "ship", ["item"] = itemId, ["n"] = count, ["mult"] = mult }, new() { [itemId] = -count }, 0);
            return true;
        }

        public bool Unship(string itemId)
        {
            int today = GameManager.Instance.Day;
            int idx = Bin.FindIndex(b => b.day == today && b.item == itemId);
            if (idx < 0) return false;
            var row = Bin[idx];
            Bin.RemoveAt(idx);
            using (Suppress()) if (!Inventory.Instance.AddItem(itemId, row.count)) Backpack.Instance.AddItem(itemId, row.count);
            Push(new() { ["t"] = "unship", ["item"] = itemId }, new() { [itemId] = row.count }, 0);
            return true;
        }

        private void AddToLocalBin(string itemId, int count, int unit)
        {
            int today = GameManager.Instance.Day;
            int idx = Bin.FindIndex(b => b.day == today && b.item == itemId);
            if (idx >= 0) Bin[idx] = (today, itemId, Bin[idx].count + count, Math.Max(unit, Bin[idx].unitPrice));
            else Bin.Add((today, itemId, count, unit));
        }

        public bool UpgradeHouse(int gold, IEnumerable<(string id, int count)> mats)
        {
            var effects = new Dictionary<string, int>();
            using (Suppress())
            {
                GameManager.Instance.SpendGold(gold);
                foreach (var (id, n) in mats)
                {
                    RemoveAnywhere(id, n);
                    effects[id] = -n;
                }
            }
            HouseLevel++;
            Push(new() { ["t"] = "house" }, effects, -gold);
            return true;
        }

        // Thuong thanh tuu: server tu tra vang theo achievements.json (1 lan/thanh tuu).
        public void ClaimAchievement(string id, int rewardGold)
        {
            using (Suppress()) if (rewardGold > 0) GameManager.Instance.AddGold(rewardGold);
            Push(new() { ["t"] = "achievement", ["id"] = id }, new(), rewardGold, flushSoon: false);
        }

        // Phan thuong 1 LAN (ruong/thu/nhiem vu/le hoi/hop dong): them do vao tui->balo->ruong (khong
        // bao gio mat do vi tui day) va gui DUNG 1 op co key - server tu choi neu key da nhan roi.
        public void GrantReward(string itemId, int count, string source, string key)
        {
            if (count <= 0 || ItemDatabase.Instance.GetItem(itemId) == null) return;
            AddAnywhere(itemId, count);
            if (Online) Push(new() { ["t"] = "gain", ["item"] = itemId, ["n"] = count, ["src"] = source, ["key"] = key }, new() { [itemId] = count }, 0, flushSoon: false);
        }

        public void GrantGold(int amount, string source, string key)
        {
            if (amount <= 0) return;
            using (Suppress()) GameManager.Instance.AddGold(amount);
            if (Online) Push(new() { ["t"] = "gold+", ["n"] = amount, ["src"] = source, ["key"] = key }, new(), amount, flushSoon: false);
        }

        public void RequestSync() => _syncRequested = true;

        private static bool Online => BackendClient.Instance != null && BackendClient.Instance.IsLoggedIn;

        // ==== Vong gui/dong bo ====
        public override void _Process(double delta)
        {
            if (!Online || _busy) return;
            _sinceFlush += delta;
            _sinceFullSync += delta;
            bool hasLocal = _queue.Count > 0 || _gains.Count > 0 || _consumes.Count > 0 || _goldGains.Count > 0 || _goldSpend > 0;
            bool due = _inflight != null
                || (hasLocal && _sinceFlush >= FlushInterval)
                || _syncRequested
                || _sinceFullSync >= FullSyncInterval;
            if (due) Flush();
        }

        // Goi ngay (vd luc thoat game) - gui het nhung gi con lai.
        public void FlushNow(Action<bool> onDone = null)
        {
            if (!Online) { onDone?.Invoke(false); return; }
            if (_busy) { _pendingDoneCallbacks.Add(onDone); return; }
            Flush(onDone);
        }

        private readonly List<Action<bool>> _pendingDoneCallbacks = new();

        private void Flush(Action<bool> onDone = null)
        {
            _busy = true;
            _sinceFlush = 0;
            _sinceFullSync = 0;
            _syncRequested = false;
            if (_inflight == null && !HasSnapshot)
            {
                // Lan dau moi phien: CHI xin anh chup (seq=0, khong op) de biet seq hien tai cua
                // server truoc - neu gui op voi seq tu 1 trong khi server da o seq 50 (phien truoc),
                // server se coi la lo cu va BO QUA.
                _inflight = new();
                _inflightSeq = 0;
            }
            else if (_inflight == null)
            {
                // Lo moi. Lo cu loi mang (_inflight != null) duoc GUI LAI NGUYEN VEN cung seq -
                // server bo qua neu da nhan roi (khong ap dung 2 lan).
                Seal();
                _inflight = _queue;
                _queue = new();
                _inflightSeq = _inflight.Count > 0 ? ++_seq : 0;
            }
            var body = new Dictionary<string, object>
            {
                ["seq"] = _inflightSeq,
                ["ops"] = _inflight.Select(p => p.Op).ToList(),
            };
            BackendClient.Instance.PostJson("/api/economy/ops", JsonSerializer.Serialize(body), (ok, status, text) =>
            {
                _busy = false;
                if (ok && status == 200)
                {
                    var sent = _inflight;
                    _inflight = null;
                    ApplySnapshot(text, sent);
                }
                else if (status == 400)
                {
                    // Lo khong hop le ve hinh dang - bo lo, xin anh chup moi de dong bo lai.
                    GD.PushWarning($"Server tu choi lo kinh te: {text}");
                    _inflight = null;
                    _syncRequested = true;
                }
                onDone?.Invoke(ok && status == 200);
                foreach (var cb in _pendingDoneCallbacks) cb?.Invoke(ok);
                _pendingDoneCallbacks.Clear();
            });
        }

        // ==== Dong bo theo anh chup server ====
        private void ApplySnapshot(string json, List<PendingOp> sent)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            long seq = root.GetProperty("seq").GetInt64();
            if (seq > _seq) _seq = seq;

            // Bao op bi tu choi (chi hien neu la hanh dong nguoi choi chu dong lam).
            if (root.TryGetProperty("rejected", out var rej))
                foreach (var r in rej.EnumerateArray())
                {
                    int i = r.GetProperty("index").GetInt32();
                    string reason = r.GetProperty("reason").GetString();
                    string t = i >= 0 && i < sent.Count ? sent[i].Op["t"].ToString() : "?";
                    GD.PushWarning($"Server bo op {t}: {reason}");
                    if (t is "buy" or "sell" or "craft" or "cook" or "ship" or "house")
                        Notify.Show(string.Format(Loc.T("econ.rejected_fmt"), reason));
                }

            // Muc tieu = server + cac thay doi CHUA gui (van dang cho trong hang doi/cong don).
            var target = new Dictionary<string, long>();
            foreach (var p in root.GetProperty("items").EnumerateObject()) target[p.Name] = p.Value.GetInt64();
            long gold = root.GetProperty("gold").GetInt64();
            foreach (var p in _queue)
            {
                foreach (var (id, n) in p.Items) target[id] = (target.TryGetValue(id, out var v) ? v : 0) + n;
                gold += p.Gold;
            }
            foreach (var (id, bySrc) in _gains) target[id] = (target.TryGetValue(id, out var v) ? v : 0) + bySrc.Values.Sum();
            foreach (var (id, n) in _consumes) target[id] = (target.TryGetValue(id, out var v) ? v : 0) - n;
            gold += _goldGains.Values.Sum() - _goldSpend;

            Reconcile(target, Math.Max(0, gold));

            HouseLevel = root.GetProperty("houseLevel").GetInt32();
            Bin = root.GetProperty("bin").EnumerateArray()
                .Select(b => (b.GetProperty("day").GetInt32(), b.GetProperty("item").GetString(), b.GetProperty("count").GetInt32(), b.GetProperty("unitPrice").GetInt32()))
                .ToList();
            foreach (var p in _queue)
                if (p.Op["t"].ToString() == "ship")
                    AddToLocalBin(p.Op["item"].ToString(), Convert.ToInt32(p.Op["n"]), 0);

            if (root.TryGetProperty("time", out var time)) GameManager.Instance.ApplyServerTime(time);
            if (root.TryGetProperty("events", out var events))
                foreach (var e in events.EnumerateArray()) HandleEvent(e);

            HasSnapshot = true;
            EmitSignal(SignalName.SnapshotApplied);
        }

        public static int TotalOwned(string id) =>
            Inventory.Instance.CountOf(id) + Backpack.Instance.CountOf(id) + (HomeSystem.Instance?.ChestCount(id) ?? 0);

        private void Reconcile(Dictionary<string, long> target, long gold)
        {
            using (Suppress())
            {
                var local = new HashSet<string>();
                foreach (var s in Inventory.Instance.Slots) local.Add(s.ItemId);
                foreach (var s in Backpack.Instance.Slots) local.Add(s.ItemId);
                if (HomeSystem.Instance != null) foreach (var e in HomeSystem.Instance.Chest) local.Add(e.Id);
                int fixedCount = 0;
                foreach (var id in local.Union(target.Keys).ToList())
                {
                    if (ItemDatabase.Instance.GetItem(id) == null) continue;
                    long want = Math.Max(0, target.TryGetValue(id, out var v) ? v : 0);
                    long have = TotalOwned(id);
                    if (want == have) continue;
                    fixedCount++;
                    if (have > want) RemoveAnywhere(id, (int)(have - want));
                    else AddAnywhere(id, (int)(want - have));
                }
                if (GameManager.Instance.Gold != gold)
                {
                    GameManager.Instance.Gold = (int)Math.Min(int.MaxValue, gold);
                    GameManager.Instance.EmitSignal(GameManager.SignalName.StatsChanged);
                }
                if (fixedCount > 0) GD.Print($"Dong bo kinh te voi server: dieu chinh {fixedCount} loai vat pham.");
            }
        }

        public static void RemoveAnywhere(string id, int n)
        {
            using (Suppress())
            {
                int a = Math.Min(n, Inventory.Instance.CountOf(id));
                if (a > 0) { Inventory.Instance.RemoveItem(id, a); n -= a; }
                int b = Math.Min(n, Backpack.Instance.CountOf(id));
                if (b > 0) { Backpack.Instance.RemoveItem(id, b); n -= b; }
                if (n > 0) HomeSystem.Instance?.ChestRemove(id, n);
            }
        }

        public static void AddAnywhere(string id, int n)
        {
            using (Suppress())
            {
                int before = Inventory.Instance.CountOf(id);
                Inventory.Instance.AddItem(id, n);
                n -= Inventory.Instance.CountOf(id) - before;
                if (n <= 0) return;
                before = Backpack.Instance.CountOf(id);
                Backpack.Instance.AddItem(id, n);
                n -= Backpack.Instance.CountOf(id) - before;
                if (n > 0) HomeSystem.Instance?.ChestAdd(id, n);
            }
        }

        private void HandleEvent(JsonElement e)
        {
            string type = e.GetProperty("type").GetString();
            if (type == "ship_payout")
            {
                int gold = e.GetProperty("gold").GetInt32();
                var lines = e.GetProperty("lines").EnumerateArray()
                    .Select(l => $"- {ItemDatabase.Instance.GetDisplayName(l.GetProperty("item").GetString())} x{l.GetProperty("count").GetInt32()}: {l.GetProperty("gold").GetInt32()}")
                    .ToList();
                AchievementSystem.Instance?.Add("gold_shipped", gold);
                MailSystem.Instance?.Send($"ship_{GameManager.Instance.Day}_{gold}",
                    string.Format(Loc.T("mail.ship_title_fmt"), gold),
                    string.Format(Loc.T("mail.ship_body_fmt"), string.Join("\n", lines), gold));
                Notify.Show(string.Format(Loc.T("ship.paid_fmt"), gold));
            }
            else if (type == "market_expired")
            {
                Notify.Show(string.Format(Loc.T("market.expired_fmt"), ItemDatabase.Instance.GetDisplayName(e.GetProperty("item").GetString()), e.GetProperty("count").GetInt32()));
            }
        }

        // Dang xuat/doi tai khoan: bo trang thai cu.
        public void Reset()
        {
            _queue.Clear();
            _inflight = null;
            _gains.Clear();
            _consumes.Clear();
            _goldGains.Clear();
            _goldSpend = 0;
            _seq = 0;
            HasSnapshot = false;
            Bin.Clear();
        }
    }
}
