using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Core
{
    // Dieu phoi man choi: dung the gioi 3D kieu Stardew Valley (nha nong dan + ruong quanh nha,
    // hang rao/duong dat noi sang khu lang co NPC), vong ngay-dem don gian, xu ly luu/nap.
    public partial class Main : Node3D
    {
        // Cache vat lieu mau don gian (khong texture, chi mau+do nham) - dung SAN chung nhau
        // thay vi tao Resource moi moi lan goi (AddInteriorWall/BuildRoom goi rat nhieu lan: moi
        // 1 trong 13 cong trinh x 2 tang x [4 tuong + san + tran] ~ 150+ lan neu khong cache).
        // Giam manh so luong RefCounted object sinh ra dong loat luc dung san - vua nhanh hon,
        // vua giam tai cho GC/he thong tham chieu native cua Godot Mono.
        private readonly Dictionary<(Color, float), StandardMaterial3D> _materialCache = new();

        private StandardMaterial3D GetCachedMaterial(Color color, float roughness = 0.85f)
        {
            var key = (color, roughness);
            if (!_materialCache.TryGetValue(key, out var mat))
            {
                mat = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Roughness = roughness,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                };
                _materialCache[key] = mat;
            }
            return mat;
        }

        private PackedScene _farmScene = GD.Load<PackedScene>("res://scenes/FarmPlot.tscn");
        private PackedScene _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
        private PackedScene _npcScene = GD.Load<PackedScene>("res://scenes/NPC.tscn");
        private PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");
        private PackedScene _cowScene = GD.Load<PackedScene>("res://scenes/Cow.tscn");
        private PackedScene _farmhandScene = GD.Load<PackedScene>("res://scenes/FarmhandNpc.tscn");
        private PackedScene _horseScene = GD.Load<PackedScene>("res://scenes/Horse.tscn");
        private PackedScene _stablehandScene = GD.Load<PackedScene>("res://scenes/StablehandNpc.tscn");
        private PackedScene _dogScene = GD.Load<PackedScene>("res://scenes/Dog.tscn");
        private PackedScene _farmDogScene = GD.Load<PackedScene>("res://scenes/FarmDog.tscn");
        private PackedScene _farmCatScene = GD.Load<PackedScene>("res://scenes/FarmCat.tscn");
        private PackedScene _chickenScene = GD.Load<PackedScene>("res://scenes/Chicken.tscn");
        private PackedScene _poultryKeeperScene = GD.Load<PackedScene>("res://scenes/PoultryKeeperNpc.tscn");
        private PackedScene _citizenScene = GD.Load<PackedScene>("res://scenes/TownCitizenNpc.tscn");

        // Danh sach vi tri cac can nha trong khu do thi (xem BuildCityDistrict) - dung de gan
        // "nha rieng" cho tung nguoi dan (SpawnTownCitizens) sau khi khu do thi da dung xong.
        private readonly List<Vector3> _cityHousePositions = new();
        private readonly List<Vector3> _cityHouseInteriors = new();
        // Chuong ga (Quaternius, CC0, poly.pizza/m/DM0F8siLam) - dat lam cong trinh chinh trong
        // khu chuong ga, giong cach coi cua bo/ngua la hang rao + mang an.
        private PackedScene _chickenCoopScene = GD.Load<PackedScene>("res://assets3d/quaternius/misc/chicken_coop.glb");
        // Cot den duong (Post Lantern by Kay Lousberg, CC0, poly.pizza/m/ZSQ65S4lEu) - phong
        // cach go/lang que hop voi nong trai hon den duong hien dai.
        private PackedScene _lampPostScene = GD.Load<PackedScene>("res://assets3d/misc/lamp_post.glb");

        // Model that (Poly by Google, giay phep CC-BY - can ghi cong, khac cac asset con lai
        // deu CC0 - chon vi khong tim duoc ban CC0 phu hop sau 2 lan tim, va bu nhin nguyen thuy
        // ghep tu khoi hop khong dat yeu cau hinh anh). Ghi cong: "Scarecrow" by Poly by Google,
        // CC-BY (poly.pizza/m/7qFs_DjjuVp).
        private PackedScene _scarecrowScene = GD.Load<PackedScene>("res://assets3d/polybygoogle/scarecrow.glb");
        private PackedScene _barnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/Barn.fbx");
        // Nha o that (model hoan chinh, khong ghep manh) - "House_2" trong Quaternius Medieval
        // Village Pack, CC0, nhieu chi tiet hon (9 manh vat lieu) va cao/to hon ban truoc.
        private PackedScene _farmhouseScene = GD.Load<PackedScene>("res://assets3d/quaternius/buildings/house_v2.glb");
        private PackedScene _bigBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/BigBarn.fbx");
        private PackedScene _smallBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/SmallBarn.fbx");
        private PackedScene _treeScene = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_maple_1.glb");
        private PackedScene _treeScene2 = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_birch_1.glb");
        private PackedScene _fenceScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/fence.glb");
        private PackedScene _bridgeScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/bridge.glb");

        // Chan de (X,Z) o don vi goc cua tung loai model cong trinh - dung de tao va cham dac
        // (xem AddDecor). Barn/BigBarn dung chung 1 khung, SmallBarn nho hon, Farmhouse rieng.
        private static readonly Vector2 BarnFootprint = new(7.727f, 8.222f);
        private static readonly Vector2 SmallBarnFootprint = new(6.079f, 6.274f);
        private static readonly Vector2 FarmhouseFootprint = new(2.22f, 3.42f);
        private PackedScene _roadTileScene = GD.Load<PackedScene>("res://assets3d/quaternius/road/path_straight.glb");

        // Cua vao 3D that + do noi that (Quaternius Medieval Village Pack, CC0) cho tinh nang
        // vao/ra cong trinh - xem BuildInterior/AddBuildingEntrance.
        private PackedScene _doorScene = GD.Load<PackedScene>("res://assets3d/quaternius/buildings/door_round.glb");
        private PackedScene _benchScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bench.glb");
        private PackedScene _crateScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/crate.glb");
        private PackedScene _barrelScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/barrel.glb");
        private PackedScene _bagScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bag.glb");
        // Them do noi that rieng biet cho tung loai phong (Quaternius Ultimate House Interior
        // Pack + do vat nong trai rieng, deu CC0) - nha o co giuong/ban/ghe/lo suoi, nha kho co
        // rom/bao thoc rieng biet khong giong nha dan hay Toa Thi Chinh.
        private PackedScene _tableScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/table_round.glb");
        private PackedScene _chairScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/chair.glb");
        private PackedScene _bedScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bed_single.glb");
        private PackedScene _rugScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/rug.glb");
        private PackedScene _chandelierScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/chandelier.glb");
        private PackedScene _fireplaceScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/fireplace.glb");
        private PackedScene _hayScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/hay.glb");
        private PackedScene _sackScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/sack_trench.glb");
        private PackedScene _stairsScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/stairs.glb");
        // Vat dung nong nghiep that (dung cu + nong san) rieng cho nha kho, CC0.
        private PackedScene _pitchforkScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/pitchfork.glb");
        private PackedScene _hoeScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/hoe.glb");
        private PackedScene _wateringCanScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/watering_can.glb");
        private PackedScene _pumpkinScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/pumpkin.glb");
        private PackedScene _wheatScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/wheat.glb");
        private PackedScene _cartScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/cart.glb");
        private PackedScene _woodLogScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/wood_log.glb");

        private Node3D _world;

        // Nha nong dan la tam neo cho toan bo bo cuc (ruong quanh nha, kieu Stardew Valley)
        private static readonly Vector3 FarmhousePos = new(-300, 0, -60);
        private static readonly Vector3 FarmOrigin = new(-260, 0, 180); // goc luoi ruong (gx=0, gz=0) - lui xa nha kho them 2m
        private const float FarmSpacing = 84f; // to hon 40% so voi truoc (60 -> 84), khop voi o dat da phong to
        private const int FarmGridW = 12; // rong them ~9m nua (tong ~18m so voi ban dau)
        private const int FarmGridH = 6;

        // Cong nam cua hang rao ruong (khop cong thuc trong BuildFarmFence) & tam khu lang
        private static readonly Vector3 FarmGatePos = new(
            FarmOrigin.X + (FarmGridW - 1) * FarmSpacing * 0.5f,
            0,
            FarmOrigin.Z + (FarmGridH - 1) * FarmSpacing + 42f);
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

            // Chu ky ngay-dem 24h (anh sang mat troi + mau bau troi) + vung hoang da vo han:
            // dat NGAY SAU KHI CO PLAYER, TRUOC toan bo cong trinh/vat nuoi con lai ben duoi -
            // dam bao anh sang/bau troi LUON duoc thiet lap dung cho du 1 buoc dung san nao do
            // ben duoi loi (vd 1 asset chua duoc Godot import). Truoc day doan nay nam O CUOI
            // _Ready(), nen 1 loi nho (vd .tscn/.glb loi) o giua chuoi dung san se lam ca doan
            // setup anh sang khong bao gio chay toi -> man hinh xam/mat het do hoa hoan toan.
            var dayNight = new DayNightCycle();
            dayNight.Setup(GetNode<DirectionalLight3D>("Sun"), GetNode<WorldEnvironment>("WorldEnvironment"));
            _world.AddChild(dayNight);
            _world.AddChild(new WorldStreamer());

            // Boc try/catch: neu 1 buoc dung san cu the (cong trinh/vat nuoi) loi vi ly do gi,
            // NHUNG BUOC DA CHAY TRUOC DO van giu nguyen (khong bien mat theo) - loi duoc in ro
            // rang ra log (Debugger > Errors) de de tim, thay vi im lang lam gian doan toan bo
            // phan con lai va khien ca man hinh trong xam.
            try
            {
                BuildFarm();
                BuildFarmFence();
                SpawnNpcs();
                SpawnEnemies();
                GiveStartingItems();
                BuildCowPasture();
                BuildCowherd();
                BuildHorseStable();
                BuildStablehand();
                BuildChickenCoop();
                BuildPoultryKeeper();
                BuildPlateaus();
                BuildSunflowerField();
                BuildCityDistrict();
                SpawnTownCitizens();
            }
            catch (System.Exception e)
            {
                GD.PushError($"Loi khi dung san the gioi (mot phan co the bi thieu, xem chi tiet ben duoi): {e}");
            }

            // Sang ngay thuc moi (GameManager tu phat hien qua dong ho may tinh) -> sinh them quai
            GameManager.Instance.DayChanged += _ => RespawnSomeEnemies();
            // Den duong tu bat/tat theo dung gio (18h - 6h sang). Ap dung trang thai ban dau
            // MOT LAN cho TAT CA cot den (ca 4 cot rieng va 2*13 cot o tung cong trinh) - phai
            // dat SAU khi toan bo cong trinh da duoc xay xong (BuildPoultryKeeper la cong trinh cuoi
            // cung o tren), neu khong cac cot tao sau se giu trang thai mac dinh sai.
            GameManager.Instance.HourChanged += OnStreetLampHourChanged;
            SetStreetLampsOn(IsStreetLampHour(GameManager.Instance.Hour));
            // ...va thu cho bo giao phoi sinh be con (xem TryBreedCows).
            GameManager.Instance.DayChanged += _ => TryBreedCows();

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

            // Nha nong dan (nha nguoi choi) - model nha that chi tiet hon, to them 20%, xoay 180 do
            AddDecor(_farmhouseScene, FarmhousePos, 66f, 180f, FarmhouseFootprint);
            AddBuildingEntrance(FarmhousePos, 180f, 150f, 120f, RoomKind.Farmhouse);

            // Nha kho (barn) - dat canh ruong, cach hang rao ruong dung 5m (100 don vi) ve phia tay
            var barnPos = new Vector3(-482, 0, 250);
            AddDecor(_barnScene, barnPos, 24f, 0f, BarnFootprint);
            AddBuildingEntrance(barnPos, 0f, 150f, 110f, RoomKind.Barn);

            // Bu nhin dung giua ruong (khe ho giua cac o dat) & cay that quanh nha
            AddDecor(_scarecrowScene, new Vector3(70, 0, 330), 13f);
            AddDecor(_treeScene, new Vector3(-470, 0, -90), 34f);
            AddDecor(_treeScene2, new Vector3(-160, 0, -260), 38f);

            // 4 cot den duong: 2 cai hai ben loi di truoc nha nong dan, 2 cai hai ben cong ruong.
            // (Moi cong trinh khac tu them 2 cot rieng ngay tai cua - xem AddBuildingEntrance.
            // Trang thai bat/tat that su duoc ap dung 1 lan cho TAT CA cot sau khi da dat xong,
            // xem cuoi _Ready().)
            AddStreetLamp(FarmhousePos + new Vector3(-45, 0, 80), 0f);
            AddStreetLamp(FarmhousePos + new Vector3(45, 0, 80), 180f);
            AddStreetLamp(new Vector3(FarmGatePos.X - 35, 0, FarmGatePos.Z), 90f);
            AddStreetLamp(new Vector3(FarmGatePos.X + 35, 0, FarmGatePos.Z), -90f);
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

            var townHallPos = VillageAnchor + new Vector3(0, 0, -180);
            AddDecor(_bigBarnScene, townHallPos, 18f, 0f, BarnFootprint); // Toa Thi Chinh
            AddBuildingEntrance(townHallPos, 0f, 120f, 90f, RoomKind.TownHall);

            // Vong trong
            AddSmallHouse(VillageAnchor + new Vector3(-170, 0, -60)); // gia lang
            AddSmallHouse(VillageAnchor + new Vector3(170, 0, -60));  // thuong nhan
            AddSmallHouse(VillageAnchor + new Vector3(-170, 0, 140)); // tho ren
            AddSmallHouse(VillageAnchor + new Vector3(170, 0, 140));  // ba lang
            AddSmallHouse(VillageAnchor + new Vector3(0, 0, 220));    // nguoi gac rung

            // Vong giua
            AddSmallHouse(VillageAnchor + new Vector3(-350, 0, 20));  // chu quan tro
            AddSmallHouse(VillageAnchor + new Vector3(350, 0, 20));   // tho moc
            AddSmallHouse(VillageAnchor + new Vector3(0, 0, 400));    // hoc gia

            // Vong ngoai (nha + NPC moi them)
            AddSmallHouse(VillageAnchor + new Vector3(-500, 0, 250)); // tho may
            AddSmallHouse(VillageAnchor + new Vector3(500, 0, 250));  // nguoi chan cuu

            AddDecor(_treeScene2, VillageAnchor + new Vector3(-300, 0, -140), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(300, 0, -140), 34f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(-460, 0, 120), 34f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(460, 0, 120), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(-160, 0, 480), 34f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(160, 0, 480), 38f);
            AddDecor(_treeScene2, VillageAnchor + new Vector3(-650, 0, 350), 38f);
            AddDecor(_treeScene, VillageAnchor + new Vector3(650, 0, 350), 34f);
        }

        // collisionFootprint: kich thuoc chan de (X,Z) o don vi GOC cua model (truoc khi nhan
        // "scale") - neu khac Vector2.Zero se them va cham dac, khien nguoi choi khong the di
        // xuyen qua cong trinh (chi di vong quanh, dung nhu nha/lau dai that ngoai doi).
        private void AddDecor(PackedScene scene, Vector3 pos, float scale, float rotationYDegrees = 0f, Vector2 collisionFootprint = default)
        {
            if (scene == null) return;
            var instance = scene.Instantiate<Node3D>();
            instance.Position = pos;
            instance.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
            instance.Scale = Vector3.One * scale;
            _world.AddChild(instance);

            if (collisionFootprint != default)
            {
                var body = new StaticBody3D();
                body.Position = pos;
                body.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
                const float collisionHeight = 300f; // du cao de chan nguoi choi bat ke chieu cao that cua cong trinh
                body.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = new Vector3(collisionFootprint.X * scale, collisionHeight, collisionFootprint.Y * scale) },
                    Position = Vector3.Up * (collisionHeight / 2f)
                });
                _world.AddChild(body);
            }
        }

        private enum RoomKind { Farmhouse, Barn, TownHall, Village }

        // Danh so thu tu cong trinh -> vi tri phong duoi long dat rieng cho tung cong trinh
        // (cach nhau 500 don vi tren truc X de khong bao gio chong len nhau).
        private int _nextInteriorIndex = 0;

        private void AddSmallHouse(Vector3 pos)
        {
            AddDecor(_smallBarnScene, pos, 12f, 0f, SmallBarnFootprint);
            AddBuildingEntrance(pos, 0f, 80f, 50f, RoomKind.Village);
        }

        // Cong trinh + noi that RIENG cho tung cong trinh (khong dung chung nua - moi nha co 1
        // phong khac nhau, xem BuildRoomForKind): them 1 vung tuong tac (E) bao quanh ca cong
        // trinh (du de kich hoat tu bat ky huong tiep can nao, vi khong the xac dinh chinh xac
        // mat tien that su cua tung model neu khong xem truc quan - bai hoc tu lan gan cua rieng
        // bi lech voi tuong nha truoc day) + 1 cua 3D that (Quaternius Door Round, CC0).
        // Tra ve interiorAnchor (tang tret) de nguoi goi (vd BuildCowherd) co the dua NPC vao
        // dung phong nay khi can (vd di ngu ban dem).
        private Vector3 AddBuildingEntrance(Vector3 buildingPos, float rotationYDegrees, float triggerRadius, float doorDistance, RoomKind kind)
        {
            // Phong noi that dat NGAY PHIA TREN vi tri that cua chinh ngoi nha nay (cung X,Z) -
            // gan lien voi ngoi nha thay vi giau o mot khu vuc tach biet rat xa. Khong the nhin
            // xuyen tuong vao trong (model vo ngoai dac, khong co lo hong that), nhung it nhat
            // toa do gan dung vi tri that. Do cac nha trong lang co the dung gan nhau hon kich
            // thuoc phong (vd 2 nha dan cach nhau 200 don vi nhung phong rong toi 380), moi cong
            // trinh dung 1 do cao (Y) RIENG - each 900 don vi - de phong cua nha nay khong bao
            // gio de vao khong gian cua nha ke ben du toa do X,Z co gan nhau.
            var interiorAnchor = new Vector3(buildingPos.X, 500f + _nextInteriorIndex * 900f, buildingPos.Z);
            _nextInteriorIndex++;

            AddBuildingDoor(buildingPos, triggerRadius, isExit: false, interiorAnchor);
            // Tang 2: cung 1 vi tri X,Z, nam cao han 400 don vi (du xa moi wallHeight toi da 200 +
            // du choi de khong cham tran tang 1).
            var floor2Anchor = interiorAnchor + Vector3.Up * 400f;
            BuildRoomForKind(interiorAnchor, floor2Anchor, kind);

            var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));

            if (_doorScene != null)
            {
                var door = _doorScene.Instantiate<Node3D>();
                door.Position = buildingPos + basis * new Vector3(0, 0, doorDistance);
                door.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
                door.Scale = Vector3.One * 55f;
                _world.AddChild(door);
            }

            // Theo yeu cau: moi cong trinh co them 2 cot den, dat hai ben loi vao (dung basis da
            // tinh cho cua) - khoang cach ty le voi kich thuoc cong trinh (triggerRadius) de
            // khong dam vao tuong nha nho hay qua gan nha to.
            float lampOffset = Mathf.Max(35f, triggerRadius * 0.5f);
            AddStreetLamp(buildingPos + basis * new Vector3(-lampOffset, 0, doorDistance * 0.75f), rotationYDegrees);
            AddStreetLamp(buildingPos + basis * new Vector3(lampOffset, 0, doorDistance * 0.75f), rotationYDegrees);

            return interiorAnchor;
        }

        private void AddBuildingDoor(Vector3 pos, float triggerRadius, bool isExit, Vector3 interiorAnchor = default, bool isFloorChange = false, bool isAutoTrigger = false)
        {
            var door = new BuildingDoor { IsExit = isExit, InteriorAnchor = interiorAnchor, IsFloorChange = isFloorChange, IsAutoTrigger = isAutoTrigger };
            door.Position = pos;
            door.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = triggerRadius } });
            _world.AddChild(door);
        }

        // Chon kich thuoc/mau sac/do dac theo LOAI cong trinh, de moi loai nha co khong gian
        // rieng thay vi 1 phong dung chung y het nhau cho tat ca (nha nong dan am cung, nha kho
        // chat day thung/bao nhu kho that, Toa Thi Chinh rong va trang trong, nha dan don gian
        // hon va co vai bien the mau/do dac khac nhau giua cac nha). Moi cong trinh co 2 tang:
        // tang tret (groundAnchor, co cua ra ngoai + cau thang len) va tang 2 (floor2Anchor, co
        // cau thang xuong) - noi that tang 2 la 1 bo rieng, khac tang tret.
        private void BuildRoomForKind(Vector3 groundAnchor, Vector3 floor2Anchor, RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Farmhouse:
                    // roomSize PHAI du lon: camera cua Player nam co dinh cach nhan vat 115 don
                    // vi VE PHIA SAU (dung offset ngoai troi, xem Player.tscn Camera3D) - neu
                    // roomSize/2 < 115 thi camera se nam NGOAI phong (xuyen qua tuong sau lung),
                    // nhin thay mat trong cua tuong o cu ly cuc gan -> day chinh la nguyen nhan
                    // "khong gian nha bi vo" da xay ra o moi phong truoc day.
                    // Tang tret: phong khach/an - ban ghe + lo suoi. Tang 2: phong ngu rieng.
                    BuildRoom(groundAnchor, 380f, 150f,
                        wallColor: new Color(0.83f, 0.74f, 0.58f),
                        floorColor: new Color(0.5f, 0.34f, 0.19f),
                        rugColor: new Color(0.55f, 0.15f, 0.15f),
                        backTarget: default, backIsExit: true, upTarget: floor2Anchor,
                        furnish: a =>
                        {
                            AddDecor(_fireplaceScene, a + new Vector3(0, 0, -145), 9f);
                            AddDecor(_tableScene, a + new Vector3(110, 0, -90), 12f);
                            AddDecor(_chairScene, a + new Vector3(110, 0, -55), 10f, 180f);
                            AddDecor(_chairScene, a + new Vector3(75, 0, -90), 10f, 90f);
                            AddDecor(_rugScene, a + new Vector3(0, 0, 30), 11f);
                            AddDecor(_bagScene, a + new Vector3(-135, 0, 110), 95f);
                            AddDecor(_barrelScene, a + new Vector3(130, 0, 120), 95f);
                        });
                    BuildRoom(floor2Anchor, 300f, 130f,
                        wallColor: new Color(0.86f, 0.78f, 0.64f),
                        floorColor: new Color(0.55f, 0.4f, 0.24f),
                        rugColor: new Color(0.35f, 0.3f, 0.55f),
                        backTarget: groundAnchor, backIsExit: false, upTarget: null,
                        furnish: a =>
                        {
                            AddDecor(_bedScene, a + new Vector3(-100, 0, -90), 10f, 90f);
                            AddDecor(_bagScene, a + new Vector3(90, 0, 100), 90f);
                        });
                    break;

                case RoomKind.Barn:
                    // Dac biet theo yeu cau: kho chua VAT DUNG NONG NGHIEP (cao/cuoc/binh tuoi)
                    // + SAN PHAM NONG NGHIEP (bi ngo, lua mi) that, cong voi rom/bao thoc/thung/
                    // thung go/xe day/cui go - lap day ca 4 buc tuong va khoang giua phong, khong
                    // giong bat ky nha nao khac hay Toa Thi Chinh. Phong nay to HAN HAN cac phong
                    // khac (900 thay vi ~400) vi can du dien tich xep hang ke that su (xem
                    // BuildWarehouseGrid ben duoi) - phong noi that la khong gian rieng, khong
                    // gan voi kich thuoc vo ngoai that su cua nha kho nen phong to bao nhieu cung
                    // duoc. Tranh 2 vung: goc Tay Bac (cau thang) va loi vao truoc cua o giua
                    // tuong Nam.
                    BuildRoom(groundAnchor, 900f, 220f,
                        wallColor: new Color(0.5f, 0.28f, 0.2f),
                        floorColor: new Color(0.42f, 0.3f, 0.18f),
                        rugColor: null,
                        backTarget: default, backIsExit: true, upTarget: floor2Anchor,
                        furnish: a =>
                        {
                            // Tuong Bac
                            AddDecor(_hayScene, a + new Vector3(-40, 0, -185), 118f);
                            AddDecor(_sackScene, a + new Vector3(10, 0, -185), 5f, 20f);
                            AddDecor(_hayScene, a + new Vector3(80, 0, -180), 120f, -15f);
                            AddDecor(_crateScene, a + new Vector3(150, 0, -185), 100f);
                            // Tuong Tay (phia duoi goc cau thang)
                            AddDecor(_barrelScene, a + new Vector3(-185, 0, -30), 100f);
                            AddDecor(_sackScene, a + new Vector3(-185, 0, 30), 5f, -20f);
                            AddDecor(_crateScene, a + new Vector3(-185, 0, 90), 100f);
                            AddDecor(_woodLogScene, a + new Vector3(-180, 0, 150), 5.7f, 40f);
                            // Tuong Dong
                            AddDecor(_crateScene, a + new Vector3(185, 0, -150), 100f, 10f);
                            AddDecor(_barrelScene, a + new Vector3(185, 0, -85), 100f);
                            AddDecor(_hayScene, a + new Vector3(180, 0, -20), 115f, 60f);
                            AddDecor(_barrelScene, a + new Vector3(185, 0, 60), 100f, 15f);
                            AddDecor(_sackScene, a + new Vector3(185, 0, 130), 5f);
                            // Tuong Nam (2 ben cua ra vao o giua)
                            AddDecor(_wheatScene, a + new Vector3(-150, 0, 180), 11f);
                            AddDecor(_wheatScene, a + new Vector3(-125, 0, 185), 11f, 30f);
                            AddDecor(_pumpkinScene, a + new Vector3(-95, 0, 180), 5.3f);
                            AddDecor(_wheatScene, a + new Vector3(100, 0, 180), 11f, -20f);
                            AddDecor(_pumpkinScene, a + new Vector3(130, 0, 182), 5.3f, 20f);
                            AddDecor(_pumpkinScene, a + new Vector3(150, 0, 175), 5.3f, -30f);
                            // Nong san thu hoach chat dong giua khu vuc Tay - dung loai cay trong
                            // da co san trong game (pumpkin_seed)
                            AddDecor(_pumpkinScene, a + new Vector3(-155, 0, -140), 5.3f);
                            AddDecor(_pumpkinScene, a + new Vector3(-135, 0, -155), 5.3f, 40f);
                            // Xe day cho hang giua phong - diem nhan trung tam
                            AddDecor(_cartScene, a + new Vector3(20, 0, 20), 39f, 25f);
                            AddDecor(_woodLogScene, a + new Vector3(-40, 0, 60), 5.7f, -20f);
                            // Dung cu lao dong that dung gan cua ra vao, nhu vua duoc dem ve
                            AddDecor(_pitchforkScene, a + new Vector3(60, 0, 155), 10f, 15f);
                            AddDecor(_hoeScene, a + new Vector3(75, 0, 160), 19f, -10f);
                            AddDecor(_wateringCanScene, a + new Vector3(50, 0, 165), 23f);
                            // Chat them kien hang nua (rai ngau nhien co seed co dinh, tranh goc
                            // cau thang va loi vao) cho kho day ap hang hoa nhu 1 nha kho that su
                            // dang hoat dong, khong con trong trai. Loai hang + so luong doc TU
                            // DATABASE (WarehouseDatabase/warehouse_products.json) thay vi hard-
                            // code truc tiep - muon them/doi hang chi can sua JSON.
                            ScatterBarnStock(a, 210f, 4001,
                                new Vector3(-145, 0, -145), 95f, new Vector3(0, 0, 185), 70f,
                                WarehouseDatabase.Instance.GetScatterRecipe());

                            // Khu vuc ke hang chinh (nhu 1 nha kho that su): cac cot hang xep
                            // CHONG CAO 8 tang, dung tung hang rieng theo cot (loai hang doc tu
                            // database, UseInGrid=true) - hang xep ke, hang de loi di - cho
                            // ~1000 kien hang nhung van "gon gang" (co loi di ro rang giua cac
                            // day ke, khong chong len nhau) thay vi 1 dong hang lon xon. Bo qua
                            // khu vuc gan cua/trung tam (da co do dac dot 1) va goc cau thang.
                            BuildWarehouseGrid(a, 450f,
                                new Vector3(-385, 0, -385), 90f, new Vector3(0, 0, 425), 80f, 200f,
                                50f, 50f, 60f, 10, 17f,
                                WarehouseDatabase.Instance.GetGridRecipe());

                            // Bang go treo tren cua ra vao, hien so luong san pham (trung ga...)
                            // da duoc NPC cham nuoi thu hoach va cat vao kho (xem FarmStorage,
                            // PoultryKeeperNpc.HarvestNearbyEggs) - dap ung yeu cau "quy hoach lai
                            // nha kho de dem so luong san pham".
                            _world.AddChild(new FarmStorageBoard { Position = a + new Vector3(0, 130, 180) });
                        });
                    BuildRoom(floor2Anchor, 340f, 140f,
                        wallColor: new Color(0.45f, 0.25f, 0.18f),
                        floorColor: new Color(0.4f, 0.28f, 0.16f),
                        rugColor: null,
                        backTarget: groundAnchor, backIsExit: false, upTarget: null,
                        furnish: a =>
                        {
                            // Tuong Bac
                            AddDecor(_hayScene, a + new Vector3(-100, 0, -150), 115f, 20f);
                            AddDecor(_sackScene, a + new Vector3(-30, 0, -150), 5f);
                            AddDecor(_hayScene, a + new Vector3(50, 0, -145), 112f, -30f);
                            // Tuong Tay
                            AddDecor(_sackScene, a + new Vector3(-150, 0, -60), 5f, 20f);
                            AddDecor(_crateScene, a + new Vector3(-150, 0, 20), 95f);
                            AddDecor(_barrelScene, a + new Vector3(-150, 0, 90), 95f);
                            // Nong san & rom chat quanh gac lung (tranh goc Dong Nam la cau thang xuong)
                            AddDecor(_pumpkinScene, a + new Vector3(0, 0, -100), 5.3f);
                            AddDecor(_pumpkinScene, a + new Vector3(25, 0, -85), 5.3f, 30f);
                            AddDecor(_wheatScene, a + new Vector3(-60, 0, 100), 11f);
                            AddDecor(_wheatScene, a + new Vector3(-30, 0, 110), 11f, 25f);
                            AddDecor(_woodLogScene, a + new Vector3(60, 0, -60), 5.5f);
                            AddDecor(_sackScene, a + new Vector3(90, 0, 0), 5f, -15f);
                            // Chat them ~24 kien hang nua tren gac (tranh goc cau thang xuong)
                            ScatterBarnStock(a, 170f, 4002,
                                new Vector3(105, 0, 105), 95f, new Vector3(9999, 0, 9999), 0f,
                                (_sackScene, 5f, 7), (_crateScene, 95f, 5), (_barrelScene, 95f, 5),
                                (_hayScene, 112f, 4), (_pumpkinScene, 5.3f, 2), (_wheatScene, 11f, 1));
                        });
                    break;

                case RoomKind.TownHall:
                    // Phong rong, tuong da xam, den chum treo giua tran - trang trong khac han
                    // nha o / nha kho, dung ghe dai xep doi xung nhu 1 sanh hop. Tang 2 la phong
                    // hop nho hon phia tren.
                    BuildRoom(groundAnchor, 440f, 200f,
                        wallColor: new Color(0.62f, 0.6f, 0.58f),
                        floorColor: new Color(0.4f, 0.36f, 0.32f),
                        rugColor: new Color(0.5f, 0.1f, 0.12f),
                        backTarget: default, backIsExit: true, upTarget: floor2Anchor,
                        furnish: a =>
                        {
                            AddDecor(_tableScene, a + new Vector3(0, 0, -20), 15f);
                            AddDecor(_benchScene, a + new Vector3(-130, 0, -60), 105f, 90f);
                            AddDecor(_benchScene, a + new Vector3(130, 0, -60), 105f, -90f);
                            AddDecor(_benchScene, a + new Vector3(-130, 0, 60), 105f, 90f);
                            AddDecor(_benchScene, a + new Vector3(130, 0, 60), 105f, -90f);
                            AddDecor(_rugScene, a + new Vector3(0, 0, -10), 14f);
                            AddDecor(_crateScene, a + new Vector3(-160, 0, -160), 100f);
                            AddDecor(_crateScene, a + new Vector3(160, 0, -160), 100f);
                        });
                    BuildRoom(floor2Anchor, 360f, 170f,
                        wallColor: new Color(0.66f, 0.63f, 0.6f),
                        floorColor: new Color(0.44f, 0.4f, 0.35f),
                        rugColor: new Color(0.45f, 0.12f, 0.14f),
                        backTarget: groundAnchor, backIsExit: false, upTarget: null,
                        furnish: a =>
                        {
                            AddDecor(_tableScene, a + new Vector3(0, 0, 0), 13f);
                            AddDecor(_benchScene, a + new Vector3(-90, 0, -80), 95f, 90f);
                            AddDecor(_benchScene, a + new Vector3(90, 0, -80), 95f, -90f);
                        });
                    break;

                case RoomKind.Village:
                default:
                    // 3 bien the do dac/mau sac khac nhau xoay vong theo thu tu nha, moi nha co
                    // giuong/ban rieng theo bien the - khong phai 9-10 can nha giong het nhau.
                    // Tang tret la khong gian sinh hoat, tang 2 la phong ngu rieng.
                    int variant = _nextInteriorIndex % 3;
                    var palette = variant == 0
                        ? new Color(0.8f, 0.7f, 0.55f)
                        : variant == 1 ? new Color(0.7f, 0.75f, 0.68f) : new Color(0.78f, 0.62f, 0.55f);
                    var rug = variant == 0
                        ? new Color(0.4f, 0.35f, 0.55f)
                        : variant == 1 ? new Color(0.35f, 0.45f, 0.4f) : new Color(0.5f, 0.35f, 0.3f);
                    BuildRoom(groundAnchor, 380f, 140f,
                        wallColor: palette,
                        floorColor: new Color(0.47f, 0.32f, 0.18f),
                        rugColor: rug,
                        backTarget: default, backIsExit: true, upTarget: floor2Anchor,
                        furnish: a =>
                        {
                            if (variant == 0)
                            {
                                AddDecor(_tableScene, a + new Vector3(110, 0, 110), 11f);
                                AddDecor(_chairScene, a + new Vector3(110, 0, 145), 10f, 180f);
                                AddDecor(_bagScene, a + new Vector3(125, 0, -120), 90f);
                                AddDecor(_barrelScene, a + new Vector3(-125, 0, -120), 90f);
                            }
                            else if (variant == 1)
                            {
                                AddDecor(_tableScene, a + new Vector3(-110, 0, -110), 11f);
                                AddDecor(_chairScene, a + new Vector3(-110, 0, -75), 10f, 180f);
                                AddDecor(_chairScene, a + new Vector3(-75, 0, -110), 10f, 90f);
                                AddDecor(_barrelScene, a + new Vector3(120, 0, 110), 90f);
                            }
                            else
                            {
                                AddDecor(_fireplaceScene, a + new Vector3(0, 0, 135), 8f, 180f);
                                AddDecor(_bagScene, a + new Vector3(-120, 0, -110), 90f);
                                AddDecor(_barrelScene, a + new Vector3(-130, 0, 130), 90f, -20f);
                            }
                        });
                    BuildRoom(floor2Anchor, 260f, 120f,
                        wallColor: palette,
                        floorColor: new Color(0.5f, 0.36f, 0.2f),
                        rugColor: rug,
                        backTarget: groundAnchor, backIsExit: false, upTarget: null,
                        furnish: a =>
                        {
                            AddDecor(_bedScene, a + new Vector3(-70, 0, -75), 9f, 90f);
                        });
                    break;
            }
        }

        // Khung phong dung chung (san/tran/tuong/den chum 3D/den/tham/cua) - tham so mau sac/
        // kich thuoc/do dac khac nhau theo tung loai cong trinh (xem BuildRoomForKind).
        // backTarget/backIsExit: cua o tuong nam - IsExit=true dan ra ngoai troi (tang tret),
        // false dan toi backTarget (vd cau thang xuong tang duoi). upTarget: neu co, them cau
        // thang that (Quaternius Stairs, CC0) + 1 diem [E] de len upTarget (vd tang tren).
        private void BuildRoom(Vector3 anchor, float roomSize, float wallHeight, Color wallColor, Color floorColor, Color? rugColor,
            Vector3 backTarget, bool backIsExit, Vector3? upTarget, System.Action<Vector3> furnish)
        {
            var floor = new MeshInstance3D
            {
                Name = "InteriorFloor",
                Mesh = new PlaneMesh { Size = new Vector2(roomSize, roomSize) },
                Position = anchor,
                MaterialOverride = GetCachedMaterial(floorColor)
            };
            _world.AddChild(floor);

            var floorBody = new StaticBody3D { Name = "InteriorFloorCollision" };
            floorBody.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(roomSize, 8f, roomSize) },
                Position = anchor + Vector3.Down * 4f
            });
            _world.AddChild(floorBody);

            // Luoi an toan: 1 tam chan rong hon va thap hon san chinh mot chut - neu vi ly do
            // gi nguoi choi lot qua san chinh (vd cham vao dung luc rat khit khi vua vao phong)
            // se rung ngay tai day thay vi roi mai vao khoang khong duoi long dat.
            var safetyBody = new StaticBody3D { Name = "InteriorSafetyFloor" };
            safetyBody.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(roomSize * 2f, 20f, roomSize * 2f) },
                Position = anchor + Vector3.Down * 70f
            });
            _world.AddChild(safetyBody);

            var ceiling = new MeshInstance3D
            {
                Name = "InteriorCeiling",
                Mesh = new PlaneMesh { Size = new Vector2(roomSize, roomSize) },
                Position = anchor + Vector3.Up * wallHeight,
                RotationDegrees = new Vector3(180, 0, 0),
                MaterialOverride = GetCachedMaterial(floorColor.Darkened(0.4f), 1f)
            };
            _world.AddChild(ceiling);

            AddInteriorWall(anchor + new Vector3(0, wallHeight / 2f, -roomSize / 2f), new Vector3(roomSize, wallHeight, 8f), wallColor);
            AddInteriorWall(anchor + new Vector3(0, wallHeight / 2f, roomSize / 2f), new Vector3(roomSize, wallHeight, 8f), wallColor);
            AddInteriorWall(anchor + new Vector3(-roomSize / 2f, wallHeight / 2f, 0), new Vector3(8f, wallHeight, roomSize), wallColor);
            AddInteriorWall(anchor + new Vector3(roomSize / 2f, wallHeight / 2f, 0), new Vector3(8f, wallHeight, roomSize), wallColor);

            // Anh sang am trong nha (den ngoai + Sun ngoai troi khong chieu toi day vi bi tran chan).
            // Phong lon (toi 440 don vi) nen 1 den giua khong du toi goc tuong - them 4 den goc.
            var lightHeight = wallHeight * 0.75f;
            AddInteriorLight(anchor + Vector3.Up * lightHeight, roomSize * 0.8f, 3.2f);
            float cornerOffset = roomSize * 0.32f;
            AddInteriorLight(anchor + new Vector3(-cornerOffset, lightHeight, -cornerOffset), roomSize * 0.6f, 1.8f);
            AddInteriorLight(anchor + new Vector3(cornerOffset, lightHeight, -cornerOffset), roomSize * 0.6f, 1.8f);
            AddInteriorLight(anchor + new Vector3(-cornerOffset, lightHeight, cornerOffset), roomSize * 0.6f, 1.8f);
            AddInteriorLight(anchor + new Vector3(cornerOffset, lightHeight, cornerOffset), roomSize * 0.6f, 1.8f);

            // Den chum 3D that (Quaternius Chandelier, CC0) treo o dung vi tri den chinh giua
            // tran - moi phong vao deu co, dung nhu yeu cau "den sang la den chum 3D".
            if (_chandelierScene != null)
            {
                var chandelier = _chandelierScene.Instantiate<Node3D>();
                chandelier.Position = anchor + Vector3.Up * lightHeight;
                chandelier.Scale = Vector3.One * 15f;
                _world.AddChild(chandelier);
            }

            if (rugColor.HasValue)
            {
                var rug = new MeshInstance3D
                {
                    Mesh = new PlaneMesh { Size = new Vector2(roomSize * 0.4f, roomSize * 0.27f) },
                    Position = anchor + Vector3.Up * 0.5f,
                    MaterialOverride = GetCachedMaterial(rugColor.Value, 1f)
                };
                _world.AddChild(rug);
            }

            // Cua vao (mat trong) - CHI o tang co cua thoat that (backIsExit=true) - dat sat
            // tuong doi dien cua thoat, cho cam giac day la "cua ban vua di qua" thay vi mot
            // can phong khong ro loi vao/ra. Tang tren khong co cua nay (xem cau thang xuong
            // ben duoi thay the, hop ly hon vi tang tren khong the co "cua ra ngoai troi").
            if (backIsExit && _doorScene != null)
            {
                var innerDoor = _doorScene.Instantiate<Node3D>();
                innerDoor.Position = anchor + new Vector3(0, 0, roomSize / 2f - 6f);
                innerDoor.RotationDegrees = new Vector3(0, 180, 0);
                innerDoor.Scale = Vector3.One * 55f;
                _world.AddChild(innerDoor);
            }

            furnish(anchor);

            // Cau thang len tang tren (Quaternius Stairs, CC0) - dat o goc phong (Tay Bac), xa
            // noi that va xa cua chinh o tuong Nam. Vung kich hoat bao TRON ca chan cau thang
            // (ban kinh lon hon than cau thang) va TU DONG dua len tang tren ngay khi cham vao
            // (khong can bam [E]) - giong nhu buoc chan len bac thang dau tien la tu nhien di
            // len, khong phai mot thao tac rieng nhu mo cua.
            if (upTarget.HasValue && _stairsScene != null)
            {
                var stairPos = anchor + new Vector3(-(roomSize / 2f - 65f), 0, -(roomSize / 2f - 65f));
                AddStairs(stairPos, 90f);
                AddBuildingDoor(stairPos, 65f, isExit: false, upTarget.Value, isFloorChange: true, isAutoTrigger: true);
            }

            if (backIsExit)
            {
                // Tang co cua ra ngoai that (tang tret): nhan [E] gan cua o tuong Nam de ra ngoai troi.
                AddBuildingDoor(anchor + new Vector3(0, 0, roomSize / 2f - 25f), 55f, isExit: true);
            }
            else
            {
                // Tang tren: cau thang XUONG that (cung model Stairs, xoay nguoc huong voi cau
                // thang len) o goc Dong Nam - doi dien cau thang len o goc Tay Bac de khong dam
                // vao do dac. Tu dong xuong tang duoi ngay khi cham vao, giong het cau thang len.
                var downPos = anchor + new Vector3(roomSize / 2f - 65f, 0, roomSize / 2f - 65f);
                AddStairs(downPos, -90f);
                AddBuildingDoor(downPos, 65f, isExit: false, backTarget, isFloorChange: true, isAutoTrigger: true);
            }
        }

        // Model cau thang (Quaternius Stairs, CC0) khong co tam mat bao (bbox) nam o goc toa do
        // rieng cua no - tam that lech ve phia +Z khoang 2.9 don vi (o scale goc). Neu dat thang
        // "Position" cho model nay, sau khi xoay 90 do no se bi LECH SANG NGANG rat nhieu (~78
        // don vi o scale 27), nhin nhu nam GIUA phong thay vi trong goc. Bao boc trong 1 Node3D
        // trung gian roi bu lai dung do lech nay de tam cau thang luon nam DUNG vi tri da chi
        // dinh, bat ke xoay huong nao.
        private void AddStairs(Vector3 pos, float rotationYDegrees, float scale = 27f)
        {
            if (_stairsScene == null) return;
            var wrapper = new Node3D
            {
                Position = pos,
                RotationDegrees = new Vector3(0, rotationYDegrees, 0),
                Scale = Vector3.One * scale
            };
            _world.AddChild(wrapper);
            var stairs = _stairsScene.Instantiate<Node3D>();
            stairs.Position = new Vector3(0, -0.69126f, -2.87656f);
            wrapper.AddChild(stairs);
        }

        // Xep hang GON GANG theo tung HANG rieng cho moi loai (giong 1 kho hang that su duoc sap
        // xep, khong phai 1 dong do vat vut ngau nhien) - moi loai hang chiem 1 "hang" (aisle)
        // rieng, cac mon cach deu nhau (itemSpacing), tu dong xuong hang moi (rowSpacing) khi het
        // cho ngang; huong xoay gan nhu GIONG NHAU trong cung 1 hang (chi lech ngau nhien rat nho
        // ±8 do cho tu nhien, khong con xoay lung tung 360 do nhu truoc), va XEN KE 0/180 do giua
        // cac loai de nhin da dang hon (nhu 2 day ke doi mat nhau). Seed co dinh -> luon giong
        // nhau moi lan vao lai, khong doi lung tung. Tranh 2 vung tron (goc cau thang, khu vuc
        // cua ra vao) bang cach BO QUA vi tri do (khong lui lai) va tiep tuc o o tiep theo.
        private void ScatterBarnStock(Vector3 anchor, float roomHalf, int seed,
            Vector3 avoidA, float avoidARadius, Vector3 avoidB, float avoidBRadius,
            params (PackedScene scene, float scale, int count)[] items)
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)seed };
            const float itemSpacing = 42f;
            const float rowSpacing = 50f;
            const float margin = 35f;
            float minX = -roomHalf + margin, maxX = roomHalf - margin, maxZ = roomHalf - margin;
            float z = -roomHalf + margin;
            float x = minX;
            float baseRotation = 0f;

            foreach (var (scene, scale, count) in items)
            {
                if (scene == null) continue;
                // Bat dau 1 hang MOI rieng cho loai hang nay NEU con du cho trong phong - neu
                // phong chat (vd tang 2 nho hon tang tret), tiep tuc ngay trong hang hien tai
                // thay vi de hang tran ra ngoai tuong.
                if (x > minX && z + rowSpacing <= maxZ)
                {
                    x = minX;
                    z += rowSpacing;
                }

                int placed = 0;
                int guard = 0;
                while (placed < count && guard < count * 6 + 20)
                {
                    guard++;
                    if (x > maxX)
                    {
                        x = minX;
                        z += rowSpacing;
                    }
                    var local = new Vector3(x, 0, z);
                    x += itemSpacing;
                    if (local.DistanceTo(avoidA) < avoidARadius || local.DistanceTo(avoidB) < avoidBRadius)
                        continue; // bo qua o nay, khong lui lai - di tiep sang vi tri ke tiep

                    float rot = baseRotation + rng.RandfRange(-8f, 8f);
                    AddDecor(scene, anchor + local, scale * rng.RandfRange(0.95f, 1.05f), rot);
                    placed++;
                }
                baseRotation = baseRotation == 0f ? 180f : 0f;
            }
        }

        // Ke hang nha kho that su: 1 luoi cot hang, moi cot la 1 chong hang xep CAO "layers"
        // tang (1 loai hang/cot, xoay vong qua shelfTypes de co nhieu loai khac nhau tung day).
        // CHI dat cot o hang chan (rowIdx%2==0) - hang le bo trong lam LOI DI - giu bo cuc gon
        // gang, di lai duoc giua cac day ke thay vi 1 khoi hang dac kin ca phong. Tranh vung
        // tron quanh cua/cau thang va 1 vung tron trung tam (noi da co do dac dot 1 dat rieng).
        private void BuildWarehouseGrid(Vector3 anchor, float roomHalf,
            Vector3 avoidA, float avoidARadius, Vector3 avoidB, float avoidBRadius, float centerExcludeRadius,
            float rowSpacing, float colSpacing, float margin, int layers, float layerHeight,
            params (PackedScene scene, float scale)[] shelfTypes)
        {
            int typeIndex = 0;
            int rowIdx = 0;
            for (float z = -roomHalf + margin; z <= roomHalf - margin; z += rowSpacing, rowIdx++)
            {
                if (rowIdx % 2 != 0) continue; // hang le = loi di, khong dat hang
                for (float x = -roomHalf + margin; x <= roomHalf - margin; x += colSpacing)
                {
                    var basePos = new Vector3(x, 0, z);
                    if (basePos.Length() < centerExcludeRadius) continue;
                    if (basePos.DistanceTo(avoidA) < avoidARadius) continue;
                    if (basePos.DistanceTo(avoidB) < avoidBRadius) continue;

                    var (scene, scale) = shelfTypes[typeIndex % shelfTypes.Length];
                    typeIndex++;
                    if (scene == null) continue;
                    for (int layer = 0; layer < layers; layer++)
                        AddDecor(scene, anchor + basePos + Vector3.Up * (layer * layerHeight), scale);
                }
            }
        }

        private void AddInteriorLight(Vector3 pos, float range, float energy)
        {
            _world.AddChild(new OmniLight3D
            {
                Position = pos,
                LightColor = new Color(1f, 0.9f, 0.72f),
                LightEnergy = energy,
                OmniRange = range
            });
        }

        private void AddInteriorWall(Vector3 center, Vector3 size, Color color)
        {
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                Position = center,
                MaterialOverride = GetCachedMaterial(color)
            };
            _world.AddChild(mesh);

            var body = new StaticBody3D { Position = center };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            _world.AddChild(body);
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
                var segPos = from + dir * (actualSegment * (i + 0.5f));
                var inst = fenceScene.Instantiate<Node3D>();
                inst.Position = segPos;
                inst.RotationDegrees = new Vector3(0, angleDeg, 0);
                inst.Scale = Vector3.One * fenceScale;
                _world.AddChild(inst);

                // Va cham dac cho tung doan hang rao - truoc day hang rao chi la hinh anh, ca
                // nguoi choi lan dong vat (bo) deu co the di xuyen qua. Hop va cham xoay dung
                // theo huong doan hang rao, dai bang doan do.
                var body = new StaticBody3D();
                body.Position = segPos;
                body.RotationDegrees = new Vector3(0, angleDeg, 0);
                body.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = new Vector3(actualSegment, 50f, 8f) },
                    Position = Vector3.Up * 25f
                });
                _world.AddChild(body);
            }
        }

        private void BuildFarmFence()
        {
            float minX = FarmOrigin.X - 42f;
            float maxX = FarmOrigin.X + (FarmGridW - 1) * FarmSpacing + 42f;
            float minZ = FarmOrigin.Z - 42f;
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
                MaterialOverride = GetCachedMaterial(new Color(0.3f, 0.2f, 0.1f), 1f)
            };
            _world.AddChild(post);
        }

        // Cot den duong 3D that (Post Lantern by Kay Lousberg, CC0) + 1 den vang am phat sang
        // that su tu dinh den (nhin ro nhat vao ban dem qua he thong ngay-dem da co) + va cham
        // dac (mong) de nguoi choi khong xuyen qua duoc cot.
        private void AddStreetLamp(Vector3 pos, float rotationYDegrees)
        {
            if (_lampPostScene != null)
            {
                var lamp = _lampPostScene.Instantiate<Node3D>();
                lamp.Position = pos;
                lamp.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
                lamp.Scale = Vector3.One * 18f;
                _world.AddChild(lamp);

                // Cho phan "kinh den" TU PHAT SANG that su (emission) khi den bat - khong chi
                // dua vao anh sang OmniLight chieu ra xung quanh (co the qua nho/mo, kho nhan
                // biet ro la "den dang sang" hay khong). Neu khong tim thay dung ten mesh con
                // (tuy phien ban model), du phong sang toan bo cot den.
                var glowNode = FindNodeByName(lamp, "post_lantern_lantern") as MeshInstance3D
                    ?? FindNodeByName(lamp, "post_lantern") as MeshInstance3D;
                if (glowNode != null)
                {
                    var glowMat = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(1f, 0.85f, 0.5f),
                        EmissionEnabled = true,
                        Emission = new Color(1f, 0.7f, 0.3f),
                        EmissionEnergyMultiplier = 0f // ApplyStreetLampState() dat lai dung gio ngay ben duoi
                    };
                    glowNode.MaterialOverride = glowMat;
                    _streetLampGlowMats.Add(glowMat);
                }
            }

            // Tang manh do sang/tam voi so voi ban dau - gia tri cu (1.6/110) co the qua yeu de
            // nhan ra ro rang giua khung canh ban dem da co san sang mo tu bau troi/anh sang moi.
            var light = new OmniLight3D
            {
                Position = pos + Vector3.Up * 58f,
                LightColor = new Color(1f, 0.75f, 0.4f),
                LightEnergy = 6f,
                OmniRange = 180f
            };
            _world.AddChild(light);
            _streetLamps.Add(light);

            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = 4f, Height = 60f },
                Position = Vector3.Up * 30f
            });
            _world.AddChild(body);
        }

        private static Node FindNodeByName(Node root, string name)
        {
            if (root.Name == name) return root;
            foreach (Node child in root.GetChildren())
            {
                var found = FindNodeByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private readonly System.Collections.Generic.List<OmniLight3D> _streetLamps = new();
        private readonly System.Collections.Generic.List<StandardMaterial3D> _streetLampGlowMats = new();

        private static bool IsStreetLampHour(int hour) => hour >= 18 || hour < 6;

        // Goi khi vua dat xong ca 4 cot (trang thai ban dau, dung gio hien tai) VA moi lan gio
        // thuc su thay doi (GameManager.HourChanged) - bat/tat CA den chieu sang lan phan kinh
        // den tu phat sang cung luc, sang tu 18h toi den 6h sang theo dong ho may tinh THAT.
        private void SetStreetLampsOn(bool on)
        {
            foreach (var lamp in _streetLamps)
                if (IsInstanceValid(lamp)) lamp.Visible = on;
            foreach (var mat in _streetLampGlowMats)
                mat.EmissionEnergyMultiplier = on ? 4f : 0f;
        }

        private void OnStreetLampHourChanged(int hour) => SetStreetLampsOn(IsStreetLampHour(hour));

        // Chuong cho chung, gan nha nong dan - moi con cho (ca con theo nguoi choi lan may con
        // chay rong) sau 12h dem den 6h sang deu tu dong ve day ngu.
        private static readonly Vector3 KennelPos = FarmhousePos + new Vector3(110, 0, 130);

        private void SpawnPlayer()
        {
            var player = _playerScene.Instantiate<Player>();
            // Dung Position (LOCAL, an toan truoc khi vao scene tree) chu khong phai
            // GlobalPosition - GlobalPosition can node da o TRONG tree de tinh qua chuoi cha, dat
            // truoc AddChild se bao loi "!is_inside_tree()" (xem cach lam dung cua SpawnCow/
            // SpawnChicken...). _world khong xoay/lech nen Position == GlobalPosition o day.
            // Phai dung ngoai vung va cham dac cua nha (~113 don vi moi huong) - xem AddDecor/FarmhouseFootprint
            player.Position = FarmhousePos + new Vector3(0, 0, 140);
            _world.AddChild(player);

            AddDogHouse(KennelPos, -30f);

            // Cho 3D luon di theo nguoi choi (Quaternius Shiba Inu, CC0) - dat ngay canh diem
            // xuat phat, tu tim nguoi choi qua group "player" (xem Dog.cs).
            var dog = _dogScene.Instantiate<Dog>();
            dog.Position = player.Position + new Vector3(30, 0, 20);
            dog.KennelPos = KennelPos;
            _world.AddChild(dog);

            SpawnFarmDogs();
            SpawnFarmCats();
        }

        // 5 con cho khac (nhieu giong khac nhau - Husky/Wolf/Shiba Inu, deu CC0 Quaternius) tu do
        // chay rong quanh nong trai (khac voi con Dog theo nguoi choi), sau 12h dem cung ve
        // chung 1 chuong ngu qua Dog.cs/FarmDog.cs.
        private void SpawnFarmDogs()
        {
            string[] breedPaths =
            {
                "res://assets3d/quaternius/animals/husky.glb",
                "res://assets3d/quaternius/animals/wolf.glb",
                "res://assets3d/quaternius/animals/dog.glb",
                "res://assets3d/quaternius/animals/husky.glb",
                "res://assets3d/quaternius/animals/wolf.glb",
            };
            var homeCenter = FarmhousePos + new Vector3(0, 0, 60);
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            for (int i = 0; i < breedPaths.Length; i++)
            {
                var farmDog = _farmDogScene.Instantiate<FarmDog>();
                farmDog.ModelPath = breedPaths[i];
                farmDog.HomeCenter = homeCenter;
                farmDog.KennelPos = KennelPos;
                farmDog.Position = homeCenter + new Vector3(rng.RandfRange(-120, 120), 0, rng.RandfRange(-120, 120));
                _world.AddChild(farmDog);
            }
        }

        // Chuong meo rieng (khac vi tri chuong cho) - 10 con meo (Quaternius, CC0) tu do chay
        // rong quanh nong trai, sau 12h dem den 6h sang tu ve day ngu (xem FarmCat.cs).
        private static readonly Vector3 CatKennelPos = FarmhousePos + new Vector3(-110, 0, 130);

        private void SpawnFarmCats()
        {
            AddCatHouse(CatKennelPos, 30f);

            var homeCenter = FarmhousePos + new Vector3(0, 0, 60);
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            for (int i = 0; i < 10; i++)
            {
                var cat = _farmCatScene.Instantiate<FarmCat>();
                cat.HomeCenter = homeCenter;
                cat.KennelPos = CatKennelPos;
                cat.Position = homeCenter + new Vector3(rng.RandfRange(-150, 150), 0, rng.RandfRange(-150, 150));
                _world.AddChild(cat);
            }
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

        // Khu do thi: 50 can nha 3D + 1 tru canh sat rieng biet, bao quanh loi khu trung tam thi
        // tran hien co (Toa Thi Chinh + 10 nha dan + NPC ban hang/nhiem vu) ma KHONG dung cham -
        // sap xep theo luoi (spacing 230 don vi) nhung LOAI BO cac o roi vao vung trung tam va
        // sap xep theo khoang cach tu tam (gan nhat truoc) de tao bo cuc do thi lan toa tu nhien
        // thay vi 1 khoi vuong cung nhac. Nam TRONG TownGroundSize (3500, xem DrawTownGround) nen
        // khong can vung loai tru WorldStreamer.
        private void BuildCityDistrict()
        {
            const float spacing = 230f;
            const int halfGrid = 8;
            const float coreX = 560f, coreZMin = -260f, coreZMax = 460f;
            const float bound = TownGroundSize / 2f - 260f;

            var candidates = new List<Vector2>();
            for (int gz = -halfGrid; gz <= halfGrid; gz++)
            {
                for (int gx = -halfGrid; gx <= halfGrid; gx++)
                {
                    float x = gx * spacing, z = gz * spacing;
                    if (Mathf.Abs(x) < coreX && z > coreZMin && z < coreZMax) continue; // khu trung tam
                    if (Mathf.Abs(x) > bound || Mathf.Abs(z) > bound) continue;
                    candidates.Add(new Vector2(x, z));
                }
            }
            candidates.Sort((a, b) => a.Length().CompareTo(b.Length()));

            // Tru canh sat: chon o gan nhat o phia TAY (huong duong tu nong trai toi) cua khu
            // trung tam - cong trinh RIENG BIET, dung model nha nong dan (house_v2.glb, to/chi
            // tiet hon SmallBarn) de noi bat, khac han cac can nha dan lap lai.
            Vector2 policePlot = candidates[0];
            foreach (var c in candidates)
            {
                if (c.X < -coreX && Mathf.Abs(c.Y) < 300f) { policePlot = c; break; }
            }
            var policePos = VillageAnchor + new Vector3(policePlot.X, 0, policePlot.Y);
            AddDecor(_farmhouseScene, policePos, 66f, 90f, FarmhouseFootprint);
            var policeInterior = AddBuildingEntrance(policePos, 90f, 130f, 100f, RoomKind.TownHall);
            _cityHousePositions.Add(policePos + new Vector3(0, 0, 55));
            _cityHouseInteriors.Add(policeInterior);

            var rng = new RandomNumberGenerator { Seed = 7002 };
            int placed = 0;
            foreach (var c in candidates)
            {
                if (c == policePlot) continue;
                if (placed >= 50) break;
                var pos = VillageAnchor + new Vector3(c.X, 0, c.Y);
                float rotY = rng.RandiRange(0, 3) * 90f;
                AddDecor(_smallBarnScene, pos, 12f, rotY, SmallBarnFootprint);
                var interiorPos = AddBuildingEntrance(pos, rotY, 80f, 50f, RoomKind.Village);
                _cityHousePositions.Add(pos);
                _cityHouseInteriors.Add(interiorPos);
                placed++;
            }
        }

        // Nguoi dan thi tran "song that": tu do di dao khap khu do thi ban ngay, ve nha ngu ban
        // dem (xem TownCitizenNpc.cs). Moi nguoi duoc gan 1 can nha rieng (tu danh sach
        // _cityHousePositions da dung o BuildCityDistrict) va 1 trong vai bo hoi thoai flavor -
        // KHONG phai NPC nhiem vu/cua hang (da co 10 NPC do rieng o SpawnNpcs), chi de thi tran
        // trong "song dong" nhu yeu cau.
        private void SpawnTownCitizens()
        {
            if (_cityHousePositions.Count == 0) return;

            (string name, string[] low, string[] mid, string[] high)[] flavors =
            {
                ("Nguoi Ban Hang Rong",
                    new[] { "Mua di, mua di! Hang tuoi moi ve sang nay!" },
                    new[] { "Chao khach quen! Hom nay troi dep nhi." },
                    new[] { "Cau ma ghe la ta vui ca ngay." }),
                ("Chu Tiem Banh",
                    new[] { "Banh moi ra lo, thom lam!" },
                    new[] { "Lai ghe tiem banh ta a? Vao di!" },
                    new[] { "De ta bieu cau o banh ngon nhat." }),
                ("Em Hoc Sinh",
                    new[] { "Chao anh/chi! Em dang tren duong den truong." },
                    new[] { "Em thay anh/chi hoai, quen mat roi!" },
                    new[] { "Anh/chi ke chuyen phieu luu cho em nghe di!" }),
                ("Cu Gia Trong Xom",
                    new[] { "Thi tran nay ta song ca doi roi day." },
                    new[] { "Gap cau ta thay vui, nho hoi con chau." },
                    new[] { "Ta se ke cau nghe chuyen xua cua thi tran..." }),
                ("Nguoi Lao Dong",
                    new[] { "Ngay nao cung phai lam viec cham chi thoi." },
                    new[] { "Cau cung sieng nang nhi, phuc cho cau." },
                    new[] { "Nghi ngoi chut di, ban voi cau vui that." }),
                ("Ba Noi Tro",
                    new[] { "Ta dang di cho mua do nau com day." },
                    new[] { "Hom nao ghe nha ta an com nhe!" },
                    new[] { "Cau nhu nguoi trong nha ta roi day." }),
            };

            var homeRng = new RandomNumberGenerator { Seed = 7003 };
            int citizenCount = Mathf.Min(18, _cityHousePositions.Count);
            var usedHomes = new System.Collections.Generic.HashSet<int>();

            for (int i = 0; i < citizenCount; i++)
            {
                int homeIdx;
                do { homeIdx = homeRng.RandiRange(1, _cityHousePositions.Count - 1); }
                while (!usedHomes.Add(homeIdx) && usedHomes.Count < _cityHousePositions.Count);

                var flavor = flavors[i % flavors.Length];
                var citizen = _citizenScene.Instantiate<TownCitizenNpc>();
                citizen.NpcId = $"citizen_{i}";
                citizen.NpcName = flavor.name;
                citizen.DialogueLow = flavor.low;
                citizen.DialogueMid = flavor.mid;
                citizen.DialogueHigh = flavor.high;
                citizen.WanderCenter = VillageAnchor;
                citizen.HomePos = _cityHousePositions[homeIdx] + new Vector3(0, 0, 55);
                citizen.InteriorHomePos = _cityHouseInteriors[homeIdx];
                _world.AddChild(citizen);
            }

            // 1 nguoi "cong an" rieng, gan lien voi Tru Canh Sat (nha o chinh la tru canh sat -
            // index 0 trong danh sach), di lai (tuan tra) quanh khu vuc tru thay vi ca thi tran.
            var guard = _citizenScene.Instantiate<TownCitizenNpc>();
            guard.NpcId = "town_guard";
            guard.NpcName = "Chu Cong An";
            guard.DialogueLow = new[] { "Giu gin trat tu thi tran la trach nhiem cua ta." };
            guard.DialogueMid = new[] { "Cau la nguoi tot, ta yen tam roi." };
            guard.DialogueHigh = new[] { "Co chuyen gi can giup, cu tim ta o Tru Canh Sat nhe." };
            guard.WanderRadius = 220f;
            guard.WanderCenter = _cityHousePositions[0];
            guard.HomePos = _cityHousePositions[0];
            guard.InteriorHomePos = _cityHouseInteriors[0];
            _world.AddChild(guard);
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

        // Trang trai bo: 1 khu rieng co hang rao rieng, cach xa nha kho ~85 don vi de khong
        // chong lan. Bo (Quaternius Farm Animal Pack, CC0) tu do di lai trong hang rao va tu
        // dong den mang an luc 12h trua/16h chieu theo dong ho THAT (xem Cow.cs).
        private static readonly Vector3 CowPastureCenter = new(-820, 0, -250);
        private const float CowPastureHalf = 160f;

        private void BuildCowPasture()
        {
            float minX = CowPastureCenter.X - CowPastureHalf;
            float maxX = CowPastureCenter.X + CowPastureHalf;
            float minZ = CowPastureCenter.Z - CowPastureHalf;
            float maxZ = CowPastureCenter.Z + CowPastureHalf;
            float gateX = CowPastureCenter.X;

            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
            AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
            // Nam - chua cong o giua
            AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 20f, 0, maxZ), _fenceScene);
            AddFenceLine(new Vector3(gateX + 20f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
            AddFencePost(new Vector3(minX, 0, minZ));
            AddFencePost(new Vector3(maxX, 0, minZ));
            AddFencePost(new Vector3(minX, 0, maxZ));
            AddFencePost(new Vector3(maxX, 0, maxZ));

            var troughPos = CowPastureCenter;
            AddFeedTrough(troughPos);

            // 2 cot den hai ben cong chuong bo (giong cong ruong)
            AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
            AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);

            Vector3[] cowStarts =
            {
                CowPastureCenter + new Vector3(-70, 0, -60),
                CowPastureCenter + new Vector3(70, 0, -50),
                CowPastureCenter + new Vector3(-50, 0, 70),
                CowPastureCenter + new Vector3(60, 0, 60),
            };
            foreach (var pos in cowStarts) SpawnCow(pos, isAdult: true);
        }

        // Nha o cho nguoi cham bo (SmallBarn - cung model/he thong cua+noi that 2 tang da dung
        // cho ca 12 cong trinh khac, xem AddBuildingEntrance) + NPC AI di lam theo gio hanh
        // chinh that (6h-18h) - xem FarmhandNpc.cs.
        private static readonly Vector3 CowherdHousePos = new(-1100, 0, -250);

        private void BuildCowherd()
        {
            AddDecor(_smallBarnScene, CowherdHousePos, 12f, 90f, SmallBarnFootprint);
            var interiorHomePos = AddBuildingEntrance(CowherdHousePos, 90f, 80f, 50f, RoomKind.Village);

            var npc = _farmhandScene.Instantiate<FarmhandNpc>();
            npc.NpcId = "cowherd";
            npc.NpcName = "Nguoi Cham Bo";
            npc.DialogueLow = new[] { "Chao, ta la nguoi duoc thue cham dan bo o day. Gio hanh chinh 6 gio sang toi 6 gio toi." };
            npc.DialogueMid = new[] { "Dan bo dao nay khoe re, an uong day du ca." };
            npc.DialogueHigh = new[] { "Cau hay ghe qua chuong bo xem, thinh thoang ta de lai chut sua tuoi day." };
            npc.HomePos = CowherdHousePos + new Vector3(0, 0, 55);
            // Ngoai gio lam (sau 18h), NPC di vao HAN BEN TRONG nha (dung phong noi that that
            // da xay qua AddBuildingEntrance) de ngu, khong dung ngoai san.
            npc.InteriorHomePos = interiorHomePos;
            npc.WorkPos = CowPastureCenter + new Vector3(0, 0, -40);
            npc.TroughPos = CowPastureCenter;
            _world.AddChild(npc);
        }

        // Chuong ngua: 1 khu rieng co hang rao rieng, phia bac chuong bo (cach ~80 don vi de
        // khong chong lan). Ngua (Quaternius Farm Animal Pack, CC0) tu do di lai trong hang rao
        // va tu dong den mang an luc 12h trua/16h chieu theo dong ho THAT (xem Horse.cs).
        private static readonly Vector3 HorseStableCenter = new(-820, 0, -650);
        private const float HorseStableHalf = 160f;

        private void BuildHorseStable()
        {
            float minX = HorseStableCenter.X - HorseStableHalf;
            float maxX = HorseStableCenter.X + HorseStableHalf;
            float minZ = HorseStableCenter.Z - HorseStableHalf;
            float maxZ = HorseStableCenter.Z + HorseStableHalf;
            float gateX = HorseStableCenter.X;

            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
            AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
            // Nam - chua cong o giua
            AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 20f, 0, maxZ), _fenceScene);
            AddFenceLine(new Vector3(gateX + 20f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
            AddFencePost(new Vector3(minX, 0, minZ));
            AddFencePost(new Vector3(maxX, 0, minZ));
            AddFencePost(new Vector3(minX, 0, maxZ));
            AddFencePost(new Vector3(maxX, 0, maxZ));

            AddFeedTrough(HorseStableCenter);

            // 2 cot den hai ben cong chuong ngua (giong cong ruong/chuong bo)
            AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
            AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);

            Vector3[] horseStarts =
            {
                HorseStableCenter + new Vector3(-70, 0, -60),
                HorseStableCenter + new Vector3(70, 0, -50),
                HorseStableCenter + new Vector3(-50, 0, 70),
                HorseStableCenter + new Vector3(60, 0, 60),
            };
            foreach (var pos in horseStarts)
            {
                var horse = _horseScene.Instantiate<Horse>();
                horse.Position = pos;
                horse.TroughPosition = HorseStableCenter;
                horse.HomeCenter = HorseStableCenter;
                horse.PastureHalfExtent = HorseStableHalf - 35f;
                _world.AddChild(horse);
            }
        }

        // Nha o cho nguoi cham ngua (SmallBarn - cung he thong cua+noi that 2 tang) + NPC AI di
        // lam theo gio hanh chinh that (6h-18h) - xem StablehandNpc.cs.
        private static readonly Vector3 StablehandHousePos = new(-1100, 0, -650);

        private void BuildStablehand()
        {
            AddDecor(_smallBarnScene, StablehandHousePos, 12f, 90f, SmallBarnFootprint);
            var interiorHomePos = AddBuildingEntrance(StablehandHousePos, 90f, 80f, 50f, RoomKind.Village);

            var npc = _stablehandScene.Instantiate<StablehandNpc>();
            npc.NpcId = "stablehand";
            npc.NpcName = "Nguoi Cham Ngua";
            npc.DialogueLow = new[] { "Chao, ta la nguoi duoc thue cham dan ngua o day. Gio hanh chinh 6 gio sang toi 6 gio toi." };
            npc.DialogueMid = new[] { "Dan ngua dao nay khoe re, chay nhanh lam." };
            npc.DialogueHigh = new[] { "Cau muon cuoi ngua thi cu ghe chuong hoi ta nhe." };
            npc.HomePos = StablehandHousePos + new Vector3(0, 0, 55);
            npc.InteriorHomePos = interiorHomePos;
            npc.WorkPos = HorseStableCenter + new Vector3(0, 0, -40);
            _world.AddChild(npc);
        }

        // Chuong ga: 1 khu rieng co hang rao rieng, phia nam chuong ngua (cach ~80 don vi de
        // khong chong lan, giong khoang cach giua chuong bo va chuong ngua). Ga (Quaternius, CC0)
        // tu do di lai trong hang rao va tu dong den cho thuc an luc 12h trua/16h chieu theo dong
        // ho THAT (xem Chicken.cs). Pham vi nho hon bo/ngua vi ga la con vat nho, khong can san
        // rong.
        private static readonly Vector3 ChickenCoopCenter = new(-820, 0, -990);
        private const float ChickenCoopHalf = 100f;

        private void BuildChickenCoop()
        {
            float minX = ChickenCoopCenter.X - ChickenCoopHalf;
            float maxX = ChickenCoopCenter.X + ChickenCoopHalf;
            float minZ = ChickenCoopCenter.Z - ChickenCoopHalf;
            float maxZ = ChickenCoopCenter.Z + ChickenCoopHalf;
            float gateX = ChickenCoopCenter.X;

            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
            AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
            AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
            // Nam - chua cong o giua
            AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 20f, 0, maxZ), _fenceScene);
            AddFenceLine(new Vector3(gateX + 20f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
            AddFencePost(new Vector3(minX, 0, minZ));
            AddFencePost(new Vector3(maxX, 0, minZ));
            AddFencePost(new Vector3(minX, 0, maxZ));
            AddFencePost(new Vector3(maxX, 0, maxZ));

            // Chuong ga that su (mai che, dat sat canh bac cua khu) + cho do thuc an o giua san.
            AddDecor(_chickenCoopScene, ChickenCoopCenter + new Vector3(0, 0, -55), 14f, 0f, new Vector2(2.41f, 2.21f));
            var feedPos = ChickenCoopCenter + new Vector3(0, 0, 25);
            AddChickenFeeder(feedPos);

            // 2 cot den hai ben cong chuong ga (giong cong chuong bo/chuong ngua)
            AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
            AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            for (int i = 0; i < 10; i++)
            {
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float radius = rng.RandfRange(20f, ChickenCoopHalf - 25f);
                var pos = ChickenCoopCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                SpawnChicken(pos, feedPos);
            }
        }

        private void SpawnChicken(Vector3 pos, Vector3 feedPos)
        {
            if (_chickenScene == null) { GD.PushError("Khong tai duoc Chicken.tscn"); return; }
            var chicken = _chickenScene.Instantiate<Chicken>();
            chicken.Position = pos;
            chicken.FeedPosition = feedPos;
            chicken.HomeCenter = ChickenCoopCenter;
            chicken.PastureHalfExtent = ChickenCoopHalf - 20f;
            _world.AddChild(chicken);
        }

        // Cho do thuc an cho ga: khac mang cho bo/ngua (khay go cao), ga mo thuc an SAT MAT DAT -
        // dung 1 vanh go thap + dong hat mau vang rai ngang tren nen, khong tim duoc model CC0
        // phu hop rieng nen dung go primitive (giong cach lam mang an bo/ngua).
        private void AddChickenFeeder(Vector3 pos)
        {
            var woodMat = GetCachedMaterial(new Color(0.42f, 0.28f, 0.15f), 0.9f);
            var grainMat = GetCachedMaterial(new Color(0.88f, 0.75f, 0.35f), 1f);

            _world.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 26f, BottomRadius = 28f, Height = 4f },
                Position = pos + Vector3.Up * 2f,
                MaterialOverride = woodMat
            });
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 21f, BottomRadius = 21f, Height = 3f },
                Position = pos + Vector3.Up * 4.5f,
                MaterialOverride = grainMat
            });

            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = 27f, Height = 8f },
                Position = Vector3.Up * 4f
            });
            _world.AddChild(body);
        }

        // Nha o cho nguoi cham ga (SmallBarn - cung he thong cua+noi that 2 tang) + NPC AI di
        // lam theo gio hanh chinh that (6h-18h) - xem PoultryKeeperNpc.cs.
        private static readonly Vector3 PoultryKeeperHousePos = new(-1100, 0, -990);

        private void BuildPoultryKeeper()
        {
            AddDecor(_smallBarnScene, PoultryKeeperHousePos, 12f, 90f, SmallBarnFootprint);
            var interiorHomePos = AddBuildingEntrance(PoultryKeeperHousePos, 90f, 80f, 50f, RoomKind.Village);

            if (_poultryKeeperScene == null) { GD.PushError("Khong tai duoc PoultryKeeperNpc.tscn"); return; }
            var npc = _poultryKeeperScene.Instantiate<PoultryKeeperNpc>();
            npc.NpcId = "poultrykeeper";
            npc.NpcName = "Nguoi Cham Ga";
            npc.DialogueLow = new[] { "Chao, ta la nguoi duoc thue cham dan ga o day. Gio hanh chinh 6 gio sang toi 6 gio toi." };
            npc.DialogueMid = new[] { "Dan ga dao nay de trung deu lam." };
            npc.DialogueHigh = new[] { "Cau hay ghe qua chuong ga xem, thinh thoang ta de lai vai qua trung tuoi day." };
            npc.HomePos = PoultryKeeperHousePos + new Vector3(0, 0, 55);
            npc.InteriorHomePos = interiorHomePos;
            npc.WorkPos = ChickenCoopCenter + new Vector3(0, 0, 25);
            _world.AddChild(npc);
        }

        // Cao nguyen (plateau) hoang da, cach hang rao nong trai it nhat 10m (200 don vi, quy
        // doi 20 don vi/met) ve phia dong - nam NGOAI vung reserved cua WorldStreamer (xem
        // WorldStreamer.ReservedZones), nen dang ky rieng vung LOAI TRU (ExclusionZones) de
        // cay/da/quai ngau nhien cua vung hoang da khong moc xuyen qua chan cao nguyen.
        private void BuildPlateaus()
        {
            AddPlateau(new Vector3(1600, 0, -350), 240f, 200f, 4, 5101);
            AddPlateau(new Vector3(2650, 0, 750), 210f, 170f, 3, 5102);
            AddPlateau(new Vector3(1750, 0, 1950), 260f, 220f, 5, 5103);
        }

        // 1 cao nguyen: khoi hinh non cut (frustum - CylinderMesh voi TopRadius < BottomRadius)
        // tao mat nghieng THOAI (ti le run:rise = 2.2, tuong duong ~24 do - an toan hon nhieu so
        // voi gioi han leo duoc mac dinh cua CharacterBody3D la 45 do) de nguoi choi leo tu chan
        // len dinh mot cach tu nhien, khong can bac thang rieng. Tren mat phang dinh rai vai "go
        // dat" (mound) - phan LO RA tren mat luon > 100 don vi (5m) dung yeu cau, dung SphereMesh
        // ep dep lam mo phong go dat tu nhien, chim ~20% duoi mat cho khong lo goc day tron.
        // Khong tim duoc model dia hinh CC0 phu hop (dia hinh procedural, kich thuoc/vi tri can
        // tuy chinh chinh xac theo tung noi) nen dung primitive, giong cach lam mang an/cot hang
        // rao/mat troi truoc do.
        private void AddPlateau(Vector3 center, float topRadius, float height, int moundCount, int seed)
        {
            float bottomRadius = topRadius + height * 2.2f;

            var plateauMesh = new CylinderMesh { TopRadius = topRadius, BottomRadius = bottomRadius, Height = height };
            var plateau = new MeshInstance3D
            {
                Mesh = plateauMesh,
                Position = center + Vector3.Up * (height / 2f),
                MaterialOverride = GetCachedMaterial(new Color(0.46f, 0.38f, 0.3f), 1f)
            };
            _world.AddChild(plateau);

            // Va cham KHOP DUNG HINH DANG that (non cut, khong phai tru tron) de nguoi choi leo
            // duoc theo dung mat doc thoai.
            var body = new StaticBody3D { Position = plateau.Position };
            body.AddChild(new CollisionShape3D { Shape = plateauMesh.CreateConvexShape() });
            _world.AddChild(body);

            WorldStreamer.ExclusionZones.Add((center, bottomRadius + 40f));

            var rng = new RandomNumberGenerator { Seed = (ulong)seed };
            var moundMat = GetCachedMaterial(new Color(0.38f, 0.3f, 0.22f), 1f);
            for (int i = 0; i < moundCount; i++)
            {
                float moundHeight = rng.RandfRange(130f, 220f); // phan lo ra tren mat luon > 100 (5m)
                float moundRadius = moundHeight * rng.RandfRange(0.5f, 0.7f);
                float angle = rng.RandfRange(0f, Mathf.Tau);
                float dist = rng.RandfRange(0f, topRadius * 0.55f); // giu trong pham vi mat phang tren dinh
                var moundBase = center + Vector3.Up * height
                    + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                // Chim ~20% duoi mat, lo ra ~80% chieu cao (>100 vi moundHeight>=130) cho tu nhien.
                var moundPos = moundBase + Vector3.Up * (moundHeight * 0.3f);

                _world.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = moundRadius, Height = moundHeight },
                    Position = moundPos,
                    MaterialOverride = moundMat
                });

                var moundBody = new StaticBody3D { Position = moundPos };
                moundBody.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = moundRadius * 0.9f } });
                _world.AddChild(moundBody);
            }
        }

        // Canh dong hoa huong duong, cach hang rao nong trai 100m (2000 don vi, quy doi 20 don
        // vi/met) ve phia tay. Dat o dai Z DUONG (138-642, giong hang rao ruong) de KHONG chong
        // lan voi cum chuong trai bo/ngua/ga (cung o phia tay nhung dai Z AM, -250 den -990).
        // O khoang cach nay da nam NGOAI pham vi San chinh (DrawGround, 3000x3000 quanh goc toa
        // do, toi da +-1500) nen roi vao vung hoang da cua WorldStreamer - phai dang ky vung
        // loai tru (giong Main.BuildPlateaus) de cay/da/quai ngau nhien khong moc xuyen qua canh
        // dong (nen dat/co van sinh binh thuong qua tung chunk, chi decor bi chan).
        private static readonly Vector3 SunflowerFieldCenter = new(-2552, 0, 390);
        private const float SunflowerFieldHalfX = 250f;
        private const float SunflowerFieldHalfZ = 250f;

        private void BuildSunflowerField()
        {
            float minX = SunflowerFieldCenter.X - SunflowerFieldHalfX;
            float maxX = SunflowerFieldCenter.X + SunflowerFieldHalfX;
            float minZ = SunflowerFieldCenter.Z - SunflowerFieldHalfZ;
            float maxZ = SunflowerFieldCenter.Z + SunflowerFieldHalfZ;

            WorldStreamer.ExclusionZones.Add((SunflowerFieldCenter, 395f));

            // Nen dat mau nau sam trai dai duoi ca canh dong - phan biet ro voi co xanh xung
            // quanh, giong 1 canh dong that su duoc canh tac chu khong phai hoa moc hoang.
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(SunflowerFieldHalfX * 2f, SunflowerFieldHalfZ * 2f) },
                Position = SunflowerFieldCenter + Vector3.Up * 0.4f,
                MaterialOverride = GetCachedMaterial(new Color(0.32f, 0.22f, 0.13f), 1f)
            });

            // Trong theo HANG deu (giong canh tac that su) + lech ngau nhien nho de khong qua
            // may moc - seed co dinh nen bo cuc giu nguyen moi lan tai lai.
            var rng = new RandomNumberGenerator { Seed = 6001 };
            const float rowSpacing = 46f;
            const float plantSpacing = 42f;
            for (float z = minZ + 25f; z <= maxZ - 25f; z += rowSpacing)
            {
                for (float x = minX + 25f; x <= maxX - 25f; x += plantSpacing)
                {
                    var pos = new Vector3(x + rng.RandfRange(-6f, 6f), 0, z + rng.RandfRange(-6f, 6f));
                    AddSunflower(pos, rng);
                }
            }
        }

        // 1 cay hoa huong duong: khong tim duoc model CC0 phu hop rieng (tim ky tren poly.pizza,
        // chi co goi "Flower"/"Flowers" chung chung CC0, khong co "Sunflower" CC0 rieng) nen dung
        // primitive - than cay (CylinderMesh mau xanh) + dau hoa la 2 lop dia (dia vang lon lam
        // canh hoa, dia nau nho hon lam nhuy o giua) nam GAN NHU NAM NGANG (huong duong thuong
        // "quay mat ve phia mat troi"/huong len troi) chi nghieng nhe ngau nhien cho tu nhien.
        private void AddSunflower(Vector3 pos, RandomNumberGenerator rng)
        {
            float stemHeight = rng.RandfRange(75f, 105f);
            float headRadius = rng.RandfRange(16f, 22f);

            _world.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 3f, Height = stemHeight },
                Position = pos + Vector3.Up * (stemHeight / 2f),
                MaterialOverride = GetCachedMaterial(new Color(0.25f, 0.42f, 0.15f), 0.9f)
            });

            var headAnchor = new Node3D
            {
                Position = pos + Vector3.Up * stemHeight,
                RotationDegrees = new Vector3(rng.RandfRange(-12f, 12f), rng.RandfRange(0f, 360f), rng.RandfRange(-6f, 6f))
            };
            _world.AddChild(headAnchor);

            headAnchor.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = headRadius, BottomRadius = headRadius, Height = 4f },
                MaterialOverride = GetCachedMaterial(new Color(0.95f, 0.78f, 0.1f), 0.8f)
            });
            headAnchor.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = headRadius * 0.5f, BottomRadius = headRadius * 0.5f, Height = 4.6f },
                Position = Vector3.Up * 0.8f,
                MaterialOverride = GetCachedMaterial(new Color(0.32f, 0.22f, 0.08f), 1f)
            });
        }

        private void SpawnCow(Vector3 pos, bool isAdult)
        {
            var cow = _cowScene.Instantiate<Cow>();
            cow.Position = pos;
            cow.TroughPosition = CowPastureCenter;
            cow.IsAdult = isAdult;
            // Tam wander PHAI la tam that cua hang rao (khong phai vi tri spawn rieng cua tung
            // con) + gioi han ban kinh nho hon nua be rong hang rao that (160) mot khoang an
            // toan, de bo khong bao gio wander ra ngoai hang rao.
            cow.HomeCenter = CowPastureCenter;
            cow.PastureHalfExtent = CowPastureHalf - 35f;
            _world.AddChild(cow);
        }

        private const int MaxCows = 10;

        // Moi ngay THAT: neu co it nhat 2 bo TRUONG THANH va tong dan bo chua vuot muc toi da,
        // co 1 co hoi ngau nhien sinh ra 1 be con moi (dat canh 2 "bo me", bat dau nho va phai
        // an moi ngay de lon len - xem Cow.OnDayChanged). Gioi han so luong de dan bo khong
        // tang vo han lam day chat hang rao.
        private void TryBreedCows()
        {
            var cows = GetTree().GetNodesInGroup("cows");
            if (cows.Count >= MaxCows) return;

            int adultCount = 0;
            Vector3 lastAdultPos = CowPastureCenter;
            foreach (var node in cows)
            {
                if (node is Cow c && IsInstanceValid(c) && c.IsAdult)
                {
                    adultCount++;
                    lastAdultPos = c.GlobalPosition;
                }
            }
            if (adultCount < 2) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            if (rng.Randf() > 0.5f) return; // ~50% co hoi moi ngay, khong phai ngay nao cung de

            var calfPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
            SpawnCow(calfPos, isAdult: false);
        }

        // Mang thuc an cho bo: khong tim duoc model CC0 phu hop rieng cho hinh dang nay, nen
        // dung go primitive (giong cach lam cot hang rao/mat troi truoc do) - 1 khay go ho mo
        // chat day thuc an mau vang, du don gian de khong can 1 model rieng.
        private void AddFeedTrough(Vector3 pos)
        {
            var woodMat = GetCachedMaterial(new Color(0.4f, 0.26f, 0.14f), 0.9f);
            var foodMat = GetCachedMaterial(new Color(0.85f, 0.7f, 0.3f), 1f);

            void AddBox(Vector3 offset, Vector3 size, StandardMaterial3D mat)
            {
                _world.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = size },
                    Position = pos + offset,
                    MaterialOverride = mat
                });
            }

            AddBox(new Vector3(0, 4, 0), new Vector3(70, 8, 30), woodMat);
            AddBox(new Vector3(-33, 12, 0), new Vector3(4, 16, 30), woodMat);
            AddBox(new Vector3(33, 12, 0), new Vector3(4, 16, 30), woodMat);
            AddBox(new Vector3(0, 12, -13), new Vector3(70, 16, 4), woodMat);
            AddBox(new Vector3(0, 12, 13), new Vector3(70, 16, 4), woodMat);
            AddBox(new Vector3(0, 9, 0), new Vector3(60, 6, 22), foodMat);

            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(74, 20, 34) },
                Position = Vector3.Up * 10f
            });
            _world.AddChild(body);
        }

        // Chuong cho: khong tim duoc model CC0 phu hop rieng cho hinh dang nay sau nhieu lan
        // tim (giong cach lam mang thuc an/cot hang rao truoc do) - dung go primitive: than
        // hop go + mai chop (PrismMesh) + cua vao toi mau.
        private void AddDogHouse(Vector3 pos, float rotationYDegrees)
        {
            var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));
            var rot = new Vector3(0, rotationYDegrees, 0);
            var woodMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.32f, 0.18f), Roughness = 0.9f };
            var roofMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.16f, 0.09f), Roughness = 0.9f };
            var doorMat = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.03f, 0.03f), Roughness = 1f };

            _world.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(34, 28, 34) },
                Position = pos + basis * new Vector3(0, 14, 0),
                RotationDegrees = rot,
                MaterialOverride = woodMat
            });
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(13, 18, 2) },
                Position = pos + basis * new Vector3(0, 9, 17.5f),
                RotationDegrees = rot,
                MaterialOverride = doorMat
            });
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(40, 16, 40) },
                Position = pos + basis * new Vector3(0, 28, 0),
                RotationDegrees = rot,
                MaterialOverride = roofMat
            });

            var body = new StaticBody3D { Position = pos, RotationDegrees = rot };
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(34, 30, 34) },
                Position = Vector3.Up * 15f
            });
            _world.AddChild(body);
        }

        // Chuong meo: khong tim duoc model CC0 phu hop (2 lua chon tim duoc deu CC-BY) - dung go
        // primitive nhu chuong cho nhung nho hon va mau mieng/lieu gai de khac biet.
        private void AddCatHouse(Vector3 pos, float rotationYDegrees)
        {
            var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));
            var rot = new Vector3(0, rotationYDegrees, 0);
            var wickerMat = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.55f, 0.32f), Roughness = 1f };
            var roofMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.38f, 0.2f), Roughness = 0.9f };
            var doorMat = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.03f, 0.03f), Roughness = 1f };

            _world.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 13f, BottomRadius = 14f, Height = 16f },
                Position = pos + basis * new Vector3(0, 8, 0),
                RotationDegrees = rot,
                MaterialOverride = wickerMat
            });
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(8, 10, 2) },
                Position = pos + basis * new Vector3(0, 6, 12.5f),
                RotationDegrees = rot,
                MaterialOverride = doorMat
            });
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(20, 8, 20) },
                Position = pos + basis * new Vector3(0, 16, 0),
                RotationDegrees = rot,
                MaterialOverride = roofMat
            });

            var body = new StaticBody3D { Position = pos, RotationDegrees = rot };
            body.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = 14f, Height = 18f },
                Position = Vector3.Up * 9f
            });
            _world.AddChild(body);
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
