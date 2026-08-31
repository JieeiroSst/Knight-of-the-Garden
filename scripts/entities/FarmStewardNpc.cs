using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Jean - Quan gia trang trai (Farm Steward), 55 tuoi. Di tuan tu qua cac diem moc chinh cua
    // trang trai (nha chinh/nha kho/chuong bo/canh dong...) ban ngay, ve nha ngu ban dem (giong
    // mau FarmhandNpc). Tinh cach diem tinh/ky luat/thuc te/khong thich lang phi/thinh thoang
    // phan nan ve nguoi choi - the hien qua PickDialogue (tron 1 nhom cau "phan nan" xen ke voi
    // cau binh thuong, KHONG phai lien tuc phan nan).
    //
    // QUAN TRONG - pham vi that su: Jean "dieu phoi trang trai" chi la BOI CANH CAU CHUYEN (loi
    // thoai phan anh dung vai tro), KHONG phai 1 bo may AI trung tam THAT SU dieu khien cac NPC
    // khac - moi NPC khac (Marcel/Antoine/Henri/nguoi cham nuoi...) van tu chay logic rieng cua
    // minh nhu truoc, khong nhan "lenh" tu Jean. Xay dung 1 he thong dieu phoi AI trung tam that
    // su se can viet lai kien truc cua TAT CA NPC hien co - ngoai pham vi hop ly cua 1 yeu cau bo
    // sung NPC.
    public partial class FarmStewardNpc : NPC
    {
        private enum DayPhase { Sleep, Patrol }

        [Export] public float Speed = 45f;
        [Export] public float Acceleration = 170f;
        [Export] public float Friction = 210f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WakeHour = 6;
        [Export] public int SleepHour = 22;
        [Export] public float ArriveDist = 16f;
        [Export] public double PauseAtPointSec = 8.0;

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3[] PatrolPoints = System.Array.Empty<Vector3>();

        private DayPhase _phase = DayPhase.Sleep;
        private Vector3 _facing = Vector3.Back;
        private int _pointIndex = 0;
        private bool _atPoint = false;
        private double _pauseLeft = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            _phase = IsAwakeHour(hour) ? DayPhase.Patrol : DayPhase.Sleep;
            GlobalPosition = _phase == DayPhase.Patrol ? HomePos : InteriorHomePos + Vector3.Up * 8f;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private bool IsAwakeHour(int hour) => hour >= WakeHour && hour < SleepHour;

        private void OnHourChanged(int hour)
        {
            bool awake = IsAwakeHour(hour);
            var newPhase = awake ? DayPhase.Patrol : DayPhase.Sleep;
            if (newPhase == _phase) return;

            if (_phase == DayPhase.Sleep && newPhase == DayPhase.Patrol)
                GlobalPosition = HomePos;
            if (newPhase == DayPhase.Sleep)
                GlobalPosition = InteriorHomePos + Vector3.Up * 8f;

            _phase = newPhase;
            _atPoint = false;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _phase == DayPhase.Patrol ? DoPatrol(dt) : (Vector3.Zero, 0f);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _phase == DayPhase.Sleep, _facing, FlipModelFacing, TurnSpeed * dt);

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
            if (PatrolPoints.Length == 0) return (Vector3.Zero, 0f);

            if (_atPoint)
            {
                _pauseLeft -= dt;
                if (_pauseLeft <= 0)
                {
                    _atPoint = false;
                    _pointIndex = (_pointIndex + 1) % PatrolPoints.Length;
                }
                return (Vector3.Zero, 0f);
            }

            Vector3 target = PatrolPoints[_pointIndex];
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
    }
}
