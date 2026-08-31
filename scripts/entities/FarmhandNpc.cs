using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC nguoi cham bo: ke thua toan bo he thong hoi thoai/cua hang cua NPC (Interact,
    // Trust...), them AI di chuyen theo GIO HANH CHINH THAT (6h sang - 18h toi, dong bo dong ho
    // may tinh qua GameManager.HourChanged): den gio lam thi di tu nha ra chuong bo, quanh quan
    // cham soc (rai co/mang an cho bo) va dinh ky "vat sua" (tha vat pham sua o mang), het gio
    // thi di ve nha nghi qua dem.
    public partial class FarmhandNpc : NPC
    {
        private enum WorkState { AtHome, GoingToWork, Working, GoingHome }

        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 200f;
        [Export] public float Friction = 240f;
        [Export] public float TurnSpeed = 7f;
        // Nhu Cow/Player - model nhan vat Quaternius thuong nguoc quy uoc "-Z la truoc" cua Godot.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 18;
        [Export] public float WorkWanderRadius = 90f;
        [Export] public double MilkIntervalSec = 150.0;
        // San pham dinh ky tha ra (mac dinh "milk" - giu nguyen hanh vi cu cho nguoi cham bo/van
        // sua). Nguoi chan cuu (xem Main.BuildExtraSheepPensRound2) dat lai thanh "wool" de dung
        // dung nghia "cham soc cuu, thu len" thay vi tiep tuc tha nham sua bo.
        [Export] public string ProduceItemId = "milk";

        // Main.cs gan cac vi tri nay ngay sau khi tao NPC (truoc AddChild).
        public Vector3 HomePos;       // ngay truoc cua nha (ngoai troi)
        // Phong noi that THAT SU cua nha (tang tret, xem AddBuildingEntrance) - ngoai gio lam,
        // NPC di HAN VAO TRONG day de "ngu" thay vi dung ngoai san.
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos;
        public Vector3 TroughPos;

        private WorkState _workState = WorkState.AtHome;
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private double _milkCooldown = 0;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            base._Ready(); // NPC._Ready(): gan group "npcs", gan model + AnimationPlayer

            int hour = GameManager.Instance.Hour;
            bool onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            _workState = onDuty ? WorkState.Working : WorkState.AtHome;
            // Neu game bat dau ngoai gio lam, NPC da o SAN TRONG nha (ngu), khong phai ngoai san.
            GlobalPosition = onDuty ? WorkPos : InteriorHomePos + Vector3.Up * 8f;
            _wanderTarget = GlobalPosition;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            if (hour == WorkStartHour && _workState == WorkState.AtHome)
            {
                // Thuc day, buoc ra khoi nha truoc (tu trong phong ra truoc cua), roi moi bat
                // dau di lam - khong "xuyen tuong" thang tu giuong toi chuong bo.
                GlobalPosition = HomePos;
                _workState = WorkState.GoingToWork;
            }
            else if (hour == WorkEndHour && _workState != WorkState.AtHome)
            {
                _workState = WorkState.GoingHome;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _workState switch
            {
                WorkState.GoingToWork => GoTo(WorkPos, Speed, WorkState.Working),
                WorkState.GoingHome => GoTo(HomePos, Speed, WorkState.AtHome),
                WorkState.Working => DoWorkWander(dt),
                _ => (Vector3.Zero, 0f), // AtHome: nghi ngoi, dung yen
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            // Neu gan nhu khong nhuc nhich duoc trong 1 khoang thoi gian du dang "muon di" (bi
            // hang rao/cong trinh/NPC khac chan), tu dong lach sang huong khac 1 chut - xem
            // SteeringUtil.StuckDetector.
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _workState == WorkState.AtHome, _facing, FlipModelFacing, TurnSpeed * dt);

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

        private (Vector3 dir, float speed) GoTo(Vector3 target, float speed, WorkState arrivedState)
        {
            Vector3 dir = target - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 14f)
            {
                _workState = arrivedState;
                if (arrivedState == WorkState.AtHome)
                {
                    // Da toi truoc cua - buoc HAN VAO TRONG (dung phong noi that that su) de
                    // ngu qua dem, khong dung khuya ngoai san.
                    GlobalPosition = InteriorHomePos + Vector3.Up * 8f;
                }
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), speed);
        }

        // Trong gio lam: quanh quan gan chuong bo (rai co/cham soc) va dinh ky vat 1 chai sua
        // canh mang cho nguoi choi nhat - the hien dung nghia "cho bo an, lay sua bo".
        private (Vector3 dir, float speed) DoWorkWander(float dt)
        {
            _milkCooldown -= dt;
            if (_milkCooldown <= 0)
            {
                var jitter = new Vector3((float)GD.RandRange(-14, 14), 0f, (float)GD.RandRange(-14, 14));
                DroppedItem.Spawn(GetTree().CurrentScene, TroughPos + jitter, ProduceItemId, 1);
                // Cung cong don vao kho nong san chung (xem FarmStorage) - de Antoine co so lieu
                // THAT ve sua/len, khong chi trung (truoc day chi PoultryKeeperNpc lam dieu nay).
                FarmStorage.Instance.Add(ProduceItemId, 1);
                _milkCooldown = MilkIntervalSec;
            }

            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, WorkWanderRadius);
                _wanderTarget = WorkPos + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(4000, 9000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.5f);
        }
    }
}
