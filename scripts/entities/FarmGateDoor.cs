using Godot;

namespace HiepSiVeVuon.Entities
{
    // 1 canh cua go cua Cong Chao trang trai (xem Main.AddStoneGateArch) - node nay LA ban le
    // (dat tai mep trong tru da, xem AddGateDoorLeaf), mesh+va cham cua canh cua la CON, lech ra
    // tu goc theo truc X cuc bo - xoay Y cua chinh node nay quanh diem do la xoay ca canh cua
    // quanh ban le nhu cua that. Tu dong MO khi nguoi choi lai gan, DONG lai khi di xa - "player
    // co the ra vao cua trang trai" ma khong can bam phim rieng (tu nhien nhu di qua 1 cong that).
    //
    // AnimatableBody3D (khong phai StaticBody3D nhu da so vat can khac trong game) - day la THAN
    // VAT LY THAT SU DI CHUYEN moi frame (xoay), Godot khuyen dung loai body nay cho cua/thang
    // may/san di dong de va cham duoc tinh dung khi than dang chuyen dong, StaticBody3D gia dinh
    // hinh hoc DUNG YEN nen di chuyen no qua code co the gay sai lech va cham nho.
    public partial class FarmGateDoor : AnimatableBody3D
    {
        // Goc Y "dong" (khop voi huong tuong, gan luc tao - xem AddGateDoorLeaf) va do lech can
        // THEM vao de "mo" (co the am, tuy canh trai/phai mo huong nao) - Main.cs tinh chinh xac
        // bang vector (huong doc tuong + huong ra ngoai) thay vi doan mo 90 do co dinh.
        [Export] public float ClosedYRotationDeg = 0f;
        [Export] public float OpenSwingDeg = 100f;
        [Export] public float DetectRadius = 170f;
        [Export] public float SwingSpeedDegPerSec = 160f;

        private Node3D _player;
        private float _currentSwing;

        public override void _Ready() => AddToGroup("farm_gate_doors");

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;

            bool shouldOpen = _player != null && GlobalPosition.DistanceTo(_player.GlobalPosition) <= DetectRadius;
            float targetSwing = shouldOpen ? OpenSwingDeg : 0f;
            if (Mathf.IsEqualApprox(_currentSwing, targetSwing)) return;

            _currentSwing = Mathf.MoveToward(_currentSwing, targetSwing, SwingSpeedDegPerSec * dt);
            var rot = RotationDegrees;
            rot.Y = ClosedYRotationDeg + _currentSwing;
            RotationDegrees = rot;
        }
    }
}
