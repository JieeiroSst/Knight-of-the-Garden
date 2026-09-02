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

        // Cay THAT SU lon dan tu cay non truoc khi ra qua lan dau (thay vi AN HOAN TOAN cho toi
        // lan chin dau tien - vuon nho truoc day dung CHUNG 1 model cho ca "than cay" lan "qua",
        // nen luc chua chin la TRONG KHONG, khong giong 1 cay non that su dang lon). Mac dinh
        // false de KHONG doi hanh vi hien co cua vuon cay/to ong (van an/hien fruitVisual nhu cu
        // - cac noi do da co than/tan la RIENG luon hien, chi phan "qua" moi can an/hien) - CHI
        // bat cho vuon nho (xem Main.BuildVineyard) theo dung yeu cau "phat trien tu cay non toi
        // cay ra qua nho, that chan thuc".
        [Export] public bool GrowsFromSapling = false;
        [Export] public int MaturationDays = 10;

        private int _growStage = 0;
        private bool _ripe = false;
        private Node3D _fruitVisual; // nhom mesh qua (hoac ca cum gian nho) - AN khi chua chin, HIEN khi chin

        // CHI dung khi GrowsFromSapling=true: cay LUON hien (khong bao gio an het), bat dau NHO
        // roi lon dan MOT LAN DUY NHAT toi kich thuoc that (_fullScale, doc luc Init tu scale da
        // dat san trong Main.BuildVineyard) - sau khi truong thanh, KICH THUOC the hien dang cho
        // qua chin (hoi nho lai, "chum nho con thua") hay da chin (day du, "chum nho day dan") -
        // fruitVisual la Node3D bat ky (model GLB tuy y) nen KHONG dung duoc Modulate (chi co o
        // CanvasItem/SpriteBase3D), phai dung SCALE thay cho tint mau. Khong dung Visible nua cho
        // giai doan nay (cay lau nam that khong bien mat giua cac vu, chi it/nhieu qua khac nhau).
        private bool _isMature = true;
        private int _maturationProgress = 0;
        private Vector3 _fullScale = Vector3.One;
        private const float UnripeScaleFactor = 0.82f; // qua con dang lon, chum nho hoi nho/thua hon

        // Main.cs goi NGAY SAU khi AddChild - truyen vao node hien thi qua/vine da dung san (xem
        // AddFruitTree/BuildVineyard) de FruitTree AN/HIEN toan bo cung luc thay vi tao/xoa mesh.
        public void Init(Node3D fruitVisual)
        {
            AddToGroup("fruit_trees"); // de Player.TryUseTool() tim duoc, giong "farm_plots"
            _fruitVisual = fruitVisual;
            if (_fruitVisual != null) _fullScale = _fruitVisual.Scale;

            if (GrowsFromSapling)
            {
                _isMature = false;
                if (_fruitVisual != null)
                {
                    _fruitVisual.Visible = true; // LUON hien tu dau - cay non van thay duoc, chi NHO hon
                    _fruitVisual.Scale = _fullScale * 0.18f;
                }
            }
            else if (_fruitVisual != null)
            {
                _fruitVisual.Visible = false; // hanh vi cu (vuon cay/to ong): an cho toi khi chin
            }

            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void OnDayChanged(int day)
        {
            if (GrowsFromSapling && !_isMature)
            {
                _maturationProgress++;
                float t = Mathf.Clamp((float)_maturationProgress / MaturationDays, 0f, 1f);
                if (_fruitVisual != null) _fruitVisual.Scale = _fullScale * Mathf.Lerp(0.18f, UnripeScaleFactor, t);
                if (_maturationProgress >= MaturationDays)
                {
                    _isMature = true;
                    if (_fruitVisual != null) _fruitVisual.Scale = _fullScale * UnripeScaleFactor;
                }
                return; // chua truong thanh thi chua tinh chu ky ra qua
            }

            if (_ripe) return;
            _growStage++;
            if (_growStage >= RipenDays)
            {
                _ripe = true;
                if (GrowsFromSapling)
                {
                    if (_fruitVisual != null) _fruitVisual.Scale = _fullScale; // day du, san sang hai
                }
                else if (_fruitVisual != null) _fruitVisual.Visible = true;
            }
        }

        // Goi tu Player.cs khi nguoi choi tuong tac trong tam - dung TEN "UseOn" giong FarmPlot
        // de nhat quan quy uoc dat ten trong toan bo he thong nong nghiep.
        public void UseOn()
        {
            if (!_ripe)
            {
                GD.Print("Cây chưa có quả chín, chờ thêm ngày.");
                return;
            }
            Inventory.Instance.AddItem(FruitItemId, 1);
            // Cung cong don vao kho nong san chung (xem FarmStorage), giong FarmPlot.Harvest().
            FarmStorage.Instance.Add(FruitItemId, 1);
            QuestSystem.Instance.OnItemCollected(FruitItemId);
            var def = ItemDatabase.Instance.GetItem(FruitItemId);
            GD.Print($"Hái được: {def?.Name}!");

            _ripe = false;
            _growStage = 0;
            if (GrowsFromSapling)
            {
                // Cay lau nam - VAN hien, chi hoi nho lai (chum nho vua hai xong, con thua) cho toi lan chin sau.
                if (_fruitVisual != null) _fruitVisual.Scale = _fullScale * UnripeScaleFactor;
            }
            else if (_fruitVisual != null) _fruitVisual.Visible = false;
        }
    }
}
