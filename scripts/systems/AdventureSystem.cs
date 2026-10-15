using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Kham pha & phieu luu: vung da kham pha, ruong bau da mo, cot da dich chuyen da kich
    // hoat, trum da ha (hoi sinh sau 7 ngay), tang ham nguc sau nhat, hien vat da quyen cho Bao
    // Tang. Tat ca LUU qua SaveSystem. Cac entity (TreasureChest/Waystone/BossLair/MuseumCounter/
    // DungeonFloor) doc/ghi trang thai o day.
    public partial class AdventureSystem : Node
    {
        public static AdventureSystem Instance { get; private set; }

        public const int BossRespawnDays = 7;

        public HashSet<string> Discovered = new();
        public HashSet<string> OpenedChests = new();
        public HashSet<string> Waystones = new();
        public Dictionary<string, int> BossDefeatDay = new();
        public int DeepestDungeonFloor = 0;
        public HashSet<string> MuseumDonated = new();
        public int MuseumRewardsClaimed = 0;

        // Diem dang chu y tren ban do (dang ky luc dung the gioi, khong luu).
        public enum PoiKind { Waystone, Boss, Dungeon, Museum, Festival }
        public readonly List<(PoiKind kind, string id, Vector3 pos, string labelKey)> Pois = new();

        [Signal] public delegate void AdventureChangedEventHandler();

        public override void _EnterTree() => Instance = this;

        public void Reset()
        {
            Discovered.Clear();
            OpenedChests.Clear();
            Waystones.Clear();
            BossDefeatDay.Clear();
            DeepestDungeonFloor = 0;
            MuseumDonated.Clear();
            MuseumRewardsClaimed = 0;
        }

        public void RegisterPoi(PoiKind kind, string id, Vector3 pos, string labelKey)
        {
            Pois.RemoveAll(p => p.id == id);
            Pois.Add((kind, id, pos, labelKey));
        }

        // ==== Kham pha vung ====
        public void Discover(string regionName)
        {
            if (string.IsNullOrEmpty(regionName) || !Discovered.Add(regionName)) return;
            GameManager.Instance.AddExp(60);
            AchievementSystem.Instance?.Add("regions_discovered");
            Notify.Show(string.Format(Loc.T("adv.discovered_fmt"), regionName));
            EmitSignal(SignalName.AdventureChanged);
        }

        // ==== Ruong bau ====
        public bool IsChestOpened(string id) => OpenedChests.Contains(id);

        public void MarkChestOpened(string id)
        {
            if (!OpenedChests.Add(id)) return;
            AchievementSystem.Instance?.Add("chests_opened");
            EmitSignal(SignalName.AdventureChanged);
        }

        // ==== Cot da dich chuyen ====
        public bool ActivateWaystone(string id)
        {
            if (!Waystones.Add(id)) return false;
            Notify.Show(Loc.T("adv.waystone_activated"));
            EmitSignal(SignalName.AdventureChanged);
            return true;
        }

        // ==== Trum ====
        public bool IsBossAvailable(string bossId) =>
            !BossDefeatDay.TryGetValue(bossId, out var d) || GameManager.Instance.Day - d >= BossRespawnDays;

        public int BossRespawnIn(string bossId) =>
            BossDefeatDay.TryGetValue(bossId, out var d) ? Mathf.Max(0, BossRespawnDays - (GameManager.Instance.Day - d)) : 0;

        public void OnBossDefeated(string bossId)
        {
            BossDefeatDay[bossId] = GameManager.Instance.Day;
            AchievementSystem.Instance?.Add("bosses_defeated");
            var name = ItemDatabase.Instance.GetEnemy(bossId)?.BossTitle ?? bossId;
            Notify.Show(string.Format(Loc.T("adv.boss_defeated_fmt"), name, BossRespawnDays));
            EmitSignal(SignalName.AdventureChanged);
        }

        public void OnDungeonFloorReached(int floor)
        {
            if (floor <= DeepestDungeonFloor) return;
            DeepestDungeonFloor = floor;
            AchievementSystem.Instance?.SetMax("dungeon_floor", floor);
            GameManager.Instance.AddExp(40 * floor);
        }

        // ==== Bao tang ====
        public static IEnumerable<ItemDef> AllCollectibles =>
            ItemDatabase.Instance.Items.Values.Where(d => d.Collectible).OrderBy(d => d.Rarity).ThenBy(d => d.SellPrice);

        // Moc thuong theo so hien vat da quyen.
        public static readonly (int count, int gold, string itemId, int itemCount)[] MuseumRewards =
        {
            (3, 300, "potion", 5),
            (6, 600, "hong_ngoc", 1),
            (10, 1200, "cuoc_vang", 1),
            (15, 2500, "kim_cuong", 2),
            (20, 5000, "pickaxe_vang", 1),
            (25, 10000, "chen_vang_co", 1),
        };

        public bool Donate(string itemId)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null || !def.Collectible || MuseumDonated.Contains(itemId)) return false;
            if (!Inventory.Instance.RemoveItem(itemId, 1)) return false;
            MuseumDonated.Add(itemId);
            AchievementSystem.Instance?.SetMax("museum_donated", MuseumDonated.Count);
            Notify.Show(string.Format(Loc.T("museum.donated_fmt"), ItemDatabase.Instance.GetDisplayName(itemId)));
            while (MuseumRewardsClaimed < MuseumRewards.Length && MuseumDonated.Count >= MuseumRewards[MuseumRewardsClaimed].count)
            {
                var r = MuseumRewards[MuseumRewardsClaimed++];
                MailSystem.Instance?.Send($"museum_{r.count}", Loc.T("museum.reward_title"),
                    string.Format(Loc.T("museum.reward_body_fmt"), r.count), r.itemId, r.itemCount, r.gold);
            }
            EmitSignal(SignalName.AdventureChanged);
            return true;
        }
    }
}
