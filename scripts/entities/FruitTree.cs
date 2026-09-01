using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cay/gian nho LAU NAM (vuon cay an qua + vuon nho NHO - KHONG dung cho vuon nho LON dung
    // MultiMeshInstance3D, xem Main.BuildBigVineyard) - mo phong chu ky Plant->grow->Harvest cua
    // FarmPlot.cs nhung KHONG can trong lai: sau khi hai, tu moc lai qua vai ngay thay vi bien
    // mat vinh vien (dung nghia "cay lau nam"). Ke thua StaticBody3D de tai su dung TRUC TIEP lam
    // va cham than cay/coc nho da co san (xem Main.AddFruitTree/BuildVineyard), khong can 1 node
    // rieng chi de gan script.
    public partial class FruitTree : StaticBody3D
    {
        [Export] public int RipenDays = 4;
        [Export] public string FruitItemId;

        private int _growStage = 0;
        private bool _ripe = false;
        private Node3D _fruitVisual; // nhom mesh qua (hoac ca cum gian nho) - AN khi chua chin, HIEN khi chin

        // Main.cs goi NGAY SAU khi AddChild - truyen vao node hien thi qua/vine da dung san (xem
        // AddFruitTree/BuildVineyard) de FruitTree AN/HIEN toan bo cung luc thay vi tao/xoa mesh.
        public void Init(Node3D fruitVisual)
        {
            AddToGroup("fruit_trees"); // de Player.TryUseTool() tim duoc, giong "farm_plots"
            _fruitVisual = fruitVisual;
            if (_fruitVisual != null) _fruitVisual.Visible = false;
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void OnDayChanged(int day)
        {
            if (_ripe) return;
            _growStage++;
            if (_growStage >= RipenDays)
            {
                _ripe = true;
                if (_fruitVisual != null) _fruitVisual.Visible = true;
            }
        }

        // Goi tu Player.cs khi nguoi choi tuong tac trong tam - dung TEN "UseOn" giong FarmPlot
        // de nhat quan quy uoc dat ten trong toan bo he thong nong nghiep.
        public void UseOn()
        {
            if (!_ripe)
            {
                GD.Print("Cay chua co qua chin, cho them ngay.");
                return;
            }
            Inventory.Instance.AddItem(FruitItemId, 1);
            // Cung cong don vao kho nong san chung (xem FarmStorage), giong FarmPlot.Harvest().
            FarmStorage.Instance.Add(FruitItemId, 1);
            QuestSystem.Instance.OnItemCollected(FruitItemId);
            var def = ItemDatabase.Instance.GetItem(FruitItemId);
            GD.Print($"Hai duoc: {def?.Name}!");

            _ripe = false;
            _growStage = 0;
            if (_fruitVisual != null) _fruitVisual.Visible = false;
        }
    }
}
