using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Entities;

namespace HiepSiVeVuon.Core
{
    // Sinh/don "vung hoang da" vo han xung quanh khu vuc dung san (nong trai/NPC/nha) theo chunk.
    // Moi chunk duoc seed xac dinh tu toa do (cx,cz) nen tai lai dung vi tri cay/da nhu lan truoc,
    // rieng quai vat thi khong luu trang thai (sinh moi moi lan chunk duoc tao).
    public partial class WorldStreamer : Node3D
    {
        [Export] public float ChunkSize = 500f;
        [Export] public int LoadRadius = 2;
        [Export] public int KeepRadius = 3;
        [Export] public int WorldSeed = 1337;
        [Export] public float UpdateInterval = 0.5f;

        // Bien do go dat (xem GroundMaterial.CreateGrass/ground.gdshader) cho MOI chunk vung
        // hoang da (khac han bump_height mac dinh 2.5 rat nhe cua San chinh/Thi tran) - du de
        // "gap gho" ro ret, nhung van vua phai so voi ChunkSize=500 (khong qua doc, tranh cam
        // giac "song bien" gia tao - san va cham van la BoxShape3D PHANG nhu cu, chi phan HINH
        // ANH lon xuong).
        private const float WildernessBumpHeight = 13f;

        // Cac vung do Main.cs da dung san (khong lien nhau - nong trai va thi tran cach xa nhau
        // ~500m, noi voi nhau bang 1 con duong dai chay xuyen qua vung hoang da). WorldStreamer
        // khong sinh chunk trong cac vung nay. PHAI khop chinh xac voi Main.cs.DrawGround /
        // DrawTownGround, neu khong se ho ra "vuc" o ranh gioi.
        private static readonly (int MinCx, int MaxCx, int MinCz, int MaxCz)[] ReservedZones =
        {
            (-3, 2, -3, 2),     // khu nong trai (quanh goc toa do)
            (15, 21, 4, 10),    // khu thi tran (quanh VillageAnchor, xem Main.cs)
        };
        // Luu y: phong noi that (Main.BuildRoomForKind) nam ngay phia tren toa do X,Z THAT cua
        // tung cong trinh (chi khac o do cao Y) - da nam san trong 2 vung reserved o tren, khong
        // can vung rieng nua.

        // Vung LOAI TRU dot 1 (tron, tam+ban kinh) - KHONG sinh cay/da/quai trong pham vi nay,
        // NHUNG van sinh nen dat/co binh thuong (khac ReservedZones - reserved bo qua CA CHUNK,
        // khong co nen dat, phai tu ve rieng nhu DrawTownGround). Dung cho cac dia hinh dac biet
        // dung san NAM NGOAI vung reserved (vd cao nguyen - xem Main.BuildPlateaus) de cay/da/
        // quai ngau nhien khong moc xuyen qua dia hinh do. Main.cs dang ky vao day TRUOC khi
        // WorldStreamer kip sinh chunk (dang ky trong _Ready(), WorldStreamer chi sinh chunk tu
        // _Process() o frame sau).
        public static readonly List<(Vector3 Center, float Radius)> ExclusionZones = new();

        private static bool IsExcluded(Vector3 worldPos)
        {
            foreach (var (center, radius) in ExclusionZones)
            {
                float dx = worldPos.X - center.X, dz = worldPos.Z - center.Z;
                if (dx * dx + dz * dz < radius * radius) return true;
            }
            return false;
        }

        // "Vung que nuoc Phap" (xem Main.BuildFrenchCountryside) - 1 vung hinh vuong (tam + nua
        // canh) dung BO TRANG TRI RIENG (xem GenerateFrenchDecor) thay vi vung hoang da mac dinh.
        // Main.cs dang ky truoc khi WorldStreamer kip sinh chunk (trong _Ready()).
        public static Vector3? FrenchRegionCenter = null;
        public static float FrenchRegionHalfSize = 0f;

        // Tra ve true neu worldPos nam trong vung + normalizedDist (0 = tam vung, 1 = sat ria)
        // de GenerateFrenchDecor giam dan mat do ra "khu dat hoang" o ria.
        private static bool TryGetFrenchRegionDist(Vector3 worldPos, out float normalizedDist)
        {
            normalizedDist = 0f;
            if (FrenchRegionCenter == null) return false;
            var c = FrenchRegionCenter.Value;
            float dx = Mathf.Abs(worldPos.X - c.X), dz = Mathf.Abs(worldPos.Z - c.Z);
            if (dx > FrenchRegionHalfSize || dz > FrenchRegionHalfSize) return false;
            normalizedDist = Mathf.Max(dx, dz) / FrenchRegionHalfSize;
            return true;
        }

        // Danh sach VUNG DAT TEN TONG QUAT (xem yeu cau "the gioi mo") - tong quat hoa co che
        // FrenchRegionCenter/HalfSize o tren (van GIU NGUYEN, khong dong vao) thanh 1 DANH SACH
        // de nhieu vung CUNG LUC co mat do/loai cay/bang quai RIENG, thay vi chi 1 truong hop dac
        // biet duy nhat. Main.cs dang ky vao day trong _Ready(), TRUOC khi WorldStreamer kip sinh
        // chunk dau tien (_Process chay tu frame sau). KHONG ho tro doi mau nen dat rieng (xem
        // GroundMaterial.CreateGrass - co ghi chu ro tai sao KHONG duoc tint rieng tung vung/
        // chunk: tao duong ranh mau sang giua cac o dat, da tung xay ra va sua truoc do) - moi
        // vung CHI khac o mat do/loai vat trang tri/bang quai, giong dung cach vung Phap da lam.
        public class RegionProfile
        {
            public string Name;
            public Vector3 Center;
            public float HalfSize;
            // Rieng cho vung nay - null = dung chung bo _decorOptions mac dinh cua vung hoang da.
            public (PackedScene scene, float minScale, float maxScale, bool isTree)[] DecorOptions;
            public int MinDecor = 4, MaxDecor = 9;
            // Rieng cho vung nay - null/rong = KHONG co quai (an toan, vd khu do thi/dong ruong).
            public (string enemyId, float weight)[] EnemyTable;
            public float EnemyChance = 0.5f;
            public float EnemyStatMultiplier = 1f;
        }
        public static readonly List<RegionProfile> Regions = new();

        private static RegionProfile FindRegion(Vector3 worldPos)
        {
            foreach (var r in Regions)
            {
                if (Mathf.Abs(worldPos.X - r.Center.X) <= r.HalfSize && Mathf.Abs(worldPos.Z - r.Center.Z) <= r.HalfSize)
                    return r;
            }
            return null;
        }

        // Vung KHONG cho quai vat spawn (van co cay/da binh thuong - chi chan RIENG quai) - dung
        // cho pham vi ben trong tuong da 10 hecta quanh nong trai (xem Main.BuildFarmStoneWall),
        // vi tuong nay RONG HON han ReservedZones cu (chi ~3000x3000 quanh goc toa do) nen quai
        // van co the "lot" vao trong tuong o phan dat moi neu khong chan rieng.
        public static Vector3? NoEnemyZoneCenter = null;
        public static float NoEnemyZoneHalfSize = 0f;

        private static bool IsInNoEnemyZone(Vector3 worldPos)
        {
            if (NoEnemyZoneCenter == null) return false;
            var c = NoEnemyZoneCenter.Value;
            return Mathf.Abs(worldPos.X - c.X) <= NoEnemyZoneHalfSize && Mathf.Abs(worldPos.Z - c.Z) <= NoEnemyZoneHalfSize;
        }

        private struct DecorOption
        {
            public string Path;
            public float MinScale, MaxScale;
            public bool IsTree; // true = boc trong Tree.tscn de chat duoc lay go
            public DecorOption(string path, float min, float max, bool isTree = false)
            {
                Path = path; MinScale = min; MaxScale = max; IsTree = isTree;
            }
        }

        private readonly DecorOption[] _decorOptions =
        {
            // Cay that (Quaternius Ultimate Stylized Nature Pack, CC0) - chat duoc de lay go
            // To hon han nguoi choi (cao ~40 don vi), giong cay that ngoai doi
            new("res://assets3d/quaternius/nature/tree_normal_1.glb", 32f, 40f, true),
            new("res://assets3d/quaternius/nature/tree_normal_2.glb", 32f, 40f, true),
            new("res://assets3d/quaternius/nature/tree_maple_1.glb", 32f, 40f, true),
            new("res://assets3d/quaternius/nature/tree_maple_2.glb", 32f, 40f, true),
            new("res://assets3d/quaternius/nature/tree_birch_1.glb", 38f, 46f, true),
            new("res://assets3d/quaternius/nature/tree_birch_2.glb", 38f, 46f, true),
            new("res://assets3d/quaternius/nature/rock_1.glb", 14f, 20f),
            new("res://assets3d/quaternius/nature/rock_2.glb", 14f, 20f),
            new("res://assets3d/kenney/nature/plant_bush.glb", 12f, 18f),
            new("res://assets3d/kenney/nature/flower_yellowA.glb", 6f, 10f),
            new("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f), // GrassDecorIndex = 10 (xem ScatterGrassTufts)
        };

        private PackedScene[] _decorScenes;
        private PackedScene _treeWrapperScene;
        private PackedScene _enemyScene;
        private Node3D _player;
        private float _timer = 0f;
        private readonly Dictionary<Vector2I, Node3D> _loaded = new();

        // Mesh "khuon" trich tu grass_large.glb, dung chung cho MultiMeshInstance3D cua MOI chunk
        // (xem BuildGrassField) - null neu khong tai duoc model (an toan, chi bo qua tham co).
        private Mesh _grassBladeMesh;

        // Nha 3 kich thuoc khac nhau + vat lieu doi rieng cho "vung que nuoc Phap" (xem
        // GenerateFrenchDecor) - Cottage/House deu la model CC0 tu poly.pizza (Quaternius/
        // CreativeTrio), khac han model nha/kho da dung o nong trai/thi tran de vung nay co dien
        // mao rieng.
        private PackedScene _cottageScene;
        private PackedScene _villageHouseScene;
        private PackedScene _bigHouseScene;
        private StandardMaterial3D _frenchHillMat;

        public override void _Ready()
        {
            _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            _treeWrapperScene = GD.Load<PackedScene>("res://scenes/Tree.tscn");
            _decorScenes = new PackedScene[_decorOptions.Length];
            for (int i = 0; i < _decorOptions.Length; i++)
                _decorScenes[i] = GD.Load<PackedScene>(_decorOptions[i].Path);

            _cottageScene = GD.Load<PackedScene>("res://assets3d/quaternius/french_countryside/cottage.glb");
            _villageHouseScene = GD.Load<PackedScene>("res://assets3d/quaternius/french_countryside/village_house.glb");
            _bigHouseScene = GD.Load<PackedScene>("res://assets3d/quaternius/buildings/house_v2.glb");
            _frenchHillMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.44f, 0.2f), Roughness = 1f };

            // Lay RIENG mesh cua model co (grass_large.glb, xem GrassDecorIndex) MOT LAN de dung
            // lam "khuon" cho MultiMeshInstance3D (xem BuildGrassField) - MultiMesh chi can 1
            // Mesh resource dung chung cho HANG TRAM/NGHIN instance GPU, khong the dung ca
            // PackedScene nhu cach rai decor thong thuong (qua nang neu tao tung Node3D rieng).
            var grassScene = _decorScenes.Length > GrassDecorIndex ? _decorScenes[GrassDecorIndex] : null;
            if (grassScene != null)
            {
                var temp = grassScene.Instantiate<Node3D>();
                var meshInst = FindMeshInstance(temp);
                if (meshInst != null) _grassBladeMesh = meshInst.Mesh;
                temp.QueueFree();
            }
        }

        private static MeshInstance3D FindMeshInstance(Node root)
        {
            if (root is MeshInstance3D mi) return mi;
            foreach (Node child in root.GetChildren())
            {
                var found = FindMeshInstance(child);
                if (found != null) return found;
            }
            return null;
        }

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < UpdateInterval) return;
            _timer = 0f;

            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player == null) return;

            int pcx = Mathf.FloorToInt(_player.GlobalPosition.X / ChunkSize);
            int pcz = Mathf.FloorToInt(_player.GlobalPosition.Z / ChunkSize);

            for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
            {
                for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
                {
                    int cx = pcx + dx;
                    int cz = pcz + dz;
                    if (IsReserved(cx, cz)) continue;
                    var key = new Vector2I(cx, cz);
                    if (_loaded.ContainsKey(key)) continue;
                    var chunk = GenerateChunk(cx, cz);
                    AddChild(chunk);
                    _loaded[key] = chunk;
                }
            }

            List<Vector2I> toRemove = null;
            foreach (var kv in _loaded)
            {
                if (Mathf.Abs(kv.Key.X - pcx) > KeepRadius || Mathf.Abs(kv.Key.Y - pcz) > KeepRadius)
                    (toRemove ??= new List<Vector2I>()).Add(kv.Key);
            }
            if (toRemove != null)
            {
                foreach (var key in toRemove)
                {
                    _loaded[key].QueueFree();
                    _loaded.Remove(key);
                }
            }
        }

        private static bool IsReserved(int cx, int cz)
        {
            foreach (var z in ReservedZones)
            {
                if (cx >= z.MinCx && cx <= z.MaxCx && cz >= z.MinCz && cz <= z.MaxCz) return true;
            }
            return false;
        }

        private static ulong ChunkSeed(int worldSeed, int cx, int cz)
        {
            unchecked
            {
                ulong h = (ulong)worldSeed;
                h = h * 6364136223846793005UL + (ulong)(uint)cx;
                h = h * 6364136223846793005UL + (ulong)(uint)cz;
                return h;
            }
        }

        private Node3D GenerateChunk(int cx, int cz)
        {
            var root = new Node3D { Name = $"Chunk_{cx}_{cz}" };
            var center = new Vector3(cx * ChunkSize + ChunkSize / 2f, 0f, cz * ChunkSize + ChunkSize / 2f);

            var rng = new RandomNumberGenerator { Seed = ChunkSeed(WorldSeed, cx, cz) };

            // San + collision (giong DrawGround trong Main.cs). KHONG tint rieng tung chunk
            // (truoc day co lam vay va tao duong ranh mau sang giua cac o dat) - dong deu mau
            // moi dung voi dia hinh dong bang that, chi Uv1Scale doi theo kich thuoc de mat co
            // giu dung ty le voi San chinh.
            int chunkSubdiv = GroundMaterial.SubdivisionsFor(ChunkSize);
            var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(ChunkSize, ChunkSize), SubdivideWidth = chunkSubdiv, SubdivideDepth = chunkSubdiv } };
            ground.Position = center;
            // Dia hinh gap gho ro ret hon San chinh (xem WildernessBumpHeight/GroundMaterial.CreateGrass)
            // - "ngoai nong trai thi mat dat khong bang phang" theo dung yeu cau, khac voi dat
            // nong trai da duoc san phang de canh tac (van dung bump_height mac dinh rat nhe).
            ground.MaterialOverride = GroundMaterial.CreateGrass(ChunkSize, ChunkSize, WildernessBumpHeight);
            root.AddChild(ground);

            // Khoi dat dac duoi lop co, giong het cach lam trong Main.cs.DrawGround, de khong
            // bi "hut" ra bau troi khi nhin tu goc thap o vung hoang da.
            var earthMass = GroundMaterial.CreateEarthMass(ChunkSize, ChunkSize);
            earthMass.Position += center;
            root.AddChild(earthMass);

            var floorBody = new StaticBody3D();
            floorBody.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(ChunkSize, GroundMaterial.EarthDepth, ChunkSize) },
                Position = center + new Vector3(0, -GroundMaterial.EarthDepth / 2f, 0)
            });
            root.AddChild(floorBody);

            // San luon giong nhau (co xanh) - CHI khac o bo TRANG TRI: vung que Phap (xem
            // FrenchRegionCenter) dung bo rieng (doi thap/nha kich thuoc khac nhau/khong quai),
            // ngoai ra van la vung hoang da mac dinh (cay/da/quai).
            if (TryGetFrenchRegionDist(center, out float distNorm))
                GenerateFrenchDecor(root, center, rng, distNorm);
            else if (FindRegion(center) is RegionProfile region)
                GenerateRegionDecor(root, center, rng, region);
            else
                GenerateWildernessDecor(root, center, rng);

            return root;
        }

        // Rai THEM 1 lop chum co day dac (doc lap voi _decorOptions/DecorOptions von chi ~1/11
        // co hoi la co moi lan chon ngau nhien, qua thua de "vung dat hoang phai co co" ro rang
        // theo dung yeu cau) - dung LAI DUNG model grass_large.glb da co (khong co model co nao
        // khac trong du an), chi doi ty le/xoay ngau nhien de tao cam giac tu nhien, khong deu tam.
        private const int GrassDecorIndex = 10; // vi tri "grass_large.glb" trong _decorOptions o tren

        // Tham co 3D DAY DAC phu khap nen dat 1 chunk - dung MultiMeshInstance3D (GPU instancing,
        // CHI 1 draw call cho ca tram/nghin lam co, giong cach BuildBigVineyard.cs da lam voi
        // ~1980 goc nho) thay vi tao tung Node3D rieng (qua nang neu lam vay voi so luong lon nay
        // - da tung dung cach do cho vai chum THUA THOT, gio thay bang lop tham day dac THAT SU
        // theo dung yeu cau "co phai la 3D"). Dung LAI chinh mesh cua grass_large.glb (_grassBladeMesh)
        // - khong co model "1 cong co don" rieng trong du an, nen phu day bang nhieu "chum" nho
        // chong lan nhe tao cam giac tham co lien tuc.
        private const int GrassFieldDensity = 340;

        private Node3D BuildGrassField(Vector3 center, RandomNumberGenerator rng)
        {
            if (_grassBladeMesh == null) return null;
            float half = ChunkSize / 2f - 15f;
            var transforms = new System.Collections.Generic.List<Transform3D>(GrassFieldDensity);
            for (int i = 0; i < GrassFieldDensity; i++)
            {
                var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                if (IsExcluded(center + localPos)) continue;
                float scale = rng.RandfRange(2.2f, 5.5f);
                float rotY = rng.RandfRange(0f, Mathf.Tau);
                var basis = new Basis(Vector3.Up, rotY).Scaled(Vector3.One * scale);
                transforms.Add(new Transform3D(basis, localPos));
            }
            if (transforms.Count == 0) return null;

            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = _grassBladeMesh,
                InstanceCount = transforms.Count,
            };
            for (int i = 0; i < transforms.Count; i++)
                multiMesh.SetInstanceTransform(i, transforms[i]);

            return new MultiMeshInstance3D { Multimesh = multiMesh, Position = center };
        }

        // Ban tong quat cua GenerateWildernessDecor, doc tham so tu RegionProfile thay vi hang so
        // co dinh - dung CHUNG cho MOI vung dang ky trong Regions (xem RegionProfile o tren).
        private void GenerateRegionDecor(Node3D root, Vector3 center, RandomNumberGenerator rng, RegionProfile profile)
        {
            var grassField = BuildGrassField(center, rng);
            if (grassField != null) root.AddChild(grassField);
            var options = profile.DecorOptions;
            if (options != null && options.Length > 0)
            {
                int decorCount = rng.RandiRange(profile.MinDecor, profile.MaxDecor);
                float half = ChunkSize / 2f - 20f;
                for (int i = 0; i < decorCount; i++)
                {
                    var (scene, minScale, maxScale, isTree) = options[rng.RandiRange(0, options.Length - 1)];
                    if (scene == null) continue;
                    var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                    if (IsExcluded(center + localPos)) continue;
                    float scale = rng.RandfRange(minScale, maxScale);
                    float rotY = rng.RandfRange(0f, Mathf.Tau);

                    if (isTree && _treeWrapperScene != null)
                    {
                        var wrapper = _treeWrapperScene.Instantiate<Entities.Tree>();
                        wrapper.Position = center + localPos;
                        wrapper.RotateY(rotY);
                        wrapper.Scale = Vector3.One * scale;
                        var model = scene.Instantiate<Node3D>();
                        wrapper.GetNode<Node3D>("Model").AddChild(model);
                        root.AddChild(wrapper);
                    }
                    else
                    {
                        var inst = scene.Instantiate<Node3D>();
                        inst.Position = center + localPos;
                        inst.RotateY(rotY);
                        inst.Scale = Vector3.One * scale;
                        root.AddChild(inst);
                    }
                }
            }

            if (profile.EnemyTable != null && profile.EnemyTable.Length > 0 && _enemyScene != null && rng.Randf() < profile.EnemyChance)
            {
                int enemyCount = rng.Randf() < 0.2f ? 2 : 1;
                float enemyHalf = ChunkSize / 2f - 40f;
                for (int i = 0; i < enemyCount; i++)
                {
                    var localPos = new Vector3(rng.RandfRange(-enemyHalf, enemyHalf), 0f, rng.RandfRange(-enemyHalf, enemyHalf));
                    if (IsExcluded(center + localPos)) continue;
                    if (IsInNoEnemyZone(center + localPos)) continue;
                    var e = _enemyScene.Instantiate<Enemy>();
                    e.EnemyId = PickWeighted(profile.EnemyTable, rng);
                    e.Position = center + localPos;
                    e.StatMultiplier = profile.EnemyStatMultiplier * Enemy.SeasonalMultiplier();
                    root.AddChild(e);
                }
            }
        }

        private static string PickWeighted((string enemyId, float weight)[] table, RandomNumberGenerator rng)
        {
            float total = 0f;
            foreach (var (_, w) in table) total += w;
            float roll = rng.RandfRange(0f, total);
            float acc = 0f;
            foreach (var (id, w) in table)
            {
                acc += w;
                if (roll <= acc) return id;
            }
            return table[table.Length - 1].enemyId;
        }

        // 6 model cay dau tien trong _decorOptions (xem mang o tren) - dung de gioi han lua chon
        // CHI con cay khi dang o trong 1 "mang rung nho" (xem GenerateWildernessDecor).
        private const int TreeDecorIndexMax = 5;
        // Xac suat 1 o luoi vung hoang da TRO THANH 1 mang rung nho (dung y "them cac canh rung
        // rai rac" theo yeu cau - khac voi khu "Rung" duy nhat co san, day la NHIEU mang rung NHO
        // xen ke khap vung hoang da chung, giong rung that xen ke dong co ngoai doi thuc).
        private const float ForestPatchChance = 0.16f;

        private void GenerateWildernessDecor(Node3D root, Vector3 center, RandomNumberGenerator rng)
        {
            var grassField = BuildGrassField(center, rng);
            if (grassField != null) root.AddChild(grassField);

            // Mang rung nho: o luoi nay CHI rai cay (mat do day hon nhieu), khong xen rock/bui/hoa
            // - tao cam giac 1 cum rung ro net thay vi cay le te lan trong decor thong thuong.
            bool isForestPatch = rng.Randf() < ForestPatchChance;
            int decorCount = isForestPatch ? rng.RandiRange(16, 26) : rng.RandiRange(4, 9);
            float half = ChunkSize / 2f - 20f;
            for (int i = 0; i < decorCount; i++)
            {
                int idx = isForestPatch ? rng.RandiRange(0, TreeDecorIndexMax) : rng.RandiRange(0, _decorScenes.Length - 1);
                var scene = _decorScenes[idx];
                if (scene == null) continue;
                var opt = _decorOptions[idx];
                var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                if (IsExcluded(center + localPos)) continue; // vd nam trong chan 1 cao nguyen
                float scale = rng.RandfRange(opt.MinScale, opt.MaxScale);
                float rotY = rng.RandfRange(0f, Mathf.Tau);

                if (opt.IsTree && _treeWrapperScene != null)
                {
                    // Boc cay trong Tree.tscn (co collision + script Chop) de chat duoc lay go
                    var wrapper = _treeWrapperScene.Instantiate<Entities.Tree>();
                    wrapper.Position = center + localPos;
                    wrapper.RotateY(rotY);
                    wrapper.Scale = Vector3.One * scale;
                    var model = scene.Instantiate<Node3D>();
                    wrapper.GetNode<Node3D>("Model").AddChild(model);
                    root.AddChild(wrapper);
                }
                else
                {
                    var inst = scene.Instantiate<Node3D>();
                    inst.Position = center + localPos;
                    inst.RotateY(rotY);
                    inst.Scale = Vector3.One * scale;
                    root.AddChild(inst);
                }
            }

            // Quai vat - khong luu trang thai, sinh moi moi lan chunk duoc tao
            if (_enemyScene != null && rng.Randf() < 0.5f)
            {
                int enemyCount = rng.Randf() < 0.2f ? 2 : 1;
                float enemyHalf = ChunkSize / 2f - 40f;
                for (int i = 0; i < enemyCount; i++)
                {
                    var localPos = new Vector3(rng.RandfRange(-enemyHalf, enemyHalf), 0f, rng.RandfRange(-enemyHalf, enemyHalf));
                    if (IsExcluded(center + localPos)) continue; // vd nam trong chan 1 cao nguyen
                    if (IsInNoEnemyZone(center + localPos)) continue; // ben trong tuong da 10 hecta quanh nong trai
                    var e = _enemyScene.Instantiate<Enemy>();
                    e.EnemyId = rng.Randf() < 0.85f ? "mud_monster" : "spiky_monster";
                    e.Position = center + localPos;
                    e.StatMultiplier = Enemy.SeasonalMultiplier();
                    root.AddChild(e);
                }
            }
        }

        // "Vung que nuoc Phap": doi thap rai rac (tao cam giac "doi + thung lung" - thung lung
        // chinh la khoang trong GIUA cac doi, khong can dia hinh lom rieng), nha cua kich thuoc
        // KHAC NHAU thua thot (cang xa tam vung cang thua dan thanh "khu dat hoang", KHONG con
        // nha/doi o ria that xa), cay coi thua hon vung hoang da, va HOAN TOAN KHONG co quai vat
        // (vung nay mang tinh chat yen binh, khac vung hoang da gan nong trai).
        private void GenerateFrenchDecor(Node3D root, Vector3 center, RandomNumberGenerator rng, float distNorm)
        {
            float half = ChunkSize / 2f - 20f;
            float density = Mathf.Lerp(1f, 0.1f, distNorm); // cang xa tam cang thua -> "dat hoang"

            // Doi thap (2.25m-4.75m, THAP hon nhieu so voi go dat o cao nguyen >5m) - la DIA
            // HINH nen khong phu thuoc density, chi phu thuoc xac suat co dinh de rai deu khap
            // vung.
            if (rng.Randf() < 0.5f)
            {
                int hillCount = rng.RandiRange(1, 2);
                for (int i = 0; i < hillCount; i++)
                {
                    float hh = rng.RandfRange(45f, 95f);
                    float hr = hh * rng.RandfRange(1.8f, 2.4f);
                    var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                    if (IsExcluded(center + localPos)) continue;
                    var hillPos = center + localPos + Vector3.Up * (hh * 0.15f);
                    root.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = hr, Height = hh * 2f },
                        Position = hillPos,
                        MaterialOverride = _frenchHillMat
                    });
                    var hillBody = new StaticBody3D { Position = hillPos };
                    hillBody.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = hr * 0.95f } });
                    root.AddChild(hillBody);
                }
            }

            // Cay thua thot (tai su dung model cay/da co san, mat do thap hon vung hoang da).
            if (rng.Randf() < density * 0.5f)
            {
                int idx = rng.RandiRange(0, _decorScenes.Length - 1);
                var scene = _decorScenes[idx];
                if (scene != null)
                {
                    var opt = _decorOptions[idx];
                    var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                    if (!IsExcluded(center + localPos))
                    {
                        var inst = scene.Instantiate<Node3D>();
                        inst.Position = center + localPos;
                        inst.RotateY(rng.RandfRange(0f, Mathf.Tau));
                        inst.Scale = Vector3.One * rng.RandfRange(opt.MinScale, opt.MaxScale);
                        root.AddChild(inst);
                    }
                }
            }

            // Thinh thoang 1 can nha KICH THUOC KHAC NHAU (nho/vua/lon - 3 model khac nhau, moi
            // model lai co khoang scale rieng) - cang xa tam vung cang hiem gap nha (density).
            if (rng.Randf() < density * 0.08f)
            {
                var options = new (PackedScene scene, float min, float max)[]
                {
                    (_cottageScene, 28f, 38f),
                    (_villageHouseScene, 38f, 52f),
                    (_bigHouseScene, 55f, 72f),
                };
                var (scene, min, max) = options[rng.RandiRange(0, options.Length - 1)];
                if (scene != null)
                {
                    var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                    if (!IsExcluded(center + localPos))
                    {
                        var inst = scene.Instantiate<Node3D>();
                        inst.Position = center + localPos;
                        inst.RotateY(rng.RandfRange(0f, Mathf.Tau));
                        inst.Scale = Vector3.One * rng.RandfRange(min, max);
                        root.AddChild(inst);
                    }
                }
            }
        }
    }
}
