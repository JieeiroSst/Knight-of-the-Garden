using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Mot o dat trong: Trong -> Tuoi/Bon phan/Diet sau -> Lon len (theo ngay) -> Thu hoach.
    // Vong lap nong trai lien ket voi phieu luu (thu hoach ban lay gold / craft).
    // Cac loai dat: anh huong toc do lon (GrowDays) va mau nen dat - gan tu Main.cs theo mau
    // co dinh tren luoi (khong ngau nhien, giu nguyen moi lan tai lai).
    public enum SoilType { Normal, Fertile, Dry, Wet, Toxic, Special }

    public partial class FarmPlot : StaticBody3D
    {
        public int GridX;
        public int GridY;
        [Export] public SoilType Soil = SoilType.Normal;

        private string _cropId = null;   // hat giong dang trong -> se ra crop
        private string _growsInto = null;
        private int _growStage = 0;       // 0 = dat trong, khi = GrowDays thi chin
        private int _growDays = 3;
        private bool _watered = false;

        // Cham soc them (thu hoach cang ky cang chat luong tot) - xem UseOn/Harvest.
        private bool _fertilized = false;
        private int _daysUnwatered = 0;
        private bool _pestAfflicted = false;
        private int _pestDays = 0;
        private bool _wasPestDamaged = false; // sau benh tung gay hai chua chua (giu du da diet)
        private float _qualityScore = 0f;     // tich luy trong 1 chu ky trong, phan cap luc Harvest()
        private string _lastCropId = null;    // luan canh: cay lan truoc o o nay la gi

        private MeshInstance3D _soilMesh;
        private Sprite3D _cropSprite;

        // Hat giong mac dinh de demo (khong co UI chon giong)
        [Export] public string DefaultSeedId = "pumpkin_seed";
        [Export] public string DefaultFertilizerId = "fertilizer_basic";
        [Export] public string DefaultPesticideId = "pesticide";

        // Qua nguong nay so ngay LIEN TIEP khong tuoi (va khong mua) -> cay chet, mat trang.
        [Export] public int NeglectDeathThreshold = 3;
        // Xac suat MOI NGAY phat sinh sau benh (khi dang lon, chua bi sau).
        [Export] public float DailyPestChance = 0.04f;
        // Qua nguong nay so ngay LIEN TIEP bi sau ma chua diet -> tru vinh vien vao chat luong.
        [Export] public int PestDamageThreshold = 3;

        // Nguong phan cap chat luong luc thu hoach - xem ResolveHarvestVariant().
        private const float GoodQualityThreshold = 0.6f;
        private const float PremiumQualityThreshold = 0.8f;

        private const string DirtPatchPath = "res://assets3d/quaternius/farm/dirt_patch.glb";

        public override void _Ready()
        {
            AddToGroup("farm_plots");
            var dirtScene = GD.Load<PackedScene>(DirtPatchPath);
            if (dirtScene != null)
            {
                var soil = dirtScene.Instantiate<Node3D>();
                soil.Name = "Soil";
                soil.Scale = Vector3.One * 30.8f; // to hon 40% so voi truoc (22 -> 30.8)
                AddChild(soil);
                _soilMesh = FindMeshInstance(soil);
            }
            _cropSprite = GetNodeOrNull<Sprite3D>("Crop");
            if (_cropSprite == null)
            {
                _cropSprite = new Sprite3D();
                AddChild(_cropSprite);
            }
            _cropSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _cropSprite.PixelSize = 0.4f;
            _cropSprite.Position = Vector3.Up * 6f;
            _cropSprite.Scale = Vector3.One;
            GameManager.Instance.DayChanged += OnDayChanged;
            RestoreFromSave();
            UpdateVisual();
        }

        private void RestoreFromSave()
        {
            foreach (var t in SaveSystem.Instance.FarmState)
            {
                if (t.X == GridX && t.Y == GridY)
                {
                    _cropId = t.CropId;
                    _growStage = t.GrowStage;
                    _watered = t.Watered;
                    _daysUnwatered = t.DaysUnwatered;
                    _fertilized = t.Fertilized;
                    _lastCropId = t.LastCropId;
                    _pestAfflicted = t.PestAfflicted;
                    _pestDays = t.PestDays;
                    _wasPestDamaged = t.WasPestDamaged;
                    _qualityScore = t.QualityScore;
                    if (_cropId != null)
                    {
                        var seed = ItemDatabase.Instance.GetItem(GuessSeedFor(_cropId));
                        _growsInto = _cropId;
                    }
                }
            }
        }

        private string GuessSeedFor(string cropId) => cropId + "_seed";

        // Goi khi nguoi choi dung cong cu (phim Space). Thu tu uu tien khi cay CHUA chin: diet
        // sau (neu dang bi sau VA co thuoc) -> bon phan (neu CHUA bon VA co phan) -> tuoi nuoc -
        // moi lan bam CHI lam DUNG 1 hanh dong, giong quy uoc cu (khong tu dong gop nhieu buoc).
        public void UseOn()
        {
            if (_cropId == null)
            {
                Plant(DefaultSeedId);
            }
            else if (_growStage >= _growDays)
            {
                Harvest();
            }
            else if (_pestAfflicted && Inventory.Instance.CountOf(DefaultPesticideId) > 0)
            {
                Inventory.Instance.RemoveItem(DefaultPesticideId, 1);
                _pestAfflicted = false;
                _pestDays = 0;
                GD.Print("Da diet sau benh.");
                UpdateVisual();
            }
            else if (!_fertilized && Inventory.Instance.CountOf(DefaultFertilizerId) > 0)
            {
                var fert = ItemDatabase.Instance.GetItem(DefaultFertilizerId);
                if (fert != null)
                {
                    Inventory.Instance.RemoveItem(DefaultFertilizerId, 1);
                    _growDays = Mathf.Max(1, _growDays - fert.FertilizerGrowDaysBonus);
                    _qualityScore += fert.FertilizerQualityBonus;
                    _fertilized = true;
                    GD.Print("Da bon phan.");
                    UpdateVisual();
                }
            }
            else if (!_watered)
            {
                _watered = true;
                _daysUnwatered = 0;
                GD.Print("Da tuoi nuoc.");
                UpdateVisual();
            }
            else
            {
                GD.Print("Cay dang lon, cho them ngay.");
            }
            SyncToSave();
        }

        private void Plant(string seedId)
        {
            var seed = ItemDatabase.Instance.GetItem(seedId);
            if (seed == null || seed.Type != ItemType.Seed)
            {
                GD.Print("Khong co hat giong hop le.");
                return;
            }
            if (Inventory.Instance.CountOf(seedId) <= 0)
            {
                GD.Print($"Ban khong co {seed.Name}. Mua o cua hang!");
                return;
            }
            Inventory.Instance.RemoveItem(seedId, 1);
            _growsInto = seed.GrowsIntoCropId;
            _cropId = _growsInto;

            // Loai dat anh huong toc do lon: mau mo/dac biet lon nhanh hon, kho/nhiem doc lon
            // cham hon, dat uot khong doi (nhung tu dong duoc "tuoi" moi ngay - xem OnDayChanged).
            int soilAdjust = Soil switch
            {
                SoilType.Fertile => -1,
                SoilType.Special => -2,
                SoilType.Dry => 1,
                SoilType.Toxic => 2,
                _ => 0,
            };

            // Luan canh: trong KHAC cay lan truoc o CHINH o nay -> thuong nho toc do lon + chat
            // luong (dat con "khoe"); trong LAI DUNG 1 loai -> phat "dat bac mau" (nguoc lai).
            // Lan trong DAU TIEN (_lastCropId == null) khong thuong cung khong phat.
            int rotationDaysAdjust = 0;
            _qualityScore = 0.5f; // baseline trung tinh - khong lam gi them van ra hang "Normal"
            if (_lastCropId != null)
            {
                if (_lastCropId == _growsInto) { rotationDaysAdjust = 1; _qualityScore -= 0.15f; }
                else { rotationDaysAdjust = -1; _qualityScore += 0.15f; }
            }

            // Hat giong cao cap thien ve cho ra san pham cao cap hon (khong dam bao chac chan).
            if (seedId.Contains("_premium")) _qualityScore += 0.25f;
            else if (seedId.Contains("_good")) _qualityScore += 0.1f;

            _growDays = Mathf.Max(1, seed.GrowDays + soilAdjust + rotationDaysAdjust);
            _growStage = 0;
            _watered = false;
            _fertilized = false;
            _daysUnwatered = 0;
            _pestAfflicted = false;
            _pestDays = 0;
            _wasPestDamaged = false;
            GD.Print($"Da trong {seed.Name}.");
            UpdateVisual();
        }

        private void Harvest()
        {
            if (_growsInto != null)
            {
                string finalId = ResolveHarvestVariant(_growsInto);
                Inventory.Instance.AddItem(finalId, 1);
                // Cung cong don vao kho nong san chung (xem FarmStorage) - de Antoine (nguoi
                // quan ly kho) co so lieu THAT de bao cao, bat ke ai thu hoach (nguoi choi bam
                // Space hay NPC lam ruong tu dong qua ScheduledFarmNpc/FarmWorkerNpc).
                FarmStorage.Instance.Add(finalId, 1);
                QuestSystem.Instance.OnItemCollected(finalId);
                var def = ItemDatabase.Instance.GetItem(finalId);
                GD.Print($"Thu hoach: {def?.Name}!");
                _lastCropId = _growsInto;
            }
            _cropId = null;
            _growsInto = null;
            _growStage = 0;
            _watered = false;
            _fertilized = false;
            _daysUnwatered = 0;
            _pestAfflicted = false;
            _pestDays = 0;
            _wasPestDamaged = false;
            _qualityScore = 0f;
            UpdateVisual();
        }

        // Chon ban "thuong/tot/cao cap" cua san pham dua vao _qualityScore tich luy suot chu ky
        // trong (tuoi deu + bon phan + luan canh + tranh sau benh + hat giong tot) - tra ve DUNG
        // id GOC neu item khong co bien the tuong ung duoc dinh nghia trong items.json (xem
        // ItemDef.GoodVariantId/PremiumVariantId), tranh gia dinh sai id bang cach tu ghep chuoi.
        private string ResolveHarvestVariant(string baseId)
        {
            var baseDef = ItemDatabase.Instance.GetItem(baseId);
            if (baseDef == null) return baseId;
            if (_qualityScore >= PremiumQualityThreshold && !string.IsNullOrEmpty(baseDef.PremiumVariantId))
                return baseDef.PremiumVariantId;
            if (_qualityScore >= GoodQualityThreshold && !string.IsNullOrEmpty(baseDef.GoodVariantId))
                return baseDef.GoodVariantId;
            return baseId;
        }

        private void OnDayChanged(int day)
        {
            if (_cropId == null) return;

            // Dat uot ("Wet") tu nhien du am moi ngay - khong can nguoi choi tuoi tay.
            if (Soil == SoilType.Wet) _watered = true;
            // Troi mua thi cay ngoai troi cung tu duoc tuoi (thoi tiet anh huong cay trong that
            // su, thay vi bia them 1 co che "bao lam hong cay" tach biet khoi he thong thoi tiet
            // da co - xem GameManager.IsRaining).
            if (GameManager.Instance.IsRaining) _watered = true;

            // Sau benh: chi phat sinh khi con dang lon (chua chin) va chua dang bi.
            if (_growStage < _growDays && !_pestAfflicted)
            {
                var pestRng = new RandomNumberGenerator();
                pestRng.Randomize();
                if (pestRng.Randf() < DailyPestChance) _pestAfflicted = true;
            }
            if (_pestAfflicted)
            {
                _pestDays++;
                if (_pestDays >= PestDamageThreshold && !_wasPestDamaged)
                {
                    _wasPestDamaged = true;
                    _qualityScore -= 0.3f;
                }
            }

            if (_watered)
            {
                if (_growStage < _growDays) _growStage++;
                _watered = false;
                _daysUnwatered = 0;
            }
            else
            {
                _daysUnwatered++;
                if (_daysUnwatered >= NeglectDeathThreshold)
                {
                    Wither();
                    return; // Wither() da tu goi UpdateVisual/SyncToSave, khong lam lai o duoi
                }
            }

            UpdateVisual();
            SyncToSave();
        }

        // Cay chet vi qua lau khong duoc tuoi (va khong co mua) - mat trang, khong co san pham.
        // KHONG cap nhat _lastCropId: cay chet thi khong tinh la "da thu hoach" cho luan canh.
        private void Wither()
        {
            GD.Print("Cay da chet vi qua lau khong duoc tuoi nuoc.");
            _cropId = null;
            _growsInto = null;
            _growStage = 0;
            _watered = false;
            _fertilized = false;
            _daysUnwatered = 0;
            _pestAfflicted = false;
            _pestDays = 0;
            _wasPestDamaged = false;
            _qualityScore = 0f;
            UpdateVisual();
            SyncToSave();
        }

        private void UpdateVisual()
        {
            if (_soilMesh != null)
            {
                var baseColor = _watered ? new Color(0.35f, 0.25f, 0.15f) : new Color(0.5f, 0.38f, 0.24f);
                // Tinh mau theo loai dat de nguoi choi nhan biet duoc tung o (mau mo=xanh dam,
                // kho=vang nhat, uot=nau sam hon, nhiem doc=tim, dac biet=vang kim).
                var color = Soil switch
                {
                    SoilType.Fertile => baseColor.Lerp(new Color(0.25f, 0.35f, 0.12f), 0.5f),
                    SoilType.Dry => baseColor.Lerp(new Color(0.68f, 0.58f, 0.35f), 0.5f),
                    SoilType.Wet => baseColor.Darkened(0.25f),
                    SoilType.Toxic => baseColor.Lerp(new Color(0.4f, 0.15f, 0.45f), 0.45f),
                    SoilType.Special => baseColor.Lerp(new Color(0.75f, 0.65f, 0.15f), 0.4f),
                    _ => baseColor,
                };
                if (_soilMesh.GetSurfaceOverrideMaterial(0) is not StandardMaterial3D mat)
                {
                    mat = new StandardMaterial3D();
                    _soilMesh.SetSurfaceOverrideMaterial(0, mat);
                }
                mat.AlbedoColor = color;
            }

            if (_cropId == null)
            {
                _cropSprite.Visible = false;
                return;
            }
            _cropSprite.Visible = true;
            var tex = ItemDatabase.Instance.GetItemIcon(_cropId);
            if (tex != null) _cropSprite.Texture = tex;
            // Lon dan theo giai doan
            float ratio = _growDays > 0 ? (float)_growStage / _growDays : 1f;
            _cropSprite.Scale = Vector3.One * (0.15f + 0.25f * ratio);
            // Bi sau benh -> tint vang-xam om yeu, de nguoi choi nhan ra ngay can xu ly.
            _cropSprite.Modulate = _pestAfflicted
                ? new Color(0.72f, 0.68f, 0.35f)
                : _growStage >= _growDays
                    ? Colors.White
                    : new Color(0.7f, 0.9f, 0.7f);
        }

        private static MeshInstance3D FindMeshInstance(Node root)
        {
            if (root is MeshInstance3D mi) return mi;
            foreach (Node child in root.GetChildren())
            {
                var found = FindMeshInstance(child);
                if (found != null) return found;
            }
            return null;
        }

        private void SyncToSave()
        {
            SaveSystem.Instance.FarmState.RemoveAll(t => t.X == GridX && t.Y == GridY);
            // Luu ca khi o dat dang TRONG (_cropId == null) neu van con _lastCropId (lich su
            // luan canh) - neu khong, sau 1 lan luu/nap lai, o dat vua thu hoach se "quen" mat
            // da trong gi lan truoc, mat het thuong/phat luan canh cho lan trong tiep theo.
            if (_cropId != null || _lastCropId != null)
            {
                SaveSystem.Instance.FarmState.Add(new SaveSystem.FarmTileState
                {
                    X = GridX, Y = GridY, CropId = _cropId,
                    GrowStage = _growStage, Watered = _watered,
                    DaysUnwatered = _daysUnwatered, Fertilized = _fertilized,
                    LastCropId = _lastCropId, PestAfflicted = _pestAfflicted,
                    PestDays = _pestDays, WasPestDamaged = _wasPestDamaged,
                    QualityScore = _qualityScore,
                });
            }
        }
    }
}
