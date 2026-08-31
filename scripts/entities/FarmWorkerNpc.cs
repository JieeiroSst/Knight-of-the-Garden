using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // NPC "nguoi lam ruong thue": ke thua he thong hoi thoai NPC, di lam theo GIO HANH CHINH
    // THAT (6h-18h, dong bo GameManager.HourChanged) - trong gio lam, di TUAN TU qua TUNG O DAT
    // trong ruong (nhom "farm_plots" - xem FarmPlot.cs) va goi UseOn() tren moi o. Ham UseOn()
    // DA CO SAN tu dong lam DUNG viec theo trang thai hien tai cua o (trong hat neu dat trong,
    // tuoi nuoc neu da trong nhung chua tuoi, thu hoach neu da chin) - HET giong het thao tac
    // cua nguoi choi khi dung cong cu, nen NPC nay THAT SU "lam viec nhu 1 nong dan" (cay/gieo
    // hat/tuoi nuoc/thu hoach ca canh dong lien tuc), khong phai chi dung yen mot cho. Nong san
    // thu hoach duoc TU DONG cho vao tui do nguoi choi (dung y nghia "nguoi lam thue giao nong
    // san cho chu trai"). Het gio lam thi ve nha ngu (giong FarmhandNpc.cs/StablehandNpc.cs).
    public partial class FarmWorkerNpc : NPC
    {
        private enum WorkState { AtHome, GoingToWork, Working, GoingHome }

        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 190f;
        [Export] public float Friction = 230f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 18;
        [Export] public double WorkPauseSec = 1.2; // dung "lam viec" tai moi o bao lau

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos; // vi tri dung dau tien khi bat dau vao ruong lam viec

        private WorkState _workState = WorkState.AtHome;
        private Vector3 _facing = Vector3.Back;
        private List<FarmPlot> _plots = new();
        private int _plotIndex = 0;
        private double _workPauseLeft = 0;
        private bool _atPlot = false;

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            bool onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            _workState = onDuty ? WorkState.Working : WorkState.AtHome;
            GlobalPosition = onDuty ? WorkPos : InteriorHomePos + Vector3.Up * 8f;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void RefreshPlots()
        {
            _plots = GetTree().GetNodesInGroup("farm_plots")
                .OfType<FarmPlot>()
                .OrderBy(p => p.GridY)
                .ThenBy(p => p.GridX)
                .ToList();
            _plotIndex = 0;
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
                WorkState.Working => DoFieldWork(dt),
                _ => (Vector3.Zero, 0f), // AtHome: dang ngu, dung yen
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
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
                else if (arrivedState == WorkState.Working)
                    RefreshPlots();
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), speed);
        }

        // Di tuan tu qua tung o dat, "lam viec" (goi UseOn - tu dong trong/tuoi/thu hoach dung
        // theo trang thai hien tai cua o) roi chuyen sang o tiep theo, lap vong lai tu dau khi
        // het ruong - giong 1 nguoi tho that su cham het ca canh dong lien tuc suot ngay.
        private (Vector3 dir, float speed) DoFieldWork(float dt)
        {
            if (_plots.Count == 0)
            {
                RefreshPlots();
                if (_plots.Count == 0) return (Vector3.Zero, 0f);
            }

            if (_atPlot)
            {
                _workPauseLeft -= dt;
                if (_workPauseLeft <= 0)
                {
                    _atPlot = false;
                    _plotIndex = (_plotIndex + 1) % _plots.Count;
                }
                return (Vector3.Zero, 0f);
            }

            var plot = _plots[_plotIndex];
            if (!IsInstanceValid(plot))
            {
                RefreshPlots();
                return (Vector3.Zero, 0f);
            }

            Vector3 dir = plot.GlobalPosition - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 16f)
            {
                plot.UseOn();
                _atPlot = true;
                _workPauseLeft = WorkPauseSec;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), Speed);
        }
    }
}
