using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Data
{
    // ==== Cac loai vat pham ====
    public enum ItemType { Tool, Weapon, Armor, Seed, Crop, Consumable, Material, Quest }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    // Dinh nghia mot vat pham (data-driven, nap tu JSON)
    public class ItemDef
    {
        public string Id;
        public string Name;             // Ten hien thi (tieng Viet)
        public string Description;
        public ItemType Type;
        public Rarity Rarity = Rarity.Common;
        public int MaxStack = 99;
        public int SellPrice = 0;
        public int BuyPrice = 0;
        public string IconPath;         // res:// duong dan icon

        // Chi so cho vu khi / giap
        public int Damage = 0;
        public int Defense = 0;
        public int HealAmount = 0;      // cho consumable

        // Cho hat giong -> cay trong
        public string GrowsIntoCropId;  // id cay thu hoach
        public int GrowDays = 3;
    }

    // Trang thai mot o inventory
    public class ItemStack
    {
        public string ItemId;
        public int Count;
        public ItemStack(string id, int count) { ItemId = id; Count = count; }
    }

    // Dinh nghia quai vat (data-driven)
    public class EnemyDef
    {
        public string Id;
        public string Name;
        public int MaxHp;
        public int Damage;
        public float Speed;
        public float DetectRange;
        public int ExpReward;
        public int GoldReward;
        public string SpritePath;
        public List<LootEntry> Loot = new();
    }

    public class LootEntry
    {
        public string ItemId;
        public float Chance;   // 0..1
        public int Min = 1;
        public int Max = 1;
    }

    // Dinh nghia nhiem vu
    public class QuestDef
    {
        public string Id;
        public string Title;
        public string Description;
        public string ObjectiveType;   // "collect" | "kill" | "talk"
        public string TargetId;        // itemId hoac enemyId hoac npcId
        public int TargetCount = 1;
        public int RewardGold = 0;
        public string RewardItemId = null;
        public int RewardItemCount = 0;
    }
}
