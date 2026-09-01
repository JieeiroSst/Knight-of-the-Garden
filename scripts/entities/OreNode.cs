using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Quang mo trong ham (xem Main.BuildMine) - ket hop 2 mau da co trong game: Hp + nhan sat
    // thuong NHIEU LAN + vo/roi vat pham (giong Enemy.TakeDamage/Die) + tu moc lai sau N ngay
    // (giong FruitTree.cs). Nguoi choi dung Pickaxe (xem Player.TryAttack, Inventory.GetToolPower)
    // de "dao" - moi lan trung tru Hp, het Hp thi vo (an mesh, roi vat pham), roi tu hien lai sau
    // RegrowDays ngay THAT.
    public partial class OreNode : StaticBody3D
    {
        [Export] public int MaxHp = 30;
        [Export] public string OreItemId;
        [Export] public int DropAmount = 2;
        [Export] public int RegrowDays = 3;

        private int _hp;
        private bool _depleted = false;
        private int _regrowDaysLeft = 0;
        private Node3D _visual; // mesh da/khoi quang - Main.cs gan qua Init(), AN khi vo, HIEN khi moc lai

        public void Init(Node3D visual)
        {
            _visual = visual;
            _hp = MaxHp;
            AddToGroup("ore_nodes");
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        // Goi tu Player.TryAttack() khi vung Pickaxe trung ban kinh - dung TEN "Mine" (rieng, khac
        // "UseOn"/"TakeDamage" cua cac nhom khac) de ro rang day la hanh dong dao khoang, khong
        // phai tuong tac nong trai hay chien dau.
        public void Mine(int dmg)
        {
            if (_depleted) return;
            _hp -= dmg;
            if (_hp <= 0) Deplete();
        }

        private void Deplete()
        {
            _depleted = true;
            _regrowDaysLeft = RegrowDays;
            if (_visual != null) _visual.Visible = false;
            // Vat pham nguoi choi TU DAO duoc - KHONG goi FarmStorage.Add (kho do la so lieu
            // "nong san trang trai", khac ban chat voi khoang san dao thu cong).
            Inventory.Instance.AddItem(OreItemId, DropAmount);
            QuestSystem.Instance.OnItemCollected(OreItemId);
        }

        private void OnDayChanged(int day)
        {
            if (!_depleted) return;
            _regrowDaysLeft--;
            if (_regrowDaysLeft <= 0)
            {
                _depleted = false;
                _hp = MaxHp;
                if (_visual != null) _visual.Visible = true;
            }
        }
    }
}
