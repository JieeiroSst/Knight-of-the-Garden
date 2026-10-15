using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] 5 ky nang rieng (Trong trot/Chan nuoi/Cau ca/Khai khoang/Hai luom), moi ky nang
    // co EXP/cap 0..10. Moi cap mo bonus nho (xem cac ham *Bonus ben duoi) + cong 3 the luc toi
    // da (GameManager.MaxStamina) + mo khoa cong thuc che tao (recipes.json UnlockSkill/Level).
    public partial class SkillSystem : Node
    {
        public static SkillSystem Instance { get; private set; }

        public const int MaxLevel = 10;
        // EXP TICH LUY can de dat cap 1..10.
        private static readonly int[] XpForLevel = { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };

        public Dictionary<SkillType, int> Xp = new();

        [Signal] public delegate void SkillLevelUpEventHandler(int skill, int level);

        public override void _EnterTree()
        {
            Instance = this;
            Reset();
        }

        public void Reset()
        {
            foreach (SkillType s in System.Enum.GetValues(typeof(SkillType))) Xp[s] = 0;
        }

        public int GetLevel(SkillType s)
        {
            int xp = Xp.TryGetValue(s, out var v) ? v : 0;
            int lvl = 0;
            while (lvl < MaxLevel && xp >= XpForLevel[lvl]) lvl++;
            return lvl;
        }

        // (EXP trong cap hien tai, EXP can cho cap ke tiep) - dung cho thanh tien trinh UI.
        public (int cur, int need) GetProgress(SkillType s)
        {
            int lvl = GetLevel(s);
            if (lvl >= MaxLevel) return (1, 1);
            int prev = lvl == 0 ? 0 : XpForLevel[lvl - 1];
            return (Xp[s] - prev, XpForLevel[lvl] - prev);
        }

        public int TotalLevels
        {
            get
            {
                int t = 0;
                foreach (SkillType s in System.Enum.GetValues(typeof(SkillType))) t += GetLevel(s);
                return t;
            }
        }

        public void AddXp(SkillType s, int amount)
        {
            if (amount <= 0) return;
            int before = GetLevel(s);
            Xp[s] = (Xp.TryGetValue(s, out var v) ? v : 0) + amount;
            int after = GetLevel(s);
            if (after > before)
            {
                Notify.Show(string.Format(Loc.T("skill.levelup_fmt"), Loc.T("skill." + s), after));
                EmitSignal(SignalName.SkillLevelUp, (int)s, after);
                AchievementSystem.Instance?.SetMax("max_skill_level", after);
                GameManager.Instance.EmitSignal(GameManager.SignalName.StatsChanged);
            }
        }

        // ==== Bonus theo cap ====

        // Trong trot: +2%/cap co hoi thu hoach them 1 nong san, +0.03 diem chat luong/cap.
        public float ExtraCropChance => GetLevel(SkillType.Farming) * 0.02f;
        public float FarmingQualityBonus => GetLevel(SkillType.Farming) * 0.03f;
        // Cau ca: vung xanh minigame rong them 4%/cap.
        public float FishingZoneBonus => GetLevel(SkillType.Fishing) * 0.04f;
        // Khai khoang: +5%/cap co hoi dao duoc gap doi.
        public float ExtraOreChance => GetLevel(SkillType.Mining) * 0.05f;
        // Hai luom: +5%/cap co hoi nhat duoc gap doi do hai luom/go.
        public float DoubleForageChance => GetLevel(SkillType.Foraging) * 0.05f;

        // Giam the luc ton theo cap ky nang lien quan (toi da -40% o cap 10).
        public float StaminaCost(SkillType s, float baseCost) =>
            baseCost * (1f - GetLevel(s) * 0.04f) * (Inventory.Instance?.OutfitStaminaMult(s) ?? 1f);

        private static readonly HashSet<string> AnimalProducts = new()
        {
            "milk", "egg", "wool", "cheese", "pho_mai", "mayonnaise", "vai", "honey", "mat_ong", "beeswax",
        };
        public static bool IsAnimalProduct(string id) => AnimalProducts.Contains(id);

        // He so gia ban cong them (cong vao GameManager.GetSeasonalPriceMultiplier - dung chung
        // cho ca Cua Hang lan Thung Giao Hang).
        public float PriceBonus(ItemDef def)
        {
            if (def == null) return 0f;
            if (def.Type == ItemType.Crop) return GetLevel(SkillType.Farming) * 0.02f;
            if (IsAnimalProduct(def.Id)) return GetLevel(SkillType.Animal) * 0.03f + (HomeSystem.Instance?.AnimalPriceBonus ?? 0f);
            if (def.Id == "ca" || def.Id.StartsWith("ca_")) return GetLevel(SkillType.Fishing) * 0.03f;
            return 0f;
        }
    }
}
