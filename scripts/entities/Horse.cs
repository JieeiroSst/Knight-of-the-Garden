using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Ngua 3D that (Quaternius Farm Animal Pack, CC0) tu do di lai trong pham vi chuong ngua.
    // Den gio an (12h trua va 16h chieu, theo DONG HO THAT cua may tinh qua GameManager.HourChanged)
    // se tu dong di den mang thuc an, dung do an 1 luc roi quay lai di lai binh thuong.
    public partial class Horse : CharacterBody3D, IHungryAnimal
    {
        private enum State { Wander, GoToTrough, Eating }

        [Export] public float Speed = 55f;
        [Export] public float Acceleration = 160f;
        [Export] public float Friction = 200f;
        [Export] public float TurnSpeed = 6.5f;
        // Nhu Cow/Player - model Quaternius thuong nguoc quy uoc "-Z la truoc" cua Godot.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 4.6f;
        [Export] public double EatDurationSec = 90.0;
        [Export] public float NeighRange = 110f;
        [Export] public float NeighCooldownSec = 10f;
        [Export] public float RideSpeed = 260f;
        // Vi tri "yen ngua": GlobalPosition cua Player la goc o CHAN (pivot chan, khong phai
        // hong/mong) - neu dat pivot chan dung bang chieu cao lung ngua (~32 don vi) thi MONG
        // nguoi choi (cao hon chan ~1 nua chieu cao nguoi, ~18-20 don vi voi capsule cao 40) se
        // lo lung PHIA TREN lung ngua, khong hoan toan cham vao. Phai TRU them chieu cao hong
        // (~18-20) de MONG (khong phai chan) la diem thuc su cham lung ngua: 32 - 19 ~= 13.
        public static readonly Vector3 SeatOffset = new(0, 13f, 2f);
        public Vector3 Facing => _facing;

        // Sinh san & lon len (mau Cow.cs): ngua con sinh ra tu 2 ngua lon (xem Main.TryBreedHorses),
        // bat dau nho va CAN AN MOI NGAY (den mang gio 12h/16h) de lon dan qua tung NGAY THAT.
        [Export] public bool IsAdult = true;
        [Export] public float BirthScaleFactor = 0.5f;
        [Export] public int GrowthDaysNeeded = 4;
        private int _daysFed = 0;
        private bool _ateToday = false;
        private CollisionShape3D _collision;

        // Xem ghi chu chi tiet trong Cow.cs - cung 1 co che doi THAT.
        public int HungerDays { get; private set; } = 0;
        public bool IsHungry => HungerDays > 0;

        private const string AnimIdle = "Armature|Idle";
        private const string AnimWalkSlow = "Armature|WalkSlow";
        private const string AnimWalk = "Armature|Walk";
        private const string AnimRun = "Armature|Run";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        private State _state = State.Wander;
        private Vector3 _homeCenter;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        // Huong than THAT cua con ngua (khac voi huong toi diem den) - chi di ve phia truoc theo
        // huong nay, khong bao gio truot ngang/lui - giong dong vat that (Cow.cs).
        private Vector3 _facing = Vector3.Back;
        private float _speedJitter = 1f;

        // Vi tri mang thuc an - Main.cs gan ngay sau khi tao.
        public Vector3 TroughPosition;
        // Tam that cua khu chuong (Main.cs gan) + nua be rong an toan, dam bao ngua khong bao
        // gio wander ra ngoai hang rao (xem Cow.cs cho ly do dung toa do cuc).
        public Vector3 HomeCenter = new(float.NaN, 0, float.NaN);
        public float PastureHalfExtent = 999999f;

        private AudioStreamPlayer3D _neighPlayer;
        private Player _player;
        private double _neighCooldownLeft = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            AddToGroup("horses");
            _model = GetNodeOrNull<Node3D>("Model");
            _collision = GetNodeOrNull<CollisionShape3D>("Collision");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/horse.glb", ModelScale);
                PlayLoop(AnimIdle);
            }
            _homeCenter = float.IsNaN(HomeCenter.X) ? GlobalPosition : HomeCenter;
            _wanderTarget = GlobalPosition;
            ApplyGrowthVisual();

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.15f);

            _neighPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/horse_neigh.mp3"),
                MaxDistance = 320f,
                UnitSize = 13f
            };
            AddChild(_neighPlayer);

            GameManager.Instance.HourChanged += OnHourChanged;
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void ApplyGrowthVisual()
        {
            float t = IsAdult ? 1f : Mathf.Clamp((float)_daysFed / GrowthDaysNeeded, 0f, 1f);
            float scale = Mathf.Lerp(BirthScaleFactor, 1f, t);
            if (_model != null) _model.Scale = Vector3.One * scale;
            if (_collision != null) _collision.Scale = Vector3.One * scale;
        }

        private void OnDayChanged(int day)
        {
            if (IsAdult) return;
            if (_ateToday) _daysFed++;
            _ateToday = false;
            ApplyGrowthVisual();
            if (_daysFed >= GrowthDaysNeeded) IsAdult = true;
        }

        private void OnHourChanged(int hour)
        {
            if ((hour == 12 || hour == 16) && _state != State.Eating)
                _state = State.GoToTrough;
        }

        private void UpdateNeigh(float dt)
        {
            if (_neighCooldownLeft > 0) _neighCooldownLeft -= dt;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (_player == null || _neighPlayer == null) return;

            if (_neighCooldownLeft <= 0 && GlobalPosition.DistanceTo(_player.GlobalPosition) <= NeighRange)
            {
                _neighPlayer.Play();
                _neighCooldownLeft = NeighCooldownSec;
            }
        }

        // Ngua dang duoc nguoi choi cuoi: TU DOC INPUT truc tiep (giong Player.cs) va tu di
        // chuyen bang chinh vong lap vat ly cua no (MoveAndSlide/trong luc rieng) - Player.cs
        // chi "ngoi" tren lung (xem SeatOffset), KHONG con dieu khien vi tri ngua tu ben ngoai
        // nua. Nho vay chuyen dong khi cuoi dung HET cung 1 kieu re/xoay tu nhien (quay dau
        // truoc roi moi buoc toi) nhu luc ngua tu di AI, khong bi giat/khong tu nhien.
        private bool _ridden = false;

        public void SetRidden(bool ridden) => _ridden = ridden;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_ridden)
            {
                DoRiddenMovement(dt);
                return;
            }

            UpdateNeigh(dt);

            var (desiredDir, targetSpeed) = _state switch
            {
                State.Wander => DoWander(),
                State.GoToTrough => DoGoToTrough(),
                _ => (TroughDirOrZero(), 0f), // Eating: dung yen nhung quay mat ve phia mang
            };

            MoveWithFacing(desiredDir, targetSpeed, dt);
        }

        // Nguoi choi giu phim di chuyen -> ngua di theo dung huong phim (khong phai theo mot
        // "diem den" nhu AI), dung CHUNG mot ham xoay-roi-di (MoveWithFacing) nen cam giac y het
        // luc AI tu di - chi khac nguon huong den tu dau.
        private void DoRiddenMovement(float dt)
        {
            var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var desiredDir = new Vector3(input.X, 0f, input.Y);
            MoveWithFacing(desiredDir != Vector3.Zero ? desiredDir.Normalized() : Vector3.Zero, RideSpeed, dt);
        }

        // Logic di chuyen dung chung cho ca AI tu di LAN luc duoc cuoi: xoay dan than ve huong
        // muon den (KHONG gan tuc thi), roi moi buoc toi THEO DUNG huong than dang huong toi -
        // dam bao khong bao gio truot ngang/lui, luon quay dau truoc khi doi huong.
        private void MoveWithFacing(Vector3 desiredDir, float targetSpeed, float dt)
        {
            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            if (_model != null && _facing != Vector3.Zero)
            {
                var lookDir = FlipModelFacing ? -_facing : _facing;
                var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
                _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            }

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();

            // Chon dung dang di theo TOC DO THUC TE - truoc day CHI dung "WalkSlow" cho moi toc
            // do (ke ca luc phi nuoc dai 260 don vi/s khi duoc cuoi), khien chan buoc cham nhung
            // than lai lao nhanh tren mat dat, nhin rat gia. "Run" (truoc do khong bao gio dung
            // toi) gio danh rieng cho toc do nhanh (cuoi/gio lam), "Walk" cho toc do vua (di den
            // mang an), "WalkSlow" cho luc tu gam co/dao choi cham.
            float speed = horizontal.Length();
            string anim = speed < 3f ? AnimIdle
                : speed < 35f ? AnimWalkSlow
                : speed < 100f ? AnimWalk
                : AnimRun;
            PlayLoop(anim);
            // Dieu chinh nhip chan khop toc do that (tranh "truot bang" - chan dong nhung nguoi
            // di nhanh/cham hon animation), giong ky thuat da dung cho Player.cs.
            if (_animPlayer != null)
                _animPlayer.SpeedScale = anim == AnimIdle ? 1f : Mathf.Clamp(speed / 55f, 0.6f, 2f);
        }

        private Vector3 TroughDirOrZero()
        {
            Vector3 dir = TroughPosition - GlobalPosition;
            dir.Y = 0f;
            return dir.Length() > 4f ? dir.Normalized() : Vector3.Zero;
        }

        private (Vector3 dir, float speed) DoWander()
        {
            ulong now = Time.GetTicksMsec();
            if (now >= _nextWanderTime)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                // Dung DUNG PastureHalfExtent (khong con gioi han bang 1 WanderRadius nho co
                // dinh nua) - neu khong, chuong lon se co 1 vanh ngoai (gan hang rao/cong) LUON
                // LUON khong co con vat nao, de bi nham la "chuong trong" khi vua buoc vao.
                float half = PastureHalfExtent;
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, half);
                _wanderTarget = _homeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _nextWanderTime = now + (ulong)rng.RandiRange(4000, 10000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.45f * _speedJitter);
        }

        private (Vector3 dir, float speed) DoGoToTrough()
        {
            Vector3 dir = TroughPosition - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 18f)
            {
                _state = State.Eating;
                if (FarmStorage.Instance.TryRemove("thucan_giasuc", 1)) { _ateToday = true; HungerDays = 0; }
                else HungerDays++;
                GetTree().CreateTimer(EatDurationSec).Timeout += () =>
                {
                    if (IsInstanceValid(this)) _state = State.Wander;
                };
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), Speed * _speedJitter);
        }

        private void PlayLoop(string anim)
        {
            if (_animPlayer != null && _currentAnim != anim && _animPlayer.HasAnimation(anim))
            {
                _animPlayer.Play(anim);
                _currentAnim = anim;
            }
        }
    }
}
