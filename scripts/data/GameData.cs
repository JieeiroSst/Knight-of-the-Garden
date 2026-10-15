using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Data
{
    // ==== Cac loai vat pham ====
    public enum ItemType { Tool, Weapon, Armor, Seed, Crop, Consumable, Material, Quest, Outfit }
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
        public int StaminaAmount = 0;   // cho consumable - hoi the luc (xem GameManager.Stamina)
        // Trang phuc nong trai (Type = Outfit): giam the luc cho 1 ky nang (ten SkillType) va/hoac
        // tang toc chay - hinh dang xem OutfitCatalog.
        public string OutfitSkill;
        public float OutfitStaminaDiscount = 0f;
        public float OutfitSpeedBonus = 0f;

        // Cho hat giong -> cay trong
        public string GrowsIntoCropId;  // id cay thu hoach
        public int GrowDays = 3;
        // Cay thu hoach NHIEU LAN (dau tay/ngo/viet quat/ca tim): sau khi hai, cay giu lai va chin
        // lai sau RegrowDays ngay thay vi phai trong lai - 0 = thu hoach 1 lan (mac dinh). Dat tren
        // HAT GIONG (xem FarmPlot.Plant).
        public int RegrowDays = 0;

        // Mua trong duoc (xem GameManager.Season) - ten enum dang chuoi ("Spring"/"Summer"/"Fall"/
        // "Winter") de khop truc tiep voi items.json. Null/rong = trong duoc QUANH NAM (mac dinh
        // cho moi item KHONG phai hat giong, tranh phai gan cho tat ca item trong game).
        public string[] ValidSeasons;

        // Cho phan bon (xem FarmPlot.UseOn) - 0 nghia la item nay KHONG phai phan bon.
        public int FertilizerGrowDaysBonus = 0;
        public float FertilizerQualityBonus = 0f;

        // Cho item la san pham cay trong/vat nuoi GOC (vd "tomato", "milk") - id cua ban cao cap
        // hon, de FarmPlot.Harvest() tra cuu thay vi tu ghep chuoi "_good"/"_premium" (tranh gia
        // dinh sai id neu bien the chua duoc dinh nghia). Null/rong = khong co ban cao cap hon.
        // Nong san co the hoa "cay khong lo" khi chin (xem FarmPlot.OnDayChanged) - dat tren NONG SAN.
        public bool GiantCapable = false;
        // Hien vat suu tam duoc - quyen gop cho Bao Tang (xem MuseumUI/AdventureSystem).
        public bool Collectible = false;

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
        // Ma hex (vd "8a5a3c") - nhuom mau model dung chung (xem Enemy.cs) de nhieu EnemyId khac
        // nhau "nhin khac nhau" du dung CHUNG 1 file model .glb (game chi co 2 model quai) - null/
        // rong = giu nguyen mau goc cua model.
        public string TintColor;

        // ==== Hinh anh + hanh vi mo rong (xem Enemy.cs) ====
        // Model 3D rieng (Quaternius Ultimate Monsters) - rong = slime mac dinh.
        public string ModelPath;
        public float ModelScale = 16f;
        // Quai bay (ma/rong/doi): model lo lung cach mat dat + nhun len xuong.
        public float HoverHeight = 0f;
        // Ma: ban trong suot + phat sang nhe.
        public bool Ghostly = false;
        // Mau phat sang (hex) - hao quang/than phat sang nhe, rong = khong.
        public string GlowColor;
        // Trum khu vuc: thanh mau tren HUD, don dap dat (AoE) co canh bao, hao quang.
        public bool IsBoss = false;
        public string BossTitle;      // vd "Chua Te Rung Sau"
        public string BossTitleEn;
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

    // ==== He thong nong trai mo rong (xem SkillSystem/FriendshipSystem/AchievementSystem/
    // MailSystem/ShippingSystem) - tat ca data-driven qua cac file JSON rieng trong res://data ====

    // Ky nang nguoi choi - moi ky nang co EXP/cap rieng (0..10), xem SkillSystem.cs.
    public enum SkillType { Farming, Animal, Fishing, Mining, Foraging }

    public class IngredientEntry
    {
        public string ItemId;
        public int Count = 1;
    }

    // Cong thuc che tao tai Ban Lam Viec (data/recipes.json, xem CraftingUI.cs).
    public class RecipeDef
    {
        public string Id;
        public string OutputId;
        public int OutputCount = 1;
        public List<IngredientEntry> Ingredients = new();
        // Ky nang + cap toi thieu de mo khoa - null/rong = co san tu dau.
        public string UnlockSkill;
        public int UnlockLevel = 0;
        // Tram che tao: null = Ban Che Tao thuong; "forge" = Lo Ren (xem ForgeUI).
        public string Station;
    }

    // 1 loai ca (data/fish.json, xem FishingUI.cs) - loc theo mua/gio/thoi tiet.
    public class FishDef
    {
        public string ItemId;
        public string[] Seasons;       // null/rong = quanh nam
        public int HourMin = 0;        // gio bat dau co (0..23)
        public int HourMax = 24;       // gio ket thuc (khong bao gom), HourMin > HourMax = qua dem
        public bool RainOnly = false;
        public int MinFishingLevel = 0;
        public float Difficulty = 1f;  // 1 = de, 3 = rat kho (toc do con tro + vung xanh hep)
        public int Weight = 10;        // trong so ngau nhien
        public int Xp = 10;
    }

    // So thich qua tang + sinh nhat cua 1 NPC (data/npc_gifts.json, xem FriendshipSystem.cs).
    public class NpcGiftDef
    {
        public string NpcId;
        public string[] Loved = System.Array.Empty<string>();
        public string[] Liked = System.Array.Empty<string>();
        public string[] Disliked = System.Array.Empty<string>();
        public string BirthdaySeason;  // "Spring"/"Summer"/"Fall"/"Winter", null = khong co
        public int BirthdayDay = 0;    // 1..28
    }

    // Do hai luom moc tu nhien theo mua (data/forage.json, xem ForageSpawner.cs).
    public class ForageDef
    {
        public string ItemId;
        public string[] Seasons;
        public int Weight = 10;
    }

    // Nang cap nha (data/house.json - dung chung voi backend de server kiem tra chi phi).
    public class HouseUpgradeDef
    {
        public int Level;
        public int Gold;
        public List<IngredientEntry> Materials = new();
    }

    // Thanh tuu (data/achievements.json, xem AchievementSystem.cs) - dat khi chi so Stat >= Target.
    public class AchievementDef
    {
        public string Id;
        public string Title;
        public string TitleEn;
        public string Description;
        public string DescriptionEn;
        public string Stat;
        public int Target = 1;
        public int RewardGold = 0;
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
