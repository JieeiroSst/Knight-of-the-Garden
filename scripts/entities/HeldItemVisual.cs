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
            "hoe" => MakeHoe(new Color(0.42f, 0.42f, 0.45f)),
            "cuoc_bac" => MakeHoe(new Color(0.82f, 0.83f, 0.86f)),
            "cuoc_vang" => MakeHoe(new Color(0.85f, 0.68f, 0.2f)),
            "pickaxe" => MakePickaxe(new Color(0.42f, 0.42f, 0.45f)),
            "pickaxe_bac" => MakePickaxe(new Color(0.82f, 0.83f, 0.86f)),
            "pickaxe_vang" => MakePickaxe(new Color(0.85f, 0.68f, 0.2f)),
            "can_cau" => MakeFishingRod(),
            "may_tuoi_tu_dong" => MakeSprinklerHeld(),
            "binh_tuoi" => MakeWateringCan(),
            "xeng" => MakeShovel(),
            "cao_co" => MakeRake(),
            _ => null,
        };

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

        // Cuoc dat: can dai + vong dai kim loai noi can voi luoi + luoi cuoc phang goc ~70 do o dau
        // (dang cuoc that, khong phai xeng). Luoi day 0.32 (khong phai 0.15 mong nhu tam bia) de
        // nhin ro la 1 khoi co the tich that, khong phai 1 mat phang "gia 3D".
        private static Node3D MakeHoe(Color bladeColor)
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 8f }, new Vector3(0, 3f, 0),
                new Color(0.4f, 0.27f, 0.15f), roughness: 0.9f)); // can go
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.26f, Height = 0.45f }, new Vector3(0, 6.85f, 0.22f),
                new Color(0.25f, 0.25f, 0.28f), roughness: 0.4f, metallic: 0.7f)); // vong dai noi can-luoi
            var blade = MakeMesh(new BoxMesh { Size = new Vector3(2.1f, 0.32f, 1.3f) }, new Vector3(0, 7.15f, 0.75f),
                bladeColor, roughness: 0.35f, metallic: 0.6f);
            blade.RotationDegrees = new Vector3(-70f, 0, 0);
            root.AddChild(blade);
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

        // Binh tuoi nuoc: than binh hinh tru + vien mieng binh + voi phun cheo phia truoc (co dau
        // voi hinh cau nho o dau) + quai cam nho phia tren. Them vien mieng + dau voi de tang chi
        // tiet khoi (khong chi 3 tru don gian nhu truoc).
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
            root.AddChild(MakeMesh(new SphereMesh { Radius = 0.32f, Height = 0.64f }, new Vector3(0, 2.85f, 2.15f),
                darkColor, roughness: 0.5f, metallic: 0.2f)); // dau voi (hoa sen)
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 1.4f }, new Vector3(0, 2.9f, -0.3f),
                canColor, roughness: 0.45f, metallic: 0.35f)); // quai cam
            return root;
        }

        // Xeng: can dai + vong dai kim loai + luoi xeng BAU TRON o DAU can (khong phai o duoi/gan
        // tay cam nhu truoc - loi cu dat luoi ngay vi tri nam tay, chong len than can, khien nhin
        // nhu 1 con dao nho thay vi 1 cai xeng). SphereMesh ep det (Scale.z=0.36) thay vi PrismMesh
        // mong 0.25 - vua giu dang bau tron that cua luoi xeng, vua co do day nhin ro la khoi 3D.
        private static Node3D MakeShovel()
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 7f }, new Vector3(0, 3f, 0),
                new Color(0.4f, 0.27f, 0.15f), roughness: 0.9f)); // can go
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.28f, Height = 0.5f }, new Vector3(0, 6.6f, 0.1f),
                new Color(0.25f, 0.25f, 0.28f), roughness: 0.4f, metallic: 0.7f)); // vong dai noi can-luoi
            var blade = MakeMesh(new SphereMesh { Radius = 1.05f, Height = 1.9f, RadialSegments = 14, Rings = 7 },
                new Vector3(0, 7.75f, 0.15f), new Color(0.56f, 0.57f, 0.6f), roughness: 0.3f, metallic: 0.55f);
            blade.Scale = new Vector3(1f, 1.1f, 0.36f);
            root.AddChild(blade); // luoi xeng bau tron o dau can (dau xa tay cam)
            return root;
        }

        // Cao co: can dai + vong dai + thanh ngang tren dau (day hon truoc, 0.3 thay vi 0.22) + vai
        // rang cao day hon cam xuong - tong the day dan len de nhin ro la khoi kim loai that, khong
        // phai que/tam mong.
        private static Node3D MakeRake()
        {
            var root = new Node3D();
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 7.6f }, new Vector3(0, 3.2f, 0),
                new Color(0.4f, 0.27f, 0.15f), roughness: 0.9f)); // can go
            root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.25f, Height = 0.4f }, new Vector3(0, 6.9f, 0.15f),
                new Color(0.22f, 0.22f, 0.25f), roughness: 0.4f, metallic: 0.7f)); // vong dai noi can-dau cao
            root.AddChild(MakeMesh(new BoxMesh { Size = new Vector3(2.3f, 0.3f, 0.3f) }, new Vector3(0, 7.3f, 0.3f),
                new Color(0.35f, 0.35f, 0.38f), roughness: 0.4f, metallic: 0.5f)); // thanh ngang
            for (int i = -2; i <= 2; i++)
            {
                root.AddChild(MakeMesh(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 0.85f },
                    new Vector3(i * 0.44f, 6.85f, 0.55f), new Color(0.35f, 0.35f, 0.38f), roughness: 0.4f, metallic: 0.5f)); // rang cao
            }
            return root;
        }
    }
}
