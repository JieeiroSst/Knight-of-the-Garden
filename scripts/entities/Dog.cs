using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cho 3D that (Quaternius Shiba Inu, CC0) luon di theo nguoi choi: di bo binh thuong khi
    // con gan, chay nhanh (Gallop) khi bi bo lai qua xa, dung yen/ngoi cho khi da o sat ben.
    public partial class Dog : CharacterBody3D
    {
        private enum State { Idle, Walk, Run }

        [Export] public float WalkSpeed = 70f;
        [Export] public float RunSpeed = 160f;
        [Export] public float Acceleration = 260f;
        [Export] public float Friction = 320f;
        [Export] public float TurnSpeed = 8f;
        // Nhu Cow/Player - model Quaternius thuong nguoc quy uoc "-Z la truoc" cua Godot.
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 3.6f; // tang 20% (3 -> 3.6)
        [Export] public float StopDistance = 45f;   // gan hon khoang nay thi dung lai/ngoi cho
        [Export] public float RunDistance = 220f;    // xa hon khoang nay thi chay nhanh de duoi kip
        // Sau 12h dem (0h) cho tu dong ve chuong ngu, den 6h sang moi lai theo nguoi choi -
        // dong bo dong ho may tinh THAT, giong lich cua den duong/nguoi cham bo.
        [Export] public int SleepStartHour = 0;
        [Export] public int SleepEndHour = 6;
        [Export] public float KennelStopDistance = 18f;
        // Cho luon o gan nguoi choi nen sua theo KHOANG THOI GIAN ngau nhien (khong theo
        // khoang cach - luc nao cung "gan" nen se sua lien tuc neu dung khoang cach nhu bo).
        [Export] public double BarkMinIntervalSec = 18.0;
        [Export] public double BarkMaxIntervalSec = 40.0;

        private const string AnimIdle = "AnimalArmature|Idle";
        private const string AnimWalk = "AnimalArmature|Walk";
        private const string AnimRun = "AnimalArmature|Gallop";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        private Node3D _target;
        private Vector3 _facing = Vector3.Back;
        private bool _sleeping = false;
        private AudioStreamPlayer3D _barkPlayer;
        private double _barkCooldown = 0;

        // Main.cs gan vi tri chuong cho ngay sau khi tao (truoc AddChild).
        public Vector3 KennelPos;

        public override void _Ready()
        {
            AddToGroup("companions");
            _model = GetNodeOrNull<Node3D>("Model");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/dog.glb", ModelScale);
                PlayLoop(AnimIdle);
            }

            _sleeping = IsSleepHour(GameManager.Instance.Hour);
            GameManager.Instance.HourChanged += OnHourChanged;

            // Tieng sua (BigSoundBank, CC0) - keu ngau nhien theo chu ky, khong phai theo
            // khoang cach vi cho nay luon bam sat nguoi choi.
            _barkPlayer = new AudioStreamPlayer3D
            {
                Stream = GD.Load<AudioStream>("res://assets/sfx/dog_bark.mp3"),
                MaxDistance = 250f,
                UnitSize = 10f
            };
            AddChild(_barkPlayer);
            ResetBarkTimer();
        }

        private void ResetBarkTimer()
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _barkCooldown = rng.RandfRange((float)BarkMinIntervalSec, (float)BarkMaxIntervalSec);
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
            if (_target == null || !IsInstanceValid(_target))
                _target = GetTree().GetFirstNodeInGroup("player") as Node3D;

            _barkCooldown -= dt;
            if (_barkCooldown <= 0 && _barkPlayer != null)
            {
                _barkPlayer.Play();
                ResetBarkTimer();
            }

            Vector3 desiredDir = Vector3.Zero;
            float targetSpeed = 0f;
            State state = State.Idle;

            if (_sleeping)
            {
                // Qua 12h dem: bo qua nguoi choi, tu di ve chuong va nam ngu o do cho toi 6h sang.
                Vector3 toKennel = KennelPos - GlobalPosition;
                toKennel.Y = 0f;
                if (toKennel.Length() > KennelStopDistance)
                {
                    desiredDir = toKennel.Normalized();
                    targetSpeed = WalkSpeed;
                    state = State.Walk;
                }
            }
            else if (_target != null)
            {
                Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
                toTarget.Y = 0f;
                float dist = toTarget.Length();

                if (dist > StopDistance)
                {
                    desiredDir = toTarget.Normalized();
                    bool running = dist > RunDistance;
                    targetSpeed = running ? RunSpeed : WalkSpeed;
                    state = running ? State.Run : State.Walk;
                }
            }

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
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition);

            PlayLoop(state switch { State.Run => AnimRun, State.Walk => AnimWalk, _ => AnimIdle });
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
