using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // BAI QUANG LO THIEN rai KHAP BAN DO (vo han): moi chunk the gioi mo (WorldStreamer) co xac suat
    // sinh 1 bai quang (3-7 khoi + da tang lon), tinh TAT DINH theo toa do chunk (cung vi tri, cung loai
    // quang moi lan tai lai). Cang xa nong trai quang cang quy; vung Nui/Hang Dong nhieu mo hon, khu dan
    // cu it hon. Khoi da dao ghi vao OreRegistry (khoa "field_cx_cz_i") nen khong hoi sinh khi tai lai chunk.
    public static class OreFields
    {
        private record OreChoice(string Ore, int Weight, int Hp, int Required);

        // 3 vanh dai theo khoang cach toi trung tam the gioi (nong trai o gan goc toa do).
        private static readonly OreChoice[] Near =
        {
            new("da", 30, 15, 0), new("than_da", 25, 25, 0), new("dong_tho", 25, 20, 0), new("quang_nui", 15, 35, 0), new("sat_tho", 8, 40, 0),
        };
        private static readonly OreChoice[] Mid =
        {
            new("than_da", 18, 25, 0), new("sat_tho", 22, 40, 0), new("dong_tho", 12, 20, 0), new("bac_tho", 16, 55, 12),
            new("thach_anh", 14, 35, 12), new("quang_hang", 8, 45, 12), new("ngoc_luc_bao", 4, 70, 24),
        };
        private static readonly OreChoice[] Far =
        {
            new("sat_tho", 10, 40, 0), new("bac_tho", 16, 55, 12), new("vang_tho", 16, 45, 24), new("pha_le", 12, 55, 24),
            new("hong_ngoc", 8, 65, 24), new("ngoc_luc_bao", 8, 70, 24), new("kim_cuong", 4, 90, 42), new("da_hac_dien", 5, 100, 42),
        };

        public const float NearRadius = 4500f;
        public const float FarRadius = 9000f;

        // Cac bai quang da sinh (cho ban do ve cham - chi nhung bai nguoi choi da di qua gan).
        public static readonly Dictionary<Vector2I, (Vector3 pos, string ore)> Known = new();

        private static float ChanceFor(Vector3 center)
        {
            var region = WorldStreamer.FindRegion(center);
            return region?.Name switch
            {
                "Núi" or "Hang Động" => 0.45f,
                "Phế Tích" or "Nghĩa Địa" or "Rừng" or "Đầm Lầy" => 0.28f,
                "Làng" or "Thành Phố" or "Đồng Ruộng" => 0.08f,
                _ => 0.18f,
            };
        }

        public static void Generate(Node3D chunkRoot, Vector3 center, int cx, int cz, ulong seed, float chunkSize)
        {
            // RNG RIENG (khong dung chung rng trang tri cua chunk) - them bai quang khong lam doi cay/da cu.
            var rng = new RandomNumberGenerator { Seed = seed ^ 0x0E5EEDUL };
            if (rng.Randf() >= ChanceFor(center)) return;

            float half = chunkSize / 2f - 70f;
            Vector3 spot = center;
            bool found = false;
            for (int t = 0; t < 6 && !found; t++)
            {
                spot = center + new Vector3(rng.RandfRange(-half, half), 0, rng.RandfRange(-half, half));
                found = !WorldStreamer.IsExcluded(spot);
            }
            if (!found) return;

            float dist = new Vector2(center.X, center.Z).Length();
            var table = dist < NearRadius ? Near : dist < FarRadius ? Mid : Far;
            int total = 0;
            foreach (var o in table) total += o.Weight;
            OreChoice Pick()
            {
                int r = rng.RandiRange(0, total - 1);
                foreach (var o in table) { if (r < o.Weight) return o; r -= o.Weight; }
                return table[0];
            }

            // 1 bai = 1 loai quang chinh + vai khoi phu.
            var main = Pick();
            int count = rng.RandiRange(3, 7);
            var cluster = new Node3D { Name = $"OreField_{cx}_{cz}", Position = spot };
            chunkRoot.AddChild(cluster);
            for (int i = 0; i < count; i++)
            {
                var choice = i < count - 2 ? main : Pick();
                float ang = rng.Randf() * Mathf.Tau, r = rng.RandfRange(20f, 70f);
                var local = new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                var ore = new OreNode
                {
                    Position = local, MaxHp = choice.Hp, OreItemId = choice.Ore, DropAmount = 2, RegrowDays = 3,
                    RequiredPower = choice.Required, PersistKey = $"field_{cx}_{cz}_{i}",
                };
                float h = rng.RandfRange(22f, 30f);
                var visual = OreVisuals.Build(choice.Ore, h, rng);
                ore.AddChild(visual);
                ore.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(h * 0.9f, h, h * 0.9f) }, Position = Vector3.Up * h / 2f });
                cluster.AddChild(ore);
                ore.Init(visual);
            }
            // Da tang lon canh bai quang - nhan ra "co mo" tu xa.
            var big = ModelUtil.Instance(OreVisuals.BigRocks[rng.RandiRange(0, OreVisuals.BigRocks.Length - 1)]);
            if (big != null)
            {
                ModelUtil.FitToHeight(big, rng.RandfRange(40f, 60f));
                ModelUtil.Recolor(big, new Color(0.5f, 0.49f, 0.47f)); // da tang Quaternius goc nau -> xam da
                var holder = new Node3D { Position = new Vector3(rng.RandfRange(-30f, 30f), 0, -95f), RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0) };
                holder.AddChild(big);
                cluster.AddChild(holder);
                var body = new StaticBody3D { Position = holder.Position };
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 22f, Height = 40f }, Position = Vector3.Up * 20f });
                cluster.AddChild(body);
            }
            Known[new Vector2I(cx, cz)] = (spot, main.Ore);
        }
    }
}
