using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Ga 3D that (Quaternius, CC0) tu do di lai (mo) trong pham vi chuong ga. Den gio an (12h
    // trua va 16h chieu, theo DONG HO THAT cua may tinh qua GameManager.HourChanged - dong bo
    // voi bo/ngua) se tu dong di den cho thuc an, dung mo (Idle_Peck) 1 luc roi quay lai di lai
    // binh thuong.
    public partial class Chicken : CharacterBody3D, IHungryAnimal
    {
        private enum State { Wander, GoToFeed, Eating }

        [Export] public float Speed = 32f;
        [Export] public float Acceleration = 140f;
        [Export] public float Friction = 180f;
        [Export] public float TurnSpeed = 8f;
        // Nhu Cow/Cat/Dog - model Quaternius thuong nguoc quy uoc "-Z la truoc" cua Godot.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 7.84f; // tang 100% (3.92 -> 7.84)
        [Export] public double EatDurationSec = 60.0;
        [Export] public float ClusterRange = 90f;
        [Export] public float ClusterCooldownSec = 9f;
        // De trung: khoang cach thoi gian NGAU NHIEN (khong co gio co dinh, khac lich an 12h/16h)
        // - moi con ga lech pha rieng (gan luc _Ready) de khong ca dan cung de 1 luc.
        [Export] public double EggLayIntervalMinSec = 75.0;
        [Export] public double EggLayIntervalMaxSec = 150.0;
        // Ga de trung LIEN TUC nen "sinh san" khong hop dung nghia bang cach ghep doi nhu Cow/
        // Sheep/Pig/Horse - thay vao do, moi lan den luc de trung, co 1 XAC SUAT NHO trung do NO
        // luon (khong tha vat pham) thay vi de ra ngoai (xem UpdateEggLaying/Main.TryBreedChickens
        // - can >=2 ga TRUONG THANH trong CUNG chuong moi co co hoi nay).
        [Export] public float HatchChance = 0.1f;

        // Sinh san & lon len (mau Cow.cs) - ga con no ra tu trung, bat dau nho va CAN AN MOI
        // NGAY (den mang gio 12h/16h) de lon dan qua tung NGAY THAT.
        [Export] public bool IsAdult = true;
        [Export] public float BirthScaleFactor = 0.45f;
        [Export] public int GrowthDaysNeeded = 3;
        private int _daysFed = 0;
        private bool _ateToday = false;
        private CollisionShape3D _collision;

        // Xem ghi chu chi tiet trong Cow.cs - cung 1 co che doi THAT.
        public int HungerDays { get; private set; } = 0;
        public bool IsHungry => HungerDays > 0;

        // Ten hoat canh dung "AnimalArmature|AnimalArmature|AnimalArmature|X" (lap 3 lan) dung
        // theo dung ten that trong file chicken.glb - "Idle_Peck" la dang mo dat, dung vua vac
        // cho luc ga an tai mang thuc an.
        private const string AnimPrefix = "AnimalArmature|AnimalArmature|AnimalArmature|";
        private const string AnimIdle = AnimPrefix + "Idle";
        private const string AnimPeck = AnimPrefix + "Idle_Peck";
        private const string AnimRun = AnimPrefix + "Run";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        private State _state = State.Wander;
        private Vector3 _homeCenter;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private Vector3 _facing = Vector3.Back;
        private float _speedJitter = 1f;

        // Vi tri cho thuc an - Main.cs gan ngay sau khi tao.
        public Vector3 FeedPosition;
        // Tam that cua khu chuong (Main.cs gan = tam hang rao that) + nua be rong an toan, dam
        // bao ga khong bao gio wander ra ngoai hang rao (giong Cow.cs/Horse.cs).
        public Vector3 HomeCenter = new(float.NaN, 0, float.NaN);
        public float PastureHalfExtent = 999999f;

        private AudioStreamPlayer3D _clusterPlayer;
        private Player _player;
        private double _clusterCooldownLeft = 0;
        private double _eggCooldownLeft = 0;

        private readonly HiepSiVeVuon.Core.SteeringUtil.StuckDetector _stuckDetector = new();

        public override void _Ready()
        {
            AddToGroup("chickens");
            _model = GetNodeOrNull<Node3D>("Model");
            _collision = GetNodeOrNull<CollisionShape3D>("Collision");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/chicken.glb", ModelScale);
                PlayLoop(AnimIdle);
            }
            _homeCenter = float.IsNaN(HomeCenter.X) ? GlobalPosition : HomeCenter;
            _wanderTarget = GlobalPosition;
            ApplyGrowthVisual();

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.8f, 1.2f);
            _eggCooldownLeft = rng.RandfRange((float)EggLayIntervalMinSec, (float)EggLayIntervalMaxSec);

            _clusterPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/chicken_cluck.mp3"),
                MaxDistance = 220f,
                UnitSize = 8f
            };
            AddChild(_clusterPlayer);

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
                _state = State.GoToFeed;
        }

        private void UpdateCluck(float dt)
        {
            if (_clusterCooldownLeft > 0) _clusterCooldownLeft -= dt;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (_player == null || _clusterPlayer == null) return;

            if (_clusterCooldownLeft <= 0 && GlobalPosition.DistanceTo(_player.GlobalPosition) <= ClusterRange)
            {
                _clusterPlayer.Play();
                _clusterCooldownLeft = ClusterCooldownSec;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            UpdateCluck(dt);
            UpdateEggLaying(dt);

            var (desiredDir, targetSpeed) = _state switch
            {
                State.Wander => DoWander(),
                State.GoToFeed => DoGoToFeed(),
                _ => (FeedDirOrZero(), 0f), // Eating: dung yen, quay mo ve phia thuc an
            };

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

            // Chicken.glb khong co "Walk" - chi co Idle/Idle_Peck/Run, nen dung Run cho moi kieu
            // di chuyen (ga di rat nhanh/hay chay giat cuc, hop voi dang "Run" hon la mo phong
            // buoc cham).
            PlayLoop(_state == State.Eating ? AnimPeck : horizontal.Length() > 3f ? AnimRun : AnimIdle);
        }

        // De 1 qua trung (DroppedItem "egg") ngay tai vi tri hien tai, dinh ky ngau nhien - NPC
        // cham ga (PoultryKeeperNpc) se di thu hoach roi cat vao kho nong san (xem
        // FarmStorage), nguoi choi cung co the tu nhat truoc neu den kip.
        private void UpdateEggLaying(float dt)
        {
            _eggCooldownLeft -= dt;
            if (_eggCooldownLeft > 0) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            // "Sinh san": ga TRUONG THANH, dang o CUNG chuong (HomeCenter trung) co >=2 con
            // truong thanh -> co 1 co hoi nho trung do NO thanh ga con thay vi de ra ngoai.
            if (IsAdult && rng.Randf() < HatchChance && CountAdultsInSameCoop() >= 2)
            {
                HatchChick();
            }
            else
            {
                DroppedItem.Spawn(GetTree().CurrentScene, GlobalPosition, "egg", 1);
            }
            _eggCooldownLeft = rng.RandfRange((float)EggLayIntervalMinSec, (float)EggLayIntervalMaxSec);
        }

        private int CountAdultsInSameCoop()
        {
            int count = 0;
            foreach (var node in GetTree().GetNodesInGroup("chickens"))
            {
                if (node is Chicken c && IsInstanceValid(c) && c.IsAdult && c._homeCenter.DistanceSquaredTo(_homeCenter) < 4f)
                    count++;
            }
            return count;
        }

        // No 1 ga con ngay tai cho (khong tha vat pham trung lan nay) - ga con dung CHUNG
        // chuong/mang an voi ga me, tu lon len qua OnDayChanged nhu cac loai khac.
        private void HatchChick()
        {
            var scene = GD.Load<PackedScene>("res://scenes/Chicken.tscn");
            if (scene == null) return;
            var chick = scene.Instantiate<Chicken>();
            chick.Position = GlobalPosition + new Vector3((float)GD.RandRange(-10, 10), 0, (float)GD.RandRange(-10, 10));
            chick.FeedPosition = FeedPosition;
            chick.HomeCenter = HomeCenter;
            chick.PastureHalfExtent = PastureHalfExtent;
            chick.IsAdult = false;
            GetParent()?.AddChild(chick);
        }

        private Vector3 FeedDirOrZero()
        {
            Vector3 dir = FeedPosition - GlobalPosition;
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
                // Dung DUNG PastureHalfExtent - truoc day gioi han bang 1 WanderRadius nho co
                // dinh (85) trong khi chuong ga da duoc mo rong nhieu lan qua cac lan tang kich
                // thuoc (PastureHalfExtent thuc te co the toi 160), khien GAN NUA vanh ngoai
                // chuong (gan hang rao/cong, dung noi nguoi choi buoc vao) LUON LUON khong co con
                // ga nao - day rat co the la nguyen nhan chinh khien chuong ga "nhin nhu trong".
                float half = PastureHalfExtent;
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(0f, half);
                _wanderTarget = _homeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // Ga di tung doan ngan roi dung mo lung tung, khong di lien tuc.
                _nextWanderTime = now + (ulong)rng.RandiRange(2500, 6000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 8f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), Speed * 0.5f * _speedJitter);
        }

        private (Vector3 dir, float speed) DoGoToFeed()
        {
            Vector3 dir = FeedPosition - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 14f)
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
