using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // De 3D (Poly by Google, CC-BY 3.0 - xem CREDITS.md o goc du an) tu do di lai trong pham vi
    // chuong. KHONG co animation rieng (model tinh, khong xuong/rig - da tim rat ky tren
    // poly.pizza nhung khong co ban CC0 hay ban co rig dong nao cho De, xem CREDITS.md) nen than
    // De se TRUOT tren mat dat thay vi buoc chan that su - han che that su cua asset hien co,
    // khong phai loi code. Cau truc Wander/GoToTrough/Eating + tang truong/sinh san sao chep
    // NGUYEN Y tu Sheep.cs de nhat quan voi cac loai vat nuoi khac.
    public partial class Goat : CharacterBody3D, IHungryAnimal
    {
        private enum State { Wander, GoToTrough, Eating }

        [Export] public float Speed = 38f;
        [Export] public float Acceleration = 130f;
        [Export] public float Friction = 160f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = false;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 14.5f;
        [Export] public double EatDurationSec = 90.0;

        // Sinh san & lon len (mau Cow.cs): de con sinh ra tu 2 de lon (xem Main.TryBreedGoats),
        // bat dau nho va CAN AN MOI NGAY (den mang gio 12h/16h) de lon dan qua tung NGAY THAT.
        [Export] public bool IsAdult = true;
        [Export] public float BirthScaleFactor = 0.45f;
        [Export] public int GrowthDaysNeeded = 4;
        private int _daysFed = 0;
        private bool _ateToday = false;
        private CollisionShape3D _collision;

        // Xem ghi chu chi tiet trong Cow.cs - cung 1 co che doi THAT.
        public int HungerDays { get; private set; } = 0;
        public bool IsHungry => HungerDays > 0;

        private Node3D _model;
        private Node3D _body; // model tinh (khong AnimationPlayer) - rieng de con truc tiep chinh Scale luc lon

        private State _state = State.Wander;
        private Vector3 _homeCenter;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private Vector3 _facing = Vector3.Back;
        private float _speedJitter = 1f;

        public Vector3 TroughPosition;
        public Vector3 HomeCenter = new(float.NaN, 0, float.NaN);
        public float PastureHalfExtent = 999999f;

        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private NavigationAgent3D _navAgent;
        private readonly SteeringUtil.NavSteering _nav = new();

        public override void _Ready()
        {
            AddToGroup("goats");
            _model = GetNodeOrNull<Node3D>("Model");
            _collision = GetNodeOrNull<CollisionShape3D>("Collision");

            var modelScene = GD.Load<PackedScene>("res://assets3d/polypizza/goat/goat.glb");
            if (modelScene != null && _model != null)
            {
                _body = modelScene.Instantiate<Node3D>();
                _body.Name = "Body";
                _body.Scale = Vector3.One * ModelScale;
                // But pivot goc cua model (chan o Y am so voi tam) - day chan len ngang mat dat
                // cuc bo (Y=0 cua "Model") thay vi lo lung/chim xuong dat.
                _body.Position = Vector3.Up * (1.272f * ModelScale);
                _model.AddChild(_body);
            }

            _homeCenter = float.IsNaN(HomeCenter.X) ? GlobalPosition : HomeCenter;
            _wanderTarget = GlobalPosition;
            ApplyGrowthVisual();

            _navAgent = new NavigationAgent3D { PathDesiredDistance = 8f, TargetDesiredDistance = 10f, AvoidanceEnabled = false };
            AddChild(_navAgent);

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.15f);

            GameManager.Instance.HourChanged += OnHourChanged;
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void ApplyGrowthVisual()
        {
            float t = IsAdult ? 1f : Mathf.Clamp((float)_daysFed / GrowthDaysNeeded, 0f, 1f);
            float scale = Mathf.Lerp(BirthScaleFactor, 1f, t);
            if (_model != null) _model.Scale = Vector3.One * scale;
            if (_collision != null) _collision.Scale = Vector3.One * scale;
        }

        private void OnDayChanged(int day)
        {
            if (IsAdult) return;
            if (_ateToday) _daysFed++;
            _ateToday = false;
            ApplyGrowthVisual();
            if (_daysFed >= GrowthDaysNeeded) IsAdult = true;
        }

        private void OnHourChanged(int hour)
        {
            if ((hour == 12 || hour == 16) && _state != State.Eating)
                _state = State.GoToTrough;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _state switch
            {
                State.Wander => DoWander(),
                State.GoToTrough => DoGoToTrough(),
                _ => (TroughDirOrZero(), 0f),
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
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

            float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "Goat:" + Name);
            // (Khong PlayLoop - model tinh, khong co AnimationPlayer de choi hoat canh.)
        }

        private Vector3 TroughDirOrZero()
        {
            Vector3 dir = TroughPosition - GlobalPosition;
            dir.Y = 0f;
            return dir.Length() > 4f ? dir.Normalized() : Vector3.Zero;
        }

        private (Vector3 dir, float speed) DoWander()
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float half = PastureHalfExtent;
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, half);
                _wanderTarget = _homeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(5000, 12000);
            }
            var navDir = _nav.GetDirection(_navAgent, GlobalPosition, _wanderTarget);
            if (navDir == Vector3.Zero) return (Vector3.Zero, 0f);
            return (navDir, Speed * 0.45f * _speedJitter);
        }

        private (Vector3 dir, float speed) DoGoToTrough()
        {
            Vector3 straightDir = TroughPosition - GlobalPosition;
            straightDir.Y = 0f;
            if (straightDir.Length() <= 16f)
            {
                _state = State.Eating;
                if (FarmStorage.Instance.TryRemove("thucan_giasuc", 1)) { _ateToday = true; HungerDays = 0; }
                else HungerDays++;
                GetTree().CreateTimer(EatDurationSec).Timeout += () =>
                {
                    if (IsInstanceValid(this)) _state = State.Wander;
                };
                return (Vector3.Zero, 0f);
            }
            var navDir = _nav.GetDirection(_navAgent, GlobalPosition, TroughPosition);
            return (navDir != Vector3.Zero ? navDir : straightDir.Normalized(), Speed * _speedJitter);
        }
    }
}
