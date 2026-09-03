using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nong dan (5 vai tro: Farmer/Farmhand/Stable Master/Shepherd/Gardener) - QUY HOACH LAI
    // sang Utility AI + GOAP (thay THE HOAN TOAN lich 7 giai doan gio co dinh cu: Sleep/
    // MorningRoutine/WorkMorning/Lunch/WorkAfternoon/FeedLivestock/EveningHome). Vai tro van quyet
    // dinh CACH lam viec qua DoesFieldWork (xem MakeTendPlotAction, giong FarmWorkerNpc.cs) va
    // FeedPos (khu vuc phu trach cho RestockFeed/Wander) - Main.cs KHONG can doi cach gan cac
    // field nay.
    public partial class ScheduledFarmNpc : NPC
    {
        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 190f;
        [Export] public float Friction = 230f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public float WorkWanderRadius = 130f;
        [Export] public double WorkPauseSec = 1.2;
        [Export] public int FeedRestockThreshold = 10;
        [Export] public int FeedRestockQty = 20;

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;       // vi tri lam viec chinh
        public Vector3 FeedPos;       // vi tri cho gia suc an - mang thuc an gan nhat
        public bool DoesFieldWork = false; // true = Farmer/Farmhand, cham soc o dat that su

        private Vector3 _facing = Vector3.Back;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();

            if (DoesFieldWork) _brain.Actions.Add(MakeTendPlotAction());
            _brain.Actions.Add(MakeRestockFeedAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => WorkPos, WorkWanderRadius));
        }

        // Dung chung khoa voi FarmWorkerNpc.cs (xem NpcExperience.cs) - ca 2 loai NPC lam dong
        // ruong gop du lieu "kinh nghiem" chung, hoc nhanh/on dinh hon.
        private const string ExperienceRole = "field_work";

        private UtilityAction MakeTendPlotAction()
        {
            return new UtilityAction
            {
                Id = "TendPlot",
                Evaluate = ctx =>
                {
                    float distWeight = NpcExperience.DistanceWeight(ExperienceRole);
                    FarmPlot best = null;
                    float bestScore = float.NegativeInfinity;
                    float bestUrgency = 0f;
                    foreach (var node in GetTree().GetNodesInGroup("farm_plots"))
                    {
                        if (node is not FarmPlot plot || !IsInstanceValid(plot)) continue;
                        if (plot.IsEmpty) continue;
                        float u = plot.Urgency01;
                        if (u <= 0f) continue;
                        if (NpcTaskBoard.IsClaimedByOther(plot, this)) continue;
                        float dist = ctx.SelfPos.DistanceTo(plot.GlobalPosition);
                        float score = u * 100f - dist * distWeight;
                        if (score > bestScore) { bestScore = score; bestUrgency = u; best = plot; }
                    }
                    if (best == null) return new UtilityResult(float.NegativeInfinity);
                    return new UtilityResult(bestUrgency * 100f, best);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "tended", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "tended", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "UseOnPlot", Effects = { { "tended", true } }, DurationSec = (float)WorkPauseSec,
                        TargetPos = (ctx, t) => (t as FarmPlot)?.GlobalPosition ?? ctx.SelfPos,
                        Execute = (ctx, t) =>
                        {
                            (t as FarmPlot)?.UseOn();
                            NpcExperience.ReportOutcome(ExperienceRole, AverageFarmPlotUrgency());
                        },
                    },
                },
            };
        }

        // Do khan cap TRUNG BINH con lai cua ca nhom o dat dang trong - dung lam "phan hoi" cho
        // NpcExperience sau moi lan hoan thanh 1 buoc viec (xem NpcExperience.ReportOutcome).
        private float AverageFarmPlotUrgency()
        {
            float sum = 0f; int count = 0;
            foreach (var node in GetTree().GetNodesInGroup("farm_plots"))
            {
                if (node is not FarmPlot plot || !IsInstanceValid(plot) || plot.IsEmpty) continue;
                sum += plot.Urgency01;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        private UtilityAction MakeRestockFeedAction()
        {
            return new UtilityAction
            {
                Id = "RestockFeed",
                Evaluate = ctx =>
                {
                    if (!FarmStorage.Instance.IsLow("thucan_giasuc", FeedRestockThreshold))
                        return new UtilityResult(float.NegativeInfinity);
                    int hungry = AnimalCareUtil.CountHungryNear(GetTree(), FeedPos, 120f);
                    if (hungry == 0) return new UtilityResult(float.NegativeInfinity);
                    return new UtilityResult(45f + hungry * 15f);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "stocked", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "stocked", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "BuyFeed", Effects = { { "stocked", true } },
                        TargetPos = (ctx, t) => NpcEconomy.RestockPos,
                        Execute = (ctx, t) => NpcEconomy.NpcBuy("thucan_giasuc", FeedRestockQty),
                    },
                },
            };
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _brain.Tick(dt, this, ArriveDist, Speed, null, null);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _brain.IsSleeping && !wantsToMove, _facing, FlipModelFacing, TurnSpeed * dt);

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "ScheduledFarmNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
