using Godot;

namespace HiepSiVeVuon.Core
{
    // Sinh canh THU TUC cho phan the gioi vo han nam NGOAI cac khu dat tay (nong trai/thi tran/
    // 11 khu the gioi mo). Ban do khong co bien: di bao xa cung gap sinh canh moi (rung ram, cao
    // nguyen da, dam lay, hoang mac, dong hoa...) va cang xa nong trai cang nguy hiem.
    //
    // Ham nhieu o day DUOC CHEP Y HET trong assets/shaders/terrain.gdshader (biome_proc) - de mat
    // dat (shader) va cay/da/quai (WorldStreamer) luon khop sinh canh. Doi 1 ben thi PHAI doi ca
    // ben kia (hash, nguong smoothstep, ty le, offset).
    public static class WorldBiomes
    {
        public enum Biome { Plains, Forest, Highlands, Marsh, Barrens, Meadow }
        public const int BiomeCount = 6;

        // Trong vong CoreRadius quanh goc toa do = vung hoang da "thuan" nhu cu (dong co); tu do
        // chuyen dan sang sinh canh thu tuc trong khoang BlendWidth.
        public const float CoreRadius = 9500f;
        public const float BlendWidth = 3000f;
        private const float BiomeScale = 1f / 5200f;

        // Vung "nguy hiem" bat dau tang tu CoreRadius: +1 bac moi DangerStep don vi, toi da MaxDanger.
        private const float DangerStep = 12000f;
        private const float MaxDanger = 6f;

        // Trong so 8 lop dat (co, co lan dat, dat, bun, la rung, da, cat, soi) cho moi sinh canh.
        // Plains = bo "Wild" cu cua GroundMaterial.
        public static readonly float[][] Layers =
        {
            new[] { .50f, .30f, .10f, 0f, .06f, .02f, 0f, .02f },   // Plains
            new[] { .28f, .12f, 0f, .10f, .45f, .05f, 0f, 0f },     // Forest
            new[] { .08f, .25f, .08f, 0f, 0f, .45f, 0f, .14f },     // Highlands
            new[] { .28f, .05f, 0f, .50f, .17f, 0f, 0f, 0f },       // Marsh
            new[] { .06f, .18f, .26f, 0f, 0f, .08f, .36f, .06f },   // Barrens
            new[] { .30f, .58f, .08f, 0f, 0f, 0f, 0f, .04f },       // Meadow
        };

        // Mau hien thi tren ban do (MapUI).
        public static readonly Color[] MapColors =
        {
            new(0.26f, 0.42f, 0.2f),
            new(0.12f, 0.3f, 0.13f),
            new(0.48f, 0.46f, 0.42f),
            new(0.25f, 0.3f, 0.2f),
            new(0.72f, 0.62f, 0.4f),
            new(0.5f, 0.6f, 0.26f),
        };

        public static string NameKey(Biome b) => "biome." + b.ToString().ToLowerInvariant();

        private static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = ((uint)x * 0x8da6b343u) ^ ((uint)y * 0xd8163841u) ^ (seed * 0xcb1ab31fu);
                h ^= h >> 13;
                h *= 0x85ebca6bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Lattice(int x, int y, uint seed) => (Hash(x, y, seed) & 0xFFFFFFu) / 16777215f;

        private static float ValueNoise(float x, float y, uint seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = Lattice(ix, iy, seed), b = Lattice(ix + 1, iy, seed);
            float c = Lattice(ix, iy + 1, seed), d = Lattice(ix + 1, iy + 1, seed);
            float ab = a + (b - a) * ux, cd = c + (d - c) * ux;
            return ab + (cd - ab) * uy;
        }

        private static float Fbm(float x, float y, uint seed) =>
            ValueNoise(x, y, seed) * 0.6f
            + ValueNoise(x * 2.07f + 5.3f, y * 2.07f + 1.7f, seed + 1) * 0.28f
            + ValueNoise(x * 4.13f + 9.1f, y * 4.13f + 3.3f, seed + 2) * 0.12f;

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // 0 = vung loi (dong co nhu cu), 1 = hoan toan sinh canh thu tuc.
        public static float Wildness(Vector2 p) => Smooth(CoreRadius, CoreRadius + BlendWidth, p.Length());

        // Trong so tung sinh canh tai p (chua tinh Wildness) - "nhiet do" va "do am" la 2 truong
        // nhieu tan so thap doc lap.
        public static void Weights(Vector2 p, float[] w)
        {
            float t = Fbm(p.X * BiomeScale, p.Y * BiomeScale, 11u);
            float m = Fbm(p.X * BiomeScale + 37.2f, p.Y * BiomeScale - 19.4f, 23u);
            float forest = Smooth(.54f, .61f, m) * (1f - Smooth(.5f, .57f, t));
            float marsh = Smooth(.54f, .61f, m) * Smooth(.5f, .57f, t);
            float barren = (1f - Smooth(.41f, .48f, m)) * Smooth(.52f, .59f, t);
            float high = (1f - Smooth(.38f, .45f, t)) * (1f - Smooth(.54f, .61f, m));
            float meadow = Smooth(.45f, .5f, t) * (1f - Smooth(.52f, .59f, t)) * (1f - Smooth(.47f, .53f, m));
            w[1] = forest; w[2] = high; w[3] = marsh; w[4] = barren; w[5] = meadow;
            w[0] = Mathf.Max(0f, 1f - (forest + high + marsh + barren + meadow));
        }

        [System.ThreadStatic] private static float[] _scratch;

        public static Biome At(Vector2 p)
        {
            if (Wildness(p) < 0.5f) return Biome.Plains;
            var w = _scratch ??= new float[BiomeCount];
            Weights(p, w);
            int best = 0;
            for (int i = 1; i < BiomeCount; i++) if (w[i] > w[best]) best = i;
            return (Biome)best;
        }

        // Trong so 8 lop dat da tron theo sinh canh + Wildness - GroundMaterial dung lam nen cho
        // ban do sinh canh (de vung trong texture khop lien voi shader ben ngoai texture).
        public static void GroundLayers(Vector2 p, float[] outLayers)
        {
            System.Array.Copy(Layers[0], outLayers, 8);
            float wild = Wildness(p);
            if (wild <= 0f) return;
            var w = _scratch ??= new float[BiomeCount];
            Weights(p, w);
            for (int k = 0; k < 8; k++)
            {
                float v = 0f;
                for (int i = 0; i < BiomeCount; i++) v += w[i] * Layers[i][k];
                outLayers[k] = Mathf.Lerp(Layers[0][k], v, wild);
            }
        }

        // He so suc manh quai theo khoang cach toi nong trai (1 = binh thuong).
        // Tinh tu tam an toan GAN NHAT: goc the gioi (thi tran) va cac nong trai (cua minh + nguoi khac,
        // moi nguoi 1 toa do rieng cach nhau >= 5 km) - quanh nong trai nao cung yen binh nhu cu.
        public static readonly System.Collections.Generic.List<Vector2> SafeCenters = new();

        public static float DistanceToSafety(Vector2 p)
        {
            float d = p.Length();
            foreach (var c in SafeCenters) d = Mathf.Min(d, p.DistanceTo(c));
            return d;
        }

        public static float Danger(Vector2 p) =>
            1f + Mathf.Clamp((DistanceToSafety(p) - CoreRadius) / DangerStep, 0f, MaxDanger - 1f);
    }
}
