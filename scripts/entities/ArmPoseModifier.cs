using Godot;
using System;
using System.Collections.Generic;

namespace HiepSiVeVuon.Entities
{
    // Lop tu the CONG THEM len hoat canh co san (Idle/Walk/...) - goi Quaternius khong co dong tac
    // vung cuoc/tuoi nuoc/quet liem/quang can cau, nen tu dung bang cach xoay them xuong tay/than theo
    // keyframe (SkeletonModifier3D cua Godot 4.3 chay SAU AnimationPlayer moi khung hinh).
    //
    // Goc quay tinh theo TRUC CUA NHAN VAT (khong phu thuoc huong xuong tung rig nam/nu):
    //   pitch = quanh truc PHAI   (+ : gio tay ra truoc/len tren, than nga ra sau; - : cui ve truoc)
    //   yaw   = quanh truc LEN    (+ : xoay than sang trai)
    //   roll  = quanh truc TRUOC  (+ : tay phai khep vao giua than / tay trai dang ra ngoai)
    // Da kiem tra dau goc bang mo phong tren chinh rig Farmer/Woman (tools/outfits/posesim.py).
    public partial class ArmPoseModifier : SkeletonModifier3D
    {
        public enum Role { Spine, Torso, ArmR, ForeR, HandR, ArmL, ForeL }

        private static readonly Dictionary<Role, string[]> Bones = new()
        {
            [Role.Spine] = new[] { "Abdomen" },
            [Role.Torso] = new[] { "Torso" },
            [Role.ArmR] = new[] { "UpperArm.R" },
            [Role.ForeR] = new[] { "LowerArm.R" },
            [Role.HandR] = new[] { "Wrist.R", "Palm.R" },
            [Role.ArmL] = new[] { "UpperArm.L" },
            [Role.ForeL] = new[] { "LowerArm.L" },
        };
        private static readonly Role[] Order = { Role.Spine, Role.Torso, Role.ArmR, Role.ForeR, Role.HandR, Role.ArmL, Role.ForeL };

        private readonly Dictionary<Role, int> _idx = new();
        private Node3D _model;

        private Motion _motion;
        private float _t, _speed = 1f;
        private bool _impactFired;
        private Action _onImpact, _onEnd;

        // Tu the "cam do": cang tay phai hoi co len, co tay xoay ra ngoai - vat cam huong ra truoc-
        // len, khong vat cheo qua nguoi (da thu nhieu goc tren rig that, tools/outfits/heldsim2.py).
        public bool Carry;
        private float _carry;
        // Nam tay quanh can khi dang cam do (ngon tay cong vao long ban tay).
        public bool Grip;
        private float _grip;
        private int _hand = -1;
        private Vector3 _gripAxisLocal;
        private readonly List<(int bone, float deg)> _curl = new();
        // Do cong tung dot (da xem can canh tren rig that - nam tay kin quanh can, tools/outfits/handzoom.py).
        // Rig nam: dot 1 = xuong ban tay, 2 = khop goc ngon, 3-4 = cac dot ngon.
        private static readonly (string bone, float deg)[] CurlMale =
        {
            ("Index1.R", 10), ("Index2.R", 85), ("Index3.R", 80), ("Index4.R", 60),
            ("Middle1.R", 10), ("Middle2.R", 85), ("Middle3.R", 80), ("Middle4.R", 60),
            ("Ring1.R", 10), ("Ring2.R", 85), ("Ring3.R", 80), ("Ring4.R", 60),
            ("Pinky1.R", 10), ("Pinky2.R", 85), ("Pinky3.R", 80), ("Pinky4.R", 60),
            ("Thumb2.R", 40), ("Thumb3.R", 35),
        };
        private static readonly (string bone, float deg)[] CurlFemale = { ("MiddleHand.R", 80), ("Fingers.R", 85), ("Thumb1.R", 20), ("Thumb2.R", 45) };
        private float _bob;
        public float MoveRatio;

        public bool Busy => _motion != null;
        public string CurrentMotion => _motion?.Name;

        public void Setup(Skeleton3D skeleton, Node3D modelRoot)
        {
            _model = modelRoot;
            foreach (var hb in new[] { "Wrist.R", "Palm.R" })
                if (skeleton.FindBone(hb) is int hi && hi >= 0 && HeldItemVisual.GripAxis(hb) is Vector3 ax)
                {
                    _hand = hi;
                    _gripAxisLocal = ax;
                    foreach (var (bn, deg) in skeleton.FindBone("Index2.R") >= 0 ? CurlMale : CurlFemale)
                        if (skeleton.FindBone(bn) is int bi && bi >= 0) _curl.Add((bi, deg));
                    break;
                }
            foreach (var (role, names) in Bones)
                foreach (var n in names)
                {
                    int i = skeleton.FindBone(n);
                    if (i >= 0) { _idx[role] = i; break; }
                }
        }

        public void Play(Motion m, float speed = 1f, Action onImpact = null, Action onEnd = null)
        {
            // Dong tac cu chua toi luc cham -> van cho no "cham" (khong mat ket qua) roi moi doi.
            if (_motion != null && !_impactFired) _onImpact?.Invoke();
            _onEnd?.Invoke();
            _motion = m;
            _t = 0f;
            _speed = Mathf.Max(0.1f, speed);
            _impactFired = false;
            _onImpact = onImpact;
            _onEnd = onEnd;
        }

        public void Stop()
        {
            _motion = null;
            var end = _onEnd;
            _onImpact = _onEnd = null;
            end?.Invoke();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _carry = Mathf.MoveToward(_carry, Carry && _motion == null ? 1f : 0f, dt * 5f);
            _grip = Mathf.MoveToward(_grip, Grip ? 1f : 0f, dt * 8f);
            _bob += dt * Mathf.Lerp(2f, 9f, Mathf.Clamp(MoveRatio, 0f, 1f));
            if (_motion == null) return;
            _t += dt * _speed / _motion.Duration;
            if (!_impactFired && _t >= _motion.Impact)
            {
                _impactFired = true;
                var cb = _onImpact;
                _onImpact = null;
                cb?.Invoke();
            }
            if (_t >= 1f) Stop();
        }

        public override void _ProcessModification()
        {
            var skel = GetSkeleton();
            if (skel == null || _model == null || !IsInstanceValid(_model)) return;
            if (_motion == null && _carry <= 0.001f && _grip <= 0.001f) return;

            // Truc nhan vat (+Z = mat truoc, xem Player.UpdateVisuals) doi sang khong gian cua skeleton.
            var toSkel = skel.GlobalTransform.Basis.Inverse() * _model.GlobalTransform.Basis;
            var up = (toSkel * Vector3.Up).Normalized();
            var fwd = (toSkel * Vector3.Back).Normalized();
            var right = (toSkel * Vector3.Left).Normalized();

            var pose = new Dictionary<Role, Vector3>();
            if (_motion != null) _motion.Sample(Mathf.Clamp(_t, 0f, 1f), pose);
            if (_carry > 0f)
            {
                // Lac nhe theo nhip buoc chan khi dang di (vat cam dung dua tu nhien).
                float sway = Mathf.Sin(_bob) * 6f * Mathf.Clamp(MoveRatio, 0f, 1f);
                Add(pose, Role.ForeR, new Vector3(28f + sway, 0, 0) * _carry);
                Add(pose, Role.ArmR, new Vector3(8f, 0, -6f) * _carry);
                Add(pose, Role.HandR, new Vector3(0, -25f, 25f) * _carry);
            }

            foreach (var role in Order)
            {
                if (!pose.TryGetValue(role, out var a) || !_idx.TryGetValue(role, out int b)) continue;
                if (a.LengthSquared() < 0.0001f) continue;
                var rot = new Basis(fwd, Mathf.DegToRad(a.Z)) * new Basis(right, Mathf.DegToRad(a.X)) * new Basis(up, Mathf.DegToRad(a.Y));
                int parent = skel.GetBoneParent(b);
                var pg = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
                var g = skel.GetBoneGlobalPose(b).Basis;
                var local = pg.Inverse() * (rot * g);
                skel.SetBonePoseRotation(b, local.Orthonormalized().GetRotationQuaternion());
            }

            // Ngon tay nam quanh can: xoay tung dot ngon quanh TRUC NAM TAY (huong cong vao long ban tay).
            if (_grip > 0.001f && _hand >= 0)
            {
                var axis = (skel.GetBoneGlobalPose(_hand).Basis * _gripAxisLocal).Normalized();
                foreach (var (b, deg) in _curl)
                {
                    int parent = skel.GetBoneParent(b);
                    var pg = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
                    var g = skel.GetBoneGlobalPose(b).Basis;
                    var local = pg.Inverse() * (new Basis(axis, Mathf.DegToRad(deg * _grip)) * g);
                    skel.SetBonePoseRotation(b, local.Orthonormalized().GetRotationQuaternion());
                }
            }
        }

        private static void Add(Dictionary<Role, Vector3> p, Role r, Vector3 v) => p[r] = (p.TryGetValue(r, out var o) ? o : Vector3.Zero) + v;

        // ==== Dong tac (keyframe theo thoi gian chuan hoa 0..1) ====
        public enum Ease { Smooth, In, Out, Linear }
        public record Key(float T, Ease Ease, (Role role, float pitch, float yaw, float roll)[] Pose);

        public class Motion
        {
            public string Name;
            public float Duration;   // giay (o toc do 1)
            public float Impact;     // 0..1 - luc luoi cuoc cham dat / nuoc bat dau chay...
            public Key[] Keys;

            public void Sample(float t, Dictionary<Role, Vector3> outPose)
            {
                Key a = Keys[0], b = Keys[^1];
                for (int i = 0; i < Keys.Length - 1; i++)
                    if (t >= Keys[i].T && t <= Keys[i + 1].T) { a = Keys[i]; b = Keys[i + 1]; break; }
                float u = b.T > a.T ? (t - a.T) / (b.T - a.T) : 1f;
                u = b.Ease switch
                {
                    Ease.In => u * u * u,                       // tang toc (nhat bo xuong)
                    Ease.Out => 1f - Mathf.Pow(1f - u, 3f),     // giam toc (gio len, thu ve)
                    Ease.Linear => u,
                    _ => u * u * (3f - 2f * u),
                };
                foreach (Role r in Enum.GetValues(typeof(Role)))
                {
                    var va = Get(a, r); var vb = Get(b, r);
                    if (va == Vector3.Zero && vb == Vector3.Zero) continue;
                    outPose[r] = va.Lerp(vb, u);
                }
            }

            private static Vector3 Get(Key k, Role r)
            {
                foreach (var p in k.Pose) if (p.role == r) return new Vector3(p.pitch, p.yaw, p.roll);
                return Vector3.Zero;
            }
        }
    }

    // Thu vien dong tac - ten dung lam ma mang (Player.NetAnimRoles "Motion:<ten>").
    public static class Motions
    {
        private static ArmPoseModifier.Key K(float t, ArmPoseModifier.Ease e, params (ArmPoseModifier.Role, float, float, float)[] p) => new(t, e, p);
        private const ArmPoseModifier.Role Sp = ArmPoseModifier.Role.Spine, To = ArmPoseModifier.Role.Torso,
            AR = ArmPoseModifier.Role.ArmR, FR = ArmPoseModifier.Role.ForeR, HR = ArmPoseModifier.Role.HandR,
            AL = ArmPoseModifier.Role.ArmL, FL = ArmPoseModifier.Role.ForeL;
        private const ArmPoseModifier.Ease S = ArmPoseModifier.Ease.Smooth, In = ArmPoseModifier.Ease.In, Out = ArmPoseModifier.Ease.Out;

        // Cuoc/cuoc da: 2 tay gio qua dau, nga nguoi ra sau -> bo manh xuong, cui nguoi theo -> thu ve.
        public static readonly ArmPoseModifier.Motion Overhead = new()
        {
            Name = "Overhead", Duration = 0.62f, Impact = 0.56f,
            Keys = new[]
            {
                K(0f, S),
                K(0.40f, Out, (To, 12, 10, 0), (AR, 165, 0, 15), (FR, 30, 0, 0), (HR, 20, 0, 0), (AL, 150, 0, -20), (FL, 30, 0, 0)),
                K(0.56f, In, (Sp, -10, 0, 0), (To, -20, 0, 0), (AR, 45, 0, 15), (FR, 10, 0, 0), (HR, -75, 0, 0), (AL, 40, 0, -20), (FL, 10, 0, 0)),
                K(0.72f, S, (Sp, -8, 0, 0), (To, -16, 0, 0), (AR, 40, 0, 12), (FR, 8, 0, 0), (HR, -65, 0, 0), (AL, 35, 0, -16), (FL, 8, 0, 0)),
                K(1f, Out),
            },
        };

        // Xeng: nhac ngang vai roi an manh xuong dat truoc mat, hat dat sang ben.
        public static readonly ArmPoseModifier.Motion Dig = new()
        {
            Name = "Dig", Duration = 0.6f, Impact = 0.45f,
            Keys = new[]
            {
                K(0f, S),
                K(0.30f, Out, (To, 6, 15, 0), (AR, 95, 0, 10), (FR, 40, 0, 0), (AL, 70, 0, -25), (FL, 30, 0, 0)),
                K(0.45f, In, (Sp, -14, 0, 0), (To, -18, 0, 0), (AR, 35, 0, 10), (FR, 5, 0, 0), (HR, -60, 0, 0), (AL, 30, 0, -20)),
                K(0.70f, S, (To, -6, -25, 0), (AR, 70, 0, -20), (FR, 25, 0, 0), (AL, 45, 0, -10)),
                K(1f, Out),
            },
        };

        // Liem/cao co: van nguoi sang phai, quet ngang sang trai sat mat dat.
        public static readonly ArmPoseModifier.Motion Sweep = new()
        {
            Name = "Sweep", Duration = 0.55f, Impact = 0.55f,
            Keys = new[]
            {
                K(0f, S),
                K(0.35f, Out, (Sp, -6, 0, 0), (To, -8, -45, 0), (AR, 55, 0, -50), (FR, 20, 0, 0)),
                K(0.60f, In, (Sp, -10, 0, 0), (To, -10, 40, 0), (AR, 70, 0, 60), (FR, 10, 0, 0)),
                K(1f, Out),
            },
        };

        // Binh tuoi: dua binh ra truoc, nghieng co tay cho nuoc chay, giu mot luc roi thu ve.
        public static readonly ArmPoseModifier.Motion Pour = new()
        {
            Name = "Pour", Duration = 0.95f, Impact = 0.32f,
            Keys = new[]
            {
                K(0f, S),
                K(0.25f, Out, (To, -8, 0, 0), (AR, 65, 0, 5), (FR, 10, 0, 0)),
                K(0.35f, S, (To, -10, 0, 0), (AR, 62, 0, 5), (FR, 10, 0, 0), (HR, -55, 0, 0)),
                K(0.80f, S, (To, -10, 4, 0), (AR, 60, 4, 5), (FR, 12, 0, 0), (HR, -58, 0, 0)),
                K(1f, Out),
            },
        };

        // Cui xuong gieo hat / hai qua / dat do.
        public static readonly ArmPoseModifier.Motion Bend = new()
        {
            Name = "Bend", Duration = 0.55f, Impact = 0.48f,
            Keys = new[]
            {
                K(0f, S),
                K(0.45f, Out, (Sp, -22, 0, 0), (To, -20, 0, 0), (AR, 55, 0, 8), (FR, 20, 0, 0), (HR, -20, 0, 0), (AL, 15, 0, 0)),
                K(0.65f, S, (Sp, -20, 0, 0), (To, -18, 0, 0), (AR, 50, 0, 8), (FR, 25, 0, 0)),
                K(1f, Out),
            },
        };

        // Quang can cau: vung can ra sau qua vai roi quat manh ve truoc, giu can huong ra mat nuoc.
        public static readonly ArmPoseModifier.Motion Cast = new()
        {
            Name = "Cast", Duration = 0.8f, Impact = 0.6f,
            Keys = new[]
            {
                K(0f, S),
                K(0.40f, Out, (To, 10, 15, 0), (AR, 170, 0, 10), (FR, 40, 0, 0), (HR, 25, 0, 0)),
                K(0.60f, In, (To, -12, -5, 0), (AR, 70, 0, 8), (FR, 5, 0, 0), (HR, -10, 0, 0)),
                K(1f, Out),
            },
        };

        // Don ket combo kiem: nhay chem bo tu tren dau xuong (2 tay).
        public static readonly ArmPoseModifier.Motion Slam = new()
        {
            Name = "Slam", Duration = 0.58f, Impact = 0.55f,
            Keys = new[]
            {
                K(0f, S),
                K(0.38f, Out, (To, 16, -10, 0), (AR, 175, 0, 20), (FR, 20, 0, 0), (HR, 25, 0, 0), (AL, 160, 0, -25), (FL, 25, 0, 0)),
                K(0.55f, In, (Sp, -14, 0, 0), (To, -26, 0, 0), (AR, 55, 0, 18), (FR, 0, 0, 0), (HR, -60, 0, 0), (AL, 50, 0, -22)),
                K(0.75f, S, (Sp, -10, 0, 0), (To, -20, 0, 0), (AR, 50, 0, 15), (HR, -50, 0, 0), (AL, 45, 0, -18)),
                K(1f, Out),
            },
        };

        // Lop xoay than them cho don chem bang hoat canh co san (dong tac co suc nang hon).
        public static readonly ArmPoseModifier.Motion TwistSlash = new()
        {
            Name = "TwistSlash", Duration = 0.45f, Impact = 0.38f,
            Keys = new[]
            {
                K(0f, S),
                K(0.25f, Out, (To, 4, -28, 0)),
                K(0.45f, In, (Sp, -6, 0, 0), (To, -10, 26, 0)),
                K(1f, Out),
            },
        };

        public static readonly ArmPoseModifier.Motion[] All = { Overhead, Dig, Sweep, Pour, Bend, Cast, Slam, TwistSlash };
        public static ArmPoseModifier.Motion Get(string name) => Array.Find(All, m => m.Name == name);
    }
}
