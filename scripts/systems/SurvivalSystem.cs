using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Sinh ton: NO (doi), NUOC (khat), NHIET DO co the.
    //  - No/nuoc giam dan theo thoi gian that (chay/cuoi ngua phi/danh nhau giam nhanh hon, troi nong khat
    //    gap doi). An mon an hoi no (ItemDef.Food), uong hoi nuoc (ItemDef.Water); dung canh ho/song/gieng
    //    bam [E] de uong truc tiep.
    //  - Nhiet do = mua + ngay/dem + thoi tiet + sinh canh/vung (nui lanh, hoang mac nong) + do am cua giap/
    //    trang phuc (ItemDef.Warmth) + lua trai/trong nha. Lanh -> mat the luc, ret cong -> mat mau; nong ->
    //    khat nhanh, say nang -> mat mau.
    //  - Doi/khat can -> mat mau dan (co the chet). No + du nuoc -> tu hoi mau cham.
    public partial class SurvivalSystem : Node
    {
        public static SurvivalSystem Instance { get; private set; }

        public const float Max = 100f;
        public float Hunger { get; set; } = Max; // 100 = no
        public float Thirst { get; set; } = Max; // 100 = du nuoc
        public float BodyTemp { get; private set; } = 22f; // do C cam nhan
        public bool Cold => BodyTemp < 8f;
        public bool Freezing => BodyTemp < -2f;
        public bool Hot => BodyTemp > 33f;
        public bool Heatstroke => BodyTemp > 41f;

        private const float HungerPerSec = Max / (80f * 60f); // ~80 phut tu no -> doi
        private const float ThirstPerSec = Max / (55f * 60f);
        private double _tick, _hurtAcc, _regenAcc;
        private string _lastWarn = "";

        public override void _EnterTree() => Instance = this;

        public void Reset() { Hunger = Max; Thirst = Max; }

        // Goi khi an/uong 1 mon (Inventory.UseConsumable).
        public void Consume(ItemDef def)
        {
            if (def == null) return;
            if (def.Food > 0) Hunger = Mathf.Min(Max, Hunger + def.Food);
            if (def.Water > 0) Thirst = Mathf.Min(Max, Thirst + def.Water);
        }

        public void DrinkFromSource()
        {
            Thirst = Mathf.Min(Max, Thirst + 25f);
            Notify.Show(Loc.T("surv.drank"));
        }

        public void OnRespawn()
        {
            Hunger = Mathf.Max(Hunger, 60f);
            Thirst = Mathf.Max(Thirst, 60f);
        }

        private Player PlayerNode => GetTree().GetFirstNodeInGroup("player") as Player;

        public override void _Process(double delta)
        {
            _tick += delta;
            if (_tick < 1.0) return;
            float dt = (float)_tick;
            _tick = 0;
            var p = PlayerNode;
            var gm = GameManager.Instance;
            if (p == null || gm == null || p.IsDowned) return;

            float effort = p.EffortLevel; // 1 = binh thuong, cao hon khi chay/phi ngua/danh nhau
            BodyTemp = ComputeTemperature(p);
            Hunger = Mathf.Max(0f, Hunger - HungerPerSec * effort * dt * (Cold ? 1.3f : 1f));
            Thirst = Mathf.Max(0f, Thirst - ThirstPerSec * effort * dt * (Hot ? 2f : 1f));

            // Hau qua: mat mau dan khi doi/khat/ret cong/say nang (khong qua tao bao - van kip xu ly).
            float hurtPerSec = 0f;
            if (Hunger <= 0f) hurtPerSec += 0.5f;
            if (Thirst <= 0f) hurtPerSec += 0.7f;
            if (Freezing) hurtPerSec += 0.35f;
            if (Heatstroke) hurtPerSec += 0.3f;
            _hurtAcc += hurtPerSec * dt;
            if (_hurtAcc >= 1.0 && gm.Hp > 0)
            {
                int dmg = (int)_hurtAcc;
                _hurtAcc -= dmg;
                gm.TakeDamage(dmg);
            }
            if (Cold) gm.UseStamina(0.6f * dt);
            // No + du nuoc + de chiu -> tu hoi mau cham.
            if (Hunger > 70f && Thirst > 70f && !Cold && !Hot && gm.Hp < gm.MaxHp)
            {
                _regenAcc += 0.25 * dt;
                if (_regenAcc >= 1.0) { int h = (int)_regenAcc; _regenAcc -= h; gm.Heal(h); }
            }
            Warn();
        }

        private void Warn()
        {
            string w = Hunger <= 0f ? "surv.starving" : Thirst <= 0f ? "surv.dehydrated" : Freezing ? "surv.freezing" : Heatstroke ? "surv.heatstroke"
                : Hunger < 20f ? "surv.hungry" : Thirst < 20f ? "surv.thirsty" : Cold ? "surv.cold" : Hot ? "surv.hot" : "";
            if (w != _lastWarn && w != "") Notify.Show(Loc.T(w));
            _lastWarn = w;
        }

        // ==== Nhiet do ====
        public float ComputeTemperature(Player p)
        {
            var gm = GameManager.Instance;
            float t = gm.CurrentSeason switch
            {
                GameManager.Season.Summer => 30f, GameManager.Season.Winter => 2f, GameManager.Season.Fall => 15f, _ => 19f,
            };
            if (gm.IsNight) t -= 8f;
            t += (WeatherSystem.Instance?.Today ?? WeatherSystem.Kind.Sunny) switch
            {
                WeatherSystem.Kind.Rain => -4f, WeatherSystem.Kind.Storm => -6f, WeatherSystem.Kind.Snow => -10f,
                WeatherSystem.Kind.Heatwave => 9f, WeatherSystem.Kind.Fog => -2f, _ => 0f,
            };
            var pos = p.GlobalPosition;
            var flat = new Vector2(pos.X, pos.Z);
            var region = WorldStreamer.FindRegion(pos)?.Name;
            if (region == "Núi" || region == "Hang Động") t -= 9f;
            else if (region == "Đầm Lầy") t += 2f;
            else if (WorldBiomes.Wildness(flat) >= 0.5f)
                t += WorldBiomes.At(flat) switch
                {
                    WorldBiomes.Biome.Highlands => -8f,
                    WorldBiomes.Biome.Barrens => gm.IsNight ? -6f : 10f,
                    WorldBiomes.Biome.Forest => -2f,
                    WorldBiomes.Biome.Marsh => 2f,
                    _ => 0f,
                };
            t += WarmthFromGear();
            if (p.IsIndoors) t += 8f;
            if (NearFire(pos)) t += 14f;
            return t;
        }

        public static int WarmthFromGear()
        {
            int w = 0;
            foreach (var id in new[] { Inventory.Instance.EquippedArmor, Inventory.Instance.EquippedOutfit })
                if (id != null && ItemDatabase.Instance.GetItem(id) is ItemDef d && (d.MaxDurability <= 0 || ItemConditions.IsUsable(id))) w += d.Warmth;
            return w;
        }

        private bool NearFire(Vector3 pos)
        {
            foreach (Node n in GetTree().GetNodesInGroup("base_structures"))
                if (n is BaseStructure s && s.Info.Kind is "campfire" or "forge" && s.GlobalPosition.DistanceTo(pos) < 170f) return true;
            foreach (Node n in GetTree().GetNodesInGroup("forge_stations"))
                if (n is Node3D f && f.GlobalPosition.DistanceTo(pos) < 170f) return true;
            return false;
        }
    }
}
