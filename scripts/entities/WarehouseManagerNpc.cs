using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Antoine - Nguoi quan ly kho. QUY HOACH LAI sang Utility AI (chi Wander + Sleep - Antoine
    // khong lam viec chan tay, gia tri cua NPC nay la LOI THOAI DONG doc so lieu THAT tu
    // FarmStorage, xem PickDialogue, khong doi).
    public partial class WarehouseManagerNpc : NPC
    {
        [Export] public float Speed = 45f;
        [Export] public float Acceleration = 160f;
        [Export] public float Friction = 200f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 10f;
        [Export] public float WanderRadius = 90f;
        [Export] public int SellSuggestThreshold = 150;

        private static readonly (string id, string label)[] TrackedItems =
        {
            ("wheat", "Lua mi"), ("potato", "Khoai tay"), ("carrot", "Ca rot"),
            ("milk", "Sua bo"), ("egg", "Trung ga"), ("wool", "Len cuu"),
        };

        public Vector3 HomePos;
        public Vector3 InteriorHomePos;

        private Vector3 _facing = Vector3.Back;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();
            GlobalPosition = HomePos;

            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => HomePos, WanderRadius));
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

        // Ghi de PickDialogue (protected virtual tren NPC.cs) de tra ve bao cao TON KHO THUC SU
        // moi lan noi chuyen, thay vi chon ngau nhien tu 1 mang cau co dinh.
        protected override string PickDialogue()
        {
            var report = TrackedItems
                .Select(t => (t.label, count: FarmStorage.Instance.GetCount(t.id), full: FarmStorage.Instance.GetFullness(t.id)))
                .ToArray();

            string header = "Toi quan ly kho nong san chung cua trang trai. Ton hien tai:\n"
                + string.Join(", ", report.Select(r => $"{r.label} {r.count}"));

            var fullest = report.OrderByDescending(r => r.full).First();
            string warning = fullest.full >= 0.9f
                ? $"\nKho gan day roi: {fullest.label} chi con {Mathf.RoundToInt((1f - fullest.full) * 100)}% cho trong."
                : "";

            var oversupplied = report.Where(r => r.count >= SellSuggestThreshold).OrderByDescending(r => r.count).FirstOrDefault();
            string suggestion = oversupplied.label != null
                ? $"\nNen dua bot {oversupplied.label.ToLower()} ra cho ban, de trong kho lam gi cho chat."
                : "";

            return header + warning + suggestion;
        }
    }
}
