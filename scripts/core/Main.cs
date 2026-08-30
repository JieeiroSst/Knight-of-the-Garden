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

        private PackedScene _scarecrowScene = GD.Load<PackedScene>("res://scenes/decor/Scarecrow.tscn");
        private PackedScene _barnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/Barn.fbx");
        private PackedScene _bigBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/BigBarn.fbx");
        private PackedScene _smallBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/SmallBarn.fbx");
        private PackedScene _treeScene = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_maple_1.glb");
        private PackedScene _treeScene2 = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_birch_1.glb");
        private PackedScene _fenceScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/fence.glb");
        private PackedScene _bridgeScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/bridge.glb");

        private Node3D _world;

        // Nha nong dan la tam neo cho toan bo bo cuc (ruong quanh nha, kieu Stardew Valley)
        private static readonly Vector3 FarmhousePos = new(-300, 0, -60);
        private static readonly Vector3 FarmOrigin = new(-260, 0, 140); // goc luoi ruong (gx=0, gz=0) - lui xa nha kho
        private const float FarmSpacing = 60f;
        private const int FarmGridW = 6;
        private const int FarmGridH = 6;

        // Cong nam cua hang rao ruong (khop cong thuc trong BuildFarmFence) & tam khu lang
        private static readonly Vector3 FarmGatePos = new(
            FarmOrigin.X + (FarmGridW - 1) * FarmSpacing * 0.5f,
            0,
            FarmOrigin.Z + (FarmGridH - 1) * FarmSpacing + 30f);
        private static readonly Vector3 VillageAnchor = new(550, 0, 150);

        public override void _Ready()
        {
            _world = new Node3D { Name = "World" };
            AddChild(_world);

            DrawGround();
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
            // Phai khop CHINH XAC voi vung WorldStreamer bo qua (4 chunk x 500 = 2000, xem
            // ReservedMinCx/MaxCx/Cz trong WorldStreamer.cs) - neu nho hon se ho ra mot vanh
            // dai khong co dat ("vuc") giua San chinh va cac chunk vung hoang da.
            const float width = 2000f;
            const float depth = 2000f;

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

            // Duong dat noi cong ruong (canh cua BuildFarmFence) sang tam khu lang
            AddPath(FarmGatePos, VillageAnchor, 50f);

            // Cau go nho bac ngang giua duong - diem nhan trang tri (Quaternius, CC0)
            {
                var pathDir = (VillageAnchor - FarmGatePos).Normalized();
                var bridgeAngle = Mathf.RadToDeg(Mathf.Atan2(-pathDir.Z, pathDir.X));
                AddDecor(_bridgeScene, (FarmGatePos + VillageAnchor) / 2f, 19f, bridgeAngle);
            }

            // Nha nong dan (nha nguoi choi) - dung model Barn that, to hon han nguoi choi (cao ~40)
            AddDecor(_barnScene, FarmhousePos, 14f);

            // Nha kho (barn) canh nha nong dan - model that (Quaternius Farm Buildings, CC0)
            AddDecor(_barnScene, FarmhousePos + new Vector3(0, 0, -210), 24f);

            // Bu nhin canh hang rao & cay that quanh nha, deu cach xa nha/ruong de khong chong lan
            AddDecor(_scarecrowScene, new Vector3(-330, 0, 290), 1f);
            AddDecor(_treeScene, new Vector3(-470, 0, -90), 34f);
            AddDecor(_treeScene2, new Vector3(-160, 0, -260), 38f);

            // Toa nha chinh cua lang (BigBarn - to nhat trong lang) & cay trang tri quanh khu lang
            AddDecor(_bigBarnScene, VillageAnchor + new Vector3(0, 0, -260), 16f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(-180, 0, -220), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(250, 0, 0), 34f);

            // Nha rieng cho tung dan lang (gan cho khop voi vi tri NPC trong SpawnNpcs) - SmallBarn that
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-200, 0, -120), 12f); // gia lang
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(200, 0, -100), 12f);  // thuong nhan
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(-220, 0, 140), 12f);  // tho ren
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(180, 0, 160), 12f);   // ba lang
            AddDecor(_smallBarnScene, VillageAnchor + new Vector3(0, 0, 280), 12f);     // nguoi gac rung
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
            elder.Position = VillageAnchor + new Vector3(-106, 0, -64);
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
            merchant.Position = VillageAnchor + new Vector3(102, 0, -51);
            _world.AddChild(merchant);

            // Tho ren - ban/bo do vu khi & giap
            var blacksmith = _npcScene.Instantiate<NPC>();
            blacksmith.NpcId = "blacksmith";
            blacksmith.NpcName = "Tho Ren";
            blacksmith.ShopItems = new[] { "sword", "shield", "ring" };
            blacksmith.DialogueLow = new[] { "Muon vu khi tot thi tim dung nguoi roi day. Nhung ta chua quen cau lam." };
            blacksmith.DialogueMid = new[] { "Thep tot can lua tot. Cau ghe thuong xuyen nhi." };
            blacksmith.DialogueHigh = new[] { "Vi tinh ban, ta se ren cho cau mon do ngon nhat xuong." };
            blacksmith.Position = VillageAnchor + new Vector3(-127, 0, 81);
            _world.AddChild(blacksmith);

            // Ba lang thao duoc - ban thuoc hoi mau
            var herbalist = _npcScene.Instantiate<NPC>();
            herbalist.NpcId = "herbalist";
            herbalist.NpcName = "Ba Lang Thao Duoc";
            herbalist.ShopItems = new[] { "potion" };
            herbalist.DialogueLow = new[] { "Thao duoc trong vuon ta co the cuu mang nguoi day, nhung phai biet dung luc." };
            herbalist.DialogueMid = new[] { "Cau lai ghe mua thuoc a? Ta se bot chut dinh gia." };
            herbalist.DialogueHigh = new[] { "Ta se day cau vai bai thuoc bi truyen, ban tre." };
            herbalist.Position = VillageAnchor + new Vector3(98, 0, 87);
            _world.AddChild(herbalist);

            // Nguoi gac rung - giao nhiem vu san quai Gai Tim ngoai hoang da
            var ranger = _npcScene.Instantiate<NPC>();
            ranger.NpcId = "ranger";
            ranger.NpcName = "Nguoi Gac Rung";
            ranger.QuestToGive = "q_spiky_hunt";
            ranger.DialogueLow = new[] { "Vung hoang da phia bac day quai vat lam. Coi chung day." };
            ranger.DialogueMid = new[] { "Cau da chung to ban linh roi. Rung sau con nhieu bi mat." };
            ranger.DialogueHigh = new[] { "Ta tin cau du suc doi mat voi bay quai Gai Tim. Di san di." };
            ranger.Position = VillageAnchor + new Vector3(0, 0, 170);
            _world.AddChild(ranger);
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
