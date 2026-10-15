using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Hop thu: thu chao mung, bao cao giao hang, qua dau mua, nhac sinh nhat, qua tu
    // ban be than thiet, phan thuong thanh tuu. Doc/nhan qua tai Hop Thu (Mailbox.cs) hoac Nhat
    // Ky [K] (JournalUI). Moi thu co Key DUY NHAT - gui trung Key se bi bo qua (tranh gui lai
    // thu "1 lan" sau moi lan tai game).
    public partial class MailSystem : Node
    {
        public static MailSystem Instance { get; private set; }

        public class Letter
        {
            public string Key { get; set; }
            public int Day { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string ItemId { get; set; }
            public int ItemCount { get; set; }
            public int Gold { get; set; }
            public bool Read { get; set; }
            public bool Claimed { get; set; }
            [System.Text.Json.Serialization.JsonIgnore]
            public bool HasAttachment => !Claimed && (Gold > 0 || (!string.IsNullOrEmpty(ItemId) && ItemCount > 0));
        }

        public List<Letter> Letters = new();
        public HashSet<string> SentKeys = new();

        [Signal] public delegate void MailChangedEventHandler();

        public int UnreadCount => Letters.Count(l => !l.Read || l.HasAttachment);

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        public void Reset()
        {
            Letters.Clear();
            SentKeys.Clear();
            EmitSignal(SignalName.MailChanged);
        }

        public void Send(string key, string title, string body, string itemId = null, int itemCount = 0, int gold = 0)
        {
            if (!string.IsNullOrEmpty(key) && !SentKeys.Add(key)) return;
            Letters.Insert(0, new Letter
            {
                Key = key, Day = GameManager.Instance.Day, Title = title, Body = body,
                ItemId = itemId, ItemCount = itemCount, Gold = gold,
            });
            // Giu hop thu gon - bo thu cu nhat DA DOC + DA NHAN neu qua 40 la.
            while (Letters.Count > 40)
            {
                int idx = Letters.FindLastIndex(l => l.Read && !l.HasAttachment);
                if (idx < 0) break;
                Letters.RemoveAt(idx);
            }
            Notify.Show(string.Format(Loc.T("mail.new_fmt"), title));
            EmitSignal(SignalName.MailChanged);
        }

        public void MarkRead(Letter l)
        {
            if (l.Read) return;
            l.Read = true;
            EmitSignal(SignalName.MailChanged);
        }

        public bool Claim(Letter l)
        {
            if (!l.HasAttachment) return false;
            // Qua dinh kem = phan thuong 1 lan theo Key cua thu - server chan nhan lai (ke ca khi sua
            // ban luu de "chua nhan" lai thu cu).
            if (!string.IsNullOrEmpty(l.ItemId) && l.ItemCount > 0)
                EconomyClient.Instance.GrantReward(l.ItemId, l.ItemCount, "reward", $"mail_{l.Key}#i");
            if (l.Gold > 0) EconomyClient.Instance.GrantGold(l.Gold, "reward", $"mail_{l.Key}#g");
            l.Claimed = true;
            l.Read = true;
            EmitSignal(SignalName.MailChanged);
            return true;
        }

        // Goi SAU khi nap save xong (xem Main.cs) - neu goi truoc, ban luu se ghi de mat thu nay.
        public void SendWelcomeIfNeeded()
        {
            Send("welcome", Loc.T("mail.welcome_title"), Loc.T("mail.welcome_body"), "tra_thao_moc", 3);
        }

        private void OnDayChanged(int day)
        {
            var gm = GameManager.Instance;

            // Qua dau mua: 5 hat giong trong duoc trong mua moi.
            if (gm.DayInSeason == 0)
            {
                string season = gm.CurrentSeason.ToString();
                var seeds = ItemDatabase.Instance.Items.Values
                    .Where(d => d.Type == ItemType.Seed && !d.Id.Contains("_premium")
                                && (d.ValidSeasons == null || d.ValidSeasons.Length == 0 || d.ValidSeasons.Contains(season)))
                    .ToList();
                if (seeds.Count > 0)
                {
                    var seed = seeds[(int)(GD.Randi() % (uint)seeds.Count)];
                    Send($"season_{day}", string.Format(Loc.T("mail.season_title_fmt"), Loc.T("season." + season)),
                        Loc.T("mail.season_body"), seed.Id, 5);
                }
            }

            // Sinh nhat hom nay + qua tu ban than (>= 4 tim).
            var fs = FriendshipSystem.Instance;
            if (fs == null) return;
            foreach (var g in ItemDatabase.Instance.NpcGifts.Values)
            {
                if (fs.IsBirthdayToday(g.NpcId) && fs.IsKnown(g.NpcId))
                    Send($"bday_{g.NpcId}_{day}", string.Format(Loc.T("mail.bday_title_fmt"), fs.GetName(g.NpcId)),
                        string.Format(Loc.T("mail.bday_body_fmt"), fs.GetName(g.NpcId)));

                if (fs.GetHearts(g.NpcId) >= 4 && g.Liked.Length > 0 && GD.Randf() < 0.12f)
                {
                    string item = g.Liked[(int)(GD.Randi() % (uint)g.Liked.Length)];
                    Send($"fgift_{g.NpcId}_{day}", string.Format(Loc.T("mail.friend_gift_title_fmt"), fs.GetName(g.NpcId)),
                        string.Format(Loc.T("mail.friend_gift_body_fmt"), fs.GetName(g.NpcId)), item, 1);
                }
            }
        }
    }
}
