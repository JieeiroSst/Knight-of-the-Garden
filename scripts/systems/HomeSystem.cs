using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Nha & vat nuoi: cap nang cap nha (them the luc toi da + o ruong chua do), Ruong
    // Chua Do trong nha, do vui cua dan vat nuoi (vuot ve/duoc cho an -> vui; vui thi them san
    // pham + gia ban cao hon), vi tri Bu Nhin nguoi choi da cam. Tat ca LUU qua SaveSystem.
    public partial class HomeSystem : Node
    {
        public static HomeSystem Instance { get; private set; }

        public const int MaxHouseLevel = 3;
        public const int ChestBaseSlots = 24;
        public const int ChestSlotsPerLevel = 24;
        public const float StaminaPerLevel = 25f;

        // Chi phi nang cap len cap (index+1): vang + vat lieu - nap tu data/house.json (backend dung
        // CHUNG file nay de kiem tra, xem EconomyClient.UpgradeHouse).
        public static (int gold, (string id, int count)[] mats)[] Upgrades =>
            ItemDatabase.Instance.HouseUpgrades.OrderBy(h => h.Level)
                .Select(h => (h.Gold, h.Materials.Select(m => (m.ItemId, m.Count)).ToArray())).ToArray();

        public int HouseLevel = 0;
        public float HerdHappiness = 60f;
        public List<ShippingSystem.Entry> Chest = new();
        public List<Vector2> ScarecrowPositions = new();

        private int _petGainDay = -1;
        private float _petGainToday = 0f;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += _ => OnDayChanged();
            // Cap nha la du lieu kinh te -> server la nguon su that.
            EconomyClient.Instance.SnapshotApplied += () => HouseLevel = EconomyClient.Instance.HouseLevel;
        }

        public void Reset()
        {
            HouseLevel = 0;
            HerdHappiness = 60f;
            Chest.Clear();
            ScarecrowPositions.Clear();
        }

        public float StaminaBonus => HouseLevel * StaminaPerLevel;
        public int ChestCapacity => ChestBaseSlots + HouseLevel * ChestSlotsPerLevel;

        // ==== Nang cap nha ====
        public bool CanUpgrade(out string missing)
        {
            missing = null;
            if (HouseLevel >= MaxHouseLevel) { missing = Loc.T("home.max_level"); return false; }
            var (gold, mats) = Upgrades[HouseLevel];
            var lacks = new List<string>();
            if (GameManager.Instance.Gold < gold) lacks.Add($"{gold}g");
            foreach (var (id, count) in mats)
                if (EconomyClient.TotalOwned(id) < count)
                    lacks.Add($"{ItemDatabase.Instance.GetDisplayName(id)} {EconomyClient.TotalOwned(id)}/{count}");
            if (lacks.Count > 0) missing = string.Join(", ", lacks);
            return lacks.Count == 0;
        }

        public bool Upgrade()
        {
            if (!CanUpgrade(out var missing)) { Notify.Show(string.Format(Loc.T("home.missing_fmt"), missing)); return false; }
            var (gold, mats) = Upgrades[HouseLevel];
            EconomyClient.Instance.UpgradeHouse(gold, mats);
            HouseLevel++;
            AchievementSystem.Instance?.SetMax("house_level", HouseLevel);
            GameManager.Instance.RestoreStamina(StaminaPerLevel);
            Notify.Show(string.Format(Loc.T("home.upgraded_fmt"), HouseLevel, (int)StaminaPerLevel, ChestCapacity));
            return true;
        }

        // ==== Ruong chua do (moi o 1 loai vat pham, khong gioi han so luong/o) ====
        public bool Deposit(string itemId, int count)
        {
            count = Mathf.Min(count, Inventory.Instance.CountOf(itemId));
            if (count <= 0) return false;
            var e = Chest.FirstOrDefault(x => x.Id == itemId);
            if (e == null && Chest.Count >= ChestCapacity) { Notify.Show(Loc.T("home.chest_full")); return false; }
            // Tui/balo/ruong la 1 kho chung tren server -> chuyen qua lai KHONG tao op kinh te.
            using (EconomyClient.Suppress())
                if (!Inventory.Instance.RemoveItem(itemId, count)) return false;
            if (e != null) e.Count += count; else Chest.Add(new ShippingSystem.Entry { Id = itemId, Count = count });
            return true;
        }

        public bool Withdraw(string itemId)
        {
            var e = Chest.FirstOrDefault(x => x.Id == itemId);
            if (e == null) return false;
            int before = Inventory.Instance.CountOf(itemId);
            using (EconomyClient.Suppress()) Inventory.Instance.AddItem(itemId, e.Count);
            int moved = Inventory.Instance.CountOf(itemId) - before;
            if (moved <= 0) { Notify.Show(Loc.T("mail.inventory_full")); return false; }
            e.Count -= moved;
            if (e.Count <= 0) Chest.Remove(e);
            return true;
        }

        public int ChestCount(string itemId) => Chest.FirstOrDefault(x => x.Id == itemId)?.Count ?? 0;

        // Dung boi EconomyClient khi dong bo theo server (khong tao op).
        public void ChestAdd(string itemId, int n)
        {
            var e = Chest.FirstOrDefault(x => x.Id == itemId);
            if (e != null) e.Count += n; else Chest.Add(new ShippingSystem.Entry { Id = itemId, Count = n });
        }

        public void ChestRemove(string itemId, int n)
        {
            var e = Chest.FirstOrDefault(x => x.Id == itemId);
            if (e == null) return;
            e.Count -= n;
            if (e.Count <= 0) Chest.Remove(e);
        }

        // ==== Do vui vat nuoi ====
        public void OnAnimalPetted()
        {
            int day = GameManager.Instance.Day;
            if (_petGainDay != day) { _petGainDay = day; _petGainToday = 0f; }
            if (_petGainToday >= 20f) return;
            _petGainToday += 2f;
            HerdHappiness = Mathf.Min(100f, HerdHappiness + 2f);
        }

        private void OnDayChanged()
        {
            float delta = -6f;
            if (!FarmStorage.Instance.IsLow("thucan_giasuc", 5)) delta += 4f; // co thuc an trong kho -> nguoi cham soc cho an deu
            if (GameManager.Instance.CurrentSeason == GameManager.Season.Winter) delta -= 2f;
            HerdHappiness = Mathf.Clamp(HerdHappiness + delta, 0f, 100f);
        }

        // Co hoi nhan them 1 san pham chan nuoi khi nhat (0..40%), va thuong gia khi rat vui.
        public float AnimalBonusChance => Mathf.Clamp((HerdHappiness - 50f) / 125f, 0f, 0.4f);
        public float AnimalPriceBonus => HerdHappiness >= 80f ? 0.1f : 0f;

        public string HappinessLabel => HerdHappiness >= 80f ? Loc.T("home.mood.great")
            : HerdHappiness >= 55f ? Loc.T("home.mood.good")
            : HerdHappiness >= 30f ? Loc.T("home.mood.ok") : Loc.T("home.mood.sad");
    }
}
