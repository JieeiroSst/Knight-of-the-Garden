using Godot;

namespace HiepSiVeVuon.Entities
{
    // Thuyen neo canh ho (xem Main.BuildLakeRegion) - nguoi choi bam [R] (dung CHUNG phim voi
    // cuoi ngua, xem Player.TryToggleRide) de len/xuong, di chuyen tren mat nuoc NHANH hon boi
    // thuong. Mo phong don gian: KHONG trong luc/va cham vat ly that (StaticBody3D, tu doi vi tri
    // truc tiep khi dang cuoi) vi thuyen chi noi tren 1 mat phang nuoc, khong can MoveAndSlide.
    public partial class Boat : StaticBody3D
    {
        [Export] public float RideSpeed = 75f;
        [Export] public float TurnSpeed = 3.2f;

        public static readonly Vector3 SeatOffset = new(0, 6f, 2f);
        public Vector3 Facing => _facing;

        // Main.cs gan ngay sau khi Instantiate - gioi han thuyen khong troi qua khoi vung nuoc.
        public Vector3 BoundsCenter;
        public float BoundsRadius = 260f;

        private Vector3 _facing = Vector3.Back;
        private Vector3 _homePos;
        private bool _ridden = false;

        public override void _Ready()
        {
            AddToGroup("boats");
            _homePos = GlobalPosition;
        }

        public void SetRidden(bool ridden)
        {
            _ridden = ridden;
            if (!ridden) GlobalPosition = ClosestValidPos(GlobalPosition);
        }

        public override void _Process(double delta)
        {
            if (!_ridden) return;
            float dt = (float)delta;

            var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var desiredDir = new Vector3(input.X, 0f, input.Y);
            if (desiredDir == Vector3.Zero) return;

            desiredDir = desiredDir.Normalized();
            _facing = _facing.Lerp(desiredDir, Mathf.Clamp(TurnSpeed * dt, 0f, 1f)).Normalized();

            Vector3 next = GlobalPosition + _facing * RideSpeed * dt;
            GlobalPosition = ClosestValidPos(next);
        }

        // Giu thuyen trong ban kinh vung nuoc - khong "khoa cung" (nguoi choi van luon lai duoc,
        // chi khong the vuot qua bien nuoc len bo).
        private Vector3 ClosestValidPos(Vector3 pos)
        {
            Vector2 flat = new(pos.X - BoundsCenter.X, pos.Z - BoundsCenter.Z);
            if (flat.Length() <= BoundsRadius) return pos;
            flat = flat.Normalized() * BoundsRadius;
            return new Vector3(BoundsCenter.X + flat.X, pos.Y, BoundsCenter.Z + flat.Y);
        }
    }
}
