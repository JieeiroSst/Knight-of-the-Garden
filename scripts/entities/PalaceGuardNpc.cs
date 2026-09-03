using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cam Ve Quan - doi bao ve nong trai (100 NPC, xem Main.BuildPalaceGuardBarracks). QUY HOACH
    // LAI sang Utility AI (van la Utility AI THAT theo dung yeu cau "toan bo NPC", xem
    // FarmhandNpc.cs/RepairmanNpc.cs de biet kien truc chung) nhung CHI 2 hanh dong don gian
    // (Patrol/Sleep, dung UtilityPresets.MakeWander/MakeSleep co san) - KHONG dung GOAP nhieu buoc
    // cho nhom nay vi so luong qua lon (100 NPC), giu dung tinh than "logic don gian" ban dau
    // (comment cu giai thich ly do CPU van con dung).
    public partial class PalaceGuardNpc : NPC
    {
        [Export] public float Speed = 46f;
        [Export] public float Acceleration = 170f;
        [Export] public float Friction = 210f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        // Gio bat dau/ket thuc ca lam - HO TRO ca lam VUOT QUA NUA DEM (vd 18 -> 6), dung cho 2 ca
        // doi lap nhau (6h sang-6h toi / 6h toi-6h sang). Gio dung de CHAM DIEM (Utility), khong
        // con "gate" cung nhu truoc.
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 18;
        [Export] public float PatrolRadius = 1400f;

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 PatrolCenter;

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
            GlobalPosition = HomePos;

            _brain.Actions.Add(MakePatrolAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos, nightBonus: 0f, fatigueWeight: 0f));
        }

        private bool IsOnDutyHour(int hour) => WorkStartHour < WorkEndHour
            ? (hour >= WorkStartHour && hour < WorkEndHour)
            : (hour >= WorkStartHour || hour < WorkEndHour);

        // Diem tuyet doi (0 hoac 100) theo DUNG ca truc - khong can do met/gio dem chung nhu cac
        // NPC khac (Sleep goi voi fatigueWeight=0/nightBonus=0 nen chi con diem tu day quyet dinh)
        // vi 2 ca da duoc thiet ke DOI LAP HOAN TOAN san (luon co dung 50 nguoi truc bat ke gio
        // nao) - khong nen de do met/dem lam lech quan he nay.
        private UtilityAction MakePatrolAction()
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            return new UtilityAction
            {
                Id = "Patrol",
                Evaluate = ctx => IsOnDutyHour(ctx.Hour) ? new UtilityResult(100f) : new UtilityResult(float.NegativeInfinity),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "WanderStep", Effects = { { "there", true } }, DurationSec = rng.RandfRange(6f, 14f),
                        TargetPos = (ctx, t) =>
                        {
                            float angle = rng.RandfRange(0f, Mathf.Tau);
                            float radius = rng.RandfRange(0f, PatrolRadius);
                            return PatrolCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        },
                    },
                },
            };
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _brain.Tick(dt, this, 14f, Speed * 0.65f, _nav, _navAgent);

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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "PalaceGuardNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
