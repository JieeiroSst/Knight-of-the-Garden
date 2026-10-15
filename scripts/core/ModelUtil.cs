using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Core
{
    // Tien ich dung chung cho model 3D tai ve (CC0): nap + cache PackedScene, dua ve DUNG chieu cao
    // mong muon (cac goi model co don vi rat khac nhau: 0.15 -> 12 don vi), nhuom mau, phat sang.
    public static class ModelUtil
    {
        private static readonly Dictionary<string, PackedScene> _cache = new();

        public static Node3D Instance(string path)
        {
            if (!_cache.TryGetValue(path, out var scene))
            {
                scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
                _cache[path] = scene;
            }
            return scene?.Instantiate<Node3D>();
        }

        // Hop bao (AABB) cua toan bo luoi trong he toa do cua root (khong can node nam trong tree).
        public static Aabb LocalAabb(Node3D root)
        {
            Aabb? box = null;
            void Walk(Node n, Transform3D xf)
            {
                if (n is VisualInstance3D vi && vi is GeometryInstance3D)
                {
                    var b = xf * vi.GetAabb();
                    box = box.HasValue ? box.Value.Merge(b) : b;
                }
                foreach (Node c in n.GetChildren())
                    Walk(c, c is Node3D c3 ? xf * c3.Transform : xf);
            }
            foreach (Node c in root.GetChildren()) Walk(c, c is Node3D c3 ? c3.Transform : Transform3D.Identity);
            return box ?? new Aabb(Vector3.Zero, Vector3.One);
        }

        // Scale deu de cao dung targetHeight va dat day model sat mat dat (y = 0).
        public static Node3D FitToHeight(Node3D model, float targetHeight)
        {
            var box = LocalAabb(model);
            float h = Mathf.Max(0.001f, box.Size.Y);
            float s = targetHeight / h;
            model.Scale = Vector3.One * s;
            model.Position += new Vector3(-(box.Position.X + box.Size.X / 2f) * s, -box.Position.Y * s, -(box.Position.Z + box.Size.Z / 2f) * s);
            return model;
        }

        // Nhuom: nhan AlbedoColor (giu chi tiet texture atlas) - nhan ban vat lieu, khong sua ban goc.
        public static void Tint(Node root, Color color, float emission = 0f)
        {
            if (root is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is not StandardMaterial3D baseMat) continue;
                    var dup = (StandardMaterial3D)baseMat.Duplicate();
                    dup.AlbedoColor = color;
                    if (emission > 0f)
                    {
                        dup.EmissionEnabled = true;
                        dup.Emission = color;
                        dup.EmissionEnergyMultiplier = emission;
                    }
                    mi.SetSurfaceOverrideMaterial(s, dup);
                }
            }
            foreach (Node c in root.GetChildren()) Tint(c, color, emission);
        }

        // Doi HAN mau (bo texture) cho 1 vat lieu theo ten (null = moi vat lieu). Dung cho model low-poly
        // to mau phang (atlas/mau don): cho ket qua DUNG mau mong muon, khac Tint (nhan voi mau goc - vd
        // nhan tinh the tim khong bao gio ra duoc mau trang).
        public static void Recolor(Node root, Color color, string material = null, float emission = 0f, float metallic = -1f)
        {
            if (root is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is not StandardMaterial3D baseMat) continue;
                    if (material != null && baseMat.ResourceName != material) continue;
                    var dup = (StandardMaterial3D)baseMat.Duplicate();
                    dup.AlbedoTexture = null;
                    dup.AlbedoColor = color;
                    if (metallic >= 0f) { dup.Metallic = metallic; dup.Roughness = 0.35f; }
                    if (emission > 0f)
                    {
                        dup.EmissionEnabled = true;
                        dup.EmissionTexture = null;
                        dup.Emission = color;
                        dup.EmissionEnergyMultiplier = emission;
                    }
                    mi.SetSurfaceOverrideMaterial(s, dup);
                }
            }
            foreach (Node c in root.GetChildren()) Recolor(c, color, material, emission, metallic);
        }

        // Chi phat sang (giu mau goc) - dung cho tinh the/da quy co mau san.
        public static void Glow(Node root, float energy)
        {
            if (root is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is not StandardMaterial3D baseMat) continue;
                    var dup = (StandardMaterial3D)baseMat.Duplicate();
                    dup.EmissionEnabled = true;
                    dup.EmissionTexture = dup.AlbedoTexture;
                    dup.Emission = Colors.White;
                    dup.EmissionEnergyMultiplier = energy;
                    mi.SetSurfaceOverrideMaterial(s, dup);
                }
            }
            foreach (Node c in root.GetChildren()) Glow(c, energy);
        }
    }

    // Hinh 3D cho tung loai quang (model CC0 trong assets3d/mining) - dung CHUNG cho khoi quang trong
    // the gioi (OreNode) va de VE ICON vat pham tu model 3D (IconRenderer).
    public static class OreVisuals
    {
        private const string M = "res://assets3d/mining/";
        public static readonly string[] Rocks = { M + "rock_a.glb", M + "rock_d.glb", M + "rock_e.glb", M + "rock_b.glb" };
        public static readonly string[] BigRocks = { M + "rock_large_a.glb", M + "rock_large_b.glb", M + "rock_medium.glb", M + "rocks_cluster.glb" };

        // Mau lay tu DU LIEU THAT cua model (lay mau texture theo UV, xem ghi chu): da Quaternius =
        // be-xam (158,149,127); gold_ore co van vang that (230,169,69); gold_rocks = 2 vat lieu "Stone" +
        // "Gold" (khong texture); mineral = "Grey" + "Pink"; crystal = tim (177,65,255); gem xanh/luc/hong.
        // Paint: (ten vat lieu hoac null = ca model, mau dich, phat sang, kim loai) - CHI doi dung phan can
        // doi (vd van quang), da nen giu nguyen mau goc.
        private record Paint(string Material, Color Color, float Glow = 0f, float Metallic = -1f);
        private record Look(string Model, Paint[] Paints, string Topper = null, Paint TopperPaint = null);

        private static readonly Color RockGray = new(0.55f, 0.54f, 0.52f);

        private static Look LookFor(string oreId) => oreId switch
        {
            "da" => new Look(M + "rock_a.glb", new[] { new Paint(null, RockGray) }),
            "than_da" => new Look(M + "rock_d.glb", new[] { new Paint(null, new Color(0.13f, 0.12f, 0.12f)) }),
            "dong_tho" => new Look(M + "gold_rocks_b.glb", new[] { new Paint("Gold", new Color(0.78f, 0.42f, 0.22f), 0f, 0.6f) }),
            "sat_tho" => new Look(M + "gold_rocks_a.glb", new[] { new Paint("Gold", new Color(0.55f, 0.3f, 0.22f), 0f, 0.4f) }),
            "bac_tho" => new Look(M + "gold_rocks_a.glb", new[] { new Paint("Gold", new Color(0.86f, 0.88f, 0.93f), 0.05f, 0.9f) }),
            "vang_tho" => new Look(M + "gold_ore.glb", new Paint[0]),
            "quang_nui" => new Look(M + "mineral.glb", new[] { new Paint("Grey", new Color(0.32f, 0.31f, 0.3f)), new Paint("Pink", new Color(0.45f, 0.52f, 0.62f)) }),
            "quang_hang" => new Look(M + "mineral.glb", new[] { new Paint("Grey", new Color(0.25f, 0.24f, 0.26f)), new Paint("Pink", new Color(0.7f, 0.2f, 0.6f), 0.3f) }),
            "thach_anh" => new Look(M + "crystal_q.glb", new[] { new Paint(null, new Color(0.9f, 0.94f, 1f), 0.35f) }),
            "pha_le" => new Look(M + "crystal_big.glb", new[] { new Paint(null, new Color(0.69f, 0.25f, 1f), 0.6f) }),
            "da_hac_dien" => new Look(M + "crystal_i2.glb", new[] { new Paint(null, new Color(0.12f, 0.06f, 0.18f), 0.25f, 0.2f) }),
            "kim_cuong" => new Look(M + "rock_e.glb", new[] { new Paint(null, RockGray) }, M + "gem_blue.glb", new Paint(null, new Color(0.85f, 0.97f, 1f), 0.6f)),
            "ngoc_luc_bao" => new Look(M + "rock_e.glb", new[] { new Paint(null, RockGray) }, M + "gem_green.glb", new Paint(null, new Color(0.08f, 0.72f, 0.38f), 0.5f)),
            "hong_ngoc" => new Look(M + "rock_e.glb", new[] { new Paint(null, RockGray) }, M + "gem_pink.glb", new Paint(null, new Color(0.85f, 0.06f, 0.16f), 0.5f)),
            "da_quy" => new Look(M + "rock_a.glb", new[] { new Paint(null, RockGray) }, M + "gem_pink.glb", new Paint(null, new Color(0.6f, 0.25f, 0.9f), 0.5f)),
            "co_vat" => new Look(M + "rock_a.glb", new[] { new Paint(null, new Color(0.62f, 0.55f, 0.42f)) }, M + "gold_ingots.glb", null),
            _ => new Look(M + "rock_a.glb", new[] { new Paint(null, RockGray) }),
        };

        private static void ApplyPaints(Node3D model, Paint[] paints)
        {
            if (paints == null) return;
            foreach (var p in paints) ModelUtil.Recolor(model, p.Color, p.Material, p.Glow, p.Metallic);
        }

        // Khoi quang trong the gioi: cao ~height don vi (nhan vat ~40), xoay ngau nhien.
        public static Node3D Build(string oreId, float height, RandomNumberGenerator rng)
        {
            var look = LookFor(oreId);
            var root = new Node3D();
            var body = ModelUtil.Instance(look.Model) ?? ModelUtil.Instance(M + "rock_a.glb");
            if (body == null) return root;
            ModelUtil.FitToHeight(body, look.Topper != null ? height * 0.7f : height);
            ApplyPaints(body, look.Paints);
            var spin = new Node3D { RotationDegrees = new Vector3(0, rng?.RandfRange(0, 360) ?? 0, 0) };
            spin.AddChild(body);
            root.AddChild(spin);
            if (look.Topper != null && ModelUtil.Instance(look.Topper) is Node3D gem)
            {
                ModelUtil.FitToHeight(gem, height * 0.55f);
                if (look.TopperPaint != null) ApplyPaints(gem, new[] { look.TopperPaint });
                var holder = new Node3D { Position = new Vector3(0, height * 0.5f, 0), RotationDegrees = new Vector3(rng?.RandfRange(-15, 15) ?? 0, rng?.RandfRange(0, 360) ?? 0, 0) };
                holder.AddChild(gem);
                root.AddChild(holder);
            }
            return root;
        }

        // Model dai dien cho ICON vat pham khai khoang (null = khong co, dung icon PNG san co).
        public static Node3D BuildIconModel(string itemId)
        {
            switch (itemId)
            {
                case "thoi_dong": return Tinted(M + "gold_ingots.glb", new Color(0.78f, 0.42f, 0.22f));
                case "thoi_sat": return Tinted(M + "gold_ingots.glb", new Color(0.42f, 0.43f, 0.46f));
                case "thoi_bac": return Tinted(M + "gold_ingots.glb", new Color(0.86f, 0.88f, 0.93f));
                case "thoi_vang": return Tinted(M + "gold_ingots.glb", null);
                case "pickaxe_kim_cuong": return Tinted(M + "pickaxe_diamond.glb", null);
                case "pickaxe_vang": return Tinted(M + "pickaxe_gold.glb", null);
                case "than_da": case "dong_tho": case "sat_tho": case "bac_tho": case "vang_tho":
                case "quang_nui": case "quang_hang": case "thach_anh": case "pha_le": case "da_hac_dien":
                case "kim_cuong": case "ngoc_luc_bao": case "hong_ngoc": case "da_quy": case "da":
                    return Build(itemId == "da" ? "_" : itemId, 1f, null);
                default: return null;
            }
        }

        private static Node3D Tinted(string path, Color? tint)
        {
            var m = ModelUtil.Instance(path);
            if (m == null) return null;
            ModelUtil.FitToHeight(m, 1f);
            if (tint is Color c) ModelUtil.Recolor(m, c, null, 0f, 0.8f);
            var root = new Node3D();
            root.AddChild(m);
            return root;
        }

        public static bool HasIconModel(string itemId) => itemId is "thoi_dong" or "thoi_sat" or "thoi_bac" or "thoi_vang"
            or "pickaxe_kim_cuong" or "pickaxe_vang" or "than_da" or "dong_tho" or "sat_tho" or "bac_tho" or "vang_tho"
            or "quang_nui" or "quang_hang" or "thach_anh" or "pha_le" or "da_hac_dien" or "kim_cuong" or "ngoc_luc_bao"
            or "hong_ngoc" or "da_quy" or "da";
    }
}
