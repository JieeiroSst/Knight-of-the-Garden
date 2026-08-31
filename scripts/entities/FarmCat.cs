using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Meo 3D that (Quaternius, CC0) tu do chay rong quanh nong trai - giong FarmDog.cs nhung
    // nho hon/cham hon va co tieng "meo" rieng. Sau 12h dem den 6h sang tu dong ve chuong meo
    // ngu (dong bo dong ho may tinh THAT, cung lich voi cho/den duong/nguoi cham bo).
    public partial class FarmCat : CharacterBody3D
    {
        [Export] public float WalkSpeed = 42f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 3f;
        [Export] public float WanderRadius = 260f;
        [Export] public int SleepStartHour = 0;
        [Export] public int SleepEndHour = 6;
        [Export] public float KennelStopDistance = 16f;
        [Export] public float MeowRange = 90f;
        [Export] public float MeowCooldownSec = 14f;

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 HomeCenter;
        public Vector3 KennelPos;

        // Ten hoat canh dung "AnimalArmature|AnimalArmature|AnimalArmature|X" (lap 3 lan) dung
        // theo dung ten that trong file cat.glb.
        private const string AnimPrefix = "AnimalArmature|AnimalArmature|AnimalArmature|";
        private const string AnimIdle = AnimPrefix + "Idle";
        private const string AnimWalk = AnimPrefix + "Walk";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private bool _sleeping = false;
        private float _speedJitter = 1f;
        private AudioStreamPlayer3D _meowPlayer;
        private Node3D _player;
        private double _meowCooldownLeft = 0;

        public override void _Ready()
        {
            AddToGroup("farm_cats");
            _model = GetNodeOrNull<Node3D>("Model");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/cat.glb", ModelScale);
                PlayLoop(AnimIdle);
            }
            if (HomeCenter == Vector3.Zero) HomeCenter = GlobalPosition;
            _wanderTarget = GlobalPosition;

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.2f);

            _sleeping = IsSleepHour(GameManager.Instance.Hour);
            GameManager.Instance.HourChanged += OnHourChanged;

            // Tieng "meo" (BigSoundBank, CC0) khi nguoi choi lai gan.
            _meowPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/cat_meow.mp3"),
                MaxDistance = 220f,
                UnitSize = 8f
            };
            AddChild(_meowPlayer);
        }

        private bool IsSleepHour(int hour) => hour >= SleepStartHour && hour < SleepEndHour;

        private void OnHourChanged(int hour)
        {
            if (hour == SleepStartHour) _sleeping = true;
            else if (hour == SleepEndHour) _sleeping = false;
        }

        private void UpdateMeow(float dt)
        {
            if (_meowCooldownLeft > 0) _meowCooldownLeft -= dt;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player == null || _meowPlayer == null) return;

            if (_meowCooldownLeft <= 0 && GlobalPosition.DistanceTo(_player.GlobalPosition) <= MeowRange)
            {
                _meowPlayer.Play();
                _meowCooldownLeft = MeowCooldownSec;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            UpdateMeow(dt);
            var (desiredDir, targetSpeed) = _sleeping ? DoGoToKennel() : DoWander();

            bool wantsToMove = desiredDir != Vector3.Zero;
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

            PlayLoop(horizontal.Length() > 3f ? AnimWalk : AnimIdle);
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
                _wanderTarget = HomeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // Meo nghi (nam lieme long) lau hon cho - it di lai lien tuc hon.
                _nextWanderTime = now + (ulong)rng.RandiRange(5000, 12000);
            }
            Vector3 dir = _wanderTarget - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= 10f) return (Vector3.Zero, 0f);
            return (dir.Normalized(), WalkSpeed * _speedJitter);
        }

        private (Vector3 dir, float speed) DoGoToKennel()
        {
            Vector3 dir = KennelPos - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= KennelStopDistance) return (Vector3.Zero, 0f);
            return (dir.Normalized(), WalkSpeed);
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
