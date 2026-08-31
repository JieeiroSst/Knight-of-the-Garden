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
            new("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f),
        };

        private PackedScene[] _decorScenes;
        private PackedScene _treeWrapperScene;
        private PackedScene _enemyScene;
        private Node3D _player;
        private float _timer = 0f;
        private readonly Dictionary<Vector2I, Node3D> _loaded = new();

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
            var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(ChunkSize, ChunkSize) } };
            ground.Position = center;
            ground.MaterialOverride = GroundMaterial.CreateGrass(ChunkSize, ChunkSize);
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
            else
                GenerateWildernessDecor(root, center, rng);

            return root;
        }

        private void GenerateWildernessDecor(Node3D root, Vector3 center, RandomNumberGenerator rng)
        {
            // Vat trang tri rai ngau nhien
            int decorCount = rng.RandiRange(4, 9);
            float half = ChunkSize / 2f - 20f;
            for (int i = 0; i < decorCount; i++)
            {
                int idx = rng.RandiRange(0, _decorScenes.Length - 1);
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
