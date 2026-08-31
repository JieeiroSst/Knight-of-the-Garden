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
        }

        private string ReadFile(string path)
        {
            if (!FileAccess.FileExists(path))
            {
                GD.PushWarning($"Khong tim thay file: {path}");
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
            GD.Print($"Da nap {Items.Count} vat pham.");
        }

        private void LoadEnemies()
        {
            var text = ReadFile("res://data/enemies.json");
            if (text == null) return;
            var list = JsonSerializer.Deserialize<List<EnemyDef>>(text, JsonOpts);
            foreach (var e in list) Enemies[e.Id] = e;
            GD.Print($"Da nap {Enemies.Count} quai vat.");
        }

        private void LoadQuests()
        {
            var text = ReadFile("res://data/quests.json");
            if (text == null) return;
            var list = JsonSerializer.Deserialize<List<QuestDef>>(text, JsonOpts);
            foreach (var q in list) Quests[q.Id] = q;
            GD.Print($"Da nap {Quests.Count} nhiem vu.");
        }

        public ItemDef GetItem(string id) => Items.TryGetValue(id, out var v) ? v : null;
        public EnemyDef GetEnemy(string id) => Enemies.TryGetValue(id, out var v) ? v : null;
        public QuestDef GetQuest(string id) => Quests.TryGetValue(id, out var v) ? v : null;

        public Texture2D GetItemIcon(string id)
        {
            var def = GetItem(id);
            if (def == null || string.IsNullOrEmpty(def.IconPath)) return null;
            return GD.Load<Texture2D>(def.IconPath);
        }
    }
}
