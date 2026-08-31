using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Henri - Bao ve trang trai. Ban ngay (troi thuong) di tuan quanh cac diem kiem tra (cong/
    // hang rao). Troi MUA ban ngay -> KHONG tuoi cay/tuan tra nhu thuong, chuyen sang dung gan
    // khu dung cu (the hien "sua cong cu/don kho" - xem GameManager.IsRaining). Ban dem di ĐUNG
    // 1 LO TRINH CO DINH theo thu tu: Cong trang trai -> Nha kho -> Canh dong -> Bia rung ->
    // Nha chinh -> lap lai (theo dung yeu cau).
    public partial class GuardNpc : NPC
    {
        private enum Phase { DayPatrol, RainHelp, NightPatrol }

        [Export] public float Speed = 48f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public double PauseAtPointSec = 6.0;
        [Export] public float RainHelpWanderRadius = 60f;

        // Loi thoai rieng theo tinh huong (ngoai bo DialogueLow/Mid/High mac dinh cua NPC, dung
        // khi troi mua/dang tuan dem) - neu de trong se dung DialogueLow/Mid/High nhu binh
        // thuong. Main.cs gan cac mang nay ngay sau khi tao.
        public string[] DialogueRain = System.Array.Empty<string>();
        public string[] DialogueNight = System.Array.Empty<string>();

        // Main.cs gan ngay sau khi tao (truoc AddChild). KHONG co InteriorHomePos/gio ngu - theo
        // dung yeu cau, Henri tuan tra CA NGAY LAN DEM lien tuc (ban dem theo lo trinh co dinh
        // rieng, khong "nghi" nhu cac NPC khac).
        public Vector3 HomePos;
        public Vector3[] DayCheckpoints = System.Array.Empty<Vector3>();
        public Vector3[] NightPatrolPoints = System.Array.Empty<Vector3>(); // DUNG thu tu: cong -> kho -> dong -> bia rung -> nha chinh
        public Vector3 RainHelpPos;

        private Phase _phase = Phase.DayPatrol;
        private Vector3 _facing = Vector3.Back;
        private int _pointIndex = 0;
        private bool _atPoint = false;
        private double _pauseLeft = 0;
        private Vector3 _rainWanderTarget;
        private ulong _nextRainWanderTime = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready();

            _phase = CurrentPhase();
            GlobalPosition = HomePos;
            _pointIndex = 0;
            _atPoint = false;

            GameManager.Instance.HourChanged += _ => RefreshPhase();
            GameManager.Instance.WeatherChanged += _ => RefreshPhase();
        }

        private Phase CurrentPhase()
        {
            if (GameManager.Instance.IsNight) return Phase.NightPatrol;
            return GameManager.Instance.IsRaining ? Phase.RainHelp : Phase.DayPatrol;
        }

        private void RefreshPhase()
        {
            var newPhase = CurrentPhase();
            if (newPhase == _phase) return;
            _phase = newPhase;
            _pointIndex = 0;
            _atPoint = false;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _phase switch
            {
                Phase.DayPatrol => DoPatrol(DayCheckpoints, dt),
                Phase.NightPatrol => DoPatrol(NightPatrolPoints, dt),
                _ => DoRainWander(dt),
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, false, _facing, FlipModelFacing, TurnSpeed * dt);

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

        private (Vector3 dir, float speed) DoPatrol(Vector3[] points, float dt)
        {
            if (points.Length == 0) return (Vector3.Zero, 0f);

            if (_atPoint)
            {
                _pauseLeft -= dt;
                if (_pauseLeft <= 0)
                {
                    _atPoint = false;
                    _pointIndex = (_pointIndex + 1) % points.Length;
                }
                return (Vector3.Zero, 0f);
            }

            Vector3 target = points[_pointIndex];
            Vector3 dir = target - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= ArriveDist)
            {
                _atPoint = true;
                _pauseLeft = PauseAtPointSec;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), Speed);
        }

        private (Vector3 dir, float speed) DoRainWander(float dt)
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextRainWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, RainHelpWanderRadius);
                _rainWanderTarget = RainHelpPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextRainWanderTime = now + (ulong)rng.RandiRange(6000, 14000);
            }
            Vector3 dir = _rainWanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.5f);
        }

        protected override string PickDialogue()
        {
            string[] pool = _phase switch
            {
                Phase.RainHelp when DialogueRain.Length > 0 => DialogueRain,
                Phase.NightPatrol when DialogueNight.Length > 0 => DialogueNight,
                _ => null,
            };
            if (pool != null) return pool[(int)(GD.Randi() % (uint)pool.Length)];
            return base.PickDialogue();
        }
    }
}
