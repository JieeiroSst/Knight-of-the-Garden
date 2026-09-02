using Godot;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Core
{
    // Singleton trung tam: giu trang thai game, chi so nguoi choi, tien, ngay.
    // Autoload -> truy cap tu bat ky dau bang GameManager.Instance
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        // ==== Chi so nguoi choi ====
        public int MaxHp = 100;
        public int Hp = 100;
        public int Level = 1;
        public int Exp = 0;
        public int ExpToNext = 100;
        public int Gold = 50;
        public int Day = 1;

        // Da tra vang mo khoa Nha Kinh chua (xem GreenhouseGate.cs) - mo 1 LAN DUY NHAT, luu qua
        // SaveSystem de khong phai tra lai moi lan choi.
        public bool GreenhouseUnlocked = false;

        // ==== Thoi gian thuc: dong bo hoan toan theo dong ho/lich may tinh ====
        // 1 ngay trong game = 1 ngay thuc (24h that), khong con bo dem gia lap.
        public float DayProgress => (float)(System.DateTime.Now.TimeOfDay.TotalSeconds / 86400.0);
        public int Hour => System.DateTime.Now.Hour;
        public int RealDay => System.DateTime.Now.Day;
        public int RealMonth => System.DateTime.Now.Month;
        public int RealYear => System.DateTime.Now.Year;
        public bool IsNight => Hour < 6 || Hour >= 19;

        // Thoi tiet don gian: 1 co "co dang mua" hay khong, doi MOI NGAY THAT (khong doi giua
        // ngay), tinh xac dinh theo Day (cung 1 ngay luon cho cung 1 ket qua) - dung cho Henri
        // (bao ve trang trai) doi hanh vi ban ngay khi troi mua (xem RepairmanNpc/GuardNpc).
        // Khong co hieu ung hinh anh (khong doi bau troi/hat mua) - chi anh huong hanh vi NPC.
        public bool IsRaining { get; private set; }
        [Signal] public delegate void WeatherChangedEventHandler(bool isRaining);

        // ==== 4 mua (Xuan/Ha/Thu/Dong), 28 ngay THAT/mua (112 ngay/nam) ====
        // Tinh TOAN TU Day (khong luu rieng) - Day la nguon su that duy nhat va DA duoc luu qua
        // save/load (xem ApplyLoadedStats), nen mua tu dong dung ngay sau khi nap lai, khong can
        // them field/logic luu rieng.
        public enum Season { Spring, Summer, Fall, Winter }
        public Season CurrentSeason => (Season)(((Day - 1) / 28) % 4);
        public int DayInSeason => (Day - 1) % 28;
        // Le Hoi Mua Xuan (ngay dau Xuan) + Le Hoi Mua Mang (ngay dau Thu) - dung 1 ngay dac biet
        // gia tot hon, xem GetSeasonalPriceMultiplier.
        public bool IsFestivalDay => DayInSeason == 0 && (CurrentSeason == Season.Spring || CurrentSeason == Season.Fall);
        [Signal] public delegate void SeasonChangedEventHandler(int season);
        private Season _lastSeason;

        private System.DateTime _lastDate = System.DateTime.Now.Date;
        private int _lastHour = System.DateTime.Now.Hour;

        // Su kien de UI lang nghe
        [Signal] public delegate void StatsChangedEventHandler();
        [Signal] public delegate void PlayerDiedEventHandler();
        [Signal] public delegate void DayChangedEventHandler(int day);
        [Signal] public delegate void HourChangedEventHandler(int hour);

        public override void _EnterTree()
        {
            Instance = this;
            RollWeather();
            _lastSeason = CurrentSeason;
        }

        private void RollWeather()
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)Day };
            IsRaining = rng.Randf() < 0.3f; // ~30% so ngay la ngay mua
        }

        public override void _Process(double delta)
        {
            int hour = Hour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                EmitSignal(SignalName.HourChanged, hour);
            }

            var today = System.DateTime.Now.Date;
            if (today != _lastDate)
            {
                _lastDate = today;
                NextDay(); // sang ngay thuc moi -> cay lon len, quai hoi sinh (qua tin hieu DayChanged)
            }
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            if (Gold < 0) Gold = 0;
            EmitSignal(SignalName.StatsChanged);
        }

        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        public void TakeDamage(int dmg)
        {
            Hp -= dmg;
            if (Hp <= 0)
            {
                Hp = 0;
                EmitSignal(SignalName.PlayerDied);
            }
            EmitSignal(SignalName.StatsChanged);
        }

        public void Heal(int amount)
        {
            Hp = Mathf.Min(MaxHp, Hp + amount);
            EmitSignal(SignalName.StatsChanged);
        }

        public void AddExp(int amount)
        {
            Exp += amount;
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                ExpToNext = (int)(ExpToNext * 1.4f);
                MaxHp += 100; // theo yeu cau: moi lan len cap tang 100 mau (truoc day chi +20)
                Hp = MaxHp; // hoi day khi len cap
                GD.Print($"Lên cấp! Level {Level}");
            }
            EmitSignal(SignalName.StatsChanged);
        }

        // Thue + luong nhan cong trang trai gop chung thanh "chi phi van hanh", tru dinh ky moi
        // tuan (7 ngay THAT). Neu khong du vang, CHI bo qua ky do (SpendGold da tra false, khong
        // co co che no/gold am nao trong toan bo code hien tai, khong bia them).
        public const int WeeklyTax = 20;
        public const int WeeklyLaborCost = 30;

        public void NextDay()
        {
            Day++;
            RollWeather();
            EmitSignal(SignalName.DayChanged, Day);
            EmitSignal(SignalName.WeatherChanged, IsRaining);
            var season = CurrentSeason;
            if (season != _lastSeason)
            {
                _lastSeason = season;
                EmitSignal(SignalName.SeasonChanged, (int)season);
            }
            if (Day % 7 == 0)
            {
                int cost = WeeklyTax + WeeklyLaborCost;
                bool paid = SpendGold(cost);
                GD.Print(paid
                    ? $"Đã trừ {cost} vàng chi phí vận hành (thuế {WeeklyTax} + nhân công {WeeklyLaborCost})."
                    : $"Không đủ vàng để trả chi phí vận hành tuần này ({cost} vàng).");
            }
            EmitSignal(SignalName.StatsChanged);
        }

        // Thu (+30% cho nong san Type=Crop) / Dong (+20% cho san pham chan nuoi - "chan nuoi tro
        // nen quan trong" khi cay trong bi han che mua nay) / Le Hoi (+20% CONG THEM, khong thay
        // the muc mua) - CHI 1 diem goi duy nhat trong ShopUI.RefreshSell(), xem ghi chu o do.
        private static readonly string[] LivestockProduceIds = { "milk", "egg", "wool" };

        public float GetSeasonalPriceMultiplier(ItemDef def)
        {
            if (def == null) return 1f;
            float mult = 1f;
            if (CurrentSeason == Season.Fall && def.Type == ItemType.Crop) mult += 0.3f;
            if (CurrentSeason == Season.Winter && System.Array.IndexOf(LivestockProduceIds, def.Id) >= 0) mult += 0.2f;
            if (IsFestivalDay) mult += 0.2f;
            return mult;
        }

        // Dung khi nap save
        public void ApplyLoadedStats(int hp, int maxHp, int level, int exp, int expNext, int gold, int day)
        {
            Hp = hp; MaxHp = maxHp; Level = level; Exp = exp;
            ExpToNext = expNext; Gold = gold; Day = day;
            EmitSignal(SignalName.StatsChanged);
        }
    }
}
