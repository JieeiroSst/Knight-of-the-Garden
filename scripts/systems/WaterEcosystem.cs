using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // He sinh thai ho nuoc (xem Main.BuildLakeRegion/WaterTower.cs/WildAnimal.cs) - mo phong DON
    // GIAN kieu Lotka-Volterra: moi loai co 1 "quan the" SO DONG (khong phai dem tung ca the that -
    // cac WildAnimal/AquaticCreature hien co ngoai the gioi chi la 1 MAU DAI DIEN nho de nguoi choi
    // "bat gap", KHONG phai toan bo quan the). San bat/cau ca cua nguoi choi va thap nuoc (chat
    // luong nuoc) tac dong truc tiep len cac con so nay; Main.RespawnWildlife() doc lai de quyet
    // dinh con spawn/despawn ca the hien hinh nao.
    public partial class WaterEcosystem : Node
    {
        public static WaterEcosystem Instance { get; private set; }

        public float WaterQuality = 90f;
        public bool TowerMaintained = true;

        // Vi tri/ban kinh ho THAT - Main.BuildLakeRegion gan 1 lan luc dung the gioi, dung cho
        // Player.cs kiem tra "dang o gan/trong nuoc" (cau ca/boi) ma khong can tham chieu nguoc
        // lai Main.cs.
        public Vector3 LakeCenter;
        public float LakeRadius = 300f;

        public Dictionary<string, float> Population = new()
        {
            { "fish", 500f }, { "duck", 40f }, { "deer", 25f }, { "rabbit", 60f },
            { "fox", 8f }, { "wolf", 4f }, { "frog", 100f },
        };

        private static readonly Dictionary<string, float> Capacity = new()
        {
            { "fish", 900f }, { "duck", 70f }, { "deer", 70f }, { "rabbit", 220f },
            { "fox", 25f }, { "wolf", 12f }, { "frog", 250f },
        };

        [Signal] public delegate void PopulationChangedEventHandler();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        public float Get(string species) => Population.TryGetValue(species, out var v) ? v : 0f;

        public bool IsNearLake(Vector3 pos, float extraRadius = 0f) =>
            new Vector2(pos.X - LakeCenter.X, pos.Z - LakeCenter.Z).Length() <= LakeRadius + extraRadius;

        // Ke san (WildAnimal.cs) bat gap va an duoc 1 con moi - tru 1 luong CO DINH tuong trung
        // (khong phai 1.0 tuyet doi, vi so hien thi la QUAN THE, khong phai ca the don le).
        public void OnPredation(string preyId) => RemovePopulation(preyId, 4f);

        // Nguoi choi cau ca / san ban truc tiep.
        public void OnPlayerCatch(string speciesId, float amount) => RemovePopulation(speciesId, amount);

        // Cho vit an - tang nhe quan the vit (khong gioi han bang Capacity o day, Grow() moi ngay
        // se tu keo ve dung tran).
        public void OnFeedDucks() => AddPopulation("duck", 1.5f);

        private void RemovePopulation(string species, float amount)
        {
            if (!Population.ContainsKey(species)) return;
            Population[species] = Mathf.Max(0f, Population[species] - amount);
            EmitSignal(SignalName.PopulationChanged);
        }

        private void AddPopulation(string species, float amount)
        {
            if (!Population.ContainsKey(species)) return;
            float cap = Capacity.TryGetValue(species, out var c) ? c : float.MaxValue;
            Population[species] = Mathf.Min(cap, Population[species] + amount);
            EmitSignal(SignalName.PopulationChanged);
        }

        private void OnDayChanged(int day)
        {
            // Chat luong nuoc: tien dan ve muc tieu theo thap nuoc con hoat dong hay da hong (xem
            // WaterTower.cs) - dung y "Tram bom -> ong -> ho" / "O nhiem -> chat luong giam".
            float target = TowerMaintained ? 92f : 40f;
            WaterQuality = Mathf.Clamp(Mathf.MoveToward(WaterQuality, target, 4f), 5f, 100f);

            // Xuan: sinh san manh (ech de trung, ca de trung, cay moc). Dong: it hoat dong/ngu dong.
            float season = GameManager.Instance.CurrentSeason switch
            {
                GameManager.Season.Spring => 1.6f,
                GameManager.Season.Winter => 0.4f,
                _ => 1f,
            };

            // Ca: tang truong phu thuoc CHAT LUONG NUOC (dung y "WaterQuality 95 -> nhieu ca").
            float fishQualityMult = Mathf.Clamp(WaterQuality / 90f, 0.2f, 1.3f);
            Grow("fish", 0.05f * season * fishQualityMult);

            // Chim (vit) an ca nho - neu ca qua khan hiem thi chim GIAM thay vi tang (dung y "chim
            // thieu thuc an -> giam" nguoi dung neu).
            float fishRatio = Get("fish") / Capacity["fish"];
            Grow("duck", 0.03f * season * (fishRatio > 0.15f ? 1f : -1.5f));

            Grow("frog", 0.06f * season);
            Grow("rabbit", 0.08f * season);
            Grow("deer", 0.02f * season);

            // San tu nhien "ngoai man hinh" (khong phai luc nguoi choi bat gap) - dam bao quan the
            // con moi khong tang vo han neu khong ai bao gio gap cao/soi that trong game.
            RemovePopulation("rabbit", Get("fox") * 0.4f + Get("wolf") * 0.3f);
            RemovePopulation("deer", Get("wolf") * 0.5f);

            // San moi: tang neu con moi doi dao, giam (doi) neu con moi khan hiem - dung y "cao
            // san thi hut -> tang; thieu thoi thi giam" nguoi dung neu.
            float rabbitRatio = Get("rabbit") / Capacity["rabbit"];
            float deerRatio = Get("deer") / Capacity["deer"];
            Grow("fox", 0.04f * (rabbitRatio > 0.25f ? 1f : -1f));
            Grow("wolf", 0.03f * ((rabbitRatio + deerRatio) * 0.5f > 0.25f ? 1f : -1f));

            EmitSignal(SignalName.PopulationChanged);
        }

        // Tang truong logistic don gian: cang gan Capacity thi tang cang cham; rate am dung lam
        // suy giam tu nhien (vd chim thieu thuc an, thu san doi).
        private void Grow(string species, float rate)
        {
            if (!Population.ContainsKey(species) || !Capacity.TryGetValue(species, out var cap)) return;
            float cur = Population[species];
            float delta = rate * cur * (1f - cur / cap);
            Population[species] = Mathf.Clamp(cur + delta, 0f, cap);
        }
    }
}
