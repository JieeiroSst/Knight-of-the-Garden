using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    public enum WildRole { Herbivore, Predator, Waterfowl, Fish }

    // Dong vat hoang da quanh ho/rung (huou/tho/cao/soi/vit/ca) - MOT script dung CHUNG cho tat
    // ca (giong Enemy.cs tai su dung it model cho nhieu loai quai qua tint mau), cau hinh rieng
    // qua [Export] luc Main.cs spawn (SpeciesId/Role/ModelPath/TintColor/PreySpeciesGroups...).
    // Dung Utility AI + GOAP CHUNG (UtilityAi.cs) nhu moi NPC trang trai - cham diem
    // doi/khat/nguy hiem MOI LAN quyet dinh, KHONG hard-code lich trinh gio co dinh. Moi ca the
    // hien dien la 1 MAU DAI DIEN cho quan the THAT (so luong, xem WaterEcosystem.cs) - bi san/
    // cau se tru vao quan the do, KHONG dem tung con.
    public partial class WildAnimal : CharacterBody3D
    {
        [Export] public string SpeciesId = "deer"; // khoa vao WaterEcosystem.Population
        [Export] public WildRole Role = WildRole.Herbivore;
        [Export] public string ModelPath;
        [Export] public float ModelScale = 1f;
        [Export] public string TintHex = ""; // rong = giu nguyen mau goc model (vd wolf.glb that)
        [Export] public bool FlipModelFacing = true;
        [Export] public string[] PreySpeciesGroups; // chi Predator dung, vd {"wild_rabbit"}

        [Export] public float Speed = 45f;
        [Export] public float FleeSpeedMult = 1.9f;
        [Export] public float Acceleration = 140f;
        [Export] public float Friction = 170f;
        [Export] public float TurnSpeed = 6f;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public float CatchRadius = 22f;
        [Export] public float DetectRadius = 260f;      // Predator: tam phat hien con moi
        [Export] public float FleeDetectRadius = 100f;  // Herbivore/Waterfowl: tam phat hien nguoi choi
        [Export] public bool SwimsOnWater = false;       // Waterfowl/Fish: khoa Y o mat nuoc/duoi nuoc
        [Export] public float WaterSurfaceY = 1.6f;

        // Main.cs gan ngay sau khi Instantiate (truoc AddChild).
        public Vector3 HomeCenter;
        public float RoamRadius = 400f;
        public Vector3 WaterEdgePos;

        private Vector3 _facing = Vector3.Back;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private readonly UtilityBrain _brain = new();
        private readonly RandomNumberGenerator _rng = new();

        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";

        // Doi/khat RIENG cua WildAnimal (khac Fatigue noi bo cua UtilityBrain) - cac UtilityAction
        // ben duoi doc truc tiep qua closure, dung tinh than "moi vai tro tu doc du lieu rieng".
        private float _hunger = 0f;
        private float _thirst = 0f;
        private Player _player;

        private static readonly string[] IdleCandidates =
            { "Armature|Idle", "AnimalArmature|Idle", "AnimalArmature|AnimalArmature|AnimalArmature|Idle", "Idle" };
        private static readonly string[] WalkCandidates =
            { "Armature|Walk", "Armature|WalkSlow", "AnimalArmature|Walk", "AnimalArmature|AnimalArmature|AnimalArmature|Walk", "Walk" };

        private static int WildHp(string species) => species switch { "deer" => 40, "wolf" => 50, "fox" => 25, "rabbit" => 10, "duck" => 8, _ => 20 };

        public override void _Ready()
        {
            // Giet thit / san ban (xem Huntable.cs) - con da bi giet o phien truoc thi khong hien lai.
            if (!Huntable.Attach(this, SpeciesId, WildHp(SpeciesId), farm: false)) return;
            AddToGroup("wild_animals");
            AddToGroup("wild_" + SpeciesId);

            _rng.Randomize();
            _model = new Node3D();
            AddChild(_model);
            if (!string.IsNullOrEmpty(ModelPath))
            {
                _animPlayer = CharacterRig.Attach(_model, ModelPath, ModelScale);
                if (!string.IsNullOrEmpty(TintHex))
                    Enemy.ApplyTint(_model, new Color(TintHex));
            }
            else
            {
                // Khong co model GLB phu hop trong asset hien co (vd Ca - khong tim duoc model
                // ca CC0 nao) - dung 1 khoi don gian thay the, cung tinh than "khong co asset phu
                // hop -> dung hinh nguyen thuy" da ap dung cho OreNode/mo da/bia mo.
                _model.AddChild(new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = 3.5f * ModelScale, Height = 12f * ModelScale },
                    RotationDegrees = new Vector3(90f, 0f, 0f),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = string.IsNullOrEmpty(TintHex) ? new Color(0.55f, 0.6f, 0.68f) : new Color(TintHex),
                        Roughness = 0.3f,
                    },
                });
            }

            switch (Role)
            {
                case WildRole.Herbivore:
                    _brain.Actions.Add(MakeGrazeAction());
                    _brain.Actions.Add(MakeDrinkAction());
                    _brain.Actions.Add(MakeFleeAction());
                    _brain.Actions.Add(UtilityPresets.MakeSleep(() => HomeCenter));
                    _brain.Actions.Add(UtilityPresets.MakeWander(() => HomeCenter, RoamRadius));
                    break;
                case WildRole.Predator:
                    // San "con moi dang di chuyen" can quyet dinh lai NHANH hon NPC binh thuong
                    // (mac dinh ~20s) de bam theo vi tri moi nhat, khong bam theo diem da cu.
                    _brain.DecisionIntervalSec = 3f;
                    _brain.Actions.Add(MakeHuntAction());
                    _brain.Actions.Add(MakeDrinkAction());
                    _brain.Actions.Add(UtilityPresets.MakeSleep(() => HomeCenter));
                    _brain.Actions.Add(UtilityPresets.MakeWander(() => HomeCenter, RoamRadius));
                    break;
                case WildRole.Waterfowl:
                    _brain.Actions.Add(MakeFleeAction());
                    _brain.Actions.Add(UtilityPresets.MakeSleep(() => HomeCenter));
                    _brain.Actions.Add(UtilityPresets.MakeWander(() => HomeCenter, RoamRadius, baseScore: 6f));
                    break;
                case WildRole.Fish:
                    _brain.Actions.Add(UtilityPresets.MakeWander(() => HomeCenter, RoamRadius, baseScore: 6f, pauseSec: 2f));
                    break;
            }
        }

        private Vector3 RandomPointNear(Vector3 center, float radius)
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            float r = _rng.RandfRange(0f, radius);
            return center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        // Diem tang theo do DOI - con vat cang doi cang uu tien tim an (giong y "SEARCH_FOOD ->
        // EAT" trong so do trang thai nguoi dung mo ta).
        private UtilityAction MakeGrazeAction()
        {
            return new UtilityAction
            {
                Id = "Graze",
                Evaluate = ctx => new UtilityResult(_hunger * 80f),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "ate", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "ate", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Eat", Effects = { { "ate", true } }, DurationSec = 3f,
                        TargetPos = (ctx, t) => RandomPointNear(HomeCenter, RoamRadius),
                        Execute = (ctx, t) => _hunger = 0f,
                    },
                },
            };
        }

        private UtilityAction MakeDrinkAction()
        {
            return new UtilityAction
            {
                Id = "Drink",
                Evaluate = ctx => new UtilityResult(_thirst * 70f),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "drank", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "drank", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "DrinkWater", Effects = { { "drank", true } }, DurationSec = 3f,
                        TargetPos = (ctx, t) => WaterEdgePos,
                        Execute = (ctx, t) => _thirst = 0f,
                    },
                },
            };
        }

        // Dung y "Player -> Detection -> Fear -> Flee/SWIM_AWAY" nguoi dung mo ta cho dan vit -
        // ap dung chung cho ca thu an co (huou/tho) vi ca hai deu la con moi tu nhien. Diem RAT
        // cao (300) de LUON thang moi nhu cau khac khi nguoi choi lai gan.
        private UtilityAction MakeFleeAction()
        {
            return new UtilityAction
            {
                Id = "Flee",
                Evaluate = ctx =>
                {
                    if (_player == null || !IsInstanceValid(_player))
                        _player = GetTree().GetFirstNodeInGroup("player") as Player;
                    if (_player == null) return new UtilityResult(float.NegativeInfinity);
                    float d = ctx.SelfPos.DistanceTo(_player.GlobalPosition);
                    return d <= FleeDetectRadius ? new UtilityResult(300f) : new UtilityResult(float.NegativeInfinity);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "safe", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "safe", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "RunAway", Effects = { { "safe", true } },
                        TargetPos = (ctx, t) =>
                        {
                            Vector3 away = ctx.SelfPos - (_player?.GlobalPosition ?? ctx.SelfPos);
                            away.Y = 0f;
                            if (away.LengthSquared() < 1f) away = Vector3.Forward;
                            return ctx.SelfPos + away.Normalized() * 200f;
                        },
                    },
                },
            };
        }

        // Predator: tim con moi GAN NHAT trong cac nhom PreySpeciesGroups con SONG trong tam
        // DetectRadius - Target la chinh con moi (Node), UtilityBrain.Decide() tu CLAIM no (2 ke
        // san khong the cung duoi 1 con moi nho co che NpcTaskBoard co san). Execute chi thuc su
        // "an" duoc neu con moi VAN con trong CatchRadius luc den noi (co the da tron mat).
        private UtilityAction MakeHuntAction()
        {
            return new UtilityAction
            {
                Id = "Hunt",
                Evaluate = ctx =>
                {
                    if (PreySpeciesGroups == null || PreySpeciesGroups.Length == 0)
                        return new UtilityResult(float.NegativeInfinity);
                    WildAnimal nearest = null;
                    float bestDist = DetectRadius;
                    foreach (var groupName in PreySpeciesGroups)
                    {
                        foreach (var n in GetTree().GetNodesInGroup(groupName))
                        {
                            if (n is WildAnimal prey && IsInstanceValid(prey))
                            {
                                float d = ctx.SelfPos.DistanceTo(prey.GlobalPosition);
                                if (d < bestDist) { bestDist = d; nearest = prey; }
                            }
                        }
                    }
                    if (nearest == null) return new UtilityResult(float.NegativeInfinity);
                    return new UtilityResult(_hunger * 90f, nearest);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "caught", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "caught", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "Chase", Effects = { { "caught", true } },
                        TargetPos = (ctx, t) => (t as WildAnimal)?.GlobalPosition ?? ctx.SelfPos,
                        Execute = (ctx, t) =>
                        {
                            if (t is WildAnimal prey && IsInstanceValid(prey)
                                && prey.GlobalPosition.DistanceTo(ctx.SelfPos) <= CatchRadius)
                            {
                                WaterEcosystem.Instance.OnPredation(prey.SpeciesId);
                                prey.Despawn();
                                _hunger = 0f;
                            }
                        },
                    },
                },
            };
        }

        public void Despawn() => QueueFree();

        private bool _simAsleep;

        // ==== Tan cong nguoi choi ====
        // Soi: hung du ve dem / Dem Trang Mau / khi bi danh -> lao vao can nguoi choi o gan (kem tieng gam).
        // Cao: chi can lai khi bi danh. Nguoi choi chay xa (> 2.5 lan tam phat hien) thi bo cuoc.
        private ulong _provokedUntil, _nextBite;
        private bool _aggro;

        public void Provoke() => _provokedUntil = Time.GetTicksMsec() + 30000;

        private bool AggroTick(float dt, out Vector3 dir, out float speed)
        {
            dir = Vector3.Zero; speed = 0f;
            if (SpeciesId != "wolf" && SpeciesId != "fox") return false;
            var p = GetTree().GetFirstNodeInGroup("player") as Player;
            if (p == null || p.IsDowned) { _aggro = false; return false; }
            bool provoked = Time.GetTicksMsec() < _provokedUntil;
            bool hostile = provoked || (SpeciesId == "wolf" && (GameManager.Instance.IsNight || HiepSiVeVuon.Systems.WorldEventsClient.BloodMoon));
            var to = p.GlobalPosition - GlobalPosition;
            to.Y = 0;
            float d = to.Length();
            float detect = SpeciesId == "wolf" ? 220f : 120f;
            if (!hostile || d > (_aggro ? detect * 2.5f : detect)) { _aggro = false; return false; }
            if (!_aggro) { _aggro = true; AnimalSounds.Play(this, "wolf_growl", SpeciesId == "fox" ? 1.5f : 1f); }
            ulong now = Time.GetTicksMsec();
            if (d < 38f)
            {
                if (now >= _nextBite)
                {
                    _nextBite = now + 1300;
                    int dmg = Mathf.RoundToInt((SpeciesId == "wolf" ? 7f : 3f) * Enemy.SeasonalMultiplier());
                    GameManager.Instance.TakeDamage(Mathf.Max(1, dmg - Inventory.Instance.GetArmorDefense() / 2));
                    AnimalSounds.Play(this, "wolf_bite", SpeciesId == "fox" ? 1.4f : 1f);
                }
                dir = to / Mathf.Max(d, 0.01f);
                return true; // dung sat can, xoay mat vao nguoi choi
            }
            dir = to / d;
            speed = Speed * FleeSpeedMult * 1.1f;
            return true;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (HiepSiVeVuon.Core.SimLod.Sleep(this, ref _simAsleep)) return; // xa nguoi choi -> ngu mo phong
            float dt = (float)delta;

            _hunger = Mathf.Min(1f, _hunger + dt / 600f);  // ~10 phut THAT toi da doi
            _thirst = Mathf.Min(1f, _thirst + dt / 480f);  // ~8 phut THAT toi da khat

            float speed = _brain.CurrentActionId == "Flee" ? Speed * FleeSpeedMult : Speed;
            var (desiredDir, targetSpeed) = AggroTick(dt, out var aggroDir, out var aggroSpeed)
                ? (aggroDir, aggroSpeed)
                : _brain.Tick(dt, this, ArriveDist, speed, null, null);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _brain.IsSleeping && !wantsToMove, _facing, FlipModelFacing, TurnSpeed * dt);

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            if (SwimsOnWater)
            {
                Velocity = new Vector3(horizontal.X, 0f, horizontal.Z);
                MoveAndSlide();
                GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "WildAnimal:" + Name);
                var p = GlobalPosition; p.Y = WaterSurfaceY; GlobalPosition = p;
            }
            else
            {
                float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
                Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
                MoveAndSlide();
                GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "WildAnimal:" + Name);
            }

            PlayLoop(horizontal.Length() > 3f);
        }

        private void PlayLoop(bool moving)
        {
            if (_animPlayer == null) return;
            foreach (var name in moving ? WalkCandidates : IdleCandidates)
            {
                if (_animPlayer.HasAnimation(name))
                {
                    if (_currentAnim != name) { _animPlayer.Play(name); _currentAnim = name; }
                    return;
                }
            }
        }
    }
}
