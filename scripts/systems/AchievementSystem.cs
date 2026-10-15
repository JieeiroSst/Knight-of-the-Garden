using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Thong ke loi choi (so nong san thu hoach, ca cau duoc, vang tu thung giao hang...)
    // + thanh tuu dinh nghia trong data/achievements.json - dat khi Stats[Stat] >= Target. Phan
    // thuong vang gui qua THU (MailSystem) de nguoi choi tu nhan, khong cong thang.
    public partial class AchievementSystem : Node
    {
        public static AchievementSystem Instance { get; private set; }

        public Dictionary<string, int> Stats = new();
        public HashSet<string> Unlocked = new();

        [Signal] public delegate void AchievementUnlockedEventHandler(string id);

        public override void _EnterTree() => Instance = this;

        public void Reset()
        {
            Stats.Clear();
            Unlocked.Clear();
        }

        public int GetStat(string stat) => Stats.TryGetValue(stat, out var v) ? v : 0;

        public void Add(string stat, int amount = 1)
        {
            if (amount == 0) return;
            Stats[stat] = GetStat(stat) + amount;
            Check(stat);
        }

        // Cho chi so dang "ky luc" (vd cap ky nang cao nhat, so tim cao nhat) - chi tang, khong cong don.
        public void SetMax(string stat, int value)
        {
            if (value <= GetStat(stat)) return;
            Stats[stat] = value;
            Check(stat);
        }

        private void Check(string stat)
        {
            var db = ItemDatabase.Instance;
            if (db == null) return;
            int value = GetStat(stat);
            foreach (var a in db.Achievements)
            {
                if (a.Stat != stat || Unlocked.Contains(a.Id) || value < a.Target) continue;
                Unlocked.Add(a.Id);
                Notify.Show(string.Format(Loc.T("ach.unlocked_fmt"), GetTitle(a)));
                // Vang thuong do SERVER tra theo achievements.json (1 lan/thanh tuu) - thu chi de bao.
                EconomyClient.Instance?.ClaimAchievement(a.Id, a.RewardGold);
                if (a.RewardGold > 0)
                    MailSystem.Instance?.Send("ach_" + a.Id,
                        Loc.T("mail.ach_title"),
                        string.Format(Loc.T("mail.ach_body_paid_fmt"), GetTitle(a), a.RewardGold));
                EmitSignal(SignalName.AchievementUnlocked, a.Id);
            }
        }

        public static string GetTitle(AchievementDef a) =>
            Loc.Current == Loc.Lang.EN && !string.IsNullOrEmpty(a.TitleEn) ? a.TitleEn : a.Title;

        public static string GetDescription(AchievementDef a) =>
            Loc.Current == Loc.Lang.EN && !string.IsNullOrEmpty(a.DescriptionEn) ? a.DescriptionEn : a.Description;
    }
}
