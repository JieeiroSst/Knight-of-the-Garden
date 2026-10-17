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

        // true = nguoi choi da co ban luu tren server (nguoi choi cu) - quyet dinh co "chay bu" cac
        // ngay offline hay nhay thang toi ngay hom nay (xem GameManager.CatchUpToServerDay).
        public bool LoadedExistingSave { get; private set; }

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
            public string EquippedOutfit { get; set; }
            public bool ShowArmor { get; set; } = true;

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

            // ==== He thong nong trai mo rong ====
            // Moi field co gia tri mac dinh an toan cho ban luu CU (thieu field -> giu initializer).
            // Stamina nullable: ban luu cu khong co -> day the luc, thay vi 0.
            public float? Stamina { get; set; }
            public Dictionary<string, int> SkillXp { get; set; } = new();
            public Dictionary<string, int> FriendPoints { get; set; } = new();
            public Dictionary<string, int> FriendTalkDay { get; set; } = new();
            public Dictionary<string, int> FriendGiftDay { get; set; } = new();
            public Dictionary<string, int> AchievementStats { get; set; } = new();
            public List<string> AchievementsUnlocked { get; set; } = new();
            public List<MailSystem.Letter> Letters { get; set; } = new();
            public List<string> MailSentKeys { get; set; } = new();
            public List<ShippingSystem.Entry> ShippingBin { get; set; } = new();
            public int CompostFilled { get; set; }
            public int CompostDaysLeft { get; set; }
            public bool CompostReady { get; set; }

            // ==== Phieu luu / nha / le hoi ====
            public List<string> RegionsDiscovered { get; set; } = new();
            // Chunk the gioi vo han da kham pha (suong mu ban do) - "cx,cz", xem WorldStreamer.Explored.
            public List<string> ExploredChunks { get; set; } = new();
            // Vat nuoi da giet thit theo loai (xem Huntable/AnimalLedger) - khong hien lai khi vao game.
            public Dictionary<string, int> SlaughteredAnimals { get; set; } = new();
            public float? Hunger { get; set; }
            public float? Thirst { get; set; }
            public List<HorseRegistry.TamedHorse> TamedHorses { get; set; } = new();
            public List<string> ChestsOpened { get; set; } = new();
            public List<string> WaystonesActive { get; set; } = new();
            public Dictionary<string, int> BossDefeatDay { get; set; } = new();
            public int DeepestDungeonFloor { get; set; }
            public List<string> MuseumDonated { get; set; } = new();
            public int MuseumRewardsClaimed { get; set; }
            public int HouseLevel { get; set; }
            public float? HerdHappiness { get; set; }
            public List<ShippingSystem.Entry> HomeChest { get; set; } = new();
            public List<float> ScarecrowX { get; set; } = new();
            public List<float> ScarecrowZ { get; set; } = new();
            public List<string> FestivalsCompleted { get; set; } = new();
            public string SecretFriendId { get; set; }
            public int SecretFriendDay { get; set; } = -1;
            // Khoi quang da dao -> ngay game moc lai (xem OreRegistry).
            public Dictionary<string, int> OreRegrowDay { get; set; } = new();
            // Thu cung + do tu dat (trang tri/to ong/cay/may/bay cua/ho ca).
            public List<string> PetsOwned { get; set; } = new();
            public Dictionary<string, int> PetXp { get; set; } = new();
            public string PetActive { get; set; }
            public List<PlacementSystem.Entry> Placed { get; set; } = new();

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
            // Mo rong (cay nhieu lan thu hoach / cay khong lo / sua loi mat GrowDays khi tai lai).
            public int GrowDays { get; set; }
            public int RegrowDays { get; set; }
            public bool IsGiant { get; set; }

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
                EquippedOutfit = Inventory.Instance.EquippedOutfit,
                ShowArmor = Inventory.Instance.ShowArmor,
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

            data.Stamina = gm.Stamina;
            foreach (var kv in SkillSystem.Instance.Xp) data.SkillXp[kv.Key.ToString()] = kv.Value;
            data.FriendPoints = new Dictionary<string, int>(FriendshipSystem.Instance.Points);
            data.FriendTalkDay = new Dictionary<string, int>(FriendshipSystem.Instance.LastTalkDay);
            data.FriendGiftDay = new Dictionary<string, int>(FriendshipSystem.Instance.LastGiftDay);
            data.AchievementStats = new Dictionary<string, int>(AchievementSystem.Instance.Stats);
            data.AchievementsUnlocked = new List<string>(AchievementSystem.Instance.Unlocked);
            data.Letters = MailSystem.Instance.Letters;
            data.MailSentKeys = new List<string>(MailSystem.Instance.SentKeys);
            data.ShippingBin = ShippingSystem.Instance.Bin;
            data.CompostFilled = HiepSiVeVuon.Entities.CompostBin.Filled;
            data.CompostDaysLeft = HiepSiVeVuon.Entities.CompostBin.DaysLeft;
            data.CompostReady = HiepSiVeVuon.Entities.CompostBin.IsReady;

            var adv = AdventureSystem.Instance;
            data.RegionsDiscovered = new List<string>(adv.Discovered);
            data.SlaughteredAnimals = new Dictionary<string, int>(HiepSiVeVuon.Entities.AnimalLedger.Slaughtered);
            data.Hunger = SurvivalSystem.Instance?.Hunger;
            data.Thirst = SurvivalSystem.Instance?.Thirst;
            data.TamedHorses = new List<HorseRegistry.TamedHorse>(HorseRegistry.Horses);
            data.ExploredChunks = new List<string>(WorldStreamer.Explored.Count);
            foreach (var c in WorldStreamer.Explored) data.ExploredChunks.Add($"{c.X},{c.Y}");
            data.ChestsOpened = new List<string>(adv.OpenedChests);
            data.WaystonesActive = new List<string>(adv.Waystones);
            data.BossDefeatDay = new Dictionary<string, int>(adv.BossDefeatDay);
            data.DeepestDungeonFloor = adv.DeepestDungeonFloor;
            data.MuseumDonated = new List<string>(adv.MuseumDonated);
            data.MuseumRewardsClaimed = adv.MuseumRewardsClaimed;
            var home = HomeSystem.Instance;
            data.HouseLevel = home.HouseLevel;
            data.HerdHappiness = home.HerdHappiness;
            data.HomeChest = home.Chest;
            foreach (var p in home.ScarecrowPositions) { data.ScarecrowX.Add(p.X); data.ScarecrowZ.Add(p.Y); }
            data.FestivalsCompleted = new List<string>(FestivalSystem.Instance.Completed);
            data.SecretFriendId = FestivalSystem.Instance.SecretFriendId;
            data.SecretFriendDay = FestivalSystem.Instance.SecretFriendDay;
            data.OreRegrowDay = OreRegistry.Export();
            data.PetsOwned = new List<string>(PetSystem.Instance.Owned);
            data.PetXp = new Dictionary<string, int>(PetSystem.Instance.Xp);
            data.PetActive = PetSystem.Instance.Active;
            data.Placed = PlacementSystem.Instance.Entries;

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
                LoadedExistingSave = found && !string.IsNullOrEmpty(json);
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

            // Ban luu chi giu CACH SAP XEP tui do - so luong chinh thuc do server quyet dinh (anh chup
            // kinh te se dieu chinh lai ngay sau, xem EconomyClient.Reconcile). Khong tao op khi nap.
            using var suppress = EconomyClient.Suppress();
            Inventory.Instance.Clear();
            foreach (var s in data.Inventory) Inventory.Instance.AddItem(s.Id, s.Count);
            Backpack.Instance.Clear();
            foreach (var s in data.Backpack) Backpack.Instance.AddItem(s.Id, s.Count);
            if (!string.IsNullOrEmpty(data.EquippedWeapon)) Inventory.Instance.Equip(data.EquippedWeapon);
            if (!string.IsNullOrEmpty(data.EquippedArmor)) Inventory.Instance.Equip(data.EquippedArmor);
            if (!string.IsNullOrEmpty(data.EquippedTool)) Inventory.Instance.Equip(data.EquippedTool);
            if (!string.IsNullOrEmpty(data.EquippedOutfit)) Inventory.Instance.Equip(data.EquippedOutfit);
            Inventory.Instance.SetShowArmor(data.ShowArmor);

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

            ApplyFarmLife(data);
            GD.Print("Đã nạp game.");
        }

        private static void ApplyFarmLife(SaveData data)
        {
            SkillSystem.Instance.Reset();
            if (data.SkillXp != null)
                foreach (var kv in data.SkillXp)
                    if (Enum.TryParse<HiepSiVeVuon.Data.SkillType>(kv.Key, out var skill))
                        SkillSystem.Instance.Xp[skill] = kv.Value;

            var gm = GameManager.Instance;
            gm.Stamina = data.Stamina.HasValue ? Mathf.Clamp(data.Stamina.Value, 0f, gm.MaxStamina) : gm.MaxStamina;

            var fs = FriendshipSystem.Instance;
            fs.Reset();
            if (data.FriendPoints != null) foreach (var kv in data.FriendPoints) fs.Points[kv.Key] = kv.Value;
            if (data.FriendTalkDay != null) foreach (var kv in data.FriendTalkDay) fs.LastTalkDay[kv.Key] = kv.Value;
            if (data.FriendGiftDay != null) foreach (var kv in data.FriendGiftDay) fs.LastGiftDay[kv.Key] = kv.Value;

            var ach = AchievementSystem.Instance;
            ach.Reset();
            if (data.AchievementStats != null) foreach (var kv in data.AchievementStats) ach.Stats[kv.Key] = kv.Value;
            if (data.AchievementsUnlocked != null) foreach (var id in data.AchievementsUnlocked) ach.Unlocked.Add(id);

            var mail = MailSystem.Instance;
            mail.Reset();
            if (data.Letters != null) mail.Letters.AddRange(data.Letters);
            if (data.MailSentKeys != null) foreach (var k in data.MailSentKeys) mail.SentKeys.Add(k);
            mail.EmitSignal(MailSystem.SignalName.MailChanged);

            ShippingSystem.Instance.Reset();
            // Thung giao hang nay nam tren server (xem ShippingSystem) - bo qua ban sao cu trong ban luu.

            HiepSiVeVuon.Entities.CompostBin.Filled = data.CompostFilled;
            HiepSiVeVuon.Entities.CompostBin.DaysLeft = data.CompostDaysLeft;
            HiepSiVeVuon.Entities.CompostBin.IsReady = data.CompostReady;

            var adv = AdventureSystem.Instance;
            adv.Reset();
            if (data.RegionsDiscovered != null) adv.Discovered.UnionWith(data.RegionsDiscovered);
            HiepSiVeVuon.Entities.AnimalLedger.Load(data.SlaughteredAnimals, Engine.GetMainLoop() as SceneTree);
            HorseRegistry.Load(data.TamedHorses);
            if (SurvivalSystem.Instance != null)
            {
                SurvivalSystem.Instance.Hunger = data.Hunger ?? SurvivalSystem.Max;
                SurvivalSystem.Instance.Thirst = data.Thirst ?? SurvivalSystem.Max;
            }
            WorldStreamer.Explored.Clear();
            if (data.ExploredChunks != null)
                foreach (var key in data.ExploredChunks)
                {
                    var parts = key.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int cx) && int.TryParse(parts[1], out int cz))
                        WorldStreamer.Explored.Add(new Vector2I(cx, cz));
                }
            if (data.ChestsOpened != null) adv.OpenedChests.UnionWith(data.ChestsOpened);
            if (data.WaystonesActive != null) adv.Waystones.UnionWith(data.WaystonesActive);
            adv.Waystones.Add("ws_farm"); // cot o nong trai luon kich hoat san
            if (data.BossDefeatDay != null) foreach (var kv in data.BossDefeatDay) adv.BossDefeatDay[kv.Key] = kv.Value;
            adv.DeepestDungeonFloor = data.DeepestDungeonFloor;
            if (data.MuseumDonated != null) adv.MuseumDonated.UnionWith(data.MuseumDonated);
            adv.MuseumRewardsClaimed = data.MuseumRewardsClaimed;

            var home = HomeSystem.Instance;
            home.Reset();
            home.HouseLevel = Mathf.Clamp(data.HouseLevel, 0, HomeSystem.MaxHouseLevel);
            home.HerdHappiness = data.HerdHappiness ?? 60f;
            if (data.HomeChest != null) home.Chest.AddRange(data.HomeChest);
            for (int i = 0; data.ScarecrowX != null && data.ScarecrowZ != null && i < data.ScarecrowX.Count && i < data.ScarecrowZ.Count; i++)
                home.ScarecrowPositions.Add(new Vector2(data.ScarecrowX[i], data.ScarecrowZ[i]));

            var fest = FestivalSystem.Instance;
            fest.Reset();
            if (data.FestivalsCompleted != null) fest.Completed.UnionWith(data.FestivalsCompleted);
            fest.SecretFriendId = data.SecretFriendId;
            fest.SecretFriendDay = data.SecretFriendDay;
            OreRegistry.Import(data.OreRegrowDay);
            var pets = PetSystem.Instance;
            pets.Reset();
            if (data.PetsOwned != null) pets.Owned.UnionWith(data.PetsOwned);
            if (data.PetXp != null) foreach (var kv in data.PetXp) pets.Xp[kv.Key] = kv.Value;
            pets.Active = data.PetActive != null && pets.Owned.Contains(data.PetActive) ? data.PetActive : null;
            PlacementSystem.Instance.Reset();
            if (data.Placed != null) PlacementSystem.Instance.Entries.AddRange(data.Placed);

            // The luc nap TRUOC khi HouseLevel duoc ap -> kep lai theo MaxStamina day du.
            gm.Stamina = data.Stamina.HasValue ? Mathf.Clamp(data.Stamina.Value, 0f, gm.MaxStamina) : gm.MaxStamina;
            gm.EmitSignal(GameManager.SignalName.StatsChanged);
        }
    }
}
