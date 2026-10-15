using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Systems
{
    // Nap toan bo dinh nghia game (item, enemy, quest) tu file JSON trong res://data.
    // Data-driven: muon them noi dung chi can sua JSON, khong dong vao code.
    public partial class ItemDatabase : Node
    {
        public static ItemDatabase Instance { get; private set; }

        public Dictionary<string, ItemDef> Items = new();
        public Dictionary<string, EnemyDef> Enemies = new();
        public Dictionary<string, QuestDef> Quests = new();

        // He thong nong trai mo rong - xem GameData.cs (RecipeDef/FishDef/NpcGiftDef/ForageDef/
        // AchievementDef) va cac System tuong ung.
        public List<RecipeDef> Recipes = new();
        public List<RecipeDef> Cooking = new();
        public List<FishDef> Fish = new();
        public Dictionary<string, NpcGiftDef> NpcGifts = new();
        public List<ForageDef> Forage = new();
        public List<AchievementDef> Achievements = new();
        public List<HouseUpgradeDef> HouseUpgrades = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // ItemDef/EnemyDef/QuestDef dung PUBLIC FIELD (khong phai { get; set; } property) -
            // System.Text.Json MAC DINH CHI doc property, BO QUA field va de nguyen gia tri null
            // neu thieu co nay - khien MOI Id deserialize thanh null, roi Items[it.Id]=it nem
            // ArgumentNullException ngay tu phan tu dau tien (loi goc gay "Vat pham khong ton
            // tai"/"Enemy def khong tim thay" xuat hien tu truoc gio).
            IncludeFields = true,
            // ItemDef.Type/Rarity la enum, nhung items.json ghi bang CHUOI ("Weapon", "Rare"...)
            // - System.Text.Json MAC DINH chi doc enum duoi dang SO (0,1,2...), nem
            // JsonException "khong the chuyen doi sang ItemType" neu khong co converter nay.
            Converters = { new JsonStringEnumConverter() }
        };

        public override void _EnterTree()
        {
            Instance = this;
            LoadItems();
            LoadEnemies();
            LoadQuests();
            Recipes = LoadList<RecipeDef>("res://data/recipes.json");
            Cooking = LoadList<RecipeDef>("res://data/cooking.json");
            Fish = LoadList<FishDef>("res://data/fish.json");
            foreach (var g in LoadList<NpcGiftDef>("res://data/npc_gifts.json")) NpcGifts[g.NpcId] = g;
            Forage = LoadList<ForageDef>("res://data/forage.json");
            Achievements = LoadList<AchievementDef>("res://data/achievements.json");
            HouseUpgrades = LoadList<HouseUpgradeDef>("res://data/house.json");
            GD.Print($"Đã nạp {Recipes.Count} công thức, {Fish.Count} loài cá, {NpcGifts.Count} sở thích NPC, {Forage.Count} đồ hái lượm, {Achievements.Count} thành tựu.");
        }

        private List<T> LoadList<T>(string path)
        {
            var text = ReadFile(path);
            if (text == null) return new List<T>();
            return JsonSerializer.Deserialize<List<T>>(text, JsonOpts) ?? new List<T>();
        }

        private string ReadFile(string path)
        {
            if (!FileAccess.FileExists(path))
            {
                GD.PushWarning($"Không tìm thấy file: {path}");
                return null;
            }
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            return f.GetAsText();
        }

        private void LoadItems()
        {
            var text = ReadFile("res://data/items.json");
            if (text == null) return;
            var list = JsonSerializer.Deserialize<List<ItemDef>>(text, JsonOpts);
            foreach (var it in list) Items[it.Id] = it;
            GD.Print($"Đã nạp {Items.Count} vật phẩm.");
        }

        private void LoadEnemies()
        {
            var text = ReadFile("res://data/enemies.json");
            if (text == null) return;
            var list = JsonSerializer.Deserialize<List<EnemyDef>>(text, JsonOpts);
            foreach (var e in list) Enemies[e.Id] = e;
            GD.Print($"Đã nạp {Enemies.Count} quái vật.");
        }

        private void LoadQuests()
        {
            var text = ReadFile("res://data/quests.json");
            if (text == null) return;
            var list = JsonSerializer.Deserialize<List<QuestDef>>(text, JsonOpts);
            foreach (var q in list) Quests[q.Id] = q;
            GD.Print($"Đã nạp {Quests.Count} nhiệm vụ.");
        }

        // Tra nguoc: nong san -> hat giong trong ra no (cache) - dung cho kiem tra mua cua cay dang trong.
        private Dictionary<string, ItemDef> _seedForCrop;
        public ItemDef GetSeedForCrop(string cropId)
        {
            if (_seedForCrop == null)
            {
                _seedForCrop = new();
                foreach (var d in Items.Values)
                    if (d.Type == ItemType.Seed && !string.IsNullOrEmpty(d.GrowsIntoCropId) && !_seedForCrop.ContainsKey(d.GrowsIntoCropId))
                        _seedForCrop[d.GrowsIntoCropId] = d;
            }
            return cropId != null && _seedForCrop.TryGetValue(cropId, out var s) ? s : null;
        }

        public ItemDef GetItem(string id) => Items.TryGetValue(id, out var v) ? v : null;
        public EnemyDef GetEnemy(string id) => Enemies.TryGetValue(id, out var v) ? v : null;
        public QuestDef GetQuest(string id) => Quests.TryGetValue(id, out var v) ? v : null;

        public Texture2D GetItemIcon(string id)
        {
            // Icon ve tu model 3D (quang/thoi/da quy/cuoc chim - xem IconRenderer) uu tien hon PNG.
            if (IconRenderer.Instance?.TryGet(id) is Texture2D rendered) return rendered;
            var def = GetItem(id);
            if (def == null || string.IsNullOrEmpty(def.IconPath)) return null;
            return GD.Load<Texture2D>(def.IconPath);
        }

        // ==== Ten/mo ta hien thi theo ngon ngu dang chon (Loc.Current) ====
        // JSON (Items/Enemies/Quests) van la tieng Viet goc - DataLoc.cs chua ban dich tieng
        // Anh theo Id, tra ve chuoi VI mac dinh neu dang o VI hoac thieu ban dich EN.
        public string GetDisplayName(string id)
        {
            var def = GetItem(id);
            if (def == null) return id;
            return Loc.Current == Loc.Lang.EN && DataLoc.ItemNamesEn.TryGetValue(id, out var en) ? en : def.Name;
        }

        public string GetDisplayDescription(string id)
        {
            var def = GetItem(id);
            if (def == null) return "";
            return Loc.Current == Loc.Lang.EN && DataLoc.ItemDescriptionsEn.TryGetValue(id, out var en) ? en : def.Description;
        }

        public string GetEnemyDisplayName(string id)
        {
            var def = GetEnemy(id);
            if (def == null) return id;
            return Loc.Current == Loc.Lang.EN && DataLoc.EnemyNamesEn.TryGetValue(id, out var en) ? en : def.Name;
        }

        public string GetQuestDisplayTitle(string id)
        {
            var def = GetQuest(id);
            if (def == null) return id;
            return Loc.Current == Loc.Lang.EN && DataLoc.QuestTitlesEn.TryGetValue(id, out var en) ? en : def.Title;
        }

        public string GetQuestDisplayDescription(string id)
        {
            var def = GetQuest(id);
            if (def == null) return "";
            return Loc.Current == Loc.Lang.EN && DataLoc.QuestDescriptionsEn.TryGetValue(id, out var en) ? en : def.Description;
        }
    }
}
