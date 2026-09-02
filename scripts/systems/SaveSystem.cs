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
        // Vi tri (X,Z) cac May Tuoi Tu Dong da luu - Main.cs doc lai sau khi FetchAndApplySave
        // xong de tu spawn lai node (giong FarmState cho o dat cuoc tu do).
        public List<Vector2> SprinklerPositions = new();
        // Cong trinh nguoi choi tu xay (xem BuildMenuUI.cs/PlacedBuilding.cs) - (BuildingId, X, Z).
        public List<(string id, Vector2 pos)> PlacedBuildings = new();

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

            // He sinh thai ho (xem WaterEcosystem.cs) - Dictionary<string,float> serialize thang
            // qua System.Text.Json (khong can tach thanh 2 list key/value nhu Quest/Contract o
            // tren, vi key la id loai co dinh don gian, khong can giu THU TU nhu quest/contract).
            public Dictionary<string, float> LakePopulation { get; set; } = new();
            public float LakeWaterQuality { get; set; } = 90f;
            public bool LakeTowerMaintained { get; set; } = true;

            // Da mo khoa Nha Kinh chua (xem GreenhouseGate.cs).
            public bool GreenhouseUnlocked { get; set; }

            // Vi tri cac May Tuoi Tu Dong da dat (xem AutoSprinkler.cs) - danh sach song song
            // X/Z (cung mau voi cac danh sach khac trong file nay), vi Vector3 cua Godot khong
            // serialize truc tiep gon qua System.Text.Json.
            public List<float> SprinklerX { get; set; } = new();
            public List<float> SprinklerZ { get; set; } = new();

            // Cong trinh nguoi choi tu xay (xem BuildMenuUI.cs/BuildingCatalog.cs) - 3 danh sach
            // song song (Id/X/Z), cung mau voi Sprinkler o tren.
            public List<string> BuildingIds { get; set; } = new();
            public List<float> BuildingX { get; set; } = new();
            public List<float> BuildingZ { get; set; } = new();

            // Trang thai may che bien (xem ProcessingMachine.cs) KHONG duoc luu - don gian hoa co
            // y: may reset ve trong moi lan nap lai save, nguoi choi mat toi da vai ngay che bien
            // dang do dang (chap nhan duoc, tranh phai gan ID on dinh cho tung may + doi chieu lai
            // dung thu tu luc nap).
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

        // onDone: goi LAI (bool ok) sau khi request luu len backend THAT SU hoan tat (thanh cong
        // hay loi deu goi, khong chi luc thanh cong) - can cho luong "luu roi moi thoat" khi dong
        // cua so (xem Main.cs OnCloseRequested), vi PushSave la request MANG bat dong bo, khong
        // the "cho" dong bo nhu ghi file truoc day.
        public void SaveGame(Action<bool> onDone = null)
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

            data.LakePopulation = new Dictionary<string, float>(WaterEcosystem.Instance.Population);
            data.LakeWaterQuality = WaterEcosystem.Instance.WaterQuality;
            data.LakeTowerMaintained = WaterEcosystem.Instance.TowerMaintained;

            data.GreenhouseUnlocked = GameManager.Instance.GreenhouseUnlocked;
            foreach (Node n in GetTree().GetNodesInGroup("auto_sprinklers"))
            {
                if (n is Node3D sprinkler)
                {
                    data.SprinklerX.Add(sprinkler.Position.X);
                    data.SprinklerZ.Add(sprinkler.Position.Z);
                }
            }
            foreach (Node n in GetTree().GetNodesInGroup("player_buildings"))
            {
                if (n is HiepSiVeVuon.Entities.PlacedBuilding pb && !string.IsNullOrEmpty(pb.BuildingId))
                {
                    data.BuildingIds.Add(pb.BuildingId);
                    data.BuildingX.Add(pb.Position.X);
                    data.BuildingZ.Add(pb.Position.Z);
                }
            }

            string json = JsonSerializer.Serialize(data);
            BackendClient.Instance.PushSave(json, (ok, err) =>
            {
                GD.Print(ok ? "Đã lưu game lên server." : $"Lỗi lưu game lên server: {err}");
                onDone?.Invoke(ok);
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
                    GD.Print("Đã nạp game từ server.");
                }
                else if (json != null)
                {
                    // json bat dau bang "ERR:" = loi that su (mat mang/server loi), KHAC voi
                    // json==null (nguoi choi moi, chua tung luu - giu nguyen the gioi mac dinh).
                    GD.PrintErr($"Không tải được bản lưu: {json}");
                }
                else
                {
                    GD.Print("Người chơi mới - chưa có bản lưu trên server, giữ thế giới mặc định.");
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

            if (data.LakePopulation != null && data.LakePopulation.Count > 0)
                foreach (var kv in data.LakePopulation)
                    WaterEcosystem.Instance.Population[kv.Key] = kv.Value;
            WaterEcosystem.Instance.WaterQuality = data.LakeWaterQuality > 0 ? data.LakeWaterQuality : 90f;
            WaterEcosystem.Instance.TowerMaintained = data.LakeTowerMaintained;

            GameManager.Instance.GreenhouseUnlocked = data.GreenhouseUnlocked;
            SprinklerPositions.Clear();
            for (int i = 0; i < data.SprinklerX.Count && i < data.SprinklerZ.Count; i++)
                SprinklerPositions.Add(new Vector2(data.SprinklerX[i], data.SprinklerZ[i]));

            PlacedBuildings.Clear();
            for (int i = 0; i < data.BuildingIds.Count && i < data.BuildingX.Count && i < data.BuildingZ.Count; i++)
                PlacedBuildings.Add((data.BuildingIds[i], new Vector2(data.BuildingX[i], data.BuildingZ[i])));

            GD.Print("Đã nạp game.");
        }
    }
}
