using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cho 3D chay rong trong nong trai (khac voi Dog.cs - con luon bam theo nguoi choi): tu do
    // di lai ngau nhien quanh 1 khu vuc, va sau 12h dem den 6h sang thi tu dong ve chuong ngu
    // (dong bo dong ho may tinh THAT, giong lich cua Dog.cs/den duong/nguoi cham bo).
    public partial class FarmDog : CharacterBody3D
    {
        [Export] public float WalkSpeed = 55f;
        [Export] public float Acceleration = 220f;
        [Export] public float Friction = 260f;
        [Export] public float TurnSpeed = 7f;
        // Nhu Cow/Player/Dog - model Quaternius thuong nguoc quy uoc "-Z la truoc" cua Godot.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 3.6f;
        [Export] public float WanderRadius = 300f;
        [Export] public int SleepStartHour = 0;
        [Export] public int SleepEndHour = 6;
        [Export] public float KennelStopDistance = 22f;
        [Export] public float BarkRange = 130f;
        [Export] public float BarkCooldownSec = 12f;

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public string ModelPath = "res://assets3d/quaternius/animals/dog.glb";
        public Vector3 HomeCenter;
        public Vector3 KennelPos;

        private const string AnimIdle = "AnimalArmature|Idle";
        private const string AnimWalk = "AnimalArmature|Walk";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";
        private Vector3 _facing = Vector3.Back;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private bool _sleeping = false;
        private float _speedJitter = 1f;
        private AudioStreamPlayer3D _barkPlayer;
        private Node3D _player;
        private double _barkCooldownLeft = 0;

        public override void _Ready()
        {
            AddToGroup("farm_dogs");
            _model = GetNodeOrNull<Node3D>("Model");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, ModelPath, ModelScale);
                PlayLoop(AnimIdle);
            }
            if (HomeCenter == Vector3.Zero) HomeCenter = GlobalPosition;
            _wanderTarget = GlobalPosition;

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.2f);

            _sleeping = IsSleepHour(GameManager.Instance.Hour);
            GameManager.Instance.HourChanged += OnHourChanged;

            // Tieng sua (BigSoundBank, CC0) - sua khi nguoi choi lai gan, giong co che "mooo"
            // cua Cow.cs.
            _barkPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/dog_bark.mp3"),
                MaxDistance = 250f,
                UnitSize = 10f
            };
            AddChild(_barkPlayer);
        }

        private void UpdateBark(float dt)
        {
            if (_barkCooldownLeft > 0) _barkCooldownLeft -= dt;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player == null || _barkPlayer == null) return;

            if (_barkCooldownLeft <= 0 && GlobalPosition.DistanceTo(_player.GlobalPosition) <= BarkRange)
            {
                _barkPlayer.Play();
                _barkCooldownLeft = BarkCooldownSec;
            }
        }

        private bool IsSleepHour(int hour) => hour >= SleepStartHour && hour < SleepEndHour;

        private void OnHourChanged(int hour)
        {
            if (hour == SleepStartHour) _sleeping = true;
            else if (hour == SleepEndHour) _sleeping = false;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            UpdateBark(dt);
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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "FarmDog:" + Name);

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
                _nextWanderTime = now + (ulong)rng.RandiRange(3000, 8000);
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
