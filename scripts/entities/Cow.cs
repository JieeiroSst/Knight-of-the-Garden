using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Bo 3D that (Quaternius Farm Animal Pack, CC0) tu do di lai (gam co) trong pham vi trang
    // trai. Den gio an (12h trua va 16h chieu, theo DONG HO THAT cua may tinh qua
    // GameManager.HourChanged - dong bo voi he thong ngay/dem da co) se tu dong di den mang
    // thuc an, dung do an 1 luc roi quay lai gam co binh thuong.
    public partial class Cow : CharacterBody3D, IHungryAnimal
    {
        private enum State { Wander, GoToTrough, Eating }

        [Export] public float Speed = 40f;
        [Export] public float Acceleration = 130f;
        [Export] public float Friction = 160f;
        [Export] public float TurnSpeed = 6.5f;
        // (Da bo WanderRadius rieng - truoc day gioi han vung di lai chi trong 130 don vi quanh
        // tam CHUONG DU CHUONG RONG HON NHIEU (PastureHalfExtent co the toi 157+), khien vanh
        // ngoai gan hang rao/cong - dung noi nguoi choi buoc vao - LUON LUON khong co con bo nao,
        // de bi nham la "chuong trong" khi vua vao. Gio dung het toan bo PastureHalfExtent that.)
        // Model cow.glb duoc dung theo huong NGUOC voi quy uoc "-Z la truoc" cua Godot (giong
        // Farmer da gap truoc do, xem Player.FlipModelFacing) - neu khong bu lai, than se DI
        // dung huong nhung DAU/mat lai quay ve phia nguoc lai, nhin nhu dang di lui.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 5.5f;
        [Export] public double EatDurationSec = 90.0;
        [Export] public float MooRange = 90f;
        [Export] public float MooCooldownSec = 8f;

        // Sinh san & lon len: bo con sinh ra tu 2 bo lon (xem Main.TryBreedCows), bat dau nho
        // (BirthScaleFactor) va CAN AN MOI NGAY (den mang gio 12h/16h) de lon dan qua tung
        // NGAY THAT (dong ho may tinh) - giong 1 con bo ngoai doi thuc: khong an thi khong lon.
        [Export] public bool IsAdult = true;
        [Export] public float BirthScaleFactor = 0.4f;
        [Export] public int GrowthDaysNeeded = 4;
        private int _daysFed = 0;
        private bool _ateToday = false;
        private CollisionShape3D _collision;

        // Doi THAT (khong con "an vo han") - moi lan den mang, tieu thu 1 don vi
        // "thucan_giasuc" tu FarmStorage; het hang thi KHONG an duoc, HungerDays tang. Utility AI
        // (xem UtilityAi.cs) doc IsHungry de cham diem "bo can thuc an" - truoc day khong co du
        // lieu that nao de cham diem, chi la lich trinh trang tri.
        public int HungerDays { get; private set; } = 0;
        public bool IsHungry => HungerDays > 0;

        private const string AnimIdle = "Armature|Idle";
        private const string AnimWalk = "Armature|WalkSlow";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        private State _state = State.Wander;
        private Vector3 _homeCenter;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        // Huong than THAT cua con bo (khac voi huong toi diem den) - bo chi di ve phia truoc
        // theo huong nay, khong bao gio truot ngang/lui, giong dong vat that: muon doi huong
        // phai QUAY DAU truoc (xoay dan qua nhieu frame) roi moi buoc toi.
        private Vector3 _facing = Vector3.Back;
        // Moi con bo di nhanh/cham hoi khac nhau mot chut (khong phai robot dong bo tuyet doi)
        // + toc do gam co cham hon han toc do di den mang, cho dang di tu nhien hon.
        private float _speedJitter = 1f;

        // Vi tri mang thuc an - Main.cs gan gia tri nay ngay sau khi tao con bo.
        public Vector3 TroughPosition;
        // Tam that cua khu chuong trai (Main.cs gan = tam hang rao that, KHONG phai vi tri
        // spawn rieng cua tung con bo) + nua be rong an toan (nho hon nua be rong hang rao that
        // 1 khoang de tru be day than bo) - dam bao bo KHONG BAO GIO di ra ngoai hang rao du
        // wander co random the nao. Dung NaN lam gia tri "chua duoc Main.cs gan" -> tu dung
        // spawn position lam tam (truong hop du phong).
        public Vector3 HomeCenter = new(float.NaN, 0, float.NaN);
        public float PastureHalfExtent = 999999f;

        private AudioStreamPlayer3D _mooPlayer;
        private Player _player;
        private double _mooCooldownLeft = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            AddToGroup("cows");
            _model = GetNodeOrNull<Node3D>("Model");
            _collision = GetNodeOrNull<CollisionShape3D>("Collision");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/cow.glb", ModelScale);
                PlayLoop(AnimIdle);
            }
            _homeCenter = float.IsNaN(HomeCenter.X) ? GlobalPosition : HomeCenter;
            _wanderTarget = GlobalPosition;
            ApplyGrowthVisual();

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.15f);

            _mooPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/cow_moo.mp3"),
                MaxDistance = 300f,
                UnitSize = 12f
            };
            AddChild(_mooPlayer);

            GameManager.Instance.HourChanged += OnHourChanged;
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        // Ap dung kich thuoc that su (than + va cham) theo tuoi - bo con nho hon, lon dan tung
        // ngay. Chi doi Scale cua container "Model"/"Collision" (khong dung cham den ModelScale
        // rieng cua Body ben trong) nen khong anh huong toi cach CharacterRig.Attach hoat dong.
        private void ApplyGrowthVisual()
        {
            float t = IsAdult ? 1f : Mathf.Clamp((float)_daysFed / GrowthDaysNeeded, 0f, 1f);
            float scale = Mathf.Lerp(BirthScaleFactor, 1f, t);
            if (_model != null) _model.Scale = Vector3.One * scale;
            if (_collision != null) _collision.Scale = Vector3.One * scale;
        }

        // Moi ngay THAT: bo con da an hom truoc (den mang gio 12h/16h) thi lon them 1 ngay-tuoi,
        // du ngay se thanh bo truong thanh (co the tu sinh san). Khong an ngay nao -> khong lon
        // ngay do, giong dong vat that "an moi lon".
        private void OnDayChanged(int day)
        {
            if (IsAdult) return;
            if (_ateToday) _daysFed++;
            _ateToday = false;
            ApplyGrowthVisual();
            if (_daysFed >= GrowthDaysNeeded) IsAdult = true;
        }

        // Nguoi choi lai gan (trong pham vi MooRange) -> keu "mooo" (BigSoundBank, CC0). Co
        // thoi gian nghi (MooCooldownSec) de khong keu lien tuc moi frame khi nguoi choi dung yen
        // canh ben, giong bo that chi thinh thoang keu chu khong keu lien tuc khong ngung.
        private void UpdateMoo(float dt)
        {
            if (_mooCooldownLeft > 0) _mooCooldownLeft -= dt;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (_player == null || _mooPlayer == null) return;

            if (_mooCooldownLeft <= 0 && GlobalPosition.DistanceTo(_player.GlobalPosition) <= MooRange)
            {
                // Be con keu cao hon (nghe "non not"), bo lon tieng tram binh thuong.
                _mooPlayer.PitchScale = IsAdult ? 1f : 1.5f;
                _mooPlayer.Play();
                _mooCooldownLeft = MooCooldownSec;
            }
        }

        private void OnHourChanged(int hour)
        {
            if ((hour == 12 || hour == 16) && _state != State.Eating)
                _state = State.GoToTrough;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            UpdateMoo(dt);

            // Lay HUONG MUON DEN (co the la huong nguoc voi than hien tai) + toc do di theo
            // trang thai. Muc tieu la 1 huong/toc do "mong muon", KHONG phai van toc that.
            var (desiredDir, targetSpeed) = _state switch
            {
                State.Wander => DoWander(),
                State.GoToTrough => DoGoToTrough(),
                // Eating: dung yen (targetSpeed=0) nhung van QUAY MAT ve phia mang, khong dung
                // yen o huong bat ky luc vua di toi - giong dong vat that luon huong dau ve
                // phia dang an.
                _ => (TroughDirOrZero(), 0f),
            };

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
            {
                // _facing la huong DI CHUYEN THAT (mot vector thuan, khong phu thuoc vao quy uoc
                // truoc/sau cua model) - xoay dan ve huong muon den (gioi han toc do quay), KHONG
                // gan tuc thi. Neu muc tieu o phia sau, huong nay se quay dan tai cho truoc (giong
                // dong vat that) thay vi truot lui ve phia do.
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);
            }

            // Model rieng co the duoc dung nguoc quy uoc "-Z la truoc" - FlipModelFacing bu lai
            // CHI o phan HINH ANH, khong dung lam huong di chuyen thuc su (_facing van la huong
            // that, dam bao than luon buoc dung huong no dang "nghi" toi bat ke model quay kieu gi).
            if (_model != null && _facing != Vector3.Zero)
            {
                var lookDir = FlipModelFacing ? -_facing : _facing;
                var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
                _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            }

            // Bo CHI buoc toi theo dung huong than dang huong toi (_facing), khong bao gio theo
            // duong thang truc tiep den muc tieu - vi vay khi doi huong gap, no re/vong lai thay
            // vi truot ngang hay di lui.
            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition);

            PlayLoop(horizontal.Length() > 3f ? AnimWalk : AnimIdle);
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
                // Lay ngau nhien theo toa do CUC (goc + ban kinh), KHONG phai X/Z doc lap - neu
                // random rieng X va Z trong [-half,half] thi goc cua vung do se xa tam toi
                // half*sqrt(2), co the vuot qua hang rao that. Toa do cuc dam bao khoang cach
                // toi tam KHONG BAO GIO vuot qua "half".
                float half = PastureHalfExtent;
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, half);
                _wanderTarget = _homeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // Nghi gam co lau hon di chuyen - bo that khong di lien tuc suot ngay.
                _nextWanderTime = now + (ulong)rng.RandiRange(5000, 12000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.4f * _speedJitter);
        }

        private (Vector3 dir, float speed) DoGoToTrough()
        {
            Vector3 dir = TroughPosition - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 16f)
            {
                _state = State.Eating;
                if (FarmStorage.Instance.TryRemove("thucan_giasuc", 1))
                {
                    _ateToday = true;
                    HungerDays = 0;
                }
                else
                {
                    HungerDays++;
                }
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
