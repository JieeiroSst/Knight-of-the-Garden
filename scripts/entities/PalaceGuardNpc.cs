using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cam Ve Quan - doi bao ve nong trai (100 NPC, xem Main.BuildPalaceGuardBarracks). Moi NPC
    // di lang thang KHAP pham vi tuong da (PatrolCenter/PatrolRadius) trong ca lam cua minh, ve
    // doanh trai ngu het ca con lai. Dung logic DON GIAN (wander trong 1 vong tron lon, khong co
    // lo trinh co dinh tung diem nhu Henri/GuardNpc.cs) vi so luong qua lon (100 NPC) - lo trinh
    // ca nhan chi tiet cho tung nguoi se ton chi phi CPU khong can thiet, trong khi wander ngau
    // nhien tren dien rong van the hien dung "di khap trang trai de bao ve" nhu yeu cau.
    public partial class PalaceGuardNpc : NPC
    {
        private enum Phase { Patrol, Sleep }

        [Export] public float Speed = 46f;
        [Export] public float Acceleration = 170f;
        [Export] public float Friction = 210f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        // Gio bat dau/ket thuc ca lam - HO TRO ca lam VUOT QUA NUA DEM (vd 18 -> 6) qua
        // IsOnDutyHour ben duoi, dung cho 2 ca doi lap nhau (6h sang-6h toi / 6h toi-6h sang).
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 18;
        [Export] public float PatrolRadius = 1400f;

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 PatrolCenter;

        private Phase _phase = Phase.Sleep;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            _phase = IsOnDutyHour(hour) ? Phase.Patrol : Phase.Sleep;
            GlobalPosition = _phase == Phase.Patrol ? HomePos : InteriorHomePos + Vector3.Up * 8f;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private bool IsOnDutyHour(int hour) => WorkStartHour < WorkEndHour
            ? (hour >= WorkStartHour && hour < WorkEndHour)
            : (hour >= WorkStartHour || hour < WorkEndHour);

        private void OnHourChanged(int hour)
        {
            bool onDuty = IsOnDutyHour(hour);
            var newPhase = onDuty ? Phase.Patrol : Phase.Sleep;
            if (newPhase == _phase) return;
            _phase = newPhase;
            GlobalPosition = onDuty ? HomePos : InteriorHomePos + Vector3.Up * 8f;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            var (desiredDir, targetSpeed) = _phase == Phase.Patrol ? DoPatrol(dt) : (Vector3.Zero, 0f);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _phase == Phase.Sleep, _facing, FlipModelFacing, TurnSpeed * dt);

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

        private (Vector3 dir, float speed) DoPatrol(float dt)
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, PatrolRadius);
                _wanderTarget = PatrolCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(6000, 14000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 14f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.65f);
        }
    }
}
