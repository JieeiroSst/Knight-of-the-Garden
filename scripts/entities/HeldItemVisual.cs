using Godot;

namespace HiepSiVeVuon.Entities
{
    // Vat pham dang trang bi HIEN THI THAT SU tren tay nhan vat (thay vi chi la trang thai logic
    // an hinh nhu truoc) - gan vao xuong co tay phai (Wrist.R, xem CharacterRig.FindSkeleton) qua
    // BoneAttachment3D, LUON hien khi da trang bi (khong chi luc vung/dung). Theo kham khao cac
    // game nong trai/phieu luu tuong tu (Stardew Valley, My Time at Portia, Fields of Mistria):
    // game 2D thuong CHI hien vu khi/cong cu luc dung (vi can ve them frame rieng cho MOI huong
    // moi cong cu, rat ton kem), con game 3D (dung chung 1 model gan xuong) khong co chi phi do
    // nen QUY UOC PHO BIEN la gan+hien LIEN TUC ngay khi trang bi. Game nay khong co model 3D
    // rieng cho tung vu khi/cong cu (chi co icon 2D trong tui do) nen TU DUNG hinh khoi don gian
    // (low-poly, "chunky") cho tung nhom vat pham, dung phong cach hien co cua toan bo the gioi
    // game (rat nhieu cong trinh/vat trang tri khac cung xay bang khoi hinh hoc co ban).
    public partial class HeldItemVisual : Node3D
    {
        private BoneAttachment3D _attachment;
        // "Tam cam" - CON cua _attachment, KHONG PHAI chinh _attachment. BoneAttachment3D tu ghi
        // de Position/Rotation cua CHINH NO moi khung hinh de bam theo dung pose cua xuong (Wrist.
        // R) - neu dat offset/xoay/scale rieng THANG TREN _attachment, gia tri do se bi Godot GHI
        // DE mat moi frame. Dat len 1 Node3D CON thay vao do de offset "cam lech" luon giu nguyen,
        // vi Godot chi cap nhat transform cua BoneAttachment3D, khong dong vao con chau cua no.
        private Node3D _gripPivot;
        private Node3D _currentMesh;
        private string _currentItemId = "";

        // KHUNG NAM TAY (trong khong gian xuong ban tay, da bo scale) - tinh tu chinh vi tri cac
        // xuong ngon tay cua rig (tools/outfits/heldsim2.py): goc toa do = giua long ban tay noi
        // ngon tay nam lai; truc Y = truc can (tu phia ngon ut sang phia ngon tro); truc X = nguoc
        // huong ngon tay duoi thang (luoi cuoc/canh kiem huong ve phia do khi bo xuong); truc Z =
        // phia long ban tay. Vat pham chi can dat "diem cam" cua no vao goc toa do nay.
        private record GripFrame(Vector3 X, Vector3 Y, Vector3 Z, Vector3 Origin);
        private static readonly System.Collections.Generic.Dictionary<string, GripFrame> Grips = new()
        {
            // Rig nam Quaternius (Farmer...): xuong Wrist.R, don vi met.
            ["Wrist.R"] = new(new Vector3(-0.081f, -0.994f, 0.079f), new Vector3(-0.997f, 0.079f, -0.024f), new Vector3(0.018f, -0.081f, -0.997f), new Vector3(0.0059f, 0.1639f, -0.0204f)),
            // Rig nu (Animated Women): xuong Palm.R (armature scale 100 -> don vi rat nho).
            ["Palm.R"] = new(new Vector3(-0.002f, -0.998f, -0.069f), new Vector3(-0.977f, 0.016f, -0.214f), new Vector3(0.215f, 0.067f, -0.974f), new Vector3(0.0003f, 0.0018f, -0.0016f)),
        };

        // Kich thuoc that (nguoi cao ~40 don vi) + diem cam tren vat + cach cam (Upright: xach quai
        // nhu binh tuoi - voi theo truc nam tay, mieng binh huong ve co tay).
        private record Hold(float Scale, Vector3 GripPoint, bool Upright = false);
        private static Hold HoldOf(string itemId) => itemId switch
        {
            "sword" => new(1.8f, new Vector3(0, -0.7f, 0)),
            "hoe" or "cuoc_bac" or "cuoc_vang" => new(3.6f, new Vector3(0, 0.8f, 0)),
            "xeng" => new(3.2f, new Vector3(0, 0.8f, 0)),
            "cao_co" => new(2.5f, new Vector3(0, 0.6f, 0)),
            "pickaxe" or "pickaxe_bac" or "pickaxe_vang" or "pickaxe_kim_cuong" => new(2.4f, new Vector3(0, -0.4f, 0)),
            "can_cau" => new(3.3f, new Vector3(0, 0.2f, 0)),
            "binh_tuoi" => new(2.2f, new Vector3(0, 3.5f, -0.2f), true),
            "may_tuoi_tu_dong" => new(2.5f, new Vector3(0, 1.6f, 0), true),
            _ => new(2f, Vector3.Zero),
        };

        // Truc nam tay (truc can) trong khong gian xuong ban tay - ArmPoseModifier cong ngon tay quanh truc nay.
        public static Vector3? GripAxis(string bone) => Grips.TryGetValue(bone, out var f) ? f.Y : null;

        public void Setup(Skeleton3D skeleton, float modelScale)
        {
            if (skeleton == null || modelScale <= 0f) return;
            // Rig nam: Wrist.R, rig nu (Animated Women Pack): Palm.R (KHONG gan vao xuong ngon tay -
            // ngon tay se cong lai nam can, xem ArmPoseModifier.Grip).
            string bone = System.Array.Find(HiepSiVeVuon.Core.CharacterCatalog.RightHandBones, b => skeleton.FindBone(b) >= 0);
            if (bone == null) return;
            _attachment = new BoneAttachment3D { BoneName = bone };
            skeleton.AddChild(_attachment);

            var gf = Grips.TryGetValue(bone, out var f) ? f : new GripFrame(Vector3.Right, Vector3.Up, Vector3.Back, Vector3.Zero);
            _gripPivot = new Node3D
            {
                Position = gf.Origin,
                Quaternion = new Basis(gf.X, gf.Y, gf.Z).Orthonormalized().GetRotationQuaternion(),
                Scale = Vector3.One / modelScale, // tam thoi - FitGripScale do lai theo scale that cua xuong
            };
            _attachment.AddChild(_gripPivot);
        }

        // Goi lai moi khi trang thai trang bi doi (xem Inventory.InventoryChanged, Player.cs) -
        // itemId null/rong nghia la khong cam gi (bo trang bi ca vu khi lan cong cu).
        public void ShowItem(string itemId)
        {
            itemId ??= "";
            if (_gripPivot == null || itemId == _currentItemId) return;
            _currentItemId = itemId;
            if (_currentMesh != null) { _currentMesh.QueueFree(); _currentMesh = null; }
            if (itemId == "") return;
            _currentMesh = BuildMesh(itemId);
            if (_currentMesh != null)
            {
                // Dat DIEM CAM cua vat vao giua nam tay, phong to dung kich thuoc that.
                var hold = HoldOf(itemId);
                var basis = hold.Upright
                    ? new Basis(new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0)) // Y vat = ve co tay, Z vat (voi) = truc nam tay
                    : Basis.Identity;
                _currentMesh.Transform = new Transform3D(basis.Scaled(Vector3.One * hold.Scale), basis * (-hold.GripPoint * hold.Scale));
                _gripPivot.AddChild(_currentMesh);
                // Dau vat cam (dinh luoi kiem/luoi cuoc...) + diem giua - de ve vet vung va phun nuoc.
                var box = HiepSiVeVuon.Core.ModelUtil.LocalAabb(_currentMesh);
                var c = box.GetCenter();
                _tipLocal = itemId == "binh_tuoi" ? SpoutLocal : new Vector3(c.X, box.End.Y, c.Z);
                _baseLocal = new Vector3(c.X, box.Position.Y + box.Size.Y * 0.45f, c.Z);
            }
        }

        // ==== Vet vung (trail) + nuoc tuoi ====
        private static readonly Vector3 SpoutLocal = new(0, 2.85f, 2.15f); // dau voi binh tuoi (khong gian vat)
        private Vector3 _tipLocal, _baseLocal;
        private MeshInstance3D _trailMesh;
        private ImmediateMesh _trailIm;
        private readonly System.Collections.Generic.List<(Vector3 a, Vector3 b, float age)> _trail = new();
        private float _trailLeft;
        private Color _trailColor = Colors.White;

        public bool HasItem => _currentMesh != null;
        public Vector3 TipGlobal => _currentMesh != null ? _currentMesh.ToGlobal(_tipLocal) : GlobalPosition;

        // Ve vet sang theo dau vat cam trong 'seconds' giay (dai luot vung/chem).
        public void StartTrail(float seconds, Color color)
        {
            if (_currentMesh == null) return;
            _trailLeft = seconds;
            _trailColor = color;
            if (_trailMesh == null)
            {
                _trailIm = new ImmediateMesh();
                _trailMesh = new MeshInstance3D
                {
                    Mesh = _trailIm, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    },
                };
                AddChild(_trailMesh);
            }
        }

        // Tia nuoc tu voi binh tuoi (hat roi theo trong luc) trong 'seconds' giay.
        public void PourWater(float seconds)
        {
            if (_gripPivot == null || _currentItemId != "binh_tuoi") return;
            var p = new CpuParticles3D
            {
                Amount = 40, Lifetime = 0.55f, LocalCoords = false, Emitting = true,
                Direction = new Vector3(0, 1f, 0), Spread = 12f, // theo voi binh (= truc nam tay) InitialVelocityMin = 18f, InitialVelocityMax = 26f,
                Gravity = new Vector3(0, -160f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.1f,
                Color = new Color(0.55f, 0.75f, 1f, 0.85f),
                Mesh = new SphereMesh
                {
                    Radius = 0.7f, Height = 1.4f, RadialSegments = 5, Rings = 3,
                    Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
                },
            };
            _gripPivot.AddChild(p);
            p.GlobalPosition = TipGlobal;
            var tw = p.CreateTween();
            tw.TweenInterval(seconds);
            tw.TweenCallback(Callable.From(() => p.Emitting = false));
            tw.TweenInterval(0.7f);
            tw.TweenCallback(Callable.From(p.QueueFree));
        }

        // Bu tru scale THAT cua xuong (khong chi Player.ModelScale): rig nu co armature scale 100 ben
        // trong, neu chi chia cho modelScale thi do cam tay to gap ~100 lan. Do lai moi khung (re).
        private float _fitScale = -1f;
        private void FitGripScale()
        {
            if (_attachment == null || !_attachment.IsInsideTree()) return;
            float boneScale = _attachment.GlobalTransform.Basis.Scale.X;
            float rootScale = GetParent() is Node3D p ? p.GlobalTransform.Basis.Scale.X : 1f;
            float s = boneScale / Mathf.Max(0.0001f, rootScale);
            if (s <= 0.0001f || Mathf.Abs(s - _fitScale) < _fitScale * 0.01f) return;
            _fitScale = s;
            _gripPivot.Scale = Vector3.One / s;
        }

        public override void _Process(double delta)
        {
            FitGripScale();
            if (_trailIm == null) return;
            float dt = (float)delta;
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                var s = _trail[i];
                s.age += dt;
                if (s.age > TrailLife) _trail.RemoveAt(i); else _trail[i] = s;
            }
            if (_trailLeft > 0f && _currentMesh != null)
            {
                _trailLeft -= dt;
                _trail.Add((_currentMesh.ToGlobal(_baseLocal), TipGlobal, 0f));
            }
            _trailIm.ClearSurfaces();
            if (_trail.Count < 2) return;
            _trailIm.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            foreach (var (a, b, age) in _trail)
            {
                float alpha = Mathf.Clamp(1f - age / TrailLife, 0f, 1f) * 0.75f;
                _trailIm.SurfaceSetColor(new Color(_trailColor, alpha * 0.25f));
                _trailIm.SurfaceAddVertex(a);
                _trailIm.SurfaceSetColor(new Color(_trailColor, alpha));
                _trailIm.SurfaceAddVertex(b);
            }
            _trailIm.SurfaceEnd();
        }

        private const float TrailLife = 0.16f;

        private static Node3D BuildMesh(string itemId) => itemId switch
        {
            "sword" => MakeSword(),
            "hoe" => MakeImportedTool("hoe", new Color(0.42f, 0.42f, 0.45f)),
            "cuoc_bac" => MakeImportedTool("hoe", new Color(0.82f, 0.83f, 0.86f)),
            "cuoc_vang" => MakeImportedTool("hoe", new Color(0.85f, 0.68f, 0.2f)),
            "pickaxe" => MakePickaxe(new Color(0.42f, 0.42f, 0.45f)),
            "pickaxe_bac" => MakePickaxe(new Color(0.82f, 0.83f, 0.86f)),
            "pickaxe_vang" => MakePickaxe(new Color(0.85f, 0.68f, 0.2f)),
            "pickaxe_kim_cuong" => MakePickaxe(new Color(0.6f, 0.9f, 1f)),
            "can_cau" => MakeFishingRod(),
            "may_tuoi_tu_dong" => MakeSprinklerHeld(),
            "binh_tuoi" => MakeWateringCan(),
            "xeng" => MakeImportedTool("xeng", new Color(0.6f, 0.61f, 0.64f)),
            "cao_co" => MakeImportedTool("cao_co", new Color(0.48f, 0.48f, 0.52f)),
            _ => null,
        };

        // Model that GA that (khong phai khoi hop/tru tu tao) - lay tu goi "Farm Tool Pack" (CGTrader,
        // free-3d-models/industrial/tool/farm-tools-pack, nguoi dung tu tai ve). File OBJ goc chi co
        // hinh khoi (khong kem .mtl/texture), nen tach san thanh 2 file rieng (_handle/_head, xem
        // tools/icon_render.gd - CUNG mot logic tach dung de render icon Balo, giu dong nhat giua
        // icon va mo hinh cam tren tay) de AP 2 VAT LIEU khac nhau (go nau cho tay cam, kim loai mau
        // theo cap do cho phan dau/luoi) thay vi nhuom CA CUM 1 mau nhu truoc (nhin "nhua trang" don
        // dieu, khong ro dau la go dau la kim loai).
        private static readonly System.Collections.Generic.Dictionary<string, Mesh> _importedMeshCache = new();

        private static Mesh LoadImportedMesh(string fileBaseName)
        {
            if (_importedMeshCache.TryGetValue(fileBaseName, out var cached)) return cached;
            var mesh = GD.Load<Mesh>($"res://assets3d/garden_tools/{fileBaseName}.obj");
            _importedMeshCache[fileBaseName] = mesh;
            return mesh;
        }

        private static readonly Color WoodHandleColor = new(0.42f, 0.29f, 0.16f);

        private static Node3D MakeImportedTool(string fileBaseName, Color headColor)
        {
            var handleMesh = LoadImportedMesh($"{fileBaseName}_handle");
            var headMesh = LoadImportedMesh($"{fileBaseName}_head");
            if (handleMesh == null && headMesh == null) return null;
            var root = new Node3D();
            if (handleMesh != null)
                root.AddChild(MakeMesh(handleMesh, Vector3.Zero, WoodHandleColor, roughness: 0.75f, metallic: 0f));
            if (headMesh != null)
                root.AddChild(MakeMesh(headMesh, Vector3.Zero, headColor, roughness: 0.4f, metallic: 0.35f));
            return root;
        }

        private static MeshInstance3D MakeMesh(Mesh mesh, Vector3 pos, Color color, float roughness = 0.5f, float metallic = 0f)
        {
            return new MeshInstance3D
            {
                Mesh = mesh,
                Position = pos,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic },
            };
        }

        // Kiem: luoi dai + tay cam ngang (crossguard) + chuoi cam + nut chan (pommel) - dang kiem
        // co dien don gian, de nhan dien tu xa.
        private static Node3D MakeSword()
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new BoxMesh { Size = new Vector3(0.55f, 8.5f, 0.18f) }, new Vector3(0, 4.6f, 0),
                new Color(0.78f, 0.8f, 0.83f), roughness: 0.25f, metallic: 0.7f)); // luoi kiem
            root.AddChild(MakeMesh(new BoxMesh { Size = new Vector3(2.1f, 0.35f, 0.4f) }, new Vector3(0, 0.2f, 0),
                new Color(0.55f, 0.45f, 0.2f), roughness: 0.4f, metallic: 0.5f)); // tay cam ngang
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 1.8f }, new Vector3(0, -0.7f, 0),
                new Color(0.32f, 0.2f, 0.12f), roughness: 0.85f)); // chuoi cam
            root.AddChild(MakeMesh(new SphereMesh { Radius = 0.38f, Height = 0.76f }, new Vector3(0, -1.65f, 0),
                new Color(0.55f, 0.45f, 0.2f), roughness: 0.4f, metallic: 0.5f)); // nut chan
            return root;
        }

        // Cuoc da (pickaxe): can dai + dau nhon 2 canh (hinh chu V long nguoc) vuong goc voi can.
        private static Node3D MakePickaxe(Color headColor)
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 8f }, new Vector3(0, 3f, 0),
                new Color(0.4f, 0.27f, 0.15f), roughness: 0.9f)); // can go
            var headL = MakeMesh(new PrismMesh { Size = new Vector3(0.35f, 0.35f, 2.4f) }, new Vector3(-0.9f, 7.2f, 0),
                headColor, roughness: 0.3f, metallic: 0.65f);
            headL.RotationDegrees = new Vector3(0, 0, 25f);
            root.AddChild(headL);
            var headR = MakeMesh(new PrismMesh { Size = new Vector3(0.35f, 0.35f, 2.4f) }, new Vector3(0.9f, 7.2f, 0),
                headColor, roughness: 0.3f, metallic: 0.65f);
            headR.RotationDegrees = new Vector3(0, 0, -25f);
            root.AddChild(headR);
            return root;
        }

        // Can cau: 1 can dai thon nho, don gian (khong ve day cau/luoi cau chi tiet - qua nho de
        // thay ro o khoang cach thuong nhin trong game).
        private static Node3D MakeFishingRod()
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.22f, Height = 11f }, new Vector3(0, 4.5f, 0),
                new Color(0.5f, 0.36f, 0.2f), roughness: 0.7f));
            return root;
        }

        // May tuoi tu dong: cam nhu 1 thiet bi nho gon (khoi hop + voi phun) thay vi 1 cong cu dai.
        private static Node3D MakeSprinklerHeld()
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new BoxMesh { Size = new Vector3(1.6f, 1.6f, 1.6f) }, new Vector3(0, 0.8f, 0),
                new Color(0.55f, 0.58f, 0.6f), roughness: 0.5f, metallic: 0.3f));
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.2f, Height = 1.4f }, new Vector3(0, 1.9f, 0.6f),
                new Color(0.35f, 0.4f, 0.42f), roughness: 0.4f, metallic: 0.4f));
            return root;
        }

        // Binh tuoi nuoc: than binh hinh tru + vien mieng binh + voi phun cheo phia truoc (dau voi
        // la 1 khoi cau NHO - hoa sen tuoi, khong phai qua cau to nhu truoc de tranh nhin giong "keo
        // mut") + QUAI CAM HINH CUNG (2 tru dung + 1 thanh ngang noi dinh, giong quai xach that,
        // khong phai 1 que thang don gian nhu truoc - dung 1 que thang khien nhin khong ro la binh
        // tuoi khi xem ro (vd render icon), du o gan trong tay nguoi choi it thay ro hon.
        private static Node3D MakeWateringCan()
        {
            var root = new Node3D();
            var canColor = new Color(0.42f, 0.55f, 0.4f);
            var darkColor = new Color(0.3f, 0.4f, 0.28f);
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.3f, Height = 2.6f }, new Vector3(0, 1.3f, 0),
                canColor, roughness: 0.45f, metallic: 0.35f)); // than binh
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 1.18f, BottomRadius = 1.12f, Height = 0.25f }, new Vector3(0, 2.65f, 0),
                darkColor, roughness: 0.4f, metallic: 0.4f)); // vien mieng binh
            var spout = MakeMesh(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.25f, Height = 2.2f }, new Vector3(0, 2.1f, 1.1f),
                canColor, roughness: 0.45f, metallic: 0.35f);
            spout.RotationDegrees = new Vector3(70f, 0, 0);
            root.AddChild(spout); // voi phun cheo ra truoc
            root.AddChild(MakeMesh(new SphereMesh { Radius = 0.22f, Height = 0.44f }, new Vector3(0, 2.85f, 2.15f),
                darkColor, roughness: 0.5f, metallic: 0.2f)); // dau voi (hoa sen) - nho lai, khong to nhu qua bong

            // Quai cam tren dinh chay DOC tu sau ra truoc (cung huong voi voi) - nhu binh tuoi that,
            // nam tay om tron thanh quai (xem HoldOf: diem cam (0, 3.5, -0.2)).
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.9f },
                new Vector3(0, 3.1f, -0.85f), canColor, roughness: 0.45f, metallic: 0.35f));
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.9f },
                new Vector3(0, 3.1f, 0.45f), canColor, roughness: 0.45f, metallic: 0.35f));
            var handleBar = MakeMesh(new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 1.4f },
                new Vector3(0, 3.5f, -0.2f), canColor, roughness: 0.45f, metallic: 0.35f);
            handleBar.RotationDegrees = new Vector3(90f, 0, 0);
            root.AddChild(handleBar);
            return root;
        }

    }
}
