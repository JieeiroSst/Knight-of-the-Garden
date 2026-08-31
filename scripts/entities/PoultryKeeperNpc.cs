using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nguoi cham ga: ke thua toan bo he thong hoi thoai/cua hang cua NPC (Interact,
    // Trust...), them AI di chuyen theo GIO HANH CHINH THAT (6h sang - 18h toi, dong bo dong ho
    // may tinh qua GameManager.HourChanged): den gio lam thi di tu nha ra chuong ga, quanh quan
    // cham soc (rai thuc an cho ga) va DINH KY QUET tim trung ga (DroppedItem "egg") ma dan ga
    // da tu de (xem Chicken.UpdateEggLaying) trong pham vi lam viec, THU HOACH (xoa vat pham
    // khoi mat dat) roi CAT VAO KHO NONG SAN chung cua trang trai (FarmStorage, hien so luong o
    // bang go trong nha kho - xem UI.FarmStorageBoard), khong con "vat ra tu khong khi" nhu
    // truoc. Het gio thi di ve nha nghi qua dem (vao han ben trong phong noi that that su, giong
    // FarmhandNpc.cs/StablehandNpc.cs).
    public partial class PoultryKeeperNpc : NPC
    {
        private enum WorkState { AtHome, GoingToWork, Working, GoingHome }

        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 18;
        [Export] public float WorkWanderRadius = 70f;
        [Export] public float HarvestRange = 45f;
        [Export] public double HarvestScanIntervalSec = 1.0;

        // Main.cs gan cac vi tri nay ngay sau khi tao NPC (truoc AddChild).
        public Vector3 HomePos;       // ngay truoc cua nha (ngoai troi)
        public Vector3 InteriorHomePos; // phong noi that that su (tang tret) - noi ngu ban dem
        public Vector3 WorkPos;

        private WorkState _workState = WorkState.AtHome;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private double _harvestScanCooldown = 0;

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            bool onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            _workState = onDuty ? WorkState.Working : WorkState.AtHome;
            GlobalPosition = onDuty ? WorkPos : InteriorHomePos + Vector3.Up * 8f;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            if (hour == WorkStartHour && _workState == WorkState.AtHome)
            {
                GlobalPosition = HomePos;
                _workState = WorkState.GoingToWork;
            }
            else if (hour == WorkEndHour && _workState != WorkState.AtHome)
            {
                _workState = WorkState.GoingHome;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _workState switch
            {
                WorkState.GoingToWork => GoTo(WorkPos, Speed, WorkState.Working),
                WorkState.GoingHome => GoTo(HomePos, Speed, WorkState.AtHome),
                WorkState.Working => DoWorkWander(dt),
                _ => (Vector3.Zero, 0f), // AtHome: nghi ngoi, dung yen
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            if (_model != null && _facing != Vector3.Zero)
            {
                var lookDir = FlipModelFacing ? -_facing : _facing;
                var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
                _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            }

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }

        private (Vector3 dir, float speed) GoTo(Vector3 target, float speed, WorkState arrivedState)
        {
            Vector3 dir = target - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 14f)
            {
                _workState = arrivedState;
                if (arrivedState == WorkState.AtHome)
                    GlobalPosition = InteriorHomePos + Vector3.Up * 8f;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), speed);
        }

        // Trong gio lam: quanh quan gan chuong ga (rai thuc an) va DINH KY QUET thu hoach trung
        // ga (DroppedItem "egg") ma dan ga da tu de trong pham vi lam viec - cat thang vao kho
        // nong san chung (FarmStorage), khong con "vat ra tu khong khi" nhu ban thiet ke truoc.
        private (Vector3 dir, float speed) DoWorkWander(float dt)
        {
            _harvestScanCooldown -= dt;
            if (_harvestScanCooldown <= 0)
            {
                HarvestNearbyEggs();
                _harvestScanCooldown = HarvestScanIntervalSec;
            }

            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, WorkWanderRadius);
                _wanderTarget = WorkPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(4000, 9000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.5f);
        }

        // Quet toan bo vat pham roi tren mat dat (group "dropped_items"), thu hoach nhung qua
        // trung (ItemId == "egg") trong pham vi HarvestRange quanh NPC - xoa khoi mat dat va cong
        // don vao kho nong san chung (KHONG phai tui do nguoi choi).
        private void HarvestNearbyEggs()
        {
            foreach (var node in GetTree().GetNodesInGroup("dropped_items"))
            {
                if (node is not DroppedItem item || !IsInstanceValid(item)) continue;
                if (item.ItemId != "egg") continue;
                if (GlobalPosition.DistanceTo(item.GlobalPosition) > HarvestRange) continue;

                FarmStorage.Instance.Add("egg", item.Amount);
                item.QueueFree();
            }
        }
    }
}
