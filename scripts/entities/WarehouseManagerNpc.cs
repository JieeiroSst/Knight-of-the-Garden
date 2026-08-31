using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Antoine - Nguoi quan ly kho. Khong lam viec chan tay (chi quanh quan gan nha kho ban ngay,
    // ve ngu ban dem giong FarmhandNpc) - gia tri cua NPC nay la LOI THOAI DONG: moi lan noi
    // chuyen, doc THAT so lieu hien co trong FarmStorage (kho nong san chung - duoc cong don that
    // su moi khi thu hoach/vat sua/cat len, xem FarmPlot.Harvest/FarmhandNpc.DoWorkWander) va tu
    // dong canh bao khi 1 mat hang gan day + de xuat dua ra cho ban khi ton qua nhieu.
    public partial class WarehouseManagerNpc : NPC
    {
        [Export] public float Speed = 45f;
        [Export] public float Acceleration = 160f;
        [Export] public float Friction = 200f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 21;
        [Export] public float WanderRadius = 90f;
        [Export] public int SellSuggestThreshold = 150;

        // Cac mat hang thuc su duoc theo doi (khop voi nhung gi FarmStorage THUC SU nhan duoc -
        // xem ghi chu tren) - "Barley" trong yeu cau goc khong ton tai trong items.json cua game
        // nay nen KHONG the theo doi (khong co gi san xuat ra no).
        private static readonly (string id, string label)[] TrackedItems =
        {
            ("wheat", "Lua mi"), ("potato", "Khoai tay"), ("carrot", "Ca rot"),
            ("milk", "Sua bo"), ("egg", "Trung ga"), ("wool", "Len cuu"),
        };

        public Vector3 HomePos;
        public Vector3 InteriorHomePos;

        private bool _onDuty;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            _onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            GlobalPosition = _onDuty ? HomePos : InteriorHomePos + Vector3.Up * 8f;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            bool onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            if (onDuty == _onDuty) return;
            _onDuty = onDuty;
            GlobalPosition = onDuty ? HomePos : InteriorHomePos + Vector3.Up * 8f;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            var (desiredDir, targetSpeed) = _onDuty ? DoWander() : (Vector3.Zero, 0f);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, !_onDuty, _facing, FlipModelFacing, TurnSpeed * dt);

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

        private (Vector3 dir, float speed) DoWander()
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, WanderRadius);
                _wanderTarget = HomePos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(5000, 12000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.5f);
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
