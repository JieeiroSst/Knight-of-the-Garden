using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC "nguoi lam vuon" (Augustin, phu trach Khu Trong Trot: vuon cay/vuon nho/to ong) - QUY
    // HOACH LAI sang Utility AI + GOAP (xem FarmhandNpc.cs/RepairmanNpc.cs de biet chi tiet kien
    // truc chung).
    public partial class EstateWorkerNpc : NPC
    {
        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 14f;
        [Export] public float WorkWanderRadius = 260f;

        // San pham xoay vong (Main.cs gan) - moi lan den luot se tha 1 vat pham tuong ung.
        public string[] Products = { "wool" };

        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;

        private Vector3 _facing = Vector3.Back;
        private int _productIndex = 0;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();

            _brain.Actions.Add(MakeCollectProduceAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => WorkPos, WorkWanderRadius));
        }

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
                        TargetPos = (ctx, t) =>
                        {
                            var rng = new RandomNumberGenerator();
                            rng.Randomize();
                            float angle = rng.RandfRange(0f, Mathf.Tau);
                            float radius = rng.RandfRange(0f, WorkWanderRadius);
                            return WorkPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        },
                        Execute = (ctx, t) =>
                        {
                            if (Products.Length == 0) return;
                            var itemId = Products[_productIndex];
                            DroppedItem.Spawn(GetTree().CurrentScene, ctx.SelfPos, itemId, 1);
                            FarmStorage.Instance.Add(itemId, 1);
                            _productIndex = (_productIndex + 1) % Products.Length;
                        },
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

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
