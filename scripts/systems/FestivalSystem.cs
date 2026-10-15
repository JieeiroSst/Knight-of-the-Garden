using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] 4 le hoi theo mua, moi le hoi 1 ngay (ca ngay that) tai Bang Le Hoi o quang truong
    // thi tran (xem FestivalBoard.cs) - moi nam tham gia 1 lan/le hoi:
    //  - Xuan (ngay 13): San Trung - 2 phut nhat trung giau quanh quang truong.
    //  - Ha (ngay 20): Thi Cau Ca - 5 phut cau duoc cang nhieu ca cang tot.
    //  - Thu (ngay 16): Hoi Cho Nong San - cham 3 mon gia tri nhat trong tui do.
    //  - Dong (ngay 25): Anh Sao - tang qua cho "ban bi mat" duoc chi dinh.
    public partial class FestivalSystem : Node
    {
        public static FestivalSystem Instance { get; private set; }

        public enum Kind { EggHunt, FishingContest, HarvestFair, StarNight }

        public static readonly (Kind kind, GameManager.Season season, int day)[] Festivals =
        {
            (Kind.EggHunt, GameManager.Season.Spring, 13),
            (Kind.FishingContest, GameManager.Season.Summer, 20),
            (Kind.HarvestFair, GameManager.Season.Fall, 16),
            (Kind.StarNight, GameManager.Season.Winter, 25),
        };

        public const string EggItemId = "trung_le_hoi";
        private const double EggHuntSeconds = 120;
        private const double FishingSeconds = 300;

        public HashSet<string> Completed = new();
        public string SecretFriendId;
        public int SecretFriendDay = -1;

        // Su kien dang dien ra (khong luu - dang choi do thi thoat game se mat luot dang do).
        private Kind? _running;
        private double _timeLeft;
        private int _fishAtStart;
        private int _lastAnnounced;

        // Vi tri Bang Le Hoi (dat boi FestivalBoard) - noi rai trung.
        public Vector3 BoardPosition;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += _ => OnDayChanged();
        }

        public void Reset()
        {
            Completed.Clear();
            SecretFriendId = null;
            SecretFriendDay = -1;
        }

        private static int Year => (GameManager.Instance.Day - 1) / 112;

        public (Kind kind, GameManager.Season season, int day)? Today
        {
            get
            {
                var gm = GameManager.Instance;
                foreach (var f in Festivals)
                    if (f.season == gm.CurrentSeason && f.day == gm.DayInSeason + 1) return f;
                return null;
            }
        }

        public string TodayName => Today is { } f ? Loc.T("fest.name." + f.kind) : null;
        private string Key(Kind k) => $"{k}_{Year}";
        public bool DoneThisYear(Kind k) => Completed.Contains(Key(k));

        // Le hoi tiep theo (cho bang thong bao vao ngay thuong).
        public string NextFestivalText()
        {
            var gm = GameManager.Instance;
            int today = (int)gm.CurrentSeason * 28 + gm.DayInSeason + 1;
            var next = Festivals.OrderBy(f => ((int)f.season * 28 + f.day - today + 112) % 112).First();
            int inDays = ((int)next.season * 28 + next.day - today + 112) % 112;
            return string.Format(Loc.T("fest.next_fmt"), Loc.T("fest.name." + next.kind), next.day, Loc.T("season." + next.season), inDays);
        }

        private void OnDayChanged()
        {
            _running = null;
            if (Today is { } f)
                MailSystem.Instance?.Send($"fest_{f.kind}_{GameManager.Instance.Day}",
                    string.Format(Loc.T("fest.mail_title_fmt"), Loc.T("fest.name." + f.kind)),
                    Loc.T("fest.desc." + f.kind) + "\n\n" + Loc.T("fest.mail_where"));
        }

        // Bam [E] tai Bang Le Hoi.
        public void Interact()
        {
            var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueBox;
            string title = Loc.T("fest.board_title");
            if (Today is not { } f) { dialogue?.Show(title, NextFestivalText()); return; }
            string name = Loc.T("fest.name." + f.kind);
            if (DoneThisYear(f.kind)) { dialogue?.Show(name, Loc.T("fest.already_done")); return; }
            if (_running == f.kind)
            {
                dialogue?.Show(name, string.Format(Loc.T("fest.running_fmt"), (int)_timeLeft));
                return;
            }

            switch (f.kind)
            {
                case Kind.EggHunt:
                    StartEggHunt();
                    dialogue?.Show(name, Loc.T("fest.desc." + f.kind) + "\n\n" + Loc.T("fest.egg_start"));
                    break;
                case Kind.FishingContest:
                    _running = Kind.FishingContest;
                    _timeLeft = FishingSeconds;
                    _lastAnnounced = (int)FishingSeconds;
                    _fishAtStart = AchievementSystem.Instance.GetStat("fish_caught");
                    dialogue?.Show(name, Loc.T("fest.desc." + f.kind) + "\n\n" + Loc.T("fest.fish_start"));
                    break;
                case Kind.HarvestFair:
                    dialogue?.Show(name, JudgeFair());
                    break;
                case Kind.StarNight:
                    dialogue?.Show(name, StarNight());
                    break;
            }
        }

        public override void _Process(double delta)
        {
            if (_running == null) return;
            _timeLeft -= delta;
            int secs = (int)_timeLeft;
            if (secs != _lastAnnounced && (secs == 60 || secs == 30 || secs == 10))
            {
                _lastAnnounced = secs;
                Notify.Show(string.Format(Loc.T("fest.time_left_fmt"), secs));
            }
            if (_timeLeft > 0) return;
            var kind = _running.Value;
            _running = null;
            if (kind == Kind.EggHunt) FinishEggHunt();
            else if (kind == Kind.FishingContest) FinishFishing();
        }

        private void Complete(Kind k, int gold, string itemId = null, int itemCount = 0)
        {
            Completed.Add(Key(k));
            AchievementSystem.Instance?.Add("festivals_completed");
            // Giai thuong 1 lan/nam/le hoi - key giup server chan nhan lai.
            if (gold > 0) EconomyClient.Instance.GrantGold(gold, "reward", $"fest_{Key(k)}#g");
            if (itemId != null && itemCount > 0) EconomyClient.Instance.GrantReward(itemId, itemCount, "reward", $"fest_{Key(k)}#i");
        }

        // ==== Xuan: San Trung ====
        private void StartEggHunt()
        {
            _running = Kind.EggHunt;
            _timeLeft = EggHuntSeconds;
            _lastAnnounced = (int)EggHuntSeconds;
            Inventory.Instance.RemoveItem(EggItemId, Inventory.Instance.CountOf(EggItemId));
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            var parent = GetTree().CurrentScene;
            for (int i = 0; i < 15; i++)
            {
                float ang = rng.Randf() * Mathf.Tau, r = rng.RandfRange(120f, 450f);
                var drop = new DroppedItem { ItemId = EggItemId, Amount = 1, Source = "festival" };
                drop.AddToGroup("festival_eggs");
                parent.AddChild(drop);
                drop.GlobalPosition = BoardPosition + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
            }
        }

        private void FinishEggHunt()
        {
            foreach (Node n in GetTree().GetNodesInGroup("festival_eggs")) n.QueueFree();
            int eggs = Inventory.Instance.CountOf(EggItemId);
            Inventory.Instance.RemoveItem(EggItemId, eggs);
            if (eggs >= 12) Complete(Kind.EggHunt, 1000, "dau_tay_seed", 10);
            else if (eggs >= 8) Complete(Kind.EggHunt, 500, "dau_tay_seed", 5);
            else if (eggs >= 4) Complete(Kind.EggHunt, 200);
            else Complete(Kind.EggHunt, 50);
            Notify.Show(string.Format(Loc.T("fest.egg_result_fmt"), eggs));
        }

        // ==== Ha: Thi Cau Ca ====
        private void FinishFishing()
        {
            int caught = AchievementSystem.Instance.GetStat("fish_caught") - _fishAtStart;
            if (caught >= 6) Complete(Kind.FishingContest, 1200, "moi_cau", 20);
            else if (caught >= 3) Complete(Kind.FishingContest, 500, "moi_cau", 10);
            else if (caught >= 1) Complete(Kind.FishingContest, 150);
            else Complete(Kind.FishingContest, 0);
            Notify.Show(string.Format(Loc.T("fest.fish_result_fmt"), caught));
        }

        // ==== Thu: Hoi Cho Nong San ====
        private static bool IsFairEntry(ItemDef d) =>
            d != null && (d.Type == ItemType.Crop || SkillSystem.IsAnimalProduct(d.Id)
                || d.Id.StartsWith("mut_") || d.Id.StartsWith("ruou_") || d.Id is "wine" or "pho_mai");

        private string JudgeFair()
        {
            var top = Inventory.Instance.Slots.Select(s => ItemDatabase.Instance.GetItem(s.ItemId))
                .Where(IsFairEntry).Distinct().OrderByDescending(d => d.SellPrice).Take(3).ToList();
            if (top.Count == 0) return Loc.T("fest.fair_need");
            int score = top.Sum(d => d.SellPrice);
            string list = string.Join(", ", top.Select(d => ItemDatabase.Instance.GetDisplayName(d.Id)));
            string place;
            if (score >= 300) { place = Loc.T("fest.place1"); Complete(Kind.HarvestFair, 1500, "cup_hoi_cho", 1); }
            else if (score >= 120) { place = Loc.T("fest.place2"); Complete(Kind.HarvestFair, 600); }
            else { place = Loc.T("fest.place3"); Complete(Kind.HarvestFair, 200); }
            return string.Format(Loc.T("fest.fair_result_fmt"), list, score, place);
        }

        // ==== Dong: Anh Sao (ban bi mat) ====
        private string StarNight()
        {
            int day = GameManager.Instance.Day;
            var fs = FriendshipSystem.Instance;
            if (SecretFriendDay != day || string.IsNullOrEmpty(SecretFriendId))
            {
                var pool = ItemDatabase.Instance.NpcGifts.Keys.Where(fs.IsKnown).ToList();
                if (pool.Count == 0) pool = ItemDatabase.Instance.NpcGifts.Keys.ToList();
                SecretFriendId = pool[(int)(GD.Randi() % (uint)pool.Count)];
                SecretFriendDay = day;
                return string.Format(Loc.T("fest.star_assigned_fmt"), fs.GetName(SecretFriendId));
            }
            if (!fs.GiftedToday(SecretFriendId))
                return string.Format(Loc.T("fest.star_remind_fmt"), fs.GetName(SecretFriendId));
            fs.AddPoints(SecretFriendId, 100);
            Complete(Kind.StarNight, 800, "qua_dong_chi", 1);
            return string.Format(Loc.T("fest.star_done_fmt"), fs.GetName(SecretFriendId));
        }
    }
}
