using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

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
        private static readonly List<(int MinCx, int MaxCx, int MinCz, int MaxCz)> ReservedZones = new()
        {
            (-3, 2, -3, 2),     // khu nong trai (quanh goc toa do) - doi theo FarmSpace.Origin (MoveFarmReservedZone)
            (15, 21, 4, 10),    // khu thi tran (quanh VillageAnchor, xem Main.cs)
        };
        private static readonly (int MinCx, int MaxCx, int MinCz, int MaxCz) FarmReserved = (-3, 2, -3, 2);
        private static bool _reservedDirty;

        private static (int, int, int, int) FarmReservedAt(Vector3 origin)
        {
            int ox = Mathf.RoundToInt(origin.X / ExploreChunkSize), oz = Mathf.RoundToInt(origin.Z / ExploreChunkSize);
            return (FarmReserved.MinCx + ox, FarmReserved.MaxCx + ox, FarmReserved.MinCz + oz, FarmReserved.MaxCz + oz);
        }

        // Nong trai cua minh duoc doi toi toa do rieng (Main.RelocateFarm) -> vung chunk danh rieng doi theo.
        public static void MoveFarmReservedZone(Vector3 origin)
        {
            ReservedZones[0] = FarmReservedAt(origin);
            _reservedDirty = true;
        }

        // Nong trai cua nguoi choi khac (ban sao, xem FarmReplicas) cung dat san nen dat rieng.
        public static void AddFarmReserved(Vector3 origin)
        {
            var z = FarmReservedAt(origin);
            if (!ReservedZones.Contains(z)) { ReservedZones.Add(z); _reservedDirty = true; }
        }

        public static void RemoveFarmReserved(Vector3 origin)
        {
            var z = FarmReservedAt(origin);
            if (ReservedZones.IndexOf(z) >= 2) ReservedZones.Remove(z);
        }
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

        public static bool IsExcluded(Vector3 worldPos)
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

        public static RegionProfile FindRegion(Vector3 worldPos)
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

        // Nong trai cua nguoi choi khac (FarmReplicas) - cung khong sinh quai, nua canh = NoEnemyZoneHalfSize.
        public static readonly List<Vector3> ExtraNoEnemyCenters = new();

        private static bool IsInNoEnemyZone(Vector3 worldPos)
        {
            foreach (var e in ExtraNoEnemyCenters)
                if (Mathf.Abs(worldPos.X - e.X) <= NoEnemyZoneHalfSize && Mathf.Abs(worldPos.Z - e.Z) <= NoEnemyZoneHalfSize) return true;
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
        private readonly List<Vector2I> _pending = new();
        private const int MaxChunksPerTick = 2;

        private static int ChebyshevDist(Vector2I k, int pcx, int pcz) => Mathf.Max(Mathf.Abs(k.X - pcx), Mathf.Abs(k.Y - pcz));

        // ==== Kham pha (suong mu ban do) ====
        // Cac chunk nguoi choi da di qua (o dang dung + 8 o xung quanh) - MapUI ve sinh canh cua
        // cac o nay, phan con lai cua the gioi vo han de toi. Luu qua SaveSystem.ExploredChunks.
        public static readonly HashSet<Vector2I> Explored = new();
        public const float ExploreChunkSize = 500f; // = ChunkSize mac dinh (MapUI/SaveSystem dung khong can instance)
        private Vector2I? _lastPlayerChunk;
        private WorldBiomes.Biome? _lastBiome;

        private void UpdateExploration(int pcx, int pcz)
        {
            var here = new Vector2I(pcx, pcz);
            if (_lastPlayerChunk == here) return;
            _lastPlayerChunk = here;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    Explored.Add(new Vector2I(pcx + dx, pcz + dz));

            // Bao ten sinh canh khi buoc sang vung moi (chi o phan the gioi thu tuc, ben ngoai cac
            // khu dat tay - cac khu do da co bang ten rieng qua BuildingLabelZone).
            var p = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
            var p3 = new Vector3(p.X, 0, p.Y);
            if (WorldBiomes.Wildness(p) < 0.5f || FindRegion(p3) != null || TryGetFrenchRegionDist(p3, out _)
                || IsInNoEnemyZone(p3)) // trong tuong nong trai (cua minh / nguoi khac)
            {
                _lastBiome = null;
                return;
            }
            var biome = WorldBiomes.At(p);
            if (_lastBiome == biome) return;
            _lastBiome = biome;
            UI.Notify.Show(string.Format(Loc.T("biome.enter_fmt"), Loc.T(WorldBiomes.NameKey(biome)), WorldBiomes.Danger(p).ToString("0.0")));
        }

        // ==== Trai hoang (diem den thu tuc) ====
        // Rai rac ~5% chunk o vung sinh canh thu tuc - toan bo tinh tu hash toa do nen MapUI biet
        // duoc chunk da kham pha nao co trai ma khong can chunk do dang duoc tai.
        private const ulong CampSeed = 0xCA3B5EEDUL;

        public static bool HasCamp(int cx, int cz)
        {
            if (IsReserved(cx, cz)) return false;
            var center = new Vector3(cx * ExploreChunkSize + ExploreChunkSize / 2f, 0, cz * ExploreChunkSize + ExploreChunkSize / 2f);
            if (WorldBiomes.Wildness(new Vector2(center.X, center.Z)) < 0.6f) return false;
            if (FindRegion(center) != null || TryGetFrenchRegionDist(center, out _) || IsExcluded(center)) return false;
            return ChunkSeed(unchecked((int)CampSeed), cx, cz) % 1000UL < 50UL;
        }

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
            // Ban do sinh canh cua mat dat (da/cat/bun/la... theo vung) - ve lai neu Main vua dang ky them vung.
            GroundMaterial.EnsureBiomeMap();

            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player == null) return;

            int pcx = Mathf.FloorToInt(_player.GlobalPosition.X / ChunkSize);
            int pcz = Mathf.FloorToInt(_player.GlobalPosition.Z / ChunkSize);

            UpdateExploration(pcx, pcz);

            // Vung danh rieng vua them (nong trai) -> bo cac chunk hoang da da lo sinh trong do.
            if (_reservedDirty)
            {
                _reservedDirty = false;
                foreach (var key in new List<Vector2I>(_loaded.Keys))
                    if (IsReserved(key.X, key.Y)) { _loaded[key].QueueFree(); _loaded.Remove(key); }
            }

            // Sinh chunk GAN NHAT truoc. O ngay duoi chan + 8 o ke ben luon sinh ngay (tranh roi
            // xuyen dat sau khi dich chuyen), cac o xa hon gioi han MaxChunksPerTick moi lan de
            // khong khung hinh nao bi giat khi chay nhanh qua vung moi.
            _pending.Clear();
            for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
            {
                for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
                {
                    var key = new Vector2I(pcx + dx, pcz + dz);
                    if (IsReserved(key.X, key.Y) || _loaded.ContainsKey(key)) continue;
                    _pending.Add(key);
                }
            }
            _pending.Sort((a, b) => ChebyshevDist(a, pcx, pcz).CompareTo(ChebyshevDist(b, pcx, pcz)));
            int budget = MaxChunksPerTick, generated = 0;
            foreach (var key in _pending)
            {
                bool urgent = ChebyshevDist(key, pcx, pcz) <= 1;
                if (!urgent && budget <= 0) break;
                if (!urgent) budget--;
                var chunk = GenerateChunk(key.X, key.Y);
                AddChild(chunk);
                _loaded[key] = chunk;
                generated++;
            }
            // Con chunk chua sinh -> lan sau kiem tra som hon (khong doi du UpdateInterval).
            if (generated < _pending.Count) _timer = UpdateInterval * 0.7f;

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
            // Trai hoang: dung TRUOC, tam thoi dang ky vung loai tru de cay/da/co/quang/quai ngau
            // nhien cua chinh chunk nay khong moc xuyen qua trai (go bo ngay cuoi ham).
            (Vector3, float)? campZone = null;
            if (HasCamp(cx, cz))
            {
                var campPos = BuildCamp(root, cx, cz, center);
                campZone = (campPos, CampRadius);
                ExclusionZones.Add(campZone.Value);
            }

            if (TryGetFrenchRegionDist(center, out float distNorm))
                GenerateFrenchDecor(root, center, rng, distNorm);
            else if (FindRegion(center) is RegionProfile region)
                GenerateRegionDecor(root, center, rng, region);
            else
                GenerateWildernessDecor(root, center, rng);

            // Bai quang lo thien rai khap ban do (khong co o vung que Phap - khu dan cu yen binh).
            if (!TryGetFrenchRegionDist(center, out _))
                HiepSiVeVuon.Entities.OreFields.Generate(root, center, cx, cz, ChunkSeed(WorldSeed, cx, cz), ChunkSize);

            BuildHideBushes(root, center);

            if (campZone != null) ExclusionZones.Remove(campZone.Value);
            return root;
        }

        // Bui co cao de AN NAP (xem HideSpots - vi tri tat dinh, server kiem cung ham). Ve bui nao co TAM
        // nam trong chunk nay (khong ve trung o chunk ke ben). Co cao hon han nguoi (~1.5x) + vai bui
        // ram o ria de nhin la biet "chui vao day thi khong ai thay".
        private void BuildHideBushes(Node3D root, Vector3 center)
        {
            float half = ChunkSize / 2f;
            int c0x = Mathf.FloorToInt((center.X - half) / HideSpots.Cell), c1x = Mathf.FloorToInt((center.X + half) / HideSpots.Cell);
            int c0z = Mathf.FloorToInt((center.Z - half) / HideSpots.Cell), c1z = Mathf.FloorToInt((center.Z + half) / HideSpots.Cell);
            var transforms = new List<Transform3D>();
            for (int cx = c0x; cx <= c1x; cx++)
                for (int cz = c0z; cz <= c1z; cz++)
                {
                    if (!HideSpots.TryGetBush(cx, cz, out var b)) continue;
                    if (Mathf.Abs(b.X - center.X) > half || Mathf.Abs(b.Z - center.Z) > half) continue;
                    var bc = new Vector3(b.X, 0, b.Z);
                    if (IsExcluded(bc)) continue;
                    var rng = new RandomNumberGenerator { Seed = HideSpots.Hash3(cx, cz, 991u) };
                    int blades = Mathf.RoundToInt(b.R * 1.6f);
                    for (int i = 0; i < blades; i++)
                    {
                        float a = rng.RandfRange(0f, Mathf.Tau);
                        float d = Mathf.Sqrt(rng.Randf()) * b.R;
                        var pos = bc + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d) - center;
                        float sc = rng.RandfRange(9f, 13f) * (1f - 0.25f * d / b.R);
                        transforms.Add(new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau)).Scaled(new Vector3(sc, sc * 1.35f, sc)), pos));
                    }
                    var bushScene = _decorScenes.Length > 8 ? _decorScenes[8] : null; // plant_bush.glb
                    if (bushScene != null)
                        for (int i = 0; i < 3; i++)
                        {
                            float a = Mathf.Tau * i / 3f + rng.RandfRange(-0.4f, 0.4f);
                            var inst = bushScene.Instantiate<Node3D>();
                            inst.Position = bc + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * b.R * 0.75f;
                            inst.Scale = Vector3.One * rng.RandfRange(20f, 28f);
                            inst.RotateY(rng.RandfRange(0f, Mathf.Tau));
                            GraphicsSettings.SetVisibilityRange(inst, 1600f, castShadow: false);
                            root.AddChild(inst);
                        }
                }
            if (transforms.Count == 0 || _grassBladeMesh == null) return;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _grassBladeMesh, InstanceCount = transforms.Count };
            for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
            var field = new MultiMeshInstance3D { Multimesh = mm, Position = center, Name = "HideBushes" };
            // Bui an nap phai thay tu XA (de tim cho tron) - tam nhin xa hon co thuong, khong do bong.
            GraphicsSettings.SetVisibilityRange(field, 1800f, castShadow: false);
            root.AddChild(field);
        }

        private const float CampRadius = 130f;

        // Trai linh danh thue bo hoang: dong lua trai + thung/hom/khuc go/xe ho quanh, 1 ruong bau
        // (mo 1 lan, luu theo ChestId) va 1 toan quai canh giu manh hon quai thuong cung vung.
        private Vector3 BuildCamp(Node3D root, int cx, int cz, Vector3 center)
        {
            var rng = new RandomNumberGenerator { Seed = ChunkSeed(unchecked((int)CampSeed), cx, cz) ^ 0x7A11UL };
            float half = ChunkSize / 2f - CampRadius - 20f;
            var pos = center + new Vector3(rng.RandfRange(-half, half), 0, rng.RandfRange(-half, half));
            var p2 = new Vector2(pos.X, pos.Z);
            float danger = WorldBiomes.Danger(p2);
            var camp = new Node3D { Name = "Camp", Position = pos, RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0) };
            root.AddChild(camp);

            void Prop(string path, Vector3 offset, float height, float rotY)
            {
                var m = ModelUtil.Instance(path);
                if (m == null) return;
                ModelUtil.FitToHeight(m, height);
                var holder = new Node3D { Position = offset, RotationDegrees = new Vector3(0, rotY, 0) };
                holder.AddChild(m);
                GraphicsSettings.SetVisibilityRange(holder, 1600f);
                camp.AddChild(holder);
            }

            // Dong lua trai: vai khuc go xep vong + loi lua phat sang + den chap chon.
            const string F = "res://assets3d/quaternius/furniture/";
            for (int i = 0; i < 4; i++)
                Prop(F + "wood_log.glb", new Vector3(Mathf.Cos(i * Mathf.Pi / 2f) * 9f, 0, Mathf.Sin(i * Mathf.Pi / 2f) * 9f), 6f, i * 90f + 45f);
            camp.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 6f, Height = 9f },
                Position = new Vector3(0, 3f, 0),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.45f, 0.1f),
                    EmissionEnabled = true,
                    Emission = new Color(1f, 0.5f, 0.15f),
                    EmissionEnergyMultiplier = 3f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                }
            });
            camp.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.3f), LightEnergy = 2.2f, OmniRange = 160f, Position = new Vector3(0, 18f, 0), DistanceFadeEnabled = true, DistanceFadeBegin = 700f, DistanceFadeLength = 200f });

            // Do dac rai vong ngoai.
            string[] props = { "barrel.glb", "crate.glb", "barrel.glb", "crate.glb", "cart.glb", "wood_log.glb" };
            float[] heights = { 22f, 18f, 22f, 18f, 32f, 7f };
            int propCount = rng.RandiRange(5, 8);
            for (int i = 0; i < propCount; i++)
            {
                int k = rng.RandiRange(0, props.Length - 1);
                float a = Mathf.Tau * i / propCount + rng.RandfRange(-0.25f, 0.25f);
                float r = rng.RandfRange(55f, 95f);
                Prop(F + props[k], new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), heights[k], rng.RandfRange(0, 360));
            }
            Prop("res://assets3d/kenney/town/Models/GLB format/banner-red.glb", new Vector3(0, 0, -40f), 55f, 0f);

            var chest = new TreasureChest
            {
                ChestId = $"camp_{cx}_{cz}",
                Tier = Mathf.Clamp(1 + Mathf.FloorToInt((danger - 1f) / 2f), 1, 3),
                Position = new Vector3(0, 0, 30f),
            };
            camp.AddChild(chest);

            // Toan canh giu: lay bang quai cua sinh canh tai trai, manh hon 25%.
            if (_enemyScene != null)
            {
                var biome = WorldBiomes.At(p2);
                var table = BiomeProfiles[(int)biome].Enemies;
                int guards = Mathf.Clamp(2 + Mathf.FloorToInt((danger - 1f) * 0.5f), 2, 5);
                for (int i = 0; i < guards; i++)
                {
                    float a = Mathf.Tau * i / guards;
                    var e = _enemyScene.Instantiate<Enemy>();
                    e.EnemyId = PickWeighted(table, rng);
                    e.Position = pos + new Vector3(Mathf.Cos(a) * 70f, 0, Mathf.Sin(a) * 70f);
                    e.StatMultiplier = danger * 1.25f * Enemy.SeasonalMultiplier();
                    e.RewardMultiplier = danger * 1.25f;
                    root.AddChild(e);
                }
            }
            return pos;
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

        private Node3D BuildGrassField(Vector3 center, RandomNumberGenerator rng, int density = GrassFieldDensity)
        {
            if (_grassBladeMesh == null) return null;
            float half = ChunkSize / 2f - 15f;
            density = Mathf.RoundToInt(density * GraphicsSettings.GrassDensityScale);
            var transforms = new System.Collections.Generic.List<Transform3D>(density);
            for (int i = 0; i < density; i++)
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

            var field = new MultiMeshInstance3D { Multimesh = multiMesh, Position = center };
            // Co chi can thay gan nguoi choi, va bong do cua hang tram cong co gan nhu vo hinh nhung rat ton.
            GraphicsSettings.SetVisibilityRange(field, 800f, castShadow: false);
            return field;
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
                        CullDecor(wrapper, scene, true);
                        root.AddChild(wrapper);
                    }
                    else
                    {
                        var inst = scene.Instantiate<Node3D>();
                        inst.Position = center + localPos;
                        inst.RotateY(rotY);
                        inst.Scale = Vector3.One * scale;
                        CullDecor(inst, scene, false);
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

        // Tam nhin theo loai vat: cay (to, thay tu xa) > da > vat nho (bui/hoa/co - khong do bong).
        private static void CullDecor(Node3D node, PackedScene scene, bool isTree)
        {
            string path = scene?.ResourcePath ?? "";
            if (isTree || path.Contains("tree")) GraphicsSettings.SetVisibilityRange(node, 2400f);
            else if (path.Contains("rock")) GraphicsSettings.SetVisibilityRange(node, 1600f);
            else GraphicsSettings.SetVisibilityRange(node, 900f, castShadow: false);
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

        // Bo trang tri/quai cua tung sinh canh thu tuc (thu tu = WorldBiomes.Biome). Chi so Decor
        // tro vao _decorOptions: 0-5 cay, 6-7 da, 8 bui, 9 hoa, 10 co. Decor null = toan bo
        // _decorOptions (Plains giu NGUYEN cach rai cu cua vung hoang da quanh nong trai).
        private class BiomeProfile
        {
            public int[] Decor;
            public int MinDecor, MaxDecor;
            // Xac suat 1 o tro thanh "mang rung nho" chi toan cay day dac.
            public float ForestPatchChance;
            public int GrassDensity;
            public float NonTreeScale = 1f; // phong to da/bui (vd tang da lon o cao nguyen)
            public (string enemyId, float weight)[] Enemies;
            public float EnemyChance;
        }

        private static readonly BiomeProfile[] BiomeProfiles =
        {
            new() { // Plains
                Decor = null, MinDecor = 4, MaxDecor = 9, ForestPatchChance = 0.16f, GrassDensity = 340,
                Enemies = new[] { ("mud_monster", 0.85f), ("spiky_monster", 0.15f) }, EnemyChance = 0.5f },
            new() { // Forest - rung ram
                Decor = new[] { 0, 1, 2, 3, 0, 1, 2, 3, 4, 8 }, MinDecor = 22, MaxDecor = 34, GrassDensity = 200,
                Enemies = new[] { ("forest_wolf", 0.7f), ("mud_monster", 0.3f) }, EnemyChance = 0.6f },
            new() { // Highlands - cao nguyen da
                Decor = new[] { 6, 7, 6, 7, 4, 5, 8 }, MinDecor = 8, MaxDecor = 14, ForestPatchChance = 0.05f, GrassDensity = 110,
                NonTreeScale = 1.7f,
                Enemies = new[] { ("mountain_troll", 0.6f), ("spiky_monster", 0.4f) }, EnemyChance = 0.55f },
            new() { // Marsh - dam lay
                Decor = new[] { 4, 5, 8, 8, 10, 10, 6 }, MinDecor = 6, MaxDecor = 11, GrassDensity = 300,
                Enemies = new[] { ("swamp_lurker", 0.6f), ("mud_monster", 0.4f) }, EnemyChance = 0.6f },
            new() { // Barrens - hoang mac
                Decor = new[] { 6, 7, 8 }, MinDecor = 2, MaxDecor = 5, GrassDensity = 30, NonTreeScale = 1.3f,
                Enemies = new[] { ("spiky_monster", 0.6f), ("ruins_wraith", 0.4f) }, EnemyChance = 0.5f },
            new() { // Meadow - dong hoa
                Decor = new[] { 9, 9, 9, 8, 10, 2, 4 }, MinDecor = 8, MaxDecor = 14, ForestPatchChance = 0.06f, GrassDensity = 420,
                Enemies = new[] { ("mud_monster", 1f) }, EnemyChance = 0.25f },
        };

        // Dan ngua hoang (dong co / dong hoa ngoai trang trai) - RNG rieng de khong doi vi tri cay/da cu.
        private static PackedScene _horseScene;
        private void SpawnWildHorses(Node3D root, Vector3 center)
        {
            var c2 = new Vector2(center.X, center.Z);
            var biome = WorldBiomes.At(c2);
            if (biome != WorldBiomes.Biome.Plains && biome != WorldBiomes.Biome.Meadow) return;
            int cx = Mathf.FloorToInt(center.X / ChunkSize), cz = Mathf.FloorToInt(center.Z / ChunkSize);
            var rng = new RandomNumberGenerator { Seed = ChunkSeed(WorldSeed, cx, cz) ^ 0x4805EUL };
            if (rng.Randf() > 0.07f || IsInNoEnemyZone(center) || IsExcluded(center)) return;
            _horseScene ??= GD.Load<PackedScene>("res://scenes/Horse.tscn");
            int n = rng.RandiRange(1, 3);
            for (int i = 0; i < n; i++)
            {
                string key = $"{cx},{cz},{i}";
                var pos = center + new Vector3(rng.RandfRange(-150f, 150f), 0, rng.RandfRange(-150f, 150f));
                float speed = rng.RandfRange(210f, 340f);
                ulong cd = (ulong)rng.RandiRange(4500, 8500);
                if (HorseRegistry.IsSpawnTamed(key)) continue;
                var h = _horseScene.Instantiate<Horse>();
                h.IsWild = true;
                h.SpawnKey = key;
                h.RideSpeed = speed;
                h.GallopCooldownMsOverride = cd;
                h.Position = pos;
                h.HomeCenter = center;
                h.PastureHalfExtent = 260f;
                root.AddChild(h);
            }
        }

        private void GenerateWildernessDecor(Node3D root, Vector3 center, RandomNumberGenerator rng)
        {
            var c2 = new Vector2(center.X, center.Z);
            var profile = BiomeProfiles[(int)WorldBiomes.At(c2)];
            SpawnWildHorses(root, center);
            float danger = WorldBiomes.Danger(c2);

            var grassField = BuildGrassField(center, rng, profile.GrassDensity);
            if (grassField != null) root.AddChild(grassField);

            // Mang rung nho: o luoi nay CHI rai cay (mat do day hon nhieu), khong xen rock/bui/hoa
            // - tao cam giac 1 cum rung ro net thay vi cay le te lan trong decor thong thuong.
            bool isForestPatch = rng.Randf() < profile.ForestPatchChance;
            int decorCount = isForestPatch ? rng.RandiRange(16, 26) : rng.RandiRange(profile.MinDecor, profile.MaxDecor);
            float half = ChunkSize / 2f - 20f;
            for (int i = 0; i < decorCount; i++)
            {
                int idx = isForestPatch ? rng.RandiRange(0, TreeDecorIndexMax)
                    : profile.Decor == null ? rng.RandiRange(0, _decorScenes.Length - 1)
                    : profile.Decor[rng.RandiRange(0, profile.Decor.Length - 1)];
                var scene = _decorScenes[idx];
                if (scene == null) continue;
                var opt = _decorOptions[idx];
                var localPos = new Vector3(rng.RandfRange(-half, half), 0f, rng.RandfRange(-half, half));
                if (IsExcluded(center + localPos)) continue; // vd nam trong chan 1 cao nguyen
                float scale = rng.RandfRange(opt.MinScale, opt.MaxScale) * (opt.IsTree ? 1f : profile.NonTreeScale);
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
                    CullDecor(wrapper, scene, true);
                    root.AddChild(wrapper);
                }
                else
                {
                    var inst = scene.Instantiate<Node3D>();
                    inst.Position = center + localPos;
                    inst.RotateY(rotY);
                    inst.Scale = Vector3.One * scale;
                    CullDecor(inst, scene, false);
                    root.AddChild(inst);
                }
            }

            // Quai vat - khong luu trang thai, sinh moi moi lan chunk duoc tao. Cang xa nong trai
            // (danger cao) quai cang manh, cang dong va thuong cang nhieu.
            float enemyChance = Mathf.Min(0.95f, profile.EnemyChance + 0.06f * (danger - 1f) + (WorldEventsClient.BloodMoon ? 0.3f : 0f));
            if (_enemyScene != null && rng.Randf() < enemyChance)
            {
                int enemyCount = (rng.Randf() < 0.2f ? 2 : 1) + Mathf.FloorToInt((danger - 1f) * 0.6f);
                enemyCount = Mathf.Min(enemyCount, 5);
                float enemyHalf = ChunkSize / 2f - 40f;
                for (int i = 0; i < enemyCount; i++)
                {
                    var localPos = new Vector3(rng.RandfRange(-enemyHalf, enemyHalf), 0f, rng.RandfRange(-enemyHalf, enemyHalf));
                    if (IsExcluded(center + localPos)) continue; // vd nam trong chan 1 cao nguyen
                    if (IsInNoEnemyZone(center + localPos)) continue; // ben trong tuong da 10 hecta quanh nong trai
                    var e = _enemyScene.Instantiate<Enemy>();
                    e.EnemyId = PickWeighted(profile.Enemies, rng);
                    e.Position = center + localPos;
                    e.StatMultiplier = danger * Enemy.SeasonalMultiplier();
                    e.RewardMultiplier = danger;
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
                        CullDecor(inst, scene, false);
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
                        GraphicsSettings.SetVisibilityRange(inst, 3200f);
                        root.AddChild(inst);
                    }
                }
            }
        }
    }
}
