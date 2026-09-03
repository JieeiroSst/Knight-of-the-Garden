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

        private static readonly string[] TrackedItems = { "wheat", "potato", "carrot", "milk", "egg", "wool" };

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

            float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "WarehouseManagerNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }

        // Ghi de PickDialogue (protected virtual tren NPC.cs) de tra ve bao cao TON KHO THUC SU
        // moi lan noi chuyen, thay vi chon ngau nhien tu 1 mang cau co dinh. Antoine dong vai tro
        // "Cho" (bao gia thi truong) - dung nhan vat da co san thay vi mo them 1 man hinh rieng.
        protected override string PickDialogue()
        {
            var report = TrackedItems
                .Select(id => (id, label: ItemDatabase.Instance.GetDisplayName(id), count: FarmStorage.Instance.GetCount(id), full: FarmStorage.Instance.GetFullness(id),
                    price: Mathf.RoundToInt((ItemDatabase.Instance.GetItem(id)?.SellPrice ?? 0) * Market.GetSupplyMultiplier(id))))
                .ToArray();

            string header = Loc.T("warehouse.report_header")
                + string.Join(", ", report.Select(r => string.Format(Loc.T("warehouse.report_item_fmt"), r.label, r.count, r.price)));

            var fullest = report.OrderByDescending(r => r.full).First();
            string warning = fullest.full >= 0.9f
                ? "\n" + string.Format(Loc.T("warehouse.almost_full_fmt"), fullest.label, Mathf.RoundToInt((1f - fullest.full) * 100))
                : "";

            var oversupplied = report.Where(r => r.count >= SellSuggestThreshold).OrderByDescending(r => r.count).FirstOrDefault();
            string suggestion = oversupplied.label != null
                ? "\n" + string.Format(Loc.T("warehouse.suggestion_fmt"), oversupplied.label)
                : "";

            // Gia tri dat trang trai - CHI la chi so hien thi (khong mua ban duoc), uoc tinh don
            // gian tu tong san luong nong san hien co.
            int landValue = report.Sum(r => r.count) * 50;
            string land = "\n" + string.Format(Loc.T("warehouse.land_value_fmt"), landValue);

            return header + warning + suggestion + land;
        }
    }
}
