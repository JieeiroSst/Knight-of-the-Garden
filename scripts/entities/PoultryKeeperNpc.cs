using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nguoi cham ga - QUY HOACH LAI sang Utility AI + GOAP (xem FarmhandNpc.cs/RepairmanNpc.cs
    // de biet chi tiet kien truc chung). Viec QUET THU HOACH TRUNG GA van chay NEN TANG lien tuc
    // (khong phu thuoc brain dang chon hanh dong gi) vi day la 1 tac vu thu dong, ren re, khong
    // can "di den" hay lap ke hoach GOAP - chi RestockFeed (khi kho thuc an sap het) moi can.
    public partial class PoultryKeeperNpc : NPC
    {
        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 14f;
        [Export] public float WorkWanderRadius = 70f;
        [Export] public float HarvestRange = 45f;
        [Export] public double HarvestScanIntervalSec = 1.0;
        [Export] public int FeedRestockThreshold = 10;
        [Export] public int FeedRestockQty = 20;

        // Main.cs gan cac vi tri nay ngay sau khi tao NPC (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;

        private Vector3 _facing = Vector3.Back;
        private double _harvestScanCooldown = 0;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private NavigationAgent3D _navAgent;
        private readonly SteeringUtil.NavSteering _nav = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();
            // Bat buoc - xem ghi chu chi tiet trong FarmWorkerNpc.cs (thieu dong nay khien NPC
            // sinh chong khit len nhau tai goc toa do, gay loi engine "Object went too far away").
            GlobalPosition = HomePos;

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

            _harvestScanCooldown -= dt;
            if (_harvestScanCooldown <= 0)
            {
                HarvestNearbyEggs();
                _harvestScanCooldown = HarvestScanIntervalSec;
            }

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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "PoultryKeeperNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }

        // Quet toan bo vat pham roi tren mat dat (group "dropped_items"), thu hoach nhung qua
        // trung (ItemId == "egg") trong pham vi HarvestRange quanh NPC - xoa khoi mat dat va cong
        // don vao kho nong san chung (KHONG phai tui do nguoi choi).
        private void HarvestNearbyEggs()
        {
            foreach (var node in GetTree().GetNodesInGroup("dropped_items"))
            {
                if (node is not DroppedItem item || !IsInstanceValid(item)) continue;
                if (item.ItemId != "egg") continue;
                if (GlobalPosition.DistanceTo(item.GlobalPosition) > HarvestRange) continue;

                FarmStorage.Instance.Add("egg", item.Amount);
                item.QueueFree();
            }
        }
    }
}
