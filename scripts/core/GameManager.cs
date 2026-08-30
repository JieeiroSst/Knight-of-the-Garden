using System;
using Godot;

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

        // ==== Thoi gian thuc: dong bo hoan toan theo dong ho/lich may tinh ====
        // 1 ngay trong game = 1 ngay thuc (24h that), khong con bo dem gia lap.
        public float DayProgress => (float)(System.DateTime.Now.TimeOfDay.TotalSeconds / 86400.0);
        public int Hour => System.DateTime.Now.Hour;
        public int RealDay => System.DateTime.Now.Day;
        public int RealMonth => System.DateTime.Now.Month;
        public int RealYear => System.DateTime.Now.Year;
        public bool IsNight => Hour < 6 || Hour >= 19;

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

        private void EmitSignal(object hourChanged, int hour)
        {
            throw new NotImplementedException();
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
                MaxHp += 20;
                Hp = MaxHp; // hoi day khi len cap
                GD.Print($"Len cap! Level {Level}");
            }
            EmitSignal(SignalName.StatsChanged);
        }

        public void NextDay()
        {
            Day++;
            EmitSignal(SignalName.DayChanged, Day);
            EmitSignal(SignalName.StatsChanged);
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
