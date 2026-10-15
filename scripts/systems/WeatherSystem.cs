using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Thoi tiet CHUNG cho moi nguoi choi (tinh tat dinh tu Day - lich server chung):
    // Nang, Nhieu May, Mua, Bao (set danh pha cay ngoai troi), Tuyet (mua dong - cay ngoai troi khong
    // lon), Nang Nong (mua he - dat kho nhanh), Suong Mu. Hieu ung hinh anh xem WeatherFX.
    public partial class WeatherSystem : Node
    {
        public static WeatherSystem Instance { get; private set; }

        public enum Kind { Sunny, Cloudy, Rain, Storm, Snow, Heatwave, Fog }

        public Kind Today { get; private set; } = Kind.Sunny;

        [Signal] public delegate void WeatherChangedEventHandler(int kind);

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += _ => Recompute(announce: true);
            Recompute(announce: false);
        }

        public static Kind ForDay(int day, GameManager.Season season)
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)(day * 7919 + 13) };
            float r = rng.Randf();
            return season switch
            {
                GameManager.Season.Spring => r < 0.30f ? Kind.Rain : r < 0.38f ? Kind.Storm : r < 0.50f ? Kind.Cloudy : r < 0.58f ? Kind.Fog : Kind.Sunny,
                GameManager.Season.Summer => r < 0.15f ? Kind.Rain : r < 0.25f ? Kind.Storm : r < 0.40f ? Kind.Heatwave : r < 0.50f ? Kind.Cloudy : Kind.Sunny,
                GameManager.Season.Fall => r < 0.28f ? Kind.Rain : r < 0.33f ? Kind.Storm : r < 0.50f ? Kind.Cloudy : r < 0.62f ? Kind.Fog : Kind.Sunny,
                _ => r < 0.45f ? Kind.Snow : r < 0.60f ? Kind.Cloudy : r < 0.68f ? Kind.Fog : Kind.Sunny,
            };
        }

        public void Recompute(bool announce)
        {
            var gm = GameManager.Instance;
            var k = ForDay(gm.Day, gm.CurrentSeason);
            bool changed = k != Today;
            Today = k;
            gm.SetRaining(k is Kind.Rain or Kind.Storm);
            if (changed || !announce) EmitSignal(SignalName.WeatherChanged, (int)k);
            if (announce && k is Kind.Storm or Kind.Snow or Kind.Heatwave)
                Notify.Show(Loc.T("weather.warn." + k));
        }

        public string DisplayName => Loc.T("weather." + Today);
    }
}
