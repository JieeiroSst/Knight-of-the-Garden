using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Mot o dat trong: Trong -> Tuoi -> Lon len (theo ngay) -> Thu hoach.
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

        private MeshInstance3D _soilMesh;
        private Sprite3D _cropSprite;

        // Hat giong mac dinh de demo (khong co UI chon giong)
        [Export] public string DefaultSeedId = "pumpkin_seed";

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
                    if (_cropId != null)
                    {
                        var seed = ItemDatabase.Instance.GetItem(GuessSeedFor(_cropId));
                        _growsInto = _cropId;
                    }
                }
            }
        }

        private string GuessSeedFor(string cropId) => cropId + "_seed";

        // Goi khi nguoi choi dung cong cu (phim Space)
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
            else if (!_watered)
            {
                _watered = true;
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
            _growDays = Mathf.Max(1, seed.GrowDays + soilAdjust);
            _growStage = 0;
            _watered = false;
            GD.Print($"Da trong {seed.Name}.");
            UpdateVisual();
        }

        private void Harvest()
        {
            if (_growsInto != null)
            {
                Inventory.Instance.AddItem(_growsInto, 1);
                // Cung cong don vao kho nong san chung (xem FarmStorage) - de Antoine (nguoi
                // quan ly kho) co so lieu THAT de bao cao, bat ke ai thu hoach (nguoi choi bam
                // Space hay NPC lam ruong tu dong qua ScheduledFarmNpc/FarmWorkerNpc).
                FarmStorage.Instance.Add(_growsInto, 1);
                QuestSystem.Instance.OnItemCollected(_growsInto);
                var def = ItemDatabase.Instance.GetItem(_growsInto);
                GD.Print($"Thu hoach: {def?.Name}!");
            }
            _cropId = null;
            _growsInto = null;
            _growStage = 0;
            _watered = false;
            UpdateVisual();
        }

        private void OnDayChanged(int day)
        {
            // Dat uot ("Wet") tu nhien du am moi ngay - khong can nguoi choi tuoi tay.
            if (_cropId != null && Soil == SoilType.Wet) _watered = true;

            if (_cropId != null && _watered && _growStage < _growDays)
            {
                _growStage++;
                _watered = false;
                UpdateVisual();
                SyncToSave();
            }
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
            _cropSprite.Modulate = _growStage >= _growDays
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
            if (_cropId != null)
            {
                SaveSystem.Instance.FarmState.Add(new SaveSystem.FarmTileState
                {
                    X = GridX, Y = GridY, CropId = _cropId,
                    GrowStage = _growStage, Watered = _watered
                });
            }
        }
    }
}
