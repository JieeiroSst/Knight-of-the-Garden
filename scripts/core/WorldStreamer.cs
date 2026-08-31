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

        public override void _Ready()
        {
            _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            _treeWrapperScene = GD.Load<PackedScene>("res://scenes/Tree.tscn");
            _decorScenes = new PackedScene[_decorOptions.Length];
            for (int i = 0; i < _decorOptions.Length; i++)
                _decorScenes[i] = GD.Load<PackedScene>(_decorOptions[i].Path);
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
                    var e = _enemyScene.Instantiate<Enemy>();
                    e.EnemyId = rng.Randf() < 0.85f ? "mud_monster" : "spiky_monster";
                    e.Position = center + localPos;
                    root.AddChild(e);
                }
            }

            return root;
        }
    }
}
