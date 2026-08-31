using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Heo 3D that (Quaternius, CC0) tu do di lai/lan trong bun trong pham vi chuong. Den gio an
    // (12h trua va 16h chieu, dong bo dong ho THAT giong Cow.cs) se tu dong di den mang thuc an,
    // dung "Idle_Eating" (co san trong model) luc an cho dung dang.
    public partial class Pig : CharacterBody3D
    {
        private enum State { Wander, GoToTrough, Eating }

        [Export] public float Speed = 34f;
        [Export] public float Acceleration = 130f;
        [Export] public float Friction = 160f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ModelScale = 4.4f;
        [Export] public double EatDurationSec = 90.0;

        private const string AnimPrefix = "AnimalArmature|AnimalArmature|AnimalArmature|";
        private const string AnimIdle = AnimPrefix + "Idle";
        private const string AnimWalk = AnimPrefix + "Walk";
        private const string AnimEating = AnimPrefix + "Idle_Eating";

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        private State _state = State.Wander;
        private Vector3 _homeCenter;
        private Vector3 _wanderTarget;
        private ulong _nextWanderTime = 0;
        private Vector3 _facing = Vector3.Back;
        private float _speedJitter = 1f;

        public Vector3 TroughPosition;
        public Vector3 HomeCenter = new(float.NaN, 0, float.NaN);
        public float PastureHalfExtent = 999999f;

        public override void _Ready()
        {
            AddToGroup("pigs");
            _model = GetNodeOrNull<Node3D>("Model");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, "res://assets3d/quaternius/animals/pig.glb", ModelScale);
                PlayLoop(AnimIdle);
            }
            _homeCenter = float.IsNaN(HomeCenter.X) ? GlobalPosition : HomeCenter;
            _wanderTarget = GlobalPosition;

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _speedJitter = rng.RandfRange(0.85f, 1.15f);

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            if ((hour == 12 || hour == 16) && _state != State.Eating)
                _state = State.GoToTrough;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _state switch
            {
                State.Wander => DoWander(),
                State.GoToTrough => DoGoToTrough(),
                _ => (TroughDirOrZero(), 0f),
            };

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

            PlayLoop(_state == State.Eating ? AnimEating : horizontal.Length() > 3f ? AnimWalk : AnimIdle);
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
                // Dung DUNG PastureHalfExtent - khong con gioi han bang 1 WanderRadius nho co
                // dinh nua (xem ghi chu tuong tu trong Horse.cs).
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
            if (dir.Length() <= 16f)
            {
                _state = State.Eating;
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
