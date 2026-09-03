using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cay lua mi trong "canh dong lua mi" rieng (xem Main.BuildWheatField) - khu DAT RIENG ngoai
    // luoi o dat thuong, cay tu moc/lon/chin qua thoi gian giong Vuon Nho/Vuon Cay (FruitTree.cs)
    // thay vi phai cuoc/trong/tuoi tung o. KHAC FruitTree.GrowsFromSapling (phong to dan 1 model
    // DUY NHAT bang scale lien tuc): o day dung 3 MODEL 3D THAT rieng biet cho 3 giai doan
    // (BabyWheat/SmallerWheat/Wheat, nguoi dung tu cung cap) - DOI HAN sang model tiep theo moi
    // khi qua 1 giai doan, vi moi giai doan la 1 HINH DANG khac han (mam non thang dung -> cay
    // giua cao hon -> bong lua rue xuong mau vang), khong the mo phong bang phong to don thuan.
    public partial class WheatPlant : StaticBody3D
    {
        [Export] public int DaysPerStage = 2; // 2 giai doan lon (0->1, 1->2) x 2 ngay = 4 ngay/vu

        private static Mesh _babyMesh;
        private static Mesh _smallerMesh;
        private static Mesh _fullMesh;

        private MeshInstance3D _visual;
        private int _stage = 0; // 0 = mam non, 1 = cay giua, 2 = bong lua chin (hai duoc)
        private int _dayCounter = 0;

        private static void EnsureMeshesLoaded()
        {
            _babyMesh ??= GD.Load<Mesh>("res://assets3d/farm_items/BabyWheat.obj");
            _smallerMesh ??= GD.Load<Mesh>("res://assets3d/farm_items/SmallerWheat.obj");
            _fullMesh ??= GD.Load<Mesh>("res://assets3d/farm_items/Wheat.obj");
        }

        // Main.BuildWheatField goi NGAY SAU khi AddChild - tao san mesh mam non + dang ky vao
        // nhom "wheat_plants" (Player.TryUseTool() quet nhom nay giong "fruit_trees").
        public void Init()
        {
            EnsureMeshesLoaded();
            AddToGroup("wheat_plants");
            _visual = new MeshInstance3D { Mesh = _babyMesh };
            AddChild(_visual);
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void OnDayChanged(int day)
        {
            if (_stage >= 2) return; // da chin, dung lai cho hai (khong "qua chin")
            _dayCounter++;
            if (_dayCounter < DaysPerStage) return;
            _dayCounter = 0;
            _stage++;
            _visual.Mesh = _stage switch { 1 => _smallerMesh, 2 => _fullMesh, _ => _babyMesh };
        }

        // Goi tu Player.cs khi nguoi choi tuong tac trong tam - dung ten "UseOn" giong FarmPlot/
        // FruitTree de nhat quan quy uoc dat ten trong toan bo he thong nong nghiep.
        public void UseOn()
        {
            if (_stage < 2)
            {
                GD.Print("Lúa mì chưa chín, chờ thêm ngày.");
                return;
            }
            Inventory.Instance.AddItem("wheat", 1);
            FarmStorage.Instance.Add("wheat", 1);
            QuestSystem.Instance.OnItemCollected("wheat");
            GD.Print("Đã gặt được Bó Lúa Mì!");

            // Cay lau nam - VE LAI mam non thay vi bien mat, tu moc lai cho vu sau (dung yeu cau
            // "tu lon lai" giong Vuon Nho/Vuon Cay, khong phai trong lai tu dau).
            _stage = 0;
            _dayCounter = 0;
            _visual.Mesh = _babyMesh;
        }
    }
}
