using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Entities;

namespace HiepSiVeVuon.Core
{
    // Dieu phoi man choi: dung the gioi 3D kieu Stardew Valley (nha nong dan + ruong quanh nha,
    // hang rao/duong dat noi sang khu lang co NPC), vong ngay-dem don gian, xu ly luu/nap.
    public partial class Main : Node3D
    {
        private PackedScene _farmScene = GD.Load<PackedScene>("res://scenes/FarmPlot.tscn");
        private PackedScene _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
        private PackedScene _npcScene = GD.Load<PackedScene>("res://scenes/NPC.tscn");
        private PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");

        // Model that (Poly by Google, giay phep CC-BY - can ghi cong, khac cac asset con lai
        // deu CC0 - chon vi khong tim duoc ban CC0 phu hop sau 2 lan tim, va bu nhin nguyen thuy
        // ghep tu khoi hop khong dat yeu cau hinh anh). Ghi cong: "Scarecrow" by Poly by Google,
        // CC-BY (poly.pizza/m/7qFs_DjjuVp).
        private PackedScene _scarecrowScene = GD.Load<PackedScene>("res://assets3d/polybygoogle/scarecrow.glb");
        private PackedScene _barnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/Barn.fbx");
        private PackedScene _bigBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/BigBarn.fbx");
        private PackedScene _smallBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/SmallBarn.fbx");
        private PackedScene _treeScene = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_maple_1.glb");
        private PackedScene _treeScene2 = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_birch_1.glb");
        private PackedScene _fenceScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/fence.glb");
        private PackedScene _bridgeScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/bridge.glb");
        private PackedScene _roadTileScene = GD.Load<PackedScene>("res://assets3d/quaternius/road/path_straight.glb");

        private Node3D _world;

        // Nha nong dan la tam neo cho toan bo bo cuc (ruong quanh nha, kieu Stardew Valley)
        private static readonly Vector3 FarmhousePos = new(-300, 0, -60);
        private static readonly Vector3 FarmOrigin = new(-260, 0, 180); // goc luoi ruong (gx=0, gz=0) - lui xa nha kho them 2m
        private const float FarmSpacing = 60f;
        private const int FarmGridW = 12; // rong them ~9m nua (tong ~18m so voi ban dau)
        private const int FarmGridH = 6;

        // Cong nam cua hang rao ruong (khop cong thuc trong BuildFarmFence) & tam khu lang
        private static readonly Vector3 FarmGatePos = new(
            FarmOrigin.X + (FarmGridW - 1) * FarmSpacing * 0.5f,
            0,
            FarmOrigin.Z + (FarmGridH - 1) * FarmSpacing + 30f);
        // Tam quang truong thi tran - cach nha nong dan ~500m (quy doi 20 don vi/met, nhan vat
        // cao ~40 don vi ~ 2m). Nam trong 1 "dao" dat rieng (xem DrawTownGround), khong dinh lien
        // voi khu nong trai - giua 2 khu la vung hoang da vo han that su cua WorldStreamer, noi
        // voi nhau bang 1 con duong dai (xem AddRoad).
        private static readonly Vector3 VillageAnchor = new(9250, 0, 3750);
        private const float TownGroundSize = 3500f; // phai khop voi vung reserved (15..21, 4..10) trong WorldStreamer.cs

        public override void _Ready()
        {
            _world = new Node3D { Name = "World" };
            AddChild(_world);

            DrawGround();
            DrawTownGround();
            // Duong bat dau cach cong ruong 50m (1000 don vi, quy doi 20 don vi/met) chu khong
            // sat ngay ruong - doan tu cong ra diem nay chi la co, chua trai duong.
            var roadStart = FarmGatePos + (VillageAnchor - FarmGatePos).Normalized() * 1000f;
            AddRoad(roadStart, VillageAnchor);
            SpawnPlayer();
            BuildFarm();
            BuildFarmFence();
            SpawnNpcs();
            SpawnEnemies();
            GiveStartingItems();

            // Vung hoang da vo han xung quanh khu dung san o tren
            _world.AddChild(new WorldStreamer());

            // Chu ky ngay-dem 24h: xoay anh sang theo GameManager.DayProgress (dong bo dong ho
            // may tinh, xem GameManager.cs), hien mat troi/trang
            var dayNight = new DayNightCycle();
            dayNight.Setup(GetNode<DirectionalLight3D>("Sun"), GetNode<WorldEnvironment>("WorldEnvironment"));
            _world.AddChild(dayNight);

            // Sang ngay thuc moi (GameManager tu phat hien qua dong ho may tinh) -> sinh them quai
            GameManager.Instance.DayChanged += _ => RespawnSomeEnemies();

            // Neu co ban luu -> nap
            if (SaveSystem.Instance.HasSave())
            {
                SaveSystem.Instance.LoadGame();
            }
        }

        public override void _Process(double delta)
        {
            if (Input.IsActionJustPressed("save_game"))
                SaveSystem.Instance.SaveGame();
        }

        private void DrawGround()
        {
            // Phai khop CHINH XAC voi vung WorldStreamer bo qua (6 chunk x 500 = 3000, xem
            // ReservedMinCx/MaxCx/Cz trong WorldStreamer.cs) - neu nho hon se ho ra mot vanh
            // dai khong co dat ("vuc") giua San chinh va cac chunk vung hoang da.
            const float width = 3000f;
            const float depth = 3000f;

            var groundMesh = new MeshInstance3D
            {
                Name = "Ground",
                Mesh = new PlaneMesh { Size = new Vector2(width, depth) }
            };
            // Texture co that (ambientCG, CC0), lap lai qua Uv1Scale
            groundMesh.MaterialOverride = GroundMaterial.CreateGrass(width, depth);
            _world.AddChild(groundMesh);

            // Khoi dat dac ngay duoi lop co - de nhin tu goc nao (kem ca ngang tam mat)
            // cung thay dat that co chieu sau, khong phai mot mat phang mong "lo lung tren troi".
            _world.AddChild(GroundMaterial.CreateEarthMass(width, depth));

            // San vat ly cho nhan vat dung/roi trung (khop voi khoi dat dac o tren).
            var floorBody = new StaticBody3D { Name = "GroundCollision" };
            var floorShape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(width, GroundMaterial.EarthDepth, depth) },
                Position = new Vector3(0, -GroundMaterial.EarthDepth / 2f, 0)
            };
            floorBody.AddChild(floorShape);
            _world.AddChild(floorBody);

            // Nha nong dan (nha nguoi choi) - dung model Barn that, to hon han nguoi choi (cao ~40)
            AddDecor(_barnScene, FarmhousePos, 14f);

            // Nha kho (barn) - dat canh ruong, cach hang rao ruong dung 5m (100 don vi) ve phia tay
            AddDecor(_barnScene, new Vector3(-482, 0, 250), 24f);

            // Bu nhin dung giua ruong (khe ho giua cac o dat) & cay that quanh nha
            AddDecor(_scarecrowScene, new Vector3(70, 0, 330), 13f);
            AddDecor(_treeScene, new Vector3(-470, 0, -90), 34f);
            AddDecor(_treeScene2, new Vector3(-160, 0, -260), 38f);

        }

        // Thi tran: mot "dao" dat rieng, cach xa khu nong trai (~500m), noi voi nhau qua AddRoad.
        // Quang truong trung tam, Toa Thi Chinh (BigBarn) o phia bac, 3 vong nha dan quanh -
        // tat ca deu cach nhau >=180 de khong chong lan du nha da phong to.
        private void DrawTownGround()
        {
            var groundMesh = new MeshInstance3D
            {
                Name = "TownGround",
                Mesh = new PlaneMesh { Size = new Vector2(TownGroundSize, TownGroundSize) },
                Position = VillageAnchor
            };
            groundMesh.MaterialOverride = GroundMaterial.CreateGrass(TownGroundSize, TownGroundSize);
            _world.AddChild(groundMesh);

            var earthMass = GroundMaterial.CreateEarthMass(TownGroundSize, TownGroundSize);
            earthMass.Position += VillageAnchor;
            _world.AddChild(earthMass);

            var floorBody = new StaticBody3D { Name = "TownGroundCollision" };
            floorBody.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(TownGroundSize, GroundMaterial.EarthDepth, TownGroundSize) },
                Position = VillageAnchor + new Vector3(0, -GroundMaterial.EarthDepth / 2f, 0)
            });
            _world.AddChild(floorBody);

            AddDecor(_bigBarnScene, VillageAnchor + new Vector3(0, 0, -180), 18f); // Toa Thi Chinh

            // Vong trong
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-170, 0, -60), 12f); // gia lang
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(170, 0, -60), 12f);  // thuong nhan
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-170, 0, 140), 12f); // tho ren
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(170, 0, 140), 12f);  // ba lang
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(0, 0, 220), 12f);    // nguoi gac rung

            // Vong giua
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-350, 0, 20), 12f);  // chu quan tro
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(350, 0, 20), 12f);   // tho moc
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(0, 0, 400), 12f);    // hoc gia

            // Vong ngoai (nha + NPC moi them)
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-500, 0, 250), 12f); // tho may
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(500, 0, 250), 12f);  // nguoi chan cuu

            AddDecor(_treeScene2, VillageAnchor + new Vector3(-300, 0, -140), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(300, 0, -140), 34f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(-460, 0, 120), 34f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(460, 0, 120), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(-160, 0, 480), 34f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(160, 0, 480), 38f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(-650, 0, 350), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(650, 0, 350), 34f);
        }

        private void AddDecor(PackedScene scene, Vector3 pos, float scale, float rotationYDegrees = 0f)
        {
            if (scene == null) return;
            var instance = scene.Instantiate<Node3D>();
            instance.Position = pos;
            instance.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
            instance.Scale = Vector3.One * scale;
            _world.AddChild(instance);
        }

        // Ve mot doan duong dat phang noi 2 diem (dung mau, khong can texture lap).
        private void AddPath(Vector3 from, Vector3 to, float width)
        {
            var mid = (from + to) / 2f;
            float length = from.DistanceTo(to);
            var mesh = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(width, length) } };
            mesh.Position = mid + Vector3.Up * 0.1f; // nhinh nhe tren co, tranh z-fighting
            mesh.RotateY(Mathf.Atan2(to.X - from.X, to.Z - from.Z));
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.45f, 0.3f),
                Roughness = 1f
            };
            _world.AddChild(mesh);
        }

        // Duong noi nong trai - thi tran: nen dat phang mong (dam bao khong ho suot quang duong
        // dai) + rai tam duong da mon 3D that (Quaternius Path Straight, CC0 - nho gon, phong
        // cach dong que, hop voi cac asset khac) len tren, cong go bac giua duong lam diem nhan.
        private void AddRoad(Vector3 from, Vector3 to)
        {
            const float nativeLength = 0.901f; // truc dai la +Z cua model
            const float nativeWidth = 0.496f;
            const float targetTileLength = 45f; // nho gon hon duong cu (50 -> ~25 be rong)
            float tileScale = targetTileLength / nativeLength;
            float roadWidth = nativeWidth * tileScale;

            AddPath(from, to, roadWidth);

            if (_roadTileScene != null)
            {
                float dist = from.DistanceTo(to);
                int count = Mathf.Max(1, Mathf.RoundToInt(dist / targetTileLength));
                float actualTileLength = dist / count;
                float actualScale = actualTileLength / nativeLength;
                Vector3 dir = (to - from).Normalized();
                float angleDeg = Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z));
                for (int i = 0; i < count; i++)
                {
                    var inst = _roadTileScene.Instantiate<Node3D>();
                    inst.Position = from + dir * (actualTileLength * (i + 0.5f)) + Vector3.Up * 0.15f;
                    inst.RotationDegrees = new Vector3(0, angleDeg, 0);
                    inst.Scale = Vector3.One * actualScale;
                    _world.AddChild(inst);
                }
            }

            var pathDir = (to - from).Normalized();
            var bridgeAngle = Mathf.RadToDeg(Mathf.Atan2(-pathDir.Z, pathDir.X));
            AddDecor(_bridgeScene, (from + to) / 2f, 19f, bridgeAngle);
        }

        // Rai lien tiep mot doan hang rao go (Quaternius Fence, truc dai la +X cua model)
        // tu diem nay sang diem kia.
        private void AddFenceLine(Vector3 from, Vector3 to, PackedScene fenceScene)
        {
            if (fenceScene == null) return;
            const float nativeLength = 5.89f;
            const float targetSegment = 100f; // hang rao to hon, cao ~1/2 nguoi choi thay vi lun thun

            float dist = from.DistanceTo(to);
            int count = Mathf.Max(1, Mathf.RoundToInt(dist / targetSegment));
            // Chia deu khop khit tu "from" den "to" (khong dung targetSegment truc tiep) de mep
            // ngoai cua manh dau/cuoi roi dung vao "from"/"to" (cot goc) - khong tam manh tai goc,
            // neu khong manh se tho ra ngoai cot mot nua chieu dai.
            float actualSegment = dist / count;
            float fenceScale = actualSegment / nativeLength;

            Vector3 dir = (to - from).Normalized();
            float angleDeg = Mathf.RadToDeg(Mathf.Atan2(-dir.Z, dir.X));
            for (int i = 0; i < count; i++)
            {
                var inst = fenceScene.Instantiate<Node3D>();
                inst.Position = from + dir * (actualSegment * (i + 0.5f));
                inst.RotationDegrees = new Vector3(0, angleDeg, 0);
                inst.Scale = Vector3.One * fenceScale;
                _world.AddChild(inst);
            }
        }

        private void BuildFarmFence()
        {
            float minX = FarmOrigin.X - 30f;
            float maxX = FarmOrigin.X + (FarmGridW - 1) * FarmSpacing + 30f;
            float minZ = FarmOrigin.Z - 30f;
            float maxZ = FarmGatePos.Z;
            float gateX = FarmGatePos.X;

            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
            AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
            // Nam - chua cong o giua huong ra duong sang lang
            AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 20f, 0, maxZ), _fenceScene);
            AddFenceLine(new Vector3(gateX + 20f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
            // Khoang ho giua 2 doan hang rao tren chinh la loi vao (khong co model cong rieng)

            // Cot go o dung 4 goc de che diem giao nhau cua 2 doan hang rao vuong goc
            // (2 tam go mong cham nhau ngay tai goc nhin bi det/xau neu khong co cot).
            AddFencePost(new Vector3(minX, 0, minZ));
            AddFencePost(new Vector3(maxX, 0, minZ));
            AddFencePost(new Vector3(minX, 0, maxZ));
            AddFencePost(new Vector3(maxX, 0, maxZ));
        }

        private void AddFencePost(Vector3 pos)
        {
            const float postRadius = 4f;
            const float postHeight = 24f;
            var post = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = postRadius, BottomRadius = postRadius * 1.15f, Height = postHeight },
                Position = pos + Vector3.Up * (postHeight / 2f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.3f, 0.2f, 0.1f),
                    Roughness = 1f
                }
            };
            _world.AddChild(post);
        }

        private void SpawnPlayer()
        {
            var player = _playerScene.Instantiate<Player>();
            player.GlobalPosition = FarmhousePos + new Vector3(0, 0, 20);
            _world.AddChild(player);
        }

        private void BuildFarm()
        {
            // Luoi ruong 6x6 ngay truoc nha nong dan (kieu Stardew Valley)
            for (int gx = 0; gx < FarmGridW; gx++)
            {
                for (int gz = 0; gz < FarmGridH; gz++)
                {
                    var plot = _farmScene.Instantiate<FarmPlot>();
                    plot.GridX = gx;
                    plot.GridY = gz;
                    plot.Position = FarmOrigin + new Vector3(gx * FarmSpacing, 0, gz * FarmSpacing);
                    // Xen ke loai giong mac dinh
                    plot.DefaultSeedId = (gx + gz) % 2 == 0 ? "pumpkin_seed" : "tomato_seed";
                    _world.AddChild(plot);
                }
            }
        }

        private void SpawnNpcs()
        {
            // Ong gia lang - giao nhiem vu don rung
            var elder = _npcScene.Instantiate<NPC>();
            elder.NpcId = "elder";
            elder.NpcName = "Ong Gia Lang";
            elder.QuestToGive = "q_clear_mud";
            elder.DialogueLow = new[] { "Chao nguoi la. Vung nay dao nay nhieu quai bun lam." };
            elder.DialogueMid = new[] { "Cau giup lang thi tot qua. Diet lu quai bun giup ta." };
            elder.DialogueHigh = new[] { "Ta tin cau. Nghe don Hang Gai Tim phia dong co kho bau..." };
            elder.Position = VillageAnchor + new Vector3(-110, 0, -20);
            _world.AddChild(elder);

            // Thuong nhan - cua hang hat giong & do
            var merchant = _npcScene.Instantiate<NPC>();
            merchant.NpcId = "merchant";
            merchant.NpcName = "Thuong Nhan";
            merchant.QuestToGive = "q_first_harvest";
            merchant.ShopItems = new[] { "pumpkin_seed", "tomato_seed", "wheat_seed", "potion" };
            merchant.DialogueLow = new[] { "Mua gi khong? Hat giong tot day!" };
            merchant.DialogueMid = new[] { "Khach quen roi! Xem hang di." };
            merchant.DialogueHigh = new[] { "Ban tot, ta se de gia re cho cau." };
            merchant.Position = VillageAnchor + new Vector3(110, 0, -20);
            _world.AddChild(merchant);

            // Tho ren - ban/bo do vu khi & giap
            var blacksmith = _npcScene.Instantiate<NPC>();
            blacksmith.NpcId = "blacksmith";
            blacksmith.NpcName = "Tho Ren";
            blacksmith.ShopItems = new[] { "sword", "shield", "ring" };
            blacksmith.DialogueLow = new[] { "Muon vu khi tot thi tim dung nguoi roi day. Nhung ta chua quen cau lam." };
            blacksmith.DialogueMid = new[] { "Thep tot can lua tot. Cau ghe thuong xuyen nhi." };
            blacksmith.DialogueHigh = new[] { "Vi tinh ban, ta se ren cho cau mon do ngon nhat xuong." };
            blacksmith.Position = VillageAnchor + new Vector3(-110, 0, 100);
            _world.AddChild(blacksmith);

            // Ba lang thao duoc - ban thuoc hoi mau
            var herbalist = _npcScene.Instantiate<NPC>();
            herbalist.NpcId = "herbalist";
            herbalist.NpcName = "Ba Lang Thao Duoc";
            herbalist.ShopItems = new[] { "potion" };
            herbalist.DialogueLow = new[] { "Thao duoc trong vuon ta co the cuu mang nguoi day, nhung phai biet dung luc." };
            herbalist.DialogueMid = new[] { "Cau lai ghe mua thuoc a? Ta se bot chut dinh gia." };
            herbalist.DialogueHigh = new[] { "Ta se day cau vai bai thuoc bi truyen, ban tre." };
            herbalist.Position = VillageAnchor + new Vector3(110, 0, 100);
            _world.AddChild(herbalist);

            // Nguoi gac rung - giao nhiem vu san quai Gai Tim ngoai hoang da
            var ranger = _npcScene.Instantiate<NPC>();
            ranger.NpcId = "ranger";
            ranger.NpcName = "Nguoi Gac Rung";
            ranger.QuestToGive = "q_spiky_hunt";
            ranger.DialogueLow = new[] { "Vung hoang da phia bac day quai vat lam. Coi chung day." };
            ranger.DialogueMid = new[] { "Cau da chung to ban linh roi. Rung sau con nhieu bi mat." };
            ranger.DialogueHigh = new[] { "Ta tin cau du suc doi mat voi bay quai Gai Tim. Di san di." };
            ranger.Position = VillageAnchor + new Vector3(0, 0, 150);
            _world.AddChild(ranger);

            // Chu quan tro - ban thuoc/do uong, tro chuyen phiem
            var innkeeper = _npcScene.Instantiate<NPC>();
            innkeeper.NpcId = "innkeeper";
            innkeeper.NpcName = "Chu Quan Tro";
            innkeeper.ShopItems = new[] { "potion" };
            innkeeper.DialogueLow = new[] { "Chao mung den quan tro. Nguoi la it khi ghe qua day." };
            innkeeper.DialogueMid = new[] { "Uong chut gi cho khoe di, khach quen!" };
            innkeeper.DialogueHigh = new[] { "Chuyen phiem voi cau vui that. Lan sau ta moi ruou ngon." };
            innkeeper.Position = VillageAnchor + new Vector3(-270, 0, 60);
            _world.AddChild(innkeeper);

            // Tho moc - sua chua/ban do go, mua go nguoi choi chat duoc (he thong ban do da co san)
            var carpenter = _npcScene.Instantiate<NPC>();
            carpenter.NpcId = "carpenter";
            carpenter.NpcName = "Tho Moc";
            carpenter.ShopItems = new[] { "shield" };
            carpenter.DialogueLow = new[] { "Co go tot thi mang den day, ta mua het." };
            carpenter.DialogueMid = new[] { "Go cau chat chat luong day. Con bao nhieu mang toi nhe." };
            carpenter.DialogueHigh = new[] { "Ta se dong cho cau mot mon do go dep nhat xuong lang." };
            carpenter.Position = VillageAnchor + new Vector3(270, 0, 60);
            _world.AddChild(carpenter);

            // Hoc gia - nhan vat ke chuyen/lore, khong ban hang, khong nhiem vu
            var scholar = _npcScene.Instantiate<NPC>();
            scholar.NpcId = "scholar";
            scholar.NpcName = "Hoc Gia";
            scholar.DialogueLow = new[] { "Ta danh ca doi nghien cuu vung dat nay. Co gi thac mac cu hoi ta." };
            scholar.DialogueMid = new[] { "Cau ngay cang gan gui voi vung dat nay roi day." };
            scholar.DialogueHigh = new[] { "Trong sach co ke ve mot hiep si bao ve khu vuon huyen thoai... co le la cau." };
            scholar.Position = VillageAnchor + new Vector3(0, 0, 320);
            _world.AddChild(scholar);

            // Tho may - ban trang phuc/phu kien (dung lai vat pham giap co san)
            var tailor = _npcScene.Instantiate<NPC>();
            tailor.NpcId = "tailor";
            tailor.NpcName = "Tho May";
            tailor.ShopItems = new[] { "ring", "shield" };
            tailor.DialogueLow = new[] { "Vai vor toi cung co the may cho cau mot bo dep." };
            tailor.DialogueMid = new[] { "Do cau mac cung kha day, nhung de ta chinh lai chut." };
            tailor.DialogueHigh = new[] { "Rieng cho cau, ta se may mon do dac biet nhat tiem." };
            tailor.Position = VillageAnchor + new Vector3(-420, 0, 210);
            _world.AddChild(tailor);

            // Nguoi chan cuu - flavor, khong ban hang/nhiem vu
            var shepherd = _npcScene.Instantiate<NPC>();
            shepherd.NpcId = "shepherd";
            shepherd.NpcName = "Nguoi Chan Cuu";
            shepherd.DialogueLow = new[] { "Dan cuu cua ta thich gam co ngoai dong lam." };
            shepherd.DialogueMid = new[] { "Cau co ve hop voi cuoc song dong que roi day." };
            shepherd.DialogueHigh = new[] { "Bao gio ranh, ghe tham dan cuu cua ta nhe." };
            shepherd.Position = VillageAnchor + new Vector3(420, 0, 210);
            _world.AddChild(shepherd);
        }

        private void SpawnEnemies()
        {
            // Quai tap trung o vung hoang da phia bac nha nong dan, tach khoi khu ruong & lang
            SpawnEnemy("mud_monster", new Vector3(-80, 0, -320));
            SpawnEnemy("mud_monster", new Vector3(60, 0, -380));
            SpawnEnemy("mud_monster", new Vector3(180, 0, -300));
            SpawnEnemy("spiky_monster", new Vector3(120, 0, -450));
        }

        private void SpawnEnemy(string id, Vector3 pos)
        {
            var e = _enemyScene.Instantiate<Enemy>();
            e.EnemyId = id;
            e.Position = pos;
            _world.AddChild(e);
        }

        private const int MaxHandPlacedEnemies = 8;

        private void RespawnSomeEnemies()
        {
            // Moi ngay sinh them vai quai bun de co gi de danh, nhung gioi han tong so de
            // khong don cuc thanh mot dong quai chong len nhau qua nhieu ngay khong ai danh.
            if (GetTree().GetNodesInGroup("enemies").Count >= MaxHandPlacedEnemies) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            for (int i = 0; i < 2; i++)
                SpawnEnemy("mud_monster", new Vector3(rng.RandfRange(-150, 250), 0, rng.RandfRange(-480, -280)));
        }

        private void GiveStartingItems()
        {
            // Chi cap do khoi dau neu chua co ban luu
            if (SaveSystem.Instance.HasSave()) return;
            Inventory.Instance.AddItem("pumpkin_seed", 3);
            Inventory.Instance.AddItem("tomato_seed", 2);
            Inventory.Instance.AddItem("potion", 2);
            Inventory.Instance.AddItem("sword", 1);
            Inventory.Instance.Equip("sword");
        }
    }
}
