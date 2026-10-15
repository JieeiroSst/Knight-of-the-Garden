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
        // Suc cuoc toi thieu (Inventory.GetToolPower: tay 3, Cuoc Chim Da 12, Bac 24, Vang 42, Kim
        // Cuong 70) - quang quy o mo sau can cuoc chim tot hon. 0 = dao bang gi cung duoc.
        [Export] public int RequiredPower = 0;

        private static ulong _lastWeakMsg;

        // Player goi TRUOC khi tru the luc - cuoc qua yeu thi bao (gioi han 1 lan/2s) va khong dao.
        public bool CanMineWith(int power)
        {
            if (_depleted) return false;
            if (power >= RequiredPower) return true;
            ulong now = Time.GetTicksMsec();
            if (now - _lastWeakMsg > 2000)
            {
                _lastWeakMsg = now;
                HiepSiVeVuon.UI.Notify.Show(string.Format(Loc.T("mine.too_weak_fmt"), ItemDatabase.Instance.GetDisplayName(OreItemId), PickaxeNameFor(RequiredPower)));
            }
            return false;
        }

        public static string PickaxeNameFor(int power) => ItemDatabase.Instance.GetDisplayName(
            power <= 12 ? "pickaxe" : power <= 24 ? "pickaxe_bac" : power <= 42 ? "pickaxe_vang" : "pickaxe_kim_cuong");

        private int _hp;
        private bool _depleted = false;
        private int _regrowDaysLeft = 0;
        private Node3D _visual; // mesh da/khoi quang - Main.cs gan qua Init(), AN khi vo, HIEN khi moc lai

        // Khoa ON DINH cua khoi quang (vd "field_12_-3_2" cho bai quang trong chunk hoang da) - de da
        // dao thi KHONG moc lai chi vi chunk bi xoa/tao lai khi nguoi choi di xa roi quay ve, va giu
        // qua lan thoat game (OreRegistry luu trong ban luu). null = khong theo doi (ham mo goc cu).
        public string PersistKey;

        public void Init(Node3D visual)
        {
            _visual = visual;
            _hp = MaxHp;
            AddToGroup("ore_nodes");
            GameManager.Instance.DayChanged += OnDayChanged;
            OreRegistry.Changed += ApplyRegistry;
            ApplyRegistry();
        }

        public override void _ExitTree() => OreRegistry.Changed -= ApplyRegistry;

        private void ApplyRegistry()
        {
            if (string.IsNullOrEmpty(PersistKey)) return;
            int left = OreRegistry.DaysLeft(PersistKey);
            _depleted = left > 0;
            _regrowDaysLeft = left;
            if (!_depleted) _hp = MaxHp;
            if (_visual != null) _visual.Visible = !_depleted;
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
            if (!string.IsNullOrEmpty(PersistKey)) OreRegistry.MarkDepleted(PersistKey, RegrowDays);
            if (_visual != null) _visual.Visible = false;
            // Vat pham nguoi choi TU DAO duoc - KHONG goi FarmStorage.Add (kho do la so lieu
            // "nong san trang trai", khac ban chat voi khoang san dao thu cong).
            // Ky nang Khai Khoang: co hoi dao duoc gap doi.
            int amount = GD.Randf() < SkillSystem.Instance.ExtraOreChance ? DropAmount * 2 : DropAmount;
            Inventory.Instance.AddItem(OreItemId, amount, "mine");
            QuestSystem.Instance.OnItemCollected(OreItemId);
            SkillSystem.Instance.AddXp(Data.SkillType.Mining, 6 + MaxHp / 10);
            AchievementSystem.Instance.Add("ores_mined");
            HiepSiVeVuon.UI.Notify.Show(string.Format(Loc.T("pickup.got_fmt"), ItemDatabase.Instance.GetDisplayName(OreItemId), amount));
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
