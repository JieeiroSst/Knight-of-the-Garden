using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // NPC "nguoi lam vuon/trang trai phu" - ke thua he thong hoi thoai/gio hanh chinh that
    // (6h-18h) giong FarmhandNpc.cs/StablehandNpc.cs, nhung quan ly CA MOT KHU VUC RONG (cuu,
    // heo, vuon cay an qua, vuon nho, to ong) thay vi 1 loai vat nuoi duy nhat - trong luc lam
    // viec, di dao khap khu vuc va dinh ky "san xuat" 1 trong nhieu loai san pham (len/mat ong/
    // sap ong/ruou/tao), mo phong dung nghia "1 nguoi phu trach ca goc trang trai phu".
    public partial class EstateWorkerNpc : NPC
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
        [Export] public float WorkWanderRadius = 260f;
        [Export] public double ProduceIntervalSec = 100.0;

        // San pham xoay vong (Main.cs gan) - moi lan den luot se tha 1 vat pham tuong ung.
        public string[] Products = { "wool" };

        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;

        private WorkState _workState = WorkState.AtHome;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private double _produceCooldown = 0;
        private int _productIndex = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

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
                _ => (Vector3.Zero, 0f),
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _workState == WorkState.AtHome, _facing, FlipModelFacing, TurnSpeed * dt);

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

        private (Vector3 dir, float speed) DoWorkWander(float dt)
        {
            _produceCooldown -= dt;
            if (_produceCooldown <= 0 && Products.Length > 0)
            {
                DroppedItem.Spawn(GetTree().CurrentScene, GlobalPosition, Products[_productIndex], 1);
                _productIndex = (_productIndex + 1) % Products.Length;
                _produceCooldown = ProduceIntervalSec;
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
    }
}
