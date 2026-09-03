using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nguoi cham bo/cuu/heo/de (tuy Main.cs spawn con gi vao chuong minh phu trach) - QUY
    // HOACH LAI sang Utility AI + GOAP (thay THE HOAN TOAN lich gio co dinh cu WorkStartHour/
    // WorkEndHour): moi lan quyet dinh, cham diem "kho thuc an gia suc sap het" so voi "toi thu
    // hoach san pham (sua/len...)" so voi "met/buon ngu" - hanh dong nao diem cao hon thi lam.
    // Khi kho thuc an sap het, GOAP tu lap ke hoach di nhap hang (xem NpcEconomy.cs) truoc khi
    // quay lai chuong - dung y vi du nguoi dung neu ("kho gan het -> NPC tu di mua -> cho an").
    public partial class FarmhandNpc : NPC
    {
        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 14f;
        [Export] public float WorkWanderRadius = 90f;
        [Export] public string ProduceItemId = "milk";
        [Export] public int FeedRestockThreshold = 10;
        [Export] public int FeedRestockQty = 20;

        // Main.cs gan cac vi tri nay ngay sau khi tao NPC (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;
        public Vector3 TroughPos;

        private Vector3 _facing = Vector3.Back;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private NavigationAgent3D _navAgent;
        private readonly SteeringUtil.NavSteering _nav = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();

            _navAgent = new NavigationAgent3D { PathDesiredDistance = 8f, TargetDesiredDistance = 10f, AvoidanceEnabled = false };
            AddChild(_navAgent);

            _brain.Actions.Add(MakeRestockFeedAction());
            _brain.Actions.Add(MakeCollectProduceAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => WorkPos, WorkWanderRadius));
        }

        // Diem CAO khi kho thuc an gia suc that su sap het VA co con vat dang doi gan chuong minh
        // - GOAP: neu da du hang thi khong can lam gi (0 buoc); neu thieu, di den diem nhap hang
        // (xem NpcEconomy.RestockPos) roi mua ve.
        private UtilityAction MakeRestockFeedAction()
        {
            return new UtilityAction
            {
                Id = "RestockFeed",
                Evaluate = ctx =>
                {
                    if (!FarmStorage.Instance.IsLow("thucan_giasuc", FeedRestockThreshold))
                        return new UtilityResult(float.NegativeInfinity);
                    int hungry = AnimalCareUtil.CountHungryNear(GetTree(), TroughPos, WorkWanderRadius + 60f);
                    return new UtilityResult(50f + hungry * 15f);
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

        // Hanh dong THUONG NGAY (khong khan cap) - dinh ky ghe mang tha 1 mon san pham (dung y
        // "cham soc, thu san pham" cu, xem ProduceItemId).
        private UtilityAction MakeCollectProduceAction()
        {
            return new UtilityAction
            {
                Id = "CollectProduce",
                Evaluate = ctx => new UtilityResult(14f),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "collected", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "collected", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Collect", Effects = { { "collected", true } }, DurationSec = 2f,
                        TargetPos = (ctx, t) => TroughPos,
                        Execute = (ctx, t) =>
                        {
                            var jitter = new Vector3((float)GD.RandRange(-14, 14), 0f, (float)GD.RandRange(-14, 14));
                            DroppedItem.Spawn(GetTree().CurrentScene, TroughPos + jitter, ProduceItemId, 1);
                            FarmStorage.Instance.Add(ProduceItemId, 1);
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

            SteeringUtil.ApplyStandingOrLyingPose(_model, _brain.IsSleeping && !wantsToMove, _facing, FlipModelFacing, TurnSpeed * dt);

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "FarmhandNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
