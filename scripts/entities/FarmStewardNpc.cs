using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Jean - Quan gia trang trai (Farm Steward). QUY HOACH LAI sang Utility AI (Patrol + Sleep,
    // xem FarmhandNpc.cs/RepairmanNpc.cs de biet chi tiet kien truc chung) - THAY THE lich gio co
    // dinh (WakeHour/SleepHour) bang cham diem do met/ban dem, giong moi NPC khac.
    //
    // QUAN TRONG - pham vi that su (khong doi so voi truoc): Jean "dieu phoi trang trai" van CHI
    // la BOI CANH CAU CHUYEN (loi thoai phan anh dung vai tro), KHONG phai 1 bo may AI trung tam
    // THAT SU dieu khien cac NPC khac - moi NPC (ke ca sau khi chuyen sang Utility AI/GOAP) van tu
    // cham diem/lap ke hoach RIENG cua minh, khong nhan "lenh" tu Jean hay tu bat ky NPC nao khac.
    public partial class FarmStewardNpc : NPC
    {
        [Export] public float Speed = 45f;
        [Export] public float Acceleration = 170f;
        [Export] public float Friction = 210f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public double PauseAtPointSec = 8.0;

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3[] PatrolPoints = System.Array.Empty<Vector3>();

        private Vector3 _facing = Vector3.Back;
        private int _pointIndex = 0;

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
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
        }

        private UtilityAction MakePatrolAction()
        {
            return new UtilityAction
            {
                Id = "Patrol",
                Evaluate = ctx => new UtilityResult(15f),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Checkpoint", Effects = { { "there", true } }, DurationSec = (float)PauseAtPointSec,
                        TargetPos = (ctx, t) => PatrolPoints.Length > 0 ? PatrolPoints[_pointIndex % PatrolPoints.Length] : ctx.SelfPos,
                        Execute = (ctx, t) => { if (PatrolPoints.Length > 0) _pointIndex = (_pointIndex + 1) % PatrolPoints.Length; },
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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
