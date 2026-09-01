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

        // Cho phan bon (xem FarmPlot.UseOn) - 0 nghia la item nay KHONG phai phan bon.
        public int FertilizerGrowDaysBonus = 0;
        public float FertilizerQualityBonus = 0f;

        // Cho item la san pham cay trong/vat nuoi GOC (vd "tomato", "milk") - id cua ban cao cap
        // hon, de FarmPlot.Harvest() tra cuu thay vi tu ghep chuoi "_good"/"_premium" (tranh gia
        // dinh sai id neu bien the chua duoc dinh nghia). Null/rong = khong co ban cao cap hon.
        public string GoodVariantId;
        public string PremiumVariantId;
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

    // Dinh nghia 1 loai hang hoa trong nha kho (vat dung trang tri nhu thung/bao/rom, HOAC san
    // pham thuc duoc theo doi so luong that qua FarmStorage nhu trung ga) - data-driven, nap tu
    // warehouse_products.json. Nha kho (xem Main.BuildRoomForKind - RoomKind.Barn) doc du lieu
    // tu day de tu sap xep hang hoa thay vi hard-code danh sach truc tiep trong code.
    public class WarehouseProductDef
    {
        public string Id;
        public string Name;
        public string Category;      // "Container" | "Feed" | "Crop" | "Product"
        public string ModelPath;     // res:// duong dan model 3D (rong neu la san pham ao nhu trung, khong co the hien vat ly rieng)
        public float Scale = 1f;
        public int ScatterCount = 0; // so luong dat rai rac ngau nhien (xem Main.ScatterBarnStock) - 0 = khong dung kieu nay
        public bool UseInGrid = false; // co dua vao ke hang chinh dang luoi khong (xem Main.BuildWarehouseGrid)
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
