using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // NPC nong dan lam viec theo LICH TRINH NHIEU GIAI DOAN trong ngay (theo dung yeu cau):
    // 6h Thuc day -> 7h An sang -> 8h Lam ruong -> 12h An trua -> 13h Lam viec -> 18h Cho gia
    // suc an -> 20h Ve nha -> 22h Ngu. Khac FarmhandNpc/StablehandNpc/PoultryKeeperNpc (chi co
    // 2 giai doan Working/AtHome), NPC nay co ĐU 6 giai doan rieng biet, dong bo GIO THAT qua
    // GameManager.HourChanged. Vai tro (Farmer/Farmhand/Stable Master/Shepherd/Gardener) quyet
    // dinh VI TRI lam viec (WorkPos) va CACH lam viec: neu DoesFieldWork=true, NPC di tuan tu
    // qua tung o dat va goi FarmPlot.UseOn() (giong FarmWorkerNpc.cs - trong/tuoi/thu hoach that
    // su); neu khong, NPC chi quanh quan (wander) gan khu vuc phu trach (chuong ngua/cuu/vuon).
    public partial class ScheduledFarmNpc : NPC
    {
        private enum DayPhase { Sleep, MorningRoutine, WorkMorning, Lunch, WorkAfternoon, FeedLivestock, EveningHome }

        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 190f;
        [Export] public float Friction = 230f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float WorkWanderRadius = 130f;
        [Export] public float HomeWanderRadius = 50f;

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;       // vi tri lam viec chinh (8h-12h, 13h-18h)
        public Vector3 FeedPos;       // vi tri cho gia suc an (18h-20h) - mang thuc an gan nhat
        public bool DoesFieldWork = false; // true = Farmer/Farmhand, di tuan tu tung o dat that su

        private DayPhase _phase = DayPhase.Sleep;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private List<FarmPlot> _plots = new();
        private int _plotIndex = 0;
        private bool _atPlot = false;
        private double _plotPauseLeft = 0;

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            _phase = PhaseForHour(hour);
            GlobalPosition = _phase == DayPhase.Sleep ? InteriorHomePos + Vector3.Up * 8f : HomePos;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private static DayPhase PhaseForHour(int hour)
        {
            if (hour >= 22 || hour < 6) return DayPhase.Sleep;
            if (hour < 8) return DayPhase.MorningRoutine;
            if (hour < 12) return DayPhase.WorkMorning;
            if (hour < 13) return DayPhase.Lunch;
            if (hour < 18) return DayPhase.WorkAfternoon;
            if (hour < 20) return DayPhase.FeedLivestock;
            return DayPhase.EveningHome;
        }

        private void OnHourChanged(int hour)
        {
            var newPhase = PhaseForHour(hour);
            if (newPhase == _phase) return;

            // Buoc ra truoc cua nha (khong "day" xuyen tuong) khi vua thuc day.
            if (_phase == DayPhase.Sleep && newPhase == DayPhase.MorningRoutine)
                GlobalPosition = HomePos;
            // Vao han trong nha ngay khi den gio ngu.
            if (newPhase == DayPhase.Sleep)
                GlobalPosition = InteriorHomePos + Vector3.Up * 8f;

            _phase = newPhase;
            if (newPhase == DayPhase.WorkMorning || newPhase == DayPhase.WorkAfternoon)
                RefreshPlots();
        }

        private void RefreshPlots()
        {
            if (!DoesFieldWork) return;
            _plots = GetTree().GetNodesInGroup("farm_plots")
                .OfType<FarmPlot>()
                .OrderBy(p => p.GridY)
                .ThenBy(p => p.GridX)
                .ToList();
            _plotIndex = 0;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _phase switch
            {
                DayPhase.MorningRoutine => DoWanderNear(HomePos, HomeWanderRadius, dt),
                DayPhase.WorkMorning => DoWork(dt),
                DayPhase.Lunch => (Vector3.Zero, 0f), // nghi an trua tai cho
                DayPhase.WorkAfternoon => DoWork(dt),
                DayPhase.FeedLivestock => DoWanderNear(FeedPos, 60f, dt),
                DayPhase.EveningHome => DoWanderNear(HomePos, HomeWanderRadius, dt),
                _ => (Vector3.Zero, 0f), // Sleep
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
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

        // Lam viec chinh: Farmer/Farmhand (DoesFieldWork) di tuan tu tung o dat that su (giong
        // FarmWorkerNpc.cs); cac vai tro khac (Stable Master/Shepherd/Gardener) chi quanh quan
        // gan khu vuc minh phu trach (WorkPos).
        private (Vector3 dir, float speed) DoWork(float dt)
        {
            if (!DoesFieldWork) return DoWanderNear(WorkPos, WorkWanderRadius, dt);

            if (_plots.Count == 0)
            {
                RefreshPlots();
                if (_plots.Count == 0) return (Vector3.Zero, 0f);
            }
            if (_atPlot)
            {
                _plotPauseLeft -= dt;
                if (_plotPauseLeft <= 0)
                {
                    _atPlot = false;
                    _plotIndex = (_plotIndex + 1) % _plots.Count;
                }
                return (Vector3.Zero, 0f);
            }
            var plot = _plots[_plotIndex];
            if (!IsInstanceValid(plot)) { RefreshPlots(); return (Vector3.Zero, 0f); }

            Vector3 dir = plot.GlobalPosition - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 16f)
            {
                plot.UseOn();
                _atPlot = true;
                _plotPauseLeft = 1.2;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), Speed);
        }

        private (Vector3 dir, float speed) DoWanderNear(Vector3 center, float radius, float dt)
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float r = rng.RandfRange(0f, radius);
                _wanderTarget = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                _nextWanderTime = now + (ulong)rng.RandiRange(4000, 9000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.55f);
        }
    }
}
