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

        // Goc cam CHEO (khong dung thang goc/ngang thuan) de dau vu khi/cong cu huong ra truoc-
        // xuong va lech ra ngoai than mot chut - quy uoc cam vu khi pho bien, doc duoc ro tu goc
        // camera hoi tren cao cua game (xem ghi chu tren). Cac gia tri nay la UOC LUONG HOP LY
        // (khong the render/xem truoc trong Godot editor tu day) - co the can chinh lai nhe trong
        // engine neu nhin chua thuan.
        private static readonly Vector3 GripRotationDeg = new(-35f, 15f, 12f);
        // Do dai/vi tri cac kich thuoc/offset trong file nay (vd luoi kiem dai 8.5) duoc THIET KE
        // theo "don vi the gioi" thong thuong (giong cac vat trang tri khac trong game) - NHUNG
        // _gripPivot nay nam BEN TRONG model nhan vat da bi PHONG TO qua Player.ModelScale (vd
        // 22x, xem Player.cs) - moi Position/Scale dat o day deu BI NHAN THEM 22 LAN khi hien ra
        // man hinh, khien vu khi to gap ~22 lan du kien (vuot qua than nguoi) VA lech xa khoi tay
        // hang chuc don vi. Phai CHIA NGUOC lai cho modelScale (ca vi tri offset LAN ty le mesh
        // con) de bu tru dung, nhu vay cac kich thuoc/offset trong MakeSword()... van giu nguyen
        // "nhin dung ty le" ma khong can tinh toan lai tung so.
        private static readonly Vector3 RawGripOffset = new(1.3f, -1.1f, 0.4f);

        public void Setup(Skeleton3D skeleton, float modelScale)
        {
            if (skeleton == null || modelScale <= 0f) return;
            int boneIdx = skeleton.FindBone("Wrist.R");
            if (boneIdx < 0) return;
            _attachment = new BoneAttachment3D { BoneName = "Wrist.R" };
            skeleton.AddChild(_attachment);

            _gripPivot = new Node3D
            {
                Position = RawGripOffset / modelScale, // bu tru scale ke thua tu model
                RotationDegrees = GripRotationDeg,
                Scale = Vector3.One / modelScale, // bu tru scale cho TAT CA mesh con (kiem/cuoc/...)
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
            if (_currentMesh != null) _gripPivot.AddChild(_currentMesh);
        }

        private static Node3D BuildMesh(string itemId) => itemId switch
        {
            "sword" => MakeSword(),
            "hoe" => MakeImportedTool("hoe", new Color(0.42f, 0.42f, 0.45f)),
            "cuoc_bac" => MakeImportedTool("hoe", new Color(0.82f, 0.83f, 0.86f)),
            "cuoc_vang" => MakeImportedTool("hoe", new Color(0.85f, 0.68f, 0.2f)),
            "pickaxe" => MakePickaxe(new Color(0.42f, 0.42f, 0.45f)),
            "pickaxe_bac" => MakePickaxe(new Color(0.82f, 0.83f, 0.86f)),
            "pickaxe_vang" => MakePickaxe(new Color(0.85f, 0.68f, 0.2f)),
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

            // Quai cam hinh cung (dang chu U nguoc): 2 tru dung hai ben + 1 thanh ngang noi dinh.
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 1.3f },
                new Vector3(-0.75f, 3.3f, -0.35f), canColor, roughness: 0.45f, metallic: 0.35f));
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 1.3f },
                new Vector3(0.75f, 3.3f, -0.35f), canColor, roughness: 0.45f, metallic: 0.35f));
            var handleBar = MakeMesh(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 1.6f },
                new Vector3(0, 3.95f, -0.35f), canColor, roughness: 0.45f, metallic: 0.35f);
            handleBar.RotationDegrees = new Vector3(0, 0, 90f);
            root.AddChild(handleBar);
            return root;
        }

    }
}
