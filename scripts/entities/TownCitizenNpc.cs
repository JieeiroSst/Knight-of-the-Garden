using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // NPC "nguoi dan thi tran": ke thua he thong hoi thoai/tin tuong cua NPC (Interact/Trust),
    // them AI SINH HOAT NHU CON NGUOI - ban ngay (6h-22h that, dong bo GameManager.HourChanged)
    // tu do di lai (wander) khap thi tran thay vi dung yen mot cho, toi den thi ve nha rieng (1
    // trong so cac can nha cua khu do thi - xem Main.BuildCityDistrict) va vao HAN BEN TRONG de
    // ngu (giong FarmhandNpc.cs/StablehandNpc.cs/PoultryKeeperNpc.cs).
    public partial class TownCitizenNpc : NPC
    {
        private enum DayState { Wandering, GoingHome, AtHome }

        [Export] public float Speed = 45f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int ActiveStartHour = 6;
        [Export] public int ActiveEndHour = 22;
        [Export] public float WanderRadius = 900f;

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 WanderCenter;    // tam vung di lai ban ngay (thuong la VillageAnchor, hoac
                                         // gan Tru Canh Sat voi NPC "cong an")
        public Vector3 HomePos;         // ngay truoc cua nha rieng (ngoai troi)
        public Vector3 InteriorHomePos; // phong noi that that su - noi ngu ban dem

        private DayState _state = DayState.Wandering;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            bool active = hour >= ActiveStartHour && hour < ActiveEndHour;
            _state = active ? DayState.Wandering : DayState.AtHome;
            GlobalPosition = active ? WanderCenter : InteriorHomePos + Vector3.Up * 8f;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            if (hour == ActiveEndHour && _state == DayState.Wandering)
            {
                GlobalPosition = HomePos; // xuat phat tu truoc cua nha, khong "day" tu giua duong
                _state = DayState.GoingHome;
            }
            else if (hour == ActiveStartHour && _state == DayState.AtHome)
            {
                GlobalPosition = HomePos;
                _state = DayState.Wandering;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _state switch
            {
                DayState.GoingHome => GoTo(HomePos, Speed, DayState.AtHome),
                DayState.Wandering => DoWander(),
                _ => (Vector3.Zero, 0f), // AtHome: dang ngu, dung yen
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _state == DayState.AtHome, _facing, FlipModelFacing, TurnSpeed * dt);

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "TownCitizenNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }

        private (Vector3 dir, float speed) GoTo(Vector3 target, float speed, DayState arrivedState)
        {
            Vector3 dir = target - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 14f)
            {
                _state = arrivedState;
                if (arrivedState == DayState.AtHome)
                    GlobalPosition = InteriorHomePos + Vector3.Up * 8f;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), speed);
        }

        private (Vector3 dir, float speed) DoWander()
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, WanderRadius);
                _wanderTarget = WanderCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // Dung lai "tan gau"/ngam pho lau hon di chuyen - giong nguoi that di dao pho.
                _nextWanderTime = now + (ulong)rng.RandiRange(6000, 14000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 12f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed);
        }
    }
}
