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
        // O luoi co dinh (Main.BuildFarm, 12x6): GridX/GridY >= 0. O CUOC TU DO (Player dung Cuoc
        // dat - xem TryTillFreeform): GridX=GridY=-1, vi tri that luu trong FreeformPos - dung
        // lam khoa luu/nap thay cho GridX/GridY (xem SyncToSave/ApplyState).
        public int GridX;
        public int GridY;
        public Vector3 FreeformPos;
        [Export] public SoilType Soil = SoilType.Normal;
        // O dat trong Nha Kinh (xem Main.BuildGreenhouse) - trong duoc QUANH NAM (bo qua
        // ValidSeasons) va TU DONG duoc tuoi moi ngay (khong can nguoi choi/may tuoi).
        [Export] public bool IsGreenhouse = false;

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
        // Mam non: hien RIENG trong giai doan dau (xem UpdateVisual/SproutPhaseEnd) thay vi dung
        // TRUC TIEP icon nong san (thu nho) cho ca qua trinh - mot qua bi ngo/ca chua thu nho tu
        // dau chu trong khong giong that, cay THAT bat dau bang mam la nho, chua co hinh dang qua/
        // hat ro rang. Dung 1 texture chum la xanh don gian (tao bang GradientTexture2D, khong can
        // asset moi) thay vi icon nong san cho giai doan nay.
        private Sprite3D _sproutSprite;
        private static Texture2D _sproutTexCache;
        private const float SproutPhaseEnd = 0.4f; // ty le _growStage/_growDays duoi muc nay = con la mam

        // Hat giong MAC DINH khi khong co SeedSelectUI trong scene (fallback an toan) - binh
        // thuong nguoi choi TU CHON giong tu tui do qua SeedSelectUI (xem RequestPlant()).
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

            _sproutSprite = GetNodeOrNull<Sprite3D>("Sprout");
            if (_sproutSprite == null)
            {
                _sproutSprite = new Sprite3D();
                AddChild(_sproutSprite);
            }
            _sproutSprite.Texture = GetSproutTexture();
            _sproutSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _sproutSprite.PixelSize = 0.4f;
            _sproutSprite.Visible = false;

            // Lech pha rieng tung o (theo vi tri, KHONG dung RNG - giu tat dinh, khong doi giua
            // cac lan tai lai) de cac cay khong du dua DONG LOAT giong het nhau, nhin tu nhien hon.
            _swayPhase = (GridX * 7.3f + GridY * 3.7f + FreeformPos.X * 0.1f + FreeformPos.Z * 0.07f) % Mathf.Tau;

            GameManager.Instance.DayChanged += OnDayChanged;
            RestoreFromSave();
            UpdateVisual();
        }

        // Cay dang lon "du dua" nhe theo gio (offset ngang nho theo song sin) - CHI ap dung khi co
        // cay dang song (khong pest chet/wither), thuan tuy tham my, khong anh huong logic/va cham
        // (o dat van la StaticBody3D dung yen, chi 2 sprite con hien thi du dua).
        private float _swayPhase;

        public override void _Process(double delta)
        {
            if (_cropId == null) return;
            var active = _sproutSprite.Visible ? _sproutSprite : (_cropSprite.Visible ? _cropSprite : null);
            if (active == null) return;
            float sway = Mathf.Sin((float)Time.GetTicksMsec() / 1000f * 1.1f + _swayPhase) * 0.35f;
            var pos = active.Position;
            pos.X = sway;
            active.Position = pos;
        }

        // Chi khop cac o dat trong LUOI CO DINH (Freeform==false) - o dat CUOC TU DO (xem
        // TryTillFreeform) duoc Main.cs tu spawn + goi ApplyState() truc tiep sau khi nap save,
        // KHONG di qua duong nay (vi X/Y mac dinh la 0 se trung voi o luoi that o (0,0)).
        private void RestoreFromSave()
        {
            foreach (var t in SaveSystem.Instance.FarmState)
            {
                if (!t.Freeform && t.X == GridX && t.Y == GridY)
                {
                    ApplyState(t);
                    return;
                }
            }
        }

        public void ApplyState(SaveSystem.FarmTileState t)
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
            if (_cropId != null) _growsInto = _cropId;
            UpdateVisual();
        }

        // Thuoc tinh DOC-ONLY cho Utility AI (xem UtilityAi.cs) cham diem tu ben ngoai - truoc day
        // KHONG co cach nao hoi "o nay can gi" ma khong goi thang UseOn() (luon THUC THI hanh
        // dong uu tien nhat thay vi cho biet can gi). Hoan toan KHONG doi hanh vi UseOn()/Plant()/
        // Harvest() hien co, chi PHOI BAY lai trang thai noi bo da co san.
        public bool IsEmpty => _cropId == null;
        public bool NeedsWater => _cropId != null && !_watered && _growStage < _growDays;
        public bool NeedsFertilizer => _cropId != null && !_fertilized && _growStage < _growDays;
        public bool HasPest => _pestAfflicted;
        public bool IsHarvestable => _cropId != null && _growStage >= _growDays;
        // Tong hop 0..1 muc do "can duoc cham soc ngay" - Utility AI dung truc tiep lam diem so
        // (nhan them he so uu tien ben ngoai neu can). Uu tien: sau benh > sap chet vi thieu nuoc
        // > can tuoi/bon phan thuong > da chin cho hai.
        public float Urgency01
        {
            get
            {
                if (IsHarvestable) return 0.9f;
                if (HasPest) return 1f;
                if (_cropId != null && _daysUnwatered >= NeglectDeathThreshold - 1) return 0.95f; // sap chet
                if (NeedsWater) return 0.7f;
                if (NeedsFertilizer) return 0.4f;
                return 0f;
            }
        }

        // May tuoi tu dong (AutoSprinkler.cs) goi MOI NGAY cho cac o dat trong tam - CHI tuoi
        // (khac UseOn: khong cay/bon phan/thu hoach thay nguoi choi), khong lam gi neu dang trong.
        public void AutoWater()
        {
            if (_cropId == null || _watered) return;
            _watered = true;
            _daysUnwatered = 0;
            UpdateVisual();
            SyncToSave();
        }

        // Goi khi nguoi choi dung cong cu (phim Space). Thu tu uu tien khi cay CHUA chin: diet
        // sau (neu dang bi sau VA co thuoc) -> bon phan (neu CHUA bon VA co phan) -> tuoi nuoc -
        // moi lan bam CHI lam DUNG 1 hanh dong, giong quy uoc cu (khong tu dong gop nhieu buoc).
        public void UseOn()
        {
            if (_cropId == null)
            {
                // NPC lam thue KHONG BAO GIO goi UseOn() tren o dat trong (loc qua IsEmpty truoc
                // - xem FarmWorkerNpc/ScheduledFarmNpc), nen mo UI o day AN TOAN, khong lam ket
                // GOAP cua NPC (chi Player moi thuc su bam Space vao o dat trong).
                RequestPlant();
            }
            else if (_growStage >= _growDays)
            {
                Harvest();
            }
            else if (_pestAfflicted && (Inventory.Instance.EquippedTool == "cao_co" || Inventory.Instance.CountOf(DefaultPesticideId) > 0))
            {
                // Cao Co: cao sach sau benh bang tay, khong can tieu hao Thuoc Diet Sau (thay the
                // "hoa hoc" bang "co hoc" - cong cu chuyen dung rieng cho viec nay).
                if (Inventory.Instance.EquippedTool != "cao_co")
                    Inventory.Instance.RemoveItem(DefaultPesticideId, 1);
                _pestAfflicted = false;
                _pestDays = 0;
                GD.Print("Đã diệt sâu bệnh.");
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
                    GD.Print("Đã bón phân.");
                    UpdateVisual();
                }
            }
            else if (!_watered)
            {
                _watered = true;
                _daysUnwatered = 0;
                // Binh Tuoi Nuoc: dung cu CHUYEN DUNG cho viec tuoi, tuoi deu nuoc hon cuoc dat
                // thong thuong - thuong nho chat luong (giong co che thuong mua/luan canh da co).
                if (Inventory.Instance.EquippedTool == "binh_tuoi") _qualityScore += 0.04f;
                GD.Print("Đã tưới nước.");
                UpdateVisual();
            }
            else
            {
                GD.Print("Cây đang lớn, chờ thêm ngày.");
            }
            SyncToSave();
        }

        // Player bam Space tren o dat trong -> mo SeedSelectUI (danh sach giong dang co trong
        // tui do) thay vi tu dong trong 1 giong co dinh. Neu UI chua san sang (vd thieu node
        // trong scene) -> fallback an toan ve DefaultSeedId, giu game khong bi "cham".
        private void RequestPlant()
        {
            var ui = GetTree().GetFirstNodeInGroup("seed_select_ui") as HiepSiVeVuon.UI.SeedSelectUI;
            if (ui != null) ui.Open(this);
            else Plant(DefaultSeedId);
        }

        // public: SeedSelectUI goi truc tiep sau khi nguoi choi chon giong tu tui do.
        public void Plant(string seedId)
        {
            var seed = ItemDatabase.Instance.GetItem(seedId);
            if (seed == null || seed.Type != ItemType.Seed)
            {
                GD.Print("Không có hạt giống hợp lệ.");
                return;
            }
            // Han che theo mua (xem GameManager.Season) - null/rong = trong duoc quanh nam. O dat
            // Nha Kinh (IsGreenhouse) BO QUA han che nay hoan toan - dung y "khong con bi gioi
            // han theo mua" cua nha kinh.
            if (!IsGreenhouse && seed.ValidSeasons != null && seed.ValidSeasons.Length > 0
                && System.Array.IndexOf(seed.ValidSeasons, GameManager.Instance.CurrentSeason.ToString()) < 0)
            {
                GD.Print($"Mùa này không trồng được {seed.Name}.");
                return;
            }
            if (Inventory.Instance.CountOf(seedId) <= 0)
            {
                GD.Print($"Bạn không có {seed.Name}. Mua ở cửa hàng!");
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

            // Mua Xuan cay lon nhanh hon (dam chieu, nhieu anh sang), mua Dong cham hon (lanh).
            int seasonAdjust = GameManager.Instance.CurrentSeason switch
            {
                GameManager.Season.Spring => -1,
                GameManager.Season.Winter => 1,
                _ => 0,
            };

            _growDays = Mathf.Max(1, seed.GrowDays + soilAdjust + rotationDaysAdjust + seasonAdjust);
            _growStage = 0;
            _watered = false;
            _fertilized = false;
            _daysUnwatered = 0;
            _pestAfflicted = false;
            _pestDays = 0;
            _wasPestDamaged = false;
            GD.Print($"Đã trồng {seed.Name}.");
            UpdateVisual();
            // Goi rieng o day (KHONG dua vao SyncToSave() cuoi UseOn()) vi RequestPlant() mo UI
            // BAT DONG BO - luc UseOn() ket thuc, Plant() CHUA chay, phai tu luu ngay khi PLANT
            // THAT SU xay ra (tu SeedSelectUI, khong di qua UseOn() nua).
            SyncToSave();
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
                GD.Print($"Thu hoạch: {def?.Name}!");
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
            SyncToSave();
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
            // da co - xem GameManager.IsRaining). Ghi lai RIENG hom nay co phai tuoi BOI MUA hay
            // khong (khac voi tuoi tay/may tuoi tu dong/dat uot san) - nuoc mua tu nhien giup cay
            // lon NHANH va KHOE hon nuoc tuoi thuong (theo yeu cau "thoi tiet anh huong toc do cay
            // lon nhu thuc te", xem duoi).
            bool wateredByRainToday = !_watered && GameManager.Instance.IsRaining;
            if (GameManager.Instance.IsRaining) _watered = true;
            // Nha Kinh tu dong tuoi MOI NGAY (khong can nguoi choi/may tuoi tu dong).
            if (IsGreenhouse) _watered = true;

            // Sau benh: chi phat sinh khi con dang lon (chua chin) va chua dang bi. Mua Xuan
            // nhieu con trung hon - nhan he so vao ty le roll THAY VI bia them 1 co che rieng.
            if (_growStage < _growDays && !_pestAfflicted)
            {
                float pestMult = GameManager.Instance.CurrentSeason == GameManager.Season.Spring ? 2.5f : 1f;
                var pestRng = new RandomNumberGenerator();
                pestRng.Randomize();
                if (pestRng.Randf() < DailyPestChance * pestMult) _pestAfflicted = true;
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
                // Nuoc mua tu nhien: ~35% co them 1 giai doan lon NGAY TRONG NGAY DO (nhanh hon
                // tuoi tay/may tuoi tu dong ro ret ve lau dai), va cay khoe hon mot chut (+chat
                // luong nho, tich luy duoc ca chu ky trong nhu fertilizer/luan canh).
                if (wateredByRainToday && _growStage < _growDays)
                {
                    var rainRng = new RandomNumberGenerator();
                    rainRng.Randomize();
                    if (rainRng.Randf() < 0.35f) _growStage++;
                    _qualityScore += 0.03f;
                }
                _watered = false;
                _daysUnwatered = 0;
            }
            else
            {
                // Han han mua He: dat kho nhanh hon (+2/ngay khong tuoi thay vi +1) - dung y "can
                // nhieu nuoc hon" ma khong them co che rieng, tai su dung nguong chet co san.
                _daysUnwatered += GameManager.Instance.CurrentSeason == GameManager.Season.Summer ? 2 : 1;
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
            GD.Print("Cây đã chết vì quá lâu không được tưới nước.");
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

        // Texture chum mam non - 1 vong tron mem mau xanh la (tam mo phong 1 chum la nho vua nhu
        // moc), tao qua GradientTexture2D (khong can file anh moi) - CACHE TINH (static) vi TAT
        // CA o dat dung CHUNG 1 texture nay, chi tao 1 lan cho ca game thay vi moi o 1 ban rieng.
        private static Texture2D GetSproutTexture()
        {
            if (_sproutTexCache != null) return _sproutTexCache;
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(0.5f, 0.78f, 0.35f, 1f));
            gradient.AddPoint(0.65f, new Color(0.4f, 0.68f, 0.28f, 0.95f));
            gradient.SetColor(1, new Color(0.35f, 0.6f, 0.24f, 0f));
            _sproutTexCache = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 32,
                Height = 32,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 1f),
            };
            return _sproutTexCache;
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
                _sproutSprite.Visible = false;
                return;
            }

            float ratio = _growDays > 0 ? (float)_growStage / _growDays : 1f;
            // Bi sau benh -> tint vang-xam om yeu, de nguoi choi nhan ra ngay can xu ly (ap dung
            // cho ca 2 giai doan, mam non lan cay truong thanh).
            var pestTint = new Color(0.72f, 0.68f, 0.35f);

            if (ratio < SproutPhaseEnd)
            {
                // Giai doan MAM NON: hat vua nay mam, chua co hinh dang qua/hat ro rang - dung
                // chum la xanh don gian (khong phai icon nong san thu nho, cay THAT khong bat dau
                // bang 1 qua/cu ti hon), lon dan tu duoi dat len.
                _cropSprite.Visible = false;
                _sproutSprite.Visible = true;
                float sproutT = ratio / SproutPhaseEnd;
                _sproutSprite.Scale = Vector3.One * Mathf.Lerp(0.12f, 0.5f, sproutT);
                _sproutSprite.Position = new Vector3(0, Mathf.Lerp(1f, 3f, sproutT), 0);
                _sproutSprite.Modulate = _pestAfflicted ? pestTint : Colors.White;
            }
            else
            {
                // Giai doan TRUONG THANH: hien icon nong san that, lon dan ro ret va NHO LEN cao
                // hon (cay that cao dan khi gan chin, khong chi "phinh to tai cho") cho toi khi
                // chin hoan toan (mau sac day du, khong con tint xanh non nua).
                _sproutSprite.Visible = false;
                _cropSprite.Visible = true;
                var tex = ItemDatabase.Instance.GetItemIcon(_cropId);
                if (tex != null) _cropSprite.Texture = tex;
                float matureT = (ratio - SproutPhaseEnd) / (1f - SproutPhaseEnd);
                _cropSprite.Scale = Vector3.One * Mathf.Lerp(0.32f, 0.85f, matureT);
                _cropSprite.Position = new Vector3(0, Mathf.Lerp(3f, 6f, matureT), 0);
                _cropSprite.Modulate = _pestAfflicted
                    ? pestTint
                    : _growStage >= _growDays
                        ? Colors.White
                        : new Color(0.72f, 0.92f, 0.72f);
            }
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

        // public: Plant()/Harvest() tu goi truc tiep (thay vi chi dua vao UseOn()), va
        // TryTillFreeform() can luu ngay trang thai "moi cuoc, chua trong" cho o dat tu do.
        public void SyncToSave()
        {
            if (GridX < 0)
            {
                // O dat CUOC TU DO: khoa luu la VI TRI (khong phai X/Y, luon la -1/-1 cho moi o
                // tu do) - va LUON luu (ke ca chua trong gi) vi ban than viec "da cuoc" o day da
                // la 1 trang thai can nho, khac o luoi co dinh (luon duoc BuildFarm() tao lai).
                SaveSystem.Instance.FarmState.RemoveAll(t =>
                    t.Freeform && t.PosX == FreeformPos.X && t.PosZ == FreeformPos.Z);
                SaveSystem.Instance.FarmState.Add(new SaveSystem.FarmTileState
                {
                    Freeform = true, PosX = FreeformPos.X, PosZ = FreeformPos.Z,
                    CropId = _cropId, GrowStage = _growStage, Watered = _watered,
                    DaysUnwatered = _daysUnwatered, Fertilized = _fertilized,
                    LastCropId = _lastCropId, PestAfflicted = _pestAfflicted,
                    PestDays = _pestDays, WasPestDamaged = _wasPestDamaged,
                    QualityScore = _qualityScore,
                });
                return;
            }

            SaveSystem.Instance.FarmState.RemoveAll(t => !t.Freeform && t.X == GridX && t.Y == GridY);
            // Luu ca khi o dat dang TRONG (_cropId == null) neu van con _lastCropId (lich su
            // luan canh) - neu khong, sau 1 lan luu/nap lai, o dat vua thu hoach se "quen" mat
            // da trong gi lan truoc, mat het thuong/phat luan canh cho lan trong tiep theo.
            if (_cropId != null || _lastCropId != null)
            {
                SaveSystem.Instance.FarmState.Add(new SaveSystem.FarmTileState
                {
                    Freeform = false, X = GridX, Y = GridY, CropId = _cropId,
                    GrowStage = _growStage, Watered = _watered,
                    DaysUnwatered = _daysUnwatered, Fertilized = _fertilized,
                    LastCropId = _lastCropId, PestAfflicted = _pestAfflicted,
                    PestDays = _pestDays, WasPestDamaged = _wasPestDamaged,
                    QualityScore = _qualityScore,
                });
            }
        }

        // ==== Cuoc dat MOI tren co (Player dung item "hoe") ====

        // Khop voi FarmSpacing (private, o Main.BuildFarm) de o dat tu do thang hang thi giac voi
        // luoi co dinh - khong tham chieu truc tiep duoc vi FarmSpacing la private trong Main.
        private const float FreeformTileSize = 84f;
        private static PackedScene _plotScene;

        public static Vector3 SnapToGrid(Vector3 pos) => new Vector3(
            Mathf.Round(pos.X / FreeformTileSize) * FreeformTileSize, 0,
            Mathf.Round(pos.Z / FreeformTileSize) * FreeformTileSize);

        // Thu cuoc 1 o dat MOI tai worldPos (se duoc snap ve luoi FreeformTileSize cho thang
        // hang). Tra ve null neu qua gan 1 o dat khac (luoi co dinh HOAC tu do) hoac nam trong 1
        // ExclusionZone (nuoc/toa nha/tuong trai/ham mo... - xem WorldStreamer.ExclusionZones) -
        // tranh cuoc dat de len nhung noi khong hop ly.
        public static FarmPlot TryTillFreeform(Vector3 worldPos, Node parent)
        {
            Vector3 snapped = SnapToGrid(worldPos);

            foreach (Node n in parent.GetTree().GetNodesInGroup("farm_plots"))
            {
                if (n is FarmPlot p && IsInstanceValid(p)
                    && p.GlobalPosition.DistanceTo(snapped) < FreeformTileSize * 0.7f)
                    return null;
            }
            foreach (var (center, radius) in WorldStreamer.ExclusionZones)
            {
                if (new Vector2(snapped.X - center.X, snapped.Z - center.Z).Length() < radius)
                    return null;
            }

            _plotScene ??= GD.Load<PackedScene>("res://scenes/FarmPlot.tscn");
            var plot = _plotScene.Instantiate<FarmPlot>();
            plot.GridX = -1;
            plot.GridY = -1;
            plot.FreeformPos = snapped;
            plot.Position = snapped;
            parent.AddChild(plot);
            plot.SyncToSave();
            return plot;
        }
    }
}
