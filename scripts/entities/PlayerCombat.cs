using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Cam giac hanh dong cua nhan vat (tach rieng khoi Player.cs):
    //  - Combo 3 don (co vu khi: chem - da - chem manh; tay khong: dam trai - dam phai - da), sat
    //    thuong ap dung DUNG khung hinh vung tay (khong phai luc vua bam), lao nhe ve truoc,
    //    tu xoay ve quai gan nhat, khung "dung hinh" ngan + rung camera khi trung.
    //  - Giu phim tan cong de danh lien tuc; bam som trong luc dang vung se duoc "nho" lai.
    //  - The thu kiem (Idle_Sword) vai giay sau khi danh.
    //  - Trung don: hoat canh HitRecieve, chop do man hinh, so sat thuong bay len.
    //  - Gục nga: hoat canh Death, khoa dieu khien, toi man hinh roi hoi sinh.
    //  - Lan ne (ky nang Luot), xoay vong (Chem Xoay), bui chan khi chay.
    public partial class Player
    {
        // Motion != null: dong tac tu dung (ArmPoseModifier) thay cho hoat canh co san.
        private record Swing(string Anim, float Speed, float DmgMult, float Lunge, float ArcSign, float Recovery, ArmPoseModifier.Motion Motion = null);

        private static readonly Swing[] WeaponCombo =
        {
            new("Sword_Slash", 1.35f, 1.0f, 120f, 1f, 0.36f),
            new("Kick_Left", 1.5f, 0.8f, 90f, -1f, 0.34f),
            // Don ket: nhay chem bo 2 tay tu tren dau xuong.
            new("Sword_Slash", 1.0f, 1.8f, 220f, 1f, 0.66f, Motions.Slam),
        };
        private static readonly Swing[] FistCombo =
        {
            new("Punch_Left", 1.5f, 1.0f, 90f, -1f, 0.3f),
            new("Punch_Right", 1.5f, 1.0f, 90f, 1f, 0.3f),
            new("Kick_Right", 1.15f, 1.6f, 170f, 1f, 0.55f),
        };

        // Ma hoat canh gui len gateway (RemotePlayer.cs dung cung bang nay).
        public static readonly string[] NetAnimRoles =
        {
            "Idle", "Walk", "Run", "Sword_Slash", "Punch_Right", "Interact", "Roll", "HitRecieve",
            "Death", "Kick_Right", "Punch_Left", "Wave", "Idle_Sword", "Kick_Left",
            "Motion:Overhead", "Motion:Dig", "Motion:Sweep", "Motion:Pour", "Motion:Bend", "Motion:Cast", "Motion:Slam",
        };

        private const ulong ComboResetMs = 900;
        private const float StanceSeconds = 4f;

        private int _comboStep;
        private ulong _comboExpire;
        private ulong _nextSwingAt;
        private bool _attackHeld;
        private bool _attackBuffered;
        private ulong _actionLockUntil;
        private float _lockMoveMult = 1f;
        private ulong _stanceUntil;
        private float _actionSpeed = 1f;
        private bool _dying;
        private float _shakeT, _shakeDur, _shakeAmp;
        private float _spinT, _spinDur;
        private int _hitAnimFlip;
        private CpuParticles3D _dust;
        private ColorRect _hurtRect;

        public bool IsDowned => _dying;

        private bool ActionLocked => Time.GetTicksMsec() < _actionLockUntil;

        private void SetupActionFeel()
        {
            GameManager.Instance.PlayerHurt += OnHurt;
            if (_fadeRect?.GetParent() is CanvasLayer layer)
            {
                _hurtRect = new ColorRect { Color = new Color(0.8f, 0.05f, 0.05f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
                _hurtRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                layer.AddChild(_hurtRect);
                layer.MoveChild(_hurtRect, 0);
            }
            _dust = new CpuParticles3D
            {
                Emitting = false, Amount = 14, Lifetime = 0.55f, LocalCoords = false,
                Direction = Vector3.Up, Spread = 60f, InitialVelocityMin = 6f, InitialVelocityMax = 14f,
                Gravity = new Vector3(0, -6f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f,
                Color = new Color(0.72f, 0.64f, 0.5f, 0.55f),
                Mesh = new SphereMesh
                {
                    Radius = 1.6f, Height = 3.2f, RadialSegments = 6, Rings = 3,
                    Material = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    },
                },
                Position = new Vector3(0, 1.5f, 0),
            };
            AddChild(_dust);
            if (_animPlayer != null && A("Idle_Sword") is string stance && _animPlayer.GetAnimation(stance) is Animation sa)
                sa.LoopMode = Animation.LoopModeEnum.Linear;
        }

        private void CleanupActionFeel()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerHurt -= OnHurt;
                GameManager.Instance.PlayerDied -= OnDied;
            }
        }

        private bool HasAnim(string role) => _animPlayer != null && _animPlayer.HasAnimation(A(role));

        // ==== Tan cong ====
        private void PressAttack()
        {
            _attackHeld = true;
            if (Time.GetTicksMsec() >= _nextSwingAt) TryAttack();
            else _attackBuffered = true;
        }

        // Goi moi khung hinh vat ly: giu phim / don da bam truoc -> danh tiep khi het hoi chieu.
        private bool _useHeld;

        private void PollAttack()
        {
            // Giu [Space]: vung nong cu lien tuc (xong nhat nay moi toi nhat sau).
            if (_useHeld && !Input.IsActionPressed("use_tool")) _useHeld = false;
            // Chi lap lai voi nong cu lam dat/tuoi/cat co - khong lap voi can cau hay do dat xuong dat.
            if (_useHeld && _armPose != null && !_armPose.Busy && !ChatUI.IsTyping
                && Inventory.Instance.EquippedTool is "hoe" or "cuoc_bac" or "cuoc_vang" or "xeng" or "cao_co" or "binh_tuoi")
                TryUseTool();
            if (_attackHeld && !Input.IsActionPressed("attack")) _attackHeld = false;
            if ((_attackHeld || _attackBuffered) && Time.GetTicksMsec() >= _nextSwingAt && !ChatUI.IsTyping)
            {
                _attackBuffered = false;
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (_dying) return;
            bool mounted = _mountedHorse != null || _mountedBoat != null;
            ulong now = Time.GetTicksMsec();
            if (now < _nextSwingAt) return;
            // Dang vung nong cu: cho xong roi moi danh (khong cat ngang nhat cuoc).
            if (_armPose != null && _armPose.Busy && _armPose.CurrentMotion != "TwistSlash") { _attackBuffered = true; return; }
            // Cam cuoc da + co quang truoc mat (khong co quai gan): bo cuoc da that su.
            if (!mounted && TryMineSwing()) return;

            bool armed = Inventory.Instance.EquippedWeapon != null;
            var combo = armed ? WeaponCombo : FistCombo;
            if (now > _comboExpire) _comboStep = 0;
            var swing = combo[_comboStep];
            bool finisher = _comboStep == combo.Length - 1;
            _comboStep = (_comboStep + 1) % combo.Length;

            float animLen = AnimLength(swing.Anim, 0.6f) / swing.Speed;
            float recovery = Mathf.Max(swing.Recovery, AttackCooldownMs / 1000f * (finisher ? 1.4f : 0.85f));
            _nextSwingAt = now + (ulong)(recovery * 1000f);
            _comboExpire = _nextSwingAt + ComboResetMs;
            _lastAttackTime = now;
            if (armed) _stanceUntil = now + (ulong)(StanceSeconds * 1000f);

            _lastActionWasAttack = true;
            RefreshHeldItem();

            if (!mounted)
            {
                AimAtNearestTarget();
                Velocity += _facing * swing.Lunge;
                LockMovement(Mathf.Min(animLen, recovery + 0.05f), 0.3f);
            }
            int dmg = Mathf.Max(1, Mathf.RoundToInt(Inventory.Instance.GetWeaponDamage() * swing.DmgMult));
            var facing = _facing;
            var trailColor = armed ? new Color(0.75f, 0.9f, 1f) : new Color(1f, 0.9f, 0.7f);

            if (swing.Motion != null && _armPose != null && !mounted)
            {
                // Don ket bang dong tac tu dung: sat thuong dung luc luoi kiem bo xuong.
                float speed = swing.Speed;
                _nextSwingAt = now + (ulong)(Mathf.Max(recovery, swing.Motion.Duration / speed) * 1000f);
                _comboExpire = _nextSwingAt + ComboResetMs;
                StartMotion(swing.Motion, speed, () =>
                {
                    if (_dying) return;
                    SpawnSlash(facing, swing.ArcSign, finisher, armed);
                    ApplyHit(facing, dmg, finisher);
                    Dust(GlobalPosition + facing * 34f, 14, new Color(0.7f, 0.62f, 0.5f));
                }, 0.25f);
                _heldItem?.StartTrail(swing.Motion.Duration * swing.Motion.Impact / speed + 0.04f, trailColor);
                return;
            }

            PlayAction(swing.Anim, swing.Speed, 0.06f);
            // Them lop xoay than (dong tac co suc nang) va vet luoi kiem cho nhat chem bang hoat canh.
            if (_armPose != null && armed && swing.Anim == "Sword_Slash") _armPose.Play(Motions.TwistSlash, Mathf.Max(1f, 0.45f / Mathf.Max(0.2f, animLen)));
            _heldItem?.StartTrail(Mathf.Clamp(animLen * 0.5f, 0.12f, 0.3f), trailColor);

            // Sat thuong roi vao khung hinh vung tay (~38% hoat canh), khong phai luc bam phim.
            float hitDelay = Mathf.Clamp(animLen * 0.38f, 0.06f, 0.24f);
            GetTree().CreateTimer(hitDelay).Timeout += () =>
            {
                if (!IsInstanceValid(this) || _dying) return;
                SpawnSlash(facing, swing.ArcSign, finisher, armed);
                ApplyHit(facing, dmg, finisher);
            };
        }

        private void ApplyHit(Vector3 facing, int dmg, bool finisher)
        {
            float reach = AttackRange * (finisher ? 1.35f : 1f);
            bool landed = false;

            // Dau truong: danh trung doi thu -> bao server (server tinh sat thuong + kiem tam/hoi chieu).
            if (DuelUI.ActiveDuelId != null && NetworkClient.Instance.FindRemote(DuelUI.OpponentId) is RemotePlayer foe)
            {
                var toFoe = foe.GlobalPosition - GlobalPosition;
                toFoe.Y = 0;
                if (toFoe.Length() <= reach + 40f) { NetworkClient.Instance.DuelHit(DuelUI.ActiveDuelId); landed = true; }
            }
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (node is Enemy enemy && IsInstanceValid(enemy) && !enemy.IsDead && InArc(enemy.GlobalPosition, facing, reach))
                {
                    enemy.TakeDamage(dmg);
                    Sparks(enemy.GlobalPosition + Vector3.Up * 18f, finisher);
                    landed = true;
                }
            }
            // Chat cay trong tam neu co (lay go) - ton the luc (giam theo cap Hai Luom).
            foreach (var node in GetTree().GetNodesInGroup("choppable_trees"))
            {
                if (node is Tree tree && IsInstanceValid(tree) && InArc(tree.GlobalPosition, facing, AttackRange) && SpendStamina(SkillType.Foraging, 2f))
                {
                    tree.Chop(dmg);
                    landed = true;
                }
            }
            // Dao quang (xem OreNode.cs) - dung suc Cuoc rieng, KHONG phai sat thuong vu khi.
            int pickPower = Inventory.Instance.GetToolPower();
            foreach (var node in GetTree().GetNodesInGroup("ore_nodes"))
            {
                if (node is OreNode ore && IsInstanceValid(ore) && InArc(ore.GlobalPosition, facing, AttackRange)
                    && ore.CanMineWith(pickPower) && SpendStamina(SkillType.Mining, 3f))
                {
                    ore.Mine(pickPower);
                    Sparks(ore.GlobalPosition + Vector3.Up * 10f, false);
                    landed = true;
                }
            }
            if (landed)
            {
                HitStop(finisher ? 0.09f : 0.05f);
                Shake(finisher ? 3.2f : 1.6f, finisher ? 0.22f : 0.12f);
            }
        }

        // ==== Dong tac tu dung (ArmPoseModifier) ====
        private ArmPoseModifier _armPose;

        // Chay 1 dong tac: dung yen tren Idle (khoa buoc chan), onImpact goi dung luc "cham".
        private void StartMotion(ArmPoseModifier.Motion m, float speed, System.Action onImpact, float moveMult = 0.15f)
        {
            if (_armPose == null) { onImpact?.Invoke(); return; }
            float dur = m.Duration / Mathf.Max(0.1f, speed);
            LockMovement(dur * 0.85f, moveMult);
            _armPose.Play(m, speed, () => { if (IsInstanceValid(this)) onImpact?.Invoke(); }, () => _actionPlaying = false);
            string idle = InStance && HasAnim("Idle_Sword") ? "Idle_Sword" : "Idle";
            if (HasAnim(idle))
            {
                _animPlayer.SpeedScale = 1f;
                _animPlayer.Play(A(idle), 0.12);
                _currentAnim = idle;
            }
            _actionPlaying = true;
            int code = System.Array.IndexOf(NetAnimRoles, "Motion:" + m.Name);
            if (code > 0)
            {
                NetAnimCode = code;
                _netActionUntil = Time.GetTicksMsec() + (ulong)(dur * 1000f);
            }
        }

        // Quay mat ve o dat / cay gan nhat truoc khi vung (khong can dung that thang hang).
        private void AimAtFarmTarget()
        {
            Node3D best = null;
            float bestD = 52f;
            foreach (var group in new[] { "farm_plots", "fruit_trees", "wheat_plants", "fruit_trees_placed" })
                foreach (Node n in GetTree().GetNodesInGroup(group))
                    if (n is Node3D t && IsInstanceValid(t))
                    {
                        float d = GlobalPosition.DistanceTo(t.GlobalPosition);
                        if (d < bestD && d > 4f) { bestD = d; best = t; }
                    }
            if (best != null) FaceToward(best);
        }

        private void ToolImpactFx(string tool, ArmPoseModifier.Motion m)
        {
            var ground = GlobalPosition + _facing * 36f + Vector3.Up * 2f;
            if (m == Motions.Overhead || m == Motions.Dig)
            {
                Dust(ground, m == Motions.Dig ? 22 : 16, new Color(0.45f, 0.32f, 0.2f));
                Shake(1.1f, 0.1f);
                HitStop(0.04f);
            }
            else if (m == Motions.Sweep)
            {
                Burst(ground + Vector3.Up * 4f, new Color(0.45f, 0.75f, 0.3f), 18, 60f, -120f, 1.1f);
            }
            else if (m == Motions.Pour)
            {
                _heldItem?.PourWater(m.Duration * 0.45f);
            }
            else if (m == Motions.Bend)
            {
                Dust(ground - _facing * 8f, 6, new Color(0.55f, 0.45f, 0.32f));
            }
        }

        // Cam cuoc da + quang trong tam: bo cuoc da qua dau thay vi chem kiem.
        private bool TryMineSwing()
        {
            string tool = Inventory.Instance.EquippedTool;
            if (_armPose == null || tool == null || !tool.StartsWith("pickaxe")) return false;
            int power = Inventory.Instance.GetToolPower();
            OreNode target = null;
            float best = AttackRange + 10f;
            foreach (Node n in GetTree().GetNodesInGroup("ore_nodes"))
                if (n is OreNode o && IsInstanceValid(o) && o.CanMineWith(power))
                {
                    float d = GlobalPosition.DistanceTo(o.GlobalPosition);
                    if (d < best) { best = d; target = o; }
                }
            if (target == null) return false;
            foreach (Node n in GetTree().GetNodesInGroup("enemies"))
                if (n is Enemy e && IsInstanceValid(e) && !e.IsDead && GlobalPosition.DistanceTo(e.GlobalPosition) < AttackRange + 30f) return false;

            _lastActionWasAttack = false;
            RefreshHeldItem();
            FaceToward(target);
            var m = Motions.Overhead;
            float speed = 1.1f;
            _nextSwingAt = Time.GetTicksMsec() + (ulong)(m.Duration / speed * 1000f);
            var ore = target;
            StartMotion(m, speed, () =>
            {
                if (!IsInstanceValid(ore) || _dying) return;
                if (SpendStamina(SkillType.Mining, 3f))
                {
                    ore.Mine(power);
                    Sparks(ore.GlobalPosition + Vector3.Up * 10f, false);
                    Dust(ore.GlobalPosition + Vector3.Up * 4f, 10, new Color(0.55f, 0.53f, 0.5f));
                    Shake(1.4f, 0.12f);
                    HitStop(0.05f);
                }
            });
            _heldItem?.StartTrail(m.Duration * m.Impact / speed + 0.04f, new Color(1f, 0.95f, 0.85f));
            return true;
        }

        private void Dust(Vector3 pos, int amount, Color color) =>
            Burst(pos, new Color(color, 0.85f), amount, 40f, -60f, 1.8f);

        private void Burst(Vector3 pos, Color color, int amount, float speed, float gravity, float size)
        {
            var p = new CpuParticles3D
            {
                OneShot = true, Emitting = true, Amount = amount, Lifetime = 0.5f, Explosiveness = 0.9f,
                Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = speed * 0.4f, InitialVelocityMax = speed,
                Gravity = new Vector3(0, gravity, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.3f, Color = color,
                Mesh = new SphereMesh
                {
                    Radius = size, Height = size * 2f, RadialSegments = 5, Rings = 3,
                    Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
                },
            };
            GetTree().CurrentScene.AddChild(p);
            p.GlobalPosition = pos;
            GetTree().CreateTimer(1.2).Timeout += () => { if (IsInstanceValid(p)) p.QueueFree(); };
        }

        private bool InArc(Vector3 target, Vector3 facing, float reach)
        {
            var to = target - GlobalPosition;
            to.Y = 0;
            float dist = to.Length();
            if (dist > reach) return false;
            return dist < 12f || facing.Dot(to / dist) > 0.25f;
        }

        // Tu xoay mat ve quai gan nhat (hoac doi thu dau truong) trong tam - khong can ngam chinh xac.
        private void AimAtNearestTarget()
        {
            float best = AttackRange + 35f;
            Vector3? dir = null;
            void Consider(Vector3 pos)
            {
                var to = pos - GlobalPosition;
                to.Y = 0;
                float d = to.Length();
                if (d < 1f || d > best) return;
                // Uu tien phia truoc: muc tieu phia sau lung phai gan hon moi duoc chon.
                if (_facing.Dot(to / d) < -0.2f && d > AttackRange * 0.6f) return;
                best = d;
                dir = to / d;
            }
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is Enemy e && IsInstanceValid(e) && !e.IsDead) Consider(e.GlobalPosition);
            if (DuelUI.ActiveDuelId != null && NetworkClient.Instance.FindRemote(DuelUI.OpponentId) is RemotePlayer foe)
                Consider(foe.GlobalPosition);
            if (dir is Vector3 d2) FaceInstantly(d2);
        }

        private void FaceInstantly(Vector3 dir)
        {
            dir.Y = 0;
            if (dir.LengthSquared() < 0.0001f) return;
            _facing = dir.Normalized();
            if (_model != null) _model.Basis = Basis.LookingAt(FlipModelFacing ? -_facing : _facing, Vector3.Up);
        }

        private void FaceToward(Node3D target)
        {
            if (target != null && IsInstanceValid(target)) FaceInstantly(target.GlobalPosition - GlobalPosition);
        }

        private void LockMovement(float seconds, float moveMult)
        {
            _actionLockUntil = Time.GetTicksMsec() + (ulong)(seconds * 1000f);
            _lockMoveMult = moveMult;
        }

        private float AnimLength(string role, float fallback) =>
            _animPlayer != null && _animPlayer.HasAnimation(A(role)) ? _animPlayer.GetAnimation(A(role)).Length : fallback;

        // "Dung hinh" rat ngan khi don trung - tao cam giac luc va cham ma khong lam cham ca the gioi.
        private void HitStop(float seconds)
        {
            if (_animPlayer == null) return;
            _animPlayer.SpeedScale = 0.05f;
            GetTree().CreateTimer(seconds).Timeout += () =>
            {
                if (IsInstanceValid(this) && _animPlayer != null && _actionPlaying) _animPlayer.SpeedScale = _actionSpeed;
            };
        }

        private void Shake(float amp, float dur)
        {
            if (amp < _shakeAmp * (_shakeT / Mathf.Max(0.001f, _shakeDur))) return;
            _shakeAmp = amp;
            _shakeDur = dur;
            _shakeT = dur;
        }

        private void UpdateShake(float dt)
        {
            if (_camera == null) return;
            if (_shakeT <= 0f)
            {
                if (_camera.HOffset != 0f || _camera.VOffset != 0f) { _camera.HOffset = 0f; _camera.VOffset = 0f; }
                return;
            }
            _shakeT -= dt;
            float k = _shakeAmp * Mathf.Max(0f, _shakeT / _shakeDur);
            _camera.HOffset = (float)GD.RandRange(-k, k);
            _camera.VOffset = (float)GD.RandRange(-k, k);
        }

        // ==== Hieu ung ====

        // Vet chem 3D hinh vong cung (thay sprite thanh kiem 2D phang truoc day).
        private void SpawnSlash(Vector3 facing, float sign, bool finisher, bool armed)
        {
            float span = Mathf.DegToRad(finisher ? 200f : 140f);
            float r0 = armed ? 14f : 10f, r1 = AttackRange * (finisher ? 1.25f : 0.95f);
            var color = armed ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.9f, 0.7f);
            var arc = BuildArc(r0, r1, span, color);
            AddChild(arc);
            float yaw = Mathf.Atan2(facing.X, facing.Z);
            arc.Position = Vector3.Up * 17f;
            // Nghieng vong cung theo chieu vung (trai/phai) cho 2 don lien tiep nhin khac nhau.
            arc.Basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, 0.25f * sign) * new Basis(Vector3.Up, -sign * 0.5f);
            var mat = (StandardMaterial3D)arc.MaterialOverride;
            var tw = arc.CreateTween().SetParallel();
            tw.TweenProperty(arc, "basis", arc.Basis * new Basis(Vector3.Up, sign * 0.9f), 0.16f);
            tw.TweenProperty(mat, "albedo_color:a", 0f, 0.22f).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(arc.QueueFree));
        }

        private static MeshInstance3D BuildArc(float r0, float r1, float span, Color color)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            const int seg = 16;
            for (int i = 0; i < seg; i++)
            {
                float a0 = -span / 2 + span * i / seg, a1 = -span / 2 + span * (i + 1) / seg;
                // Mo dan ve 2 dau vong cung.
                float f0 = Mathf.Sin(Mathf.Pi * i / seg), f1 = Mathf.Sin(Mathf.Pi * (i + 1) / seg);
                Vector3 P(float a, float r) => new(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                var c0 = new Color(color, f0); var c1 = new Color(color, f1);
                float m0 = r1 - (r1 - r0) * f0, m1 = r1 - (r1 - r0) * f1;
                st.SetColor(c0); st.AddVertex(P(a0, m0));
                st.SetColor(c0); st.AddVertex(P(a0, r1));
                st.SetColor(c1); st.AddVertex(P(a1, r1));
                st.SetColor(c0); st.AddVertex(P(a0, m0));
                st.SetColor(c1); st.AddVertex(P(a1, r1));
                st.SetColor(c1); st.AddVertex(P(a1, m1));
            }
            return new MeshInstance3D
            {
                Mesh = st.Commit(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled, AlbedoColor = new Color(1, 1, 1, 0.9f),
                },
            };
        }

        private void Sparks(Vector3 pos, bool big)
        {
            var color = big ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.95f, 0.75f);
            var p = new CpuParticles3D
            {
                OneShot = true, Emitting = true, Amount = big ? 22 : 10, Lifetime = 0.35f, Explosiveness = 1f,
                Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 40f, InitialVelocityMax = big ? 110f : 70f,
                Gravity = new Vector3(0, -200f, 0), ScaleAmountMin = 0.5f, ScaleAmountMax = 1f, Color = color,
                Mesh = new SphereMesh
                {
                    Radius = 0.9f, Height = 1.8f, RadialSegments = 4, Rings = 2,
                    Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, EmissionEnabled = true, Emission = color },
                },
            };
            GetTree().CurrentScene.AddChild(p);
            p.GlobalPosition = pos;
            GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(p)) p.QueueFree(); };
        }

        private void UpdateDust(float speedRatio)
        {
            if (_dust == null) return;
            bool on = !_dying && speedRatio >= 0.65f && IsOnFloor() && _mountedHorse == null && _mountedBoat == null
                && !WaterEcosystem.Instance.IsNearLake(GlobalPosition);
            if (_dust.Emitting != on) _dust.Emitting = on;
        }

        // ==== Ky nang (goi tu PlayerAbilities) ====

        // Lan ne theo huong dang bam phim (hoac huong dang nhin). Tra ve huong lan.
        public Vector3 PlayRoll()
        {
            var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var dir = input.LengthSquared() > 0.01f ? new Vector3(input.X, 0, input.Y).Normalized() : _facing;
            FaceInstantly(dir);
            float len = AnimLength("Roll", 0.5f);
            float speed = Mathf.Max(1f, len / 0.42f);
            LockMovement(len / speed, 0f);
            _nextSwingAt = Time.GetTicksMsec() + 250;
            PlayAction("Roll", speed, 0.05f);
            return dir;
        }

        // Chem Xoay: nhan vat xoay tron 1 vong + vong sang 360 do.
        public void PlaySpin()
        {
            _spinDur = 0.38f;
            _spinT = _spinDur;
            LockMovement(_spinDur, 0.2f);
            PlayAction(Inventory.Instance.EquippedWeapon != null ? "Sword_Slash" : "Kick_Right", 1.6f, 0.05f);
            var ring = BuildArc(20f, 80f, Mathf.Tau, new Color(1f, 0.85f, 0.45f));
            AddChild(ring);
            ring.Position = Vector3.Up * 14f;
            var mat = (StandardMaterial3D)ring.MaterialOverride;
            var tw = ring.CreateTween().SetParallel();
            tw.TweenProperty(ring, "rotation:y", Mathf.Tau, 0.35f);
            tw.TweenProperty(mat, "albedo_color:a", 0f, 0.4f);
            tw.Chain().TweenCallback(Callable.From(ring.QueueFree));
            Shake(2.4f, 0.18f);
        }

        public void PlayEmote(string role)
        {
            LockMovement(0.4f, 0.4f);
            PlayAction(role, 1f, 0.12f);
        }

        // Goc xoay them tren model khi dang Chem Xoay (cong vao huong nhin trong UpdateVisuals).
        private float SpinAngle(float dt)
        {
            if (_spinT <= 0f) return 0f;
            _spinT = Mathf.Max(0f, _spinT - dt);
            return Mathf.Tau * (1f - _spinT / _spinDur);
        }

        // ==== Trung don / guc nga ====
        private void OnHurt(int dmg)
        {
            if (_dying || !IsInstanceValid(this)) return;
            Shake(Mathf.Clamp(1f + dmg * 0.08f, 1f, 4f), 0.18f);
            if (_hurtRect != null)
            {
                _hurtRect.Color = new Color(_hurtRect.Color, Mathf.Clamp(0.12f + dmg * 0.01f, 0.12f, 0.35f));
                CreateTween().TweenProperty(_hurtRect, "color:a", 0f, 0.35f);
            }
            var pop = new Label3D
            {
                Text = "-" + dmg, Modulate = new Color(1f, 0.3f, 0.25f), OutlineSize = 6, FontSize = 40,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.35f, NoDepthTest = true,
                Position = new Vector3((float)GD.RandRange(-6, 6), 46f, 0),
            };
            AddChild(pop);
            var tw = pop.CreateTween().SetParallel();
            tw.TweenProperty(pop, "position:y", 62f, 0.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tw.TweenProperty(pop, "modulate:a", 0f, 0.6f).SetDelay(0.25f);
            tw.Chain().TweenCallback(Callable.From(pop.QueueFree));

            // Dang vung don/lan thi khong bi ngat (giu nhip combo); dung yen/di thi giat minh.
            if (ActionLocked || _mountedHorse != null || _mountedBoat != null) return;
            _hitAnimFlip ^= 1;
            string role = _hitAnimFlip == 0 || !HasAnim("HitRecieve_2") ? "HitRecieve" : "HitRecieve_2";
            if (!HasAnim(role)) return;
            LockMovement(0.25f, 0.5f);
            PlayAction(role, 1.4f, 0.05f);
        }

        private void OnDied()
        {
            if (_dying) return;
            _dying = true;
            _attackHeld = _attackBuffered = false;
            GameManager.Instance.SetInvulnerable(5);
            if (_mountedHorse != null) DismountHorse();
            if (_mountedBoat != null) DismountBoat();
            Velocity = Vector3.Zero;
            Shake(3f, 0.3f);
            _armPose?.Stop();
            PlayAction("Death", 1f, 0.1f);
            float wait = Mathf.Clamp(AnimLength("Death", 1.2f), 0.8f, 1.8f) + 0.6f;

            var tw = CreateTween();
            tw.TweenInterval(wait);
            tw.TweenProperty(_fadeRect, "color:a", 1f, 0.4f);
            tw.TweenCallback(Callable.From(() =>
            {
                GameManager.Instance.Hp = GameManager.Instance.MaxHp / 2;
                if (_indoors)
                {
                    _indoors = false;
                    if (_camera != null) _camera.Position = OutdoorCameraOffset;
                }
                Velocity = Vector3.Zero;
                GlobalPosition = Vector3.Zero;
                _actionPlaying = false;
                _currentAnim = "";
                _dying = false;
                GameManager.Instance.SetInvulnerable(2);
                GameManager.Instance.EmitSignal(GameManager.SignalName.StatsChanged);
            }));
            tw.TweenProperty(_fadeRect, "color:a", 0f, 0.4f);
        }
    }
}
