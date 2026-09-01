using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Luu/nap game qua BACKEND that su tren Internet (Node.js/Express + PostgreSQL - xem
    // BackendClient.cs va thu muc backend/) - KHONG con ghi file JSON local (user://) nua. Luu
    // chi so, tui do, quest, hop dong, nong trai.
    public partial class SaveSystem : Node
    {
        public static SaveSystem Instance { get; private set; }

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
            public string EquippedTool { get; set; }

            // Balo - tui do THU HAI, 50 o (xem Backpack.cs) - cung mau SavedStack voi Inventory.
            public List<SavedStack> Backpack { get; set; } = new();

            public List<string> ActiveQuests { get; set; } = new();
            public List<int> ActiveProgress { get; set; } = new();
            public List<string> CompletedQuests { get; set; } = new();

            // Hop dong dai han (xem ContractSystem.cs) - cung mau voi Active/CompletedQuests o tren.
            public List<string> ActiveContracts { get; set; } = new();
            public List<int> ContractDeliveries { get; set; } = new();
            public List<int> ContractNextDueDay { get; set; } = new();
            public List<string> CompletedContracts { get; set; } = new();

            public List<FarmTileState> Farm { get; set; } = new();
        }

        public class SavedStack { public string Id { get; set; } public int Count { get; set; } }
        public class FarmTileState
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string CropId { get; set; }
            public int GrowStage { get; set; }
            public bool Watered { get; set; }
            // Them cho he thong nong nghiep mo rong (xem FarmPlot.cs): tuoi/bon phan/luan canh/
            // sau benh/chat luong - moi field co gia tri mac dinh an toan (0/false/null) cho ban
            // luu CU chua co cac field nay (System.Text.Json tu dien gia tri mac dinh khi thieu).
            public int DaysUnwatered { get; set; }
            public bool Fertilized { get; set; }
            public string LastCropId { get; set; }
            public bool PestAfflicted { get; set; }
            public int PestDays { get; set; }
            public bool WasPestDamaged { get; set; }
            public float QualityScore { get; set; }

            // O dat CUOC TU DO ngoai luoi 12x6 co san (Player dung item "hoe" - xem
            // FarmPlot.TryTillFreeform): Freeform=true dung PosX/PosZ (vi tri THAT trong the
            // gioi) lam khoa thay vi X/Y (luon la -1/-1, khong dung). Mac dinh Freeform=false cho
            // ban luu CU (System.Text.Json tu dien false/0 khi thieu field).
            public bool Freeform { get; set; }
            public float PosX { get; set; }
            public float PosZ { get; set; }
        }

        public void SaveGame()
        {
            var gm = GameManager.Instance;
            var data = new SaveData
            {
                Hp = gm.Hp, MaxHp = gm.MaxHp, Level = gm.Level, Exp = gm.Exp,
                ExpToNext = gm.ExpToNext, Gold = gm.Gold, Day = gm.Day,
                EquippedWeapon = Inventory.Instance.EquippedWeapon,
                EquippedArmor = Inventory.Instance.EquippedArmor,
                EquippedTool = Inventory.Instance.EquippedTool,
                Farm = FarmState
            };

            foreach (var s in Inventory.Instance.Slots)
                data.Inventory.Add(new SavedStack { Id = s.ItemId, Count = s.Count });
            foreach (var s in Backpack.Instance.Slots)
                data.Backpack.Add(new SavedStack { Id = s.ItemId, Count = s.Count });

            foreach (var kv in QuestSystem.Instance.Active)
            {
                data.ActiveQuests.Add(kv.Key);
                data.ActiveProgress.Add(kv.Value);
            }
            foreach (var c in QuestSystem.Instance.Completed)
                data.CompletedQuests.Add(c);

            foreach (var kv in ContractSystem.Instance.Active)
            {
                data.ActiveContracts.Add(kv.Key);
                data.ContractDeliveries.Add(kv.Value.DeliveriesDone);
                data.ContractNextDueDay.Add(kv.Value.NextDueDay);
            }
            foreach (var c in ContractSystem.Instance.Completed)
                data.CompletedContracts.Add(c);

            string json = JsonSerializer.Serialize(data);
            BackendClient.Instance.PushSave(json, (ok, err) =>
            {
                GD.Print(ok ? "Da luu game len server." : $"Loi luu game len server: {err}");
            });
        }

        // Tai save cua nguoi choi dang dang nhap tu backend (goi MANG bat dong bo - KHONG the
        // "cho" dong bo nhu doc file truoc day) va AP DUNG vao cac he thong neu co. Goi 1 lan
        // SAU KHI the gioi da dung xong voi trang thai mac dinh (xem Main.cs) - the gioi se hien
        // trang thai mac dinh trong choc lat truoc khi du lieu that (neu co) duoc ap len tren.
        public void FetchAndApplySave(Action onDone = null)
        {
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn)
            {
                onDone?.Invoke();
                return;
            }

            BackendClient.Instance.FetchSave((found, json) =>
            {
                if (found && !string.IsNullOrEmpty(json))
                {
                    var data = JsonSerializer.Deserialize<SaveData>(json);
                    ApplyLoadedData(data);
                    GD.Print("Da nap game tu server.");
                }
                else if (json != null)
                {
                    // json bat dau bang "ERR:" = loi that su (mat mang/server loi), KHAC voi
                    // json==null (nguoi choi moi, chua tung luu - giu nguyen the gioi mac dinh).
                    GD.PrintErr($"Khong tai duoc ban luu: {json}");
                }
                else
                {
                    GD.Print("Nguoi choi moi - chua co ban luu tren server, giu the gioi mac dinh.");
                }
                onDone?.Invoke();
            });
        }

        private void ApplyLoadedData(SaveData data)
        {
            GameManager.Instance.ApplyLoadedStats(
                data.Hp, data.MaxHp, data.Level, data.Exp, data.ExpToNext, data.Gold, data.Day);

            Inventory.Instance.Clear();
            foreach (var s in data.Inventory) Inventory.Instance.AddItem(s.Id, s.Count);
            Backpack.Instance.Clear();
            foreach (var s in data.Backpack) Backpack.Instance.AddItem(s.Id, s.Count);
            if (!string.IsNullOrEmpty(data.EquippedWeapon)) Inventory.Instance.Equip(data.EquippedWeapon);
            if (!string.IsNullOrEmpty(data.EquippedArmor)) Inventory.Instance.Equip(data.EquippedArmor);
            if (!string.IsNullOrEmpty(data.EquippedTool)) Inventory.Instance.Equip(data.EquippedTool);

            QuestSystem.Instance.Reset();
            for (int i = 0; i < data.ActiveQuests.Count; i++)
            {
                QuestSystem.Instance.Active[data.ActiveQuests[i]] =
                    i < data.ActiveProgress.Count ? data.ActiveProgress[i] : 0;
            }
            foreach (var c in data.CompletedQuests) QuestSystem.Instance.Completed.Add(c);

            ContractSystem.Instance.Reset();
            for (int i = 0; i < data.ActiveContracts.Count; i++)
            {
                ContractSystem.Instance.Active[data.ActiveContracts[i]] = new ContractProgress
                {
                    DeliveriesDone = i < data.ContractDeliveries.Count ? data.ContractDeliveries[i] : 0,
                    NextDueDay = i < data.ContractNextDueDay.Count ? data.ContractNextDueDay[i] : GameManager.Instance.Day + 7,
                };
            }
            foreach (var c in data.CompletedContracts) ContractSystem.Instance.Completed.Add(c);

            FarmState = data.Farm ?? new List<FarmTileState>();
            GD.Print("Da nap game.");
        }
    }
}
