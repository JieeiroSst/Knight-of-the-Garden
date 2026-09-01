using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Marcel - Tho sua chua trang trai. QUY HOACH LAI sang Utility AI + GOAP (thay THE HOAN TOAN
    // lich gio co dinh cu WorkStartHour/WorkEndHour): moi lan quyet dinh, cham diem "hang rao te
    // nhat can sua bao nhieu" so voi "met/buon ngu" - hanh dong nao diem cao hon thi lam. Khi chon
    // sua hang rao, GOAP TU LAP KE HOACH chuoi buoc (lay go -> lay bua -> den hang rao -> sua) dua
    // tren dieu kien can/hieu ung (Preconditions/Effects), khong con hard-code 8 buoc co dinh nhu
    // truoc - day la NPC dau tien duoc chuyen sang kien truc moi, dung lam mau tham chieu vi logic
    // cu cua no da gan giong GOAP nhat (xem UtilityAi.cs/GoapPlanner.cs).
    public partial class RepairmanNpc : NPC
    {
        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public double FetchPauseSec = 3.0;
        [Export] public double RepairDurationSec = 6.0;
        [Export] public int RepairAmount = 45;
        [Export] public int NeedsRepairThreshold = 70; // Hp duoi nguong nay moi coi la "can sua"

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;         // truoc cua nha kho (ngoai troi)
        public Vector3 InteriorHomePos; // phong noi that that su - noi ngu ban dem
        public Vector3 WoodpilePos;
        public Vector3 ToolAreaPos;

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

            _brain.Actions.Add(MakeRepairAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => HomePos, 120f));
        }

        // Diem = "100 - Hp cua hang rao te nhat" (chi tinh neu duoi NeedsRepairThreshold) - hang
        // rao cang hu, diem cang cao, dung y tuong "muc do khan cap" nguoi dung neu.
        private UtilityAction MakeRepairAction()
        {
            return new UtilityAction
            {
                Id = "RepairFence",
                Evaluate = ctx =>
                {
                    var worst = GetTree().GetNodesInGroup("fence_markers")
                        .OfType<FenceMarker>()
                        .Where(f => IsInstanceValid(f))
                        .OrderBy(f => f.Hp)
                        .FirstOrDefault();
                    if (worst == null || worst.Hp >= NeedsRepairThreshold) return new UtilityResult(float.NegativeInfinity);
                    return new UtilityResult(100f - worst.Hp, worst);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "hasWood", false }, { "hasHammer", false }, { "repaired", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "repaired", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction { Id = "FetchWood", Effects = { { "hasWood", true } }, DurationSec = (float)FetchPauseSec, TargetPos = (ctx, t) => WoodpilePos },
                    new GoapAction { Id = "FetchHammer", Preconditions = { { "hasWood", true } }, Effects = { { "hasHammer", true } }, DurationSec = (float)FetchPauseSec, TargetPos = (ctx, t) => ToolAreaPos },
                    new GoapAction
                    {
                        Id = "Repair",
                        Preconditions = { { "hasHammer", true } },
                        Effects = { { "repaired", true } },
                        DurationSec = (float)RepairDurationSec,
                        TargetPos = (ctx, t) => (t as FenceMarker)?.GlobalPosition ?? ctx.SelfPos,
                        Execute = (ctx, t) => (t as FenceMarker)?.Repair(RepairAmount),
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
