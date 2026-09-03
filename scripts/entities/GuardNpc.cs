using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Henri - Bao ve trang trai. QUY HOACH LAI sang Utility AI (xem FarmhandNpc.cs/RepairmanNpc.cs
    // de biet chi tiet kien truc chung) - KHONG dung GOAP nhieu buoc (moi hanh dong chi la "di
    // toi 1 diem", khong can lap ke hoach chuoi) nhung VAN la Utility AI THAT: 3 hanh dong tuan
    // tra ngay/tru mua/tuan tra dem tu cham diem theo GameManager.IsNight/IsRaining thay vi
    // enum Phase gan cung truoc day. KHONG co hanh dong Sleep (giu dung tinh chat "khong bao gio
    // ngu" cua Henri).
    public partial class GuardNpc : NPC
    {
        [Export] public float Speed = 48f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public double PauseAtPointSec = 6.0;
        [Export] public float RainHelpWanderRadius = 60f;

        // Loi thoai rieng theo tinh huong. Main.cs gan cac mang nay ngay sau khi tao.
        public string[] DialogueRain = System.Array.Empty<string>();
        public string[] DialogueNight = System.Array.Empty<string>();
        // Ban dich tieng Anh (tuy chon) cho 2 mang tren - cung co che fallback nhu DialogueLowEn/...
        // trong NPC.cs (xem PickPool).
        public string[] DialogueRainEn = System.Array.Empty<string>();
        public string[] DialogueNightEn = System.Array.Empty<string>();

        // Main.cs gan ngay sau khi tao (truoc AddChild). KHONG co InteriorHomePos/gio ngu.
        public Vector3 HomePos;
        public Vector3[] DayCheckpoints = System.Array.Empty<Vector3>();
        public Vector3[] NightPatrolPoints = System.Array.Empty<Vector3>(); // DUNG thu tu: cong -> kho -> dong -> bia rung -> nha chinh
        public Vector3 RainHelpPos;

        private Vector3 _facing = Vector3.Back;
        private int _dayIdx = 0;
        private int _nightIdx = 0;

        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private NavigationAgent3D _navAgent;
        private readonly SteeringUtil.NavSteering _nav = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();

            _navAgent = new NavigationAgent3D { PathDesiredDistance = 8f, TargetDesiredDistance = 10f, AvoidanceEnabled = false };
            AddChild(_navAgent);
            GlobalPosition = HomePos;

            _brain.Actions.Add(MakeNightPatrol());
            _brain.Actions.Add(MakeRainHelp());
            _brain.Actions.Add(MakeDayPatrol());
        }

        private UtilityAction MakeDayPatrol()
        {
            return new UtilityAction
            {
                Id = "DayPatrol",
                Evaluate = ctx => (ctx.IsNight || ctx.IsRaining) ? new UtilityResult(float.NegativeInfinity) : new UtilityResult(20f),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Checkpoint", Effects = { { "there", true } }, DurationSec = (float)PauseAtPointSec,
                        TargetPos = (ctx, t) => DayCheckpoints.Length > 0 ? DayCheckpoints[_dayIdx % DayCheckpoints.Length] : ctx.SelfPos,
                        Execute = (ctx, t) => { if (DayCheckpoints.Length > 0) _dayIdx = (_dayIdx + 1) % DayCheckpoints.Length; },
                    },
                },
            };
        }

        private UtilityAction MakeNightPatrol()
        {
            return new UtilityAction
            {
                Id = "NightPatrol",
                Evaluate = ctx => ctx.IsNight ? new UtilityResult(60f) : new UtilityResult(float.NegativeInfinity),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Checkpoint", Effects = { { "there", true } }, DurationSec = (float)PauseAtPointSec,
                        TargetPos = (ctx, t) => NightPatrolPoints.Length > 0 ? NightPatrolPoints[_nightIdx % NightPatrolPoints.Length] : ctx.SelfPos,
                        Execute = (ctx, t) => { if (NightPatrolPoints.Length > 0) _nightIdx = (_nightIdx + 1) % NightPatrolPoints.Length; },
                    },
                },
            };
        }

        private UtilityAction MakeRainHelp()
        {
            return new UtilityAction
            {
                Id = "RainHelp",
                Evaluate = ctx => (!ctx.IsNight && ctx.IsRaining) ? new UtilityResult(55f) : new UtilityResult(float.NegativeInfinity),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Shelter", Effects = { { "there", true } }, DurationSec = 5f,
                        TargetPos = (ctx, t) =>
                        {
                            var rng = new RandomNumberGenerator();
                            rng.Randomize();
                            float angle = rng.RandfRange(0f, Mathf.Tau);
                            float r = rng.RandfRange(0f, RainHelpWanderRadius);
                            return RainHelpPos + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                        },
                    },
                },
            };
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _brain.Tick(dt, this, ArriveDist, Speed, _nav, _navAgent);

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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }

        protected override string PickDialogue()
        {
            string[] pool = _brain.CurrentActionId switch
            {
                "RainHelp" when DialogueRain.Length > 0 => PickPool(DialogueRain, DialogueRainEn),
                "NightPatrol" when DialogueNight.Length > 0 => PickPool(DialogueNight, DialogueNightEn),
                _ => null,
            };
            if (pool != null) return pool[(int)(GD.Randi() % (uint)pool.Length)];
            return base.PickDialogue();
        }
    }
}
