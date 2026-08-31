using Godot;
using System.Linq;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Marcel - Tho sua chua trang trai. Theo doi cac FenceMarker (xem FenceMarker.cs) rai khap
    // trang trai, ai bi hao mon nhieu nhat (Hp thap nhat) se duoc uu tien sua truoc. Quy trinh
    // dung THEO DUNG yeu cau: Nhan nhiem vu (quet tim cho hu hong nhat) -> Lay go (di toi dong
    // go) -> Lay bua (di toi khu dung cu) -> Di den hang rao (di toi FenceMarker do) -> Sua (dung
    // lai 1 luc, hoi phuc Hp) -> Ve kho (di ve nha kho) -> lap lai.
    public partial class RepairmanNpc : NPC
    {
        private enum State { Idle, FetchWood, PausingAtWood, FetchHammer, PausingAtHammer, WalkToFence, Repairing, ReturnHome }

        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 180f;
        [Export] public float Friction = 220f;
        [Export] public float TurnSpeed = 6.5f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public int WorkStartHour = 6;
        [Export] public int WorkEndHour = 20;
        [Export] public float ArriveDist = 16f;
        [Export] public double FetchPauseSec = 3.0;
        [Export] public double RepairDurationSec = 6.0;
        [Export] public int RepairAmount = 45;
        [Export] public int NeedsRepairThreshold = 70; // Hp duoi nguong nay moi coi la "can sua"
        [Export] public double CheckIntervalSec = 5.0;

        // Main.cs gan ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;         // truoc cua nha kho (ngoai troi)
        public Vector3 InteriorHomePos; // phong noi that that su - noi ngu ban dem
        public Vector3 WoodpilePos;
        public Vector3 ToolAreaPos;

        private State _state = State.Idle;
        private bool _onDuty;
        private FenceMarker _target;
        private double _pauseLeft;
        private double _checkCooldown;
        private Vector3 _facing = Vector3.Back;

        public override void _Ready()
        {
            base._Ready();

            int hour = GameManager.Instance.Hour;
            _onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            GlobalPosition = _onDuty ? HomePos : InteriorHomePos + Vector3.Up * 8f;
            _state = State.Idle;

            GameManager.Instance.HourChanged += OnHourChanged;
        }

        private void OnHourChanged(int hour)
        {
            bool onDuty = hour >= WorkStartHour && hour < WorkEndHour;
            if (onDuty == _onDuty) return;
            _onDuty = onDuty;

            if (onDuty)
            {
                GlobalPosition = HomePos;
                _state = State.Idle;
            }
            else
            {
                GlobalPosition = InteriorHomePos + Vector3.Up * 8f;
                _state = State.Idle;
                _target = null;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _onDuty ? DoWork(dt) : (Vector3.Zero, 0f);

            bool wantsToMove = desiredDir != Vector3.Zero;
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

        private (Vector3 dir, float speed) DoWork(float dt)
        {
            switch (_state)
            {
                case State.Idle:
                    _checkCooldown -= dt;
                    if (_checkCooldown <= 0)
                    {
                        _checkCooldown = CheckIntervalSec;
                        var worst = GetTree().GetNodesInGroup("fence_markers")
                            .OfType<FenceMarker>()
                            .Where(f => IsInstanceValid(f))
                            .OrderBy(f => f.Hp)
                            .FirstOrDefault();
                        if (worst != null && worst.Hp < NeedsRepairThreshold)
                        {
                            _target = worst;
                            _state = State.FetchWood;
                        }
                    }
                    return (Vector3.Zero, 0f);

                case State.FetchWood:
                    return GoTo(WoodpilePos, State.PausingAtWood, () => _pauseLeft = FetchPauseSec);

                case State.PausingAtWood:
                    _pauseLeft -= dt;
                    if (_pauseLeft <= 0) _state = State.FetchHammer;
                    return (Vector3.Zero, 0f);

                case State.FetchHammer:
                    return GoTo(ToolAreaPos, State.PausingAtHammer, () => _pauseLeft = FetchPauseSec);

                case State.PausingAtHammer:
                    _pauseLeft -= dt;
                    if (_pauseLeft <= 0) _state = State.WalkToFence;
                    return (Vector3.Zero, 0f);

                case State.WalkToFence:
                {
                    if (_target == null || !IsInstanceValid(_target)) { _state = State.ReturnHome; return (Vector3.Zero, 0f); }
                    Vector3 dir = _target.GlobalPosition - GlobalPosition;
                    dir.Y = 0f;
                    if (dir.Length() <= ArriveDist)
                    {
                        _state = State.Repairing;
                        _pauseLeft = RepairDurationSec;
                        return (Vector3.Zero, 0f);
                    }
                    return (dir.Normalized(), Speed);
                }

                case State.Repairing:
                    _pauseLeft -= dt;
                    if (_pauseLeft <= 0)
                    {
                        _target?.Repair(RepairAmount);
                        _target = null;
                        _state = State.ReturnHome;
                    }
                    return (Vector3.Zero, 0f);

                case State.ReturnHome:
                {
                    Vector3 dir = HomePos - GlobalPosition;
                    dir.Y = 0f;
                    if (dir.Length() <= ArriveDist)
                    {
                        _state = State.Idle;
                        _checkCooldown = 0; // kiem tra ngay khi vua ve, khong doi them
                        return (Vector3.Zero, 0f);
                    }
                    return (dir.Normalized(), Speed);
                }

                default:
                    return (Vector3.Zero, 0f);
            }
        }

        private (Vector3 dir, float speed) GoTo(Vector3 target, State nextState, System.Action onArrive)
        {
            Vector3 dir = target - GlobalPosition;
            dir.Y = 0f;
            if (dir.Length() <= ArriveDist)
            {
                onArrive();
                _state = nextState;
                return (Vector3.Zero, 0f);
            }
            return (dir.Normalized(), Speed);
        }
    }
}
