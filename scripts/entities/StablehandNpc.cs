using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nguoi cham ngua - QUY HOACH LAI sang Utility AI + GOAP (xem FarmhandNpc.cs de biet chi
    // tiet ly do/kien truc chung, RepairmanNpc.cs la mau tham chieu dau tien). Thay THE HOAN TOAN
    // lich gio co dinh cu.
    public partial class StablehandNpc : NPC
    {
        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 14f;
        [Export] public float WorkWanderRadius = 110f;
        [Export] public int FeedRestockThreshold = 10;
        [Export] public int FeedRestockQty = 20;

        // Main.cs gan cac vi tri nay ngay sau khi tao NPC (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;

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
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => WorkPos, WorkWanderRadius));
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
                    int hungry = AnimalCareUtil.CountHungryNear(GetTree(), WorkPos, WorkWanderRadius + 60f);
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
    }
}
