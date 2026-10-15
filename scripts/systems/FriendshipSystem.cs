using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Do than thiet voi tung NPC (0..1000 diem = 0..10 tim, 100 diem/tim) - LUU qua
    // save (truoc day NPC.Trust chi song trong bo nho, mat sau moi lan tai game). Tang diem: noi
    // chuyen lan dau moi ngay (+20), tang qua 1 lan/ngay (Yeu thich +80, Thich +45, Binh thuong
    // +20, Ghet -30), qua dung sinh nhat x5. NPC.Trust (0..100) gio doc tu day (diem/10).
    public partial class FriendshipSystem : Node
    {
        public static FriendshipSystem Instance { get; private set; }

        public const int MaxPoints = 1000;
        public const int PointsPerHeart = 100;

        public Dictionary<string, int> Points = new();
        // Ngay (GameManager.Day) noi chuyen/tang qua gan nhat - gioi han 1 lan/ngay moi loai.
        public Dictionary<string, int> LastTalkDay = new();
        public Dictionary<string, int> LastGiftDay = new();

        // Ten hien thi theo NpcId - NPC tu dang ky luc _Ready (xem NPC.cs), khong luu.
        private readonly Dictionary<string, string> _names = new();

        public enum GiftTaste { Loved, Liked, Neutral, Disliked }

        public override void _EnterTree() => Instance = this;

        public void Reset()
        {
            Points.Clear();
            LastTalkDay.Clear();
            LastGiftDay.Clear();
        }

        public void RegisterName(string npcId, string name)
        {
            if (!string.IsNullOrEmpty(npcId) && !_names.ContainsKey(npcId)) _names[npcId] = name;
        }

        public string GetName(string npcId) => _names.TryGetValue(npcId, out var n) ? n : npcId;
        public bool IsKnown(string npcId) => Points.ContainsKey(npcId);
        public int GetPoints(string npcId) => Points.TryGetValue(npcId, out var p) ? p : 0;
        public int GetHearts(string npcId) => GetPoints(npcId) / PointsPerHeart;
        public bool GiftedToday(string npcId) => LastGiftDay.TryGetValue(npcId, out var d) && d == GameManager.Instance.Day;

        public bool IsBirthdayToday(string npcId)
        {
            var gm = GameManager.Instance;
            return ItemDatabase.Instance.NpcGifts.TryGetValue(npcId, out var g)
                && g.BirthdaySeason == gm.CurrentSeason.ToString()
                && g.BirthdayDay == gm.DayInSeason + 1;
        }

        public string GetBirthdayText(string npcId)
        {
            if (!ItemDatabase.Instance.NpcGifts.TryGetValue(npcId, out var g) || string.IsNullOrEmpty(g.BirthdaySeason)) return "";
            return string.Format(Loc.T("friend.bday_fmt"), Loc.T("season." + g.BirthdaySeason), g.BirthdayDay);
        }

        public void AddPoints(string npcId, int delta)
        {
            int before = GetHearts(npcId);
            Points[npcId] = Mathf.Clamp(GetPoints(npcId) + delta, 0, MaxPoints);
            int after = GetHearts(npcId);
            if (after > before)
                Notify.Show(string.Format(Loc.T("friend.heart_up_fmt"), GetName(npcId), after));
            AchievementSystem.Instance?.SetMax("max_hearts", after);
        }

        // Goi tu NPC.Interact - chi cong diem lan noi chuyen DAU TIEN moi ngay.
        public void OnTalk(string npcId)
        {
            int day = GameManager.Instance.Day;
            if (LastTalkDay.TryGetValue(npcId, out var d) && d == day)
            {
                if (!Points.ContainsKey(npcId)) Points[npcId] = 0;
                return;
            }
            LastTalkDay[npcId] = day;
            AddPoints(npcId, 20);
        }

        public GiftTaste GetTaste(string npcId, string itemId)
        {
            if (ItemDatabase.Instance.NpcGifts.TryGetValue(npcId, out var g))
            {
                if (System.Array.IndexOf(g.Loved, itemId) >= 0) return GiftTaste.Loved;
                if (System.Array.IndexOf(g.Liked, itemId) >= 0) return GiftTaste.Liked;
                if (System.Array.IndexOf(g.Disliked, itemId) >= 0) return GiftTaste.Disliked;
            }
            // So thich chung cho NPC khong co trong npc_gifts.json (dan thi tran, nhan cong...):
            // ai cung thich mon an nau san + san pham che bien, ai cung ghet do vun.
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null) return GiftTaste.Neutral;
            if (def.Type == ItemType.Consumable && def.StaminaAmount > 0) return GiftTaste.Liked;
            if (def.Rarity >= Rarity.Rare) return GiftTaste.Liked;
            if (itemId is "rong" or "da" or "xuong" or "mud_core") return GiftTaste.Disliked;
            return GiftTaste.Neutral;
        }

        // Tra ve null neu tang thanh cong (kem thong bao), hoac chuoi ly do neu khong tang duoc.
        public string GiveGift(string npcId, string itemId)
        {
            if (GiftedToday(npcId)) return string.Format(Loc.T("friend.already_gifted_fmt"), GetName(npcId));
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null || def.Type is ItemType.Quest) return Loc.T("friend.cannot_gift");
            if (!Inventory.Instance.RemoveItem(itemId, 1)) return Loc.T("friend.cannot_gift");

            var taste = GetTaste(npcId, itemId);
            int pts = taste switch { GiftTaste.Loved => 80, GiftTaste.Liked => 45, GiftTaste.Disliked => -30, _ => 20 };
            bool bday = IsBirthdayToday(npcId);
            if (bday) pts *= 5;
            LastGiftDay[npcId] = GameManager.Instance.Day;
            AddPoints(npcId, pts);
            AchievementSystem.Instance?.Add("gifts_given");

            string reaction = Loc.T("friend.taste." + taste);
            Notify.Show(string.Format(Loc.T("friend.gift_given_fmt"), GetName(npcId), ItemDatabase.Instance.GetDisplayName(itemId), reaction)
                + (bday ? " " + Loc.T("friend.bday_bonus") : ""));
            return null;
        }
    }
}
