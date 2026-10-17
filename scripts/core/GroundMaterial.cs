using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Core
{
    // Vat lieu mat dat (ambientCG, CC0) dung chung cho San chinh, Thi tran va cac chunk WorldStreamer.
    public static class GroundMaterial
    {
        private const string EarthColorPath = "res://assets3d/textures/ground/ground_color.jpg";
        private const string EarthNormalPath = "res://assets3d/textures/ground/ground_normal.jpg";
        private const string EarthRoughnessPath = "res://assets3d/textures/ground/ground_roughness.jpg";

        private const float TileSize = 70f; // 1 lan lap texture khoi dat ben duoi (~70 don vi the gioi)

        private static Texture2D _earthColor, _earthNormal, _earthRoughness;

        // Do sau khoi dat dac ben duoi lop co (xem CreateEarthMass).
        public const float EarthDepth = 400f;

        // So lan chia luoi can co tren PlaneMesh de shader tao go va do bong theo do doc.
        public static int SubdivisionsFor(float sizeUnits) => Mathf.Clamp((int)(sizeUnits / 40f), 8, 80);

        // ==== Mat dat nhieu lop (terrain.gdshader) ====
        // 8 lop vat lieu that (ambientCG, CC0): ten file trong assets3d/textures/terrain.
        private static readonly string[] Layers = { "0_grass", "1_meadow", "2_dirt", "3_mud", "4_forest", "5_rock", "6_sand", "7_gravel" };
        private const string TerrainDir = "res://assets3d/textures/terrain/";
        private static Shader _terrainShader;
        private static Texture2DArray _albedoArray, _normalArray;

        private static Texture2DArray BuildArray(string suffix)
        {
            var images = new Godot.Collections.Array<Image>();
            foreach (var l in Layers)
            {
                var img = GD.Load<Texture2D>(TerrainDir + l + suffix)?.GetImage();
                if (img == null) return null;
                if (img.IsCompressed()) img.Decompress();
                img.Convert(Image.Format.Rgba8);
                if (!img.HasMipmaps()) img.GenerateMipmaps();
                images.Add(img);
            }
            var arr = new Texture2DArray();
            return arr.CreateFromImages(images) == Error.Ok ? arr : null;
        }

        // Ten giu nguyen (CreateGrass) vi San chinh, Thi tran va chunk WorldStreamer deu goi ham nay.
        // MOI mat dat dung CUNG 1 shader + toa do the gioi + ban do sinh canh chung -> khong co duong
        // ranh giua cac o dat; vung nao lop dat nao do ban do sinh canh quyet dinh (RebuildBiomeMap).
        // bumpHeight: 2.5 cho dat nong trai/thi tran (da san phang), lon hon cho vung hoang da.
        public static ShaderMaterial CreateGrass(float worldWidth, float worldDepth, float bumpHeight = 2.5f)
        {
            _terrainShader ??= GD.Load<Shader>("res://assets/shaders/terrain.gdshader");
            _albedoArray ??= BuildArray("_albedo_height.png");
            _normalArray ??= BuildArray("_normal_rough.png");
            EnsureBiomeMap();
            var mat = new ShaderMaterial { Shader = _terrainShader };
            mat.SetShaderParameter("layer_albedo", _albedoArray);
            mat.SetShaderParameter("layer_normal", _normalArray);
            mat.SetShaderParameter("bump_height", bumpHeight);
            return mat;
        }

        // ==== Ban do sinh canh ====
        // Trong so 8 lop cho tung loai vung (theo ten vung dang ky trong WorldStreamer.Regions).
        //                                   co   coDat  dat  bun  la   da   cat  soi
        // Nen ngoai cac vung = sinh canh thu tuc cua the gioi vo han (xem WorldBiomes.GroundLayers).
        private static readonly Dictionary<string, float[]> RegionLayers = new()
        {
            ["Núi"] =        new[] { .05f, .22f, .10f, 0f, 0f, .48f, 0f, .15f },
            ["Rừng"] =       new[] { .30f, .12f, 0f, .10f, .43f, .05f, 0f, 0f },
            ["Đồng Ruộng"] = new[] { .48f, .32f, .20f, 0f, 0f, 0f, 0f, 0f },
            ["Hồ"] =         new[] { .35f, .10f, 0f, .20f, 0f, 0f, .35f, 0f },
            ["Sông"] =       new[] { .38f, .10f, 0f, .14f, 0f, .05f, .25f, .08f },
            ["Làng"] =       new[] { .45f, .15f, .25f, 0f, 0f, 0f, 0f, .15f },
            ["Thành Phố"] =  new[] { .25f, .05f, .30f, 0f, 0f, .05f, 0f, .35f },
            ["Phế Tích"] =   new[] { .10f, .35f, .15f, 0f, 0f, .25f, 0f, .15f },
            ["Nghĩa Địa"] =  new[] { .05f, .35f, .05f, .40f, .05f, .10f, 0f, 0f },
            ["Đầm Lầy"] =    new[] { .30f, .05f, 0f, .48f, .17f, 0f, 0f, 0f },
            ["Hang Động"] =  new[] { 0f, .10f, .05f, .15f, 0f, .50f, 0f, .20f },
        };
        private static readonly float[] Farm =   { .55f, .22f, .20f, 0f, 0f, 0f, 0f, .03f };
        private static readonly float[] French = { .50f, .38f, .12f, 0f, 0f, 0f, 0f, 0f };

        private static string _biomeSignature;
        private static ImageTexture _biomeTexA, _biomeTexB;
        private const int BiomeRes = 512;
        private const float Feather = 380f; // do rong vung chuyen tiep mem giua 2 sinh canh

        // Goi lai moi khi danh sach vung thay doi (re: so sanh chu ky danh sach truoc).
        public static void EnsureBiomeMap()
        {
            var regions = WorldStreamer.Regions;
            string sig = regions.Count + "|" + WorldStreamer.FrenchRegionCenter + "|" + WorldStreamer.NoEnemyZoneCenter + "|" + WorldStreamer.NoEnemyZoneHalfSize;
            if (sig == _biomeSignature) return;
            _biomeSignature = sig;

            var rects = new List<(Vector2 c, float half, float[] w)>();
            foreach (var r in regions)
                if (RegionLayers.TryGetValue(r.Name ?? "", out var w)) rects.Add((new Vector2(r.Center.X, r.Center.Z), r.HalfSize, w));
            if (WorldStreamer.FrenchRegionCenter is Vector3 fc) rects.Add((new Vector2(fc.X, fc.Z), WorldStreamer.FrenchRegionHalfSize, French));
            // Nong trai o goc toa do (offline) nam trong ban do; nong trai o toa do rieng (xa hang chuc km)
            // ve bang tham so toan cuc farm_c* cua shader (SetFarmCenters) - khong lam ban do bi gian ra.
            var farmC = WorldStreamer.NoEnemyZoneCenter ?? Vector3.Zero;
            if (FarmSpace.Origin == Vector3.Zero)
                rects.Add((new Vector2(farmC.X, farmC.Z), Mathf.Max(1500f, WorldStreamer.NoEnemyZoneHalfSize), Farm));

            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var (c, half, _) in rects)
            {
                min = new Vector2(Mathf.Min(min.X, c.X - half), Mathf.Min(min.Y, c.Y - half));
                max = new Vector2(Mathf.Max(max.X, c.X + half), Mathf.Max(max.Y, c.Y + half));
            }
            min -= Vector2.One * 2000f;
            max += Vector2.One * 2000f;
            var size = max - min;

            var a = Image.CreateEmpty(BiomeRes, BiomeRes, false, Image.Format.Rgba8);
            var b = Image.CreateEmpty(BiomeRes, BiomeRes, false, Image.Format.Rgba8);
            var w8 = new float[8];
            for (int y = 0; y < BiomeRes; y++)
                for (int x = 0; x < BiomeRes; x++)
                {
                    var p = min + new Vector2((x + 0.5f) / BiomeRes * size.X, (y + 0.5f) / BiomeRes * size.Y);
                    WorldBiomes.GroundLayers(p, w8);
                    foreach (var (c, half, w) in rects)
                    {
                        // Khoang cach co dau toi canh hinh vuong cua vung (am = ben trong).
                        float d = Mathf.Max(Mathf.Abs(p.X - c.X), Mathf.Abs(p.Y - c.Y)) - half;
                        float m = 1f - Mathf.SmoothStep(-Feather, Feather, d);
                        if (m <= 0.001f) continue;
                        for (int k = 0; k < 8; k++) w8[k] = Mathf.Lerp(w8[k], w[k], m);
                    }
                    float sum = 0f;
                    for (int k = 0; k < 8; k++) sum += w8[k];
                    sum = Mathf.Max(sum, 0.0001f);
                    a.SetPixel(x, y, new Color(w8[0] / sum, w8[1] / sum, w8[2] / sum, w8[3] / sum));
                    b.SetPixel(x, y, new Color(w8[4] / sum, w8[5] / sum, w8[6] / sum, w8[7] / sum));
                }
            // GIU tham chieu C# toi texture dang dung lam tham so toan cuc: RenderingServer chi giu RID, neu
            // de GC cua C# thu hoi doi tuong ImageTexture thi Godot crash "FATAL: !rc_owner" (csharp_script.cpp).
            _biomeTexA = ImageTexture.CreateFromImage(a);
            _biomeTexB = ImageTexture.CreateFromImage(b);
            RenderingServer.GlobalShaderParameterSet("terrain_biome_a", _biomeTexA);
            RenderingServer.GlobalShaderParameterSet("terrain_biome_b", _biomeTexB);
            RenderingServer.GlobalShaderParameterSet("terrain_bounds", new Vector4(min.X, min.Y, size.X, size.Y));
        }

        // Toi da 4 nong trai (cua minh + 3 nguoi gan nhat) duoc to "dat nong trai" tren shader:
        // farm_cN = (x, z, nua canh, 1 = bat).
        public const int MaxFarmCenters = 4;
        public static void SetFarmCenters(System.Collections.Generic.IList<Vector2> others = null)
        {
            float half = Mathf.Max(1500f, WorldStreamer.NoEnemyZoneHalfSize);
            var list = new List<Vector4>();
            if (FarmSpace.Origin != Vector3.Zero && WorldStreamer.NoEnemyZoneCenter is Vector3 mine)
                list.Add(new Vector4(mine.X, mine.Z, half, 1f));
            if (others != null)
                foreach (var o in others) { if (list.Count >= MaxFarmCenters) break; list.Add(new Vector4(o.X, o.Y, half, 1f)); }
            for (int i = 0; i < MaxFarmCenters; i++)
                RenderingServer.GlobalShaderParameterSet("farm_c" + i, i < list.Count ? list[i] : Vector4.Zero);
        }

        // Khoi dat dac (texture dat/da that, ambientCG, CC0) nam ngay duoi mat co - de nhin tu
        // bat ky goc nao (ke ca gan ngang tam mat) van thay dat that co chieu sau va chat lieu
        // dat that, khong phai mot mat phang mong mau don "lo lung tren troi".
        public static MeshInstance3D CreateEarthMass(float width, float depth)
        {
            _earthColor ??= GD.Load<Texture2D>(EarthColorPath);
            _earthNormal ??= GD.Load<Texture2D>(EarthNormalPath);
            _earthRoughness ??= GD.Load<Texture2D>(EarthRoughnessPath);

            var mat = new StandardMaterial3D
            {
                AlbedoTexture = _earthColor,
                NormalEnabled = _earthNormal != null,
                NormalTexture = _earthNormal,
                RoughnessTexture = _earthRoughness,
                RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Grayscale,
                Uv1Scale = new Vector3(width / TileSize, EarthDepth / TileSize, depth / TileSize),
                Roughness = 1f,
                MetallicSpecular = 0.05f
            };

            return new MeshInstance3D
            {
                Name = "EarthMass",
                Mesh = new BoxMesh { Size = new Vector3(width, EarthDepth, depth) },
                MaterialOverride = mat,
                Position = new Vector3(0, -EarthDepth / 2f - 0.5f, 0)
            };
        }
    }
}
