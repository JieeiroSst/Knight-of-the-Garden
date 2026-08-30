using Godot;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Luu/nap game ra user:// duoi dang JSON. Luu chi so, tui do, quest, nong trai.
    public partial class SaveSystem : Node
    {
        public static SaveSystem Instance { get; private set; }
        private const string SavePath = "user://savegame.json";

        // Trang thai nong trai duoc dang ky boi FarmPlot (de save khong phu thuoc scene)
        public List<FarmTileState> FarmState = new();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public class SaveData
        {
            public int Hp { get; set; }
            public int MaxHp { get; set; }
            public int Level { get; set; }
            public int Exp { get; set; }
            public int ExpToNext { get; set; }
            public int Gold { get; set; }
            public int Day { get; set; }

            public List<SavedStack> Inventory { get; set; } = new();
            public string EquippedWeapon { get; set; }
            public string EquippedArmor { get; set; }

            public List<string> ActiveQuests { get; set; } = new();
            public List<int> ActiveProgress { get; set; } = new();
            public List<string> CompletedQuests { get; set; } = new();

            public List<FarmTileState> Farm { get; set; } = new();
        }

        public class SavedStack { public string Id { get; set; } public int Count { get; set; } }
        public class FarmTileState { public int X { get; set; } public int Y { get; set; } public string CropId { get; set; } public int GrowStage { get; set; } public bool Watered { get; set; } }

        public void SaveGame()
        {
            var gm = GameManager.Instance;
            var data = new SaveData
            {
                Hp = gm.Hp, MaxHp = gm.MaxHp, Level = gm.Level, Exp = gm.Exp,
                ExpToNext = gm.ExpToNext, Gold = gm.Gold, Day = gm.Day,
                EquippedWeapon = Inventory.Instance.EquippedWeapon,
                EquippedArmor = Inventory.Instance.EquippedArmor,
                Farm = FarmState
            };

            foreach (var s in Inventory.Instance.Slots)
                data.Inventory.Add(new SavedStack { Id = s.ItemId, Count = s.Count });

            foreach (var kv in QuestSystem.Instance.Active)
            {
                data.ActiveQuests.Add(kv.Key);
                data.ActiveProgress.Add(kv.Value);
            }
            foreach (var c in QuestSystem.Instance.Completed)
                data.CompletedQuests.Add(c);

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
            GD.Print("Da luu game.");
        }

        public bool HasSave() => FileAccess.FileExists(SavePath);

        public SaveData LoadRaw()
        {
            if (!HasSave()) return null;
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            string json = f.GetAsText();
            return JsonSerializer.Deserialize<SaveData>(json);
        }

        // Nap va ap dung vao cac he thong. Farm duoc tra ve de scene tu dung.
        public void LoadGame()
        {
            var data = LoadRaw();
            if (data == null) { GD.Print("Khong co ban luu."); return; }

            GameManager.Instance.ApplyLoadedStats(
                data.Hp, data.MaxHp, data.Level, data.Exp, data.ExpToNext, data.Gold, data.Day);

            Inventory.Instance.Clear();
            foreach (var s in data.Inventory) Inventory.Instance.AddItem(s.Id, s.Count);
            if (!string.IsNullOrEmpty(data.EquippedWeapon)) Inventory.Instance.Equip(data.EquippedWeapon);
            if (!string.IsNullOrEmpty(data.EquippedArmor)) Inventory.Instance.Equip(data.EquippedArmor);

            QuestSystem.Instance.Reset();
            for (int i = 0; i < data.ActiveQuests.Count; i++)
            {
                QuestSystem.Instance.Active[data.ActiveQuests[i]] =
                    i < data.ActiveProgress.Count ? data.ActiveProgress[i] : 0;
            }
            foreach (var c in data.CompletedQuests) QuestSystem.Instance.Completed.Add(c);

            FarmState = data.Farm ?? new List<FarmTileState>();
            GD.Print("Da nap game.");
        }
    }
}
