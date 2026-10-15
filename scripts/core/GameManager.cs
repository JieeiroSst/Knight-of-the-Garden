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

        // ==== The luc (Stamina) ====
        // Dung cong cu/cau ca/dao quang/chat cay ton the luc (xem Player.cs). Hoi phuc: tu nhien
        // cham theo thoi gian, an mon an (ItemDef.StaminaAmount), ngu tren giuong (Bed.cs).
        // Toi da = 120 + 3 moi cap ky nang (xem SkillSystem.TotalLevels) - tinh dong, khong luu.
        public const float BaseMaxStamina = 120f;
        public const float StaminaRegenPerSec = 1f / 6f; // ~12 phut thuc hoi day tu 0
        public float Stamina = BaseMaxStamina;
        public float MaxStamina => BaseMaxStamina + 3f * (HiepSiVeVuon.Systems.SkillSystem.Instance?.TotalLevels ?? 0)
            + (HiepSiVeVuon.Systems.HomeSystem.Instance?.StaminaBonus ?? 0f);
        // Duoi 10% = kiet suc: di cham hon (xem Player._PhysicsProcess).
        public bool IsExhausted => Stamina < MaxStamina * 0.1f;
        private int _lastStaminaInt = -1;

        // Da tra vang mo khoa Nha Kinh chua (xem GreenhouseGate.cs) - mo 1 LAN DUY NHAT, luu qua
        // SaveSystem de khong phai tra lai moi lan choi.
        public bool GreenhouseUnlocked = false;

        // ==== Thoi gian thuc DONG BO THEO SERVER (game online) ====
        // 1 ngay game = 1 ngay that, LICH CHUNG cho moi nguoi choi (backend game/calendar.js: ngay 1
        // = GAME_EPOCH, mui gio game). Client lay gio server qua /api/time (va moi lan dong bo kinh te)
        // roi tu chay tiep bang dong ho may + do lech - doi gio may tinh KHONG lam cay lon nhanh hon.
        // Truoc khi dong bo (man hinh dang nhap/mat mang) tam dung dong ho may.
        private bool _timeSynced;
        private double _serverOffsetMs;          // serverNow - dong ho may luc dong bo
        private int _tzOffsetMin = 420;
        private long _nextDayAtMs;
        public int ServerDay { get; private set; }
        public bool TimeSynced => _timeSynced;

        private static double LocalUnixMs => Time.GetUnixTimeFromSystem() * 1000.0;
        public double ServerNowMs => LocalUnixMs + _serverOffsetMs;
        // Gio dia phuong CUA GAME (theo mui gio game, khong phai mui gio may nguoi choi).
        public System.DateTime GameNow => _timeSynced
            ? System.DateTimeOffset.FromUnixTimeMilliseconds((long)ServerNowMs).UtcDateTime.AddMinutes(_tzOffsetMin)
            : System.DateTime.Now;

        public float DayProgress => (float)(GameNow.TimeOfDay.TotalSeconds / 86400.0);
        public int Hour => GameNow.Hour;
        public int RealDay => GameNow.Day;
        public int RealMonth => GameNow.Month;
        public int RealYear => GameNow.Year;
        public bool IsNight => Hour < 6 || Hour >= 19;

        // Dang "chay bu" nhung ngay nguoi choi offline (xem CatchUpToServerDay) - nhan cong cham ho
        // cay trong (FarmPlot), bo qua sinh quai/sinh san hang loat (Main).
        public bool IsCatchingUp { get; private set; }
        public const int MaxCatchUpDays = 28;

        // root: JSON tu /api/time hoac truong "time" cua anh chup kinh te.
        public void ApplyServerTime(System.Text.Json.JsonElement root)
        {
            double serverNow = root.GetProperty("serverNow").GetDouble();
            _serverOffsetMs = serverNow - LocalUnixMs;
            _tzOffsetMin = root.GetProperty("tzOffsetMin").GetInt32();
            ServerDay = root.GetProperty("day").GetInt32();
            _nextDayAtMs = root.GetProperty("nextDayAt").GetInt64();
            _timeSynced = true;
            // Nap save xong TRUOC khi dong ho kip dong bo -> chay bu ngay bay gio.
            if (_pendingCatchUp != 0)
            {
                bool newPlayer = _pendingCatchUp == 1;
                _pendingCatchUp = 0;
                CatchUpToServerDay(newPlayer);
            }
        }

        private int _pendingCatchUp; // 0 = khong, 1 = nguoi moi, 2 = nguoi cu

        // Goi sau khi nap save: tien ngay game toi ngay server. newPlayer=true (chua co ban luu) ->
        // nhay thang toi hom nay, khong mo phong gi. Nguoi choi cu -> chay bu tung ngay (toi da
        // MaxCatchUpDays ngay gan nhat) de cay lon/may che bien chay/hang giao duoc ban dung thuc te.
        public void CatchUpToServerDay(bool newPlayer)
        {
            if (!_timeSynced)
            {
                _pendingCatchUp = newPlayer ? 1 : 2;
                return;
            }
            if (ServerDay <= Day) return;
            if (newPlayer || Day <= 0)
            {
                Day = ServerDay;
                RollWeather();
                _lastSeason = CurrentSeason;
                EmitSignal(SignalName.StatsChanged);
                return;
            }
            if (ServerDay - Day > MaxCatchUpDays) Day = ServerDay - MaxCatchUpDays;
            int gap = ServerDay - Day;
            IsCatchingUp = true;
            try { while (Day < ServerDay) NextDay(); }
            finally { IsCatchingUp = false; }
            HiepSiVeVuon.UI.Notify.Show(string.Format(HiepSiVeVuon.Systems.Loc.T("time.caught_up_fmt"), gap));
        }

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
        private int _lastHour = -1;

        // Su kien de UI lang nghe
        [Signal] public delegate void StatsChangedEventHandler();
        [Signal] public delegate void PlayerDiedEventHandler();
        // Nguoi choi trung don (Player.cs choi hoat canh giat minh, rung camera, so sat thuong).
        [Signal] public delegate void PlayerHurtEventHandler(int dmg);
        [Signal] public delegate void DayChangedEventHandler(int day);
        [Signal] public delegate void HourChangedEventHandler(int hour);

        public override void _EnterTree()
        {
            Instance = this;
            RollWeather();
            _lastSeason = CurrentSeason;
        }

        // Thoi tiet chi tiet do WeatherSystem quyet dinh; o day chi giu co "dang mua" cho code cu.
        public void SetRaining(bool raining)
        {
            if (IsRaining == raining) return;
            IsRaining = raining;
            EmitSignal(SignalName.WeatherChanged, raining);
        }

        private void RollWeather()
        {
            if (HiepSiVeVuon.Systems.WeatherSystem.Instance != null) { HiepSiVeVuon.Systems.WeatherSystem.Instance.Recompute(announce: false); return; }
            var rng = new RandomNumberGenerator { Seed = (ulong)Day };
            IsRaining = rng.Randf() < 0.3f; // ~30% so ngay la ngay mua
        }

        public override void _Process(double delta)
        {
            if (Stamina < MaxStamina)
            {
                Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenPerSec * (float)delta);
                // Chi bao UI khi phan nguyen thay doi - tranh phat StatsChanged moi frame.
                int si = (int)Stamina;
                if (si != _lastStaminaInt) { _lastStaminaInt = si; EmitSignal(SignalName.StatsChanged); }
            }

            int hour = Hour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                EmitSignal(SignalName.HourChanged, hour);
            }

            if (_timeSynced)
            {
                // Qua 00:00 gio game (theo dong ho server) -> sang ngay moi chung cho moi nguoi.
                if (ServerNowMs >= _nextDayAtMs)
                {
                    _nextDayAtMs += 86400000;
                    ServerDay++;
                    if (Day < ServerDay) NextDay();
                }
                return;
            }
            var today = System.DateTime.Now.Date;
            if (today != _lastDate)
            {
                _lastDate = today;
                NextDay(); // sang ngay thuc moi -> cay lon len, quai hoi sinh (qua tin hieu DayChanged)
            }
        }

        // source/key: nguon vang cho server kiem tra (kill/chest/reward... - xem EconomyClient).
        public void AddGold(int amount, string source = null, string key = null)
        {
            int before = Gold;
            Gold += amount;
            if (Gold < 0) Gold = 0;
            HiepSiVeVuon.Systems.EconomyClient.Instance?.OnGoldDelta(Gold - before, source, key);
            EmitSignal(SignalName.StatsChanged);
        }

        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            HiepSiVeVuon.Systems.EconomyClient.Instance?.OnGoldDelta(-amount, null, null);
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        // Mien sat thuong ngan (ky nang Luot) - tinh theo dong ho may.
        private ulong _invulnUntil;
        public void SetInvulnerable(double seconds) => _invulnUntil = Time.GetTicksMsec() + (ulong)(seconds * 1000);

        public void TakeDamage(int dmg)
        {
            if (Time.GetTicksMsec() < _invulnUntil || Hp <= 0) return;
            Hp -= dmg;
            EmitSignal(SignalName.PlayerHurt, dmg);
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

        // Tru the luc cho 1 hanh dong - tra false (va KHONG tru) neu khong du, de noi goi bo qua
        // hanh dong do.
        public bool UseStamina(float cost)
        {
            if (cost <= 0f) return true;
            if (Stamina < cost) return false;
            Stamina -= cost;
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        public void RestoreStamina(float amount)
        {
            Stamina = Mathf.Min(MaxStamina, Stamina + amount);
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
            mult += HiepSiVeVuon.Systems.SkillSystem.Instance?.PriceBonus(def) ?? 0f;
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
