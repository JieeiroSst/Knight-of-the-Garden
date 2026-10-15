using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Core
{
    // Model 3D (CC0) dai dien cho cac vat pham KHONG phai khai khoang - cay trong, hat giong, co vat,
    // chien loi pham trum, do le hoi - de IconRenderer chup icon tu 3D (thay pixel-art ve tay truoc day).
    // Mau doi lai theo DUNG ten vat lieu trong file .glb (vd hanh "brown" -> mau kem cho thanh toi).
    public static class ItemModels
    {
        private const string I = "res://assets3d/items3d/";
        private const string Mon = "res://assets3d/quaternius/monsters/";

        private record Paint(string Material, Color Color, float Glow = 0f);
        private record Def(string Model, Paint[] Paints = null);

        private static readonly Dictionary<string, Def> Defs = new()
        {
            // ---- Cay trong ----
            ["dau_tay"] = new(I + "strawberry.glb"),
            ["toi"] = new(I + "onion.glb", new[] { new Paint("brown", new Color(0.93f, 0.9f, 0.82f)) }),
            ["cai_xoan"] = new(I + "cabbage.glb", new[] { new Paint("green", new Color(0.1f, 0.42f, 0.22f)) }),
            ["dua_hau"] = new(I + "watermelon.glb"),
            ["ngo"] = new(I + "corn.glb"),
            ["viet_quat"] = new(I + "berry_bush.glb", new[] { new Paint("Berry", new Color(0.18f, 0.24f, 0.62f)) }),
            ["ca_tim"] = new(I + "eggplant.glb"),
            ["cu_den"] = new(I + "beet.glb", new[] { new Paint("purpleLight", new Color(0.55f, 0.05f, 0.2f)) }),
            ["cu_cai_dong"] = new(I + "turnip.glb"),
            // ---- Co vat / chien loi pham ----
            ["dong_xu_co"] = new(I + "coin.glb"),
            ["tuong_da_co"] = new(I + "fox_statue.glb", new[] { new Paint("Stone", new Color(0.48f, 0.46f, 0.42f)) }),
            ["mat_na_co"] = new(Mon + "tribal.glb"),
            ["chen_vang_co"] = new(I + "chalice.glb"),
            ["vuong_mien_nam"] = new(I + "crown.glb"),
            ["long_yeti"] = new(Mon + "yeti.glb"),
            ["trung_khung_long"] = new(I + "egg_q.glb", new[] { new Paint("LightYellow", new Color(0.55f, 0.75f, 0.45f)) }),
            ["sung_quy"] = new(Mon + "demon.glb"),
            ["sung_quy_xanh"] = new(Mon + "blue_demon.glb"),
            ["vay_rong"] = new(Mon + "dragon.glb"),
            ["vay_rong_co"] = new(Mon + "dragon_evolved.glb"),
            ["ngoc_linh_hon"] = new("res://assets3d/mining/crystal_q.glb", new[] { new Paint(null, new Color(0.5f, 0.9f, 1f), 0.8f) }),
            // ---- Le hoi / khac ----
            ["trung_le_hoi"] = new(I + "egg_k.glb", new[] { new Paint("brownLight", new Color(0.98f, 0.62f, 0.8f)) }),
            ["qua_dong_chi"] = new(I + "present.glb"),
            ["cup_hoi_cho"] = new(I + "trophy.glb"),
            ["bu_nhin"] = new("res://assets3d/polybygoogle/scarecrow.glb"),
            // ---- Trai cay (model that thay icon ca chua dung tam truoc day) ----
            ["apple"] = new(F + "apple.glb"),
            ["pear"] = new(F + "pear.glb"),
            ["cherry"] = new(F + "cherries.glb"),
            ["grape"] = new(F + "grapes.glb"),
            // ---- Vu khi / giap (ren) ----
            ["sword"] = new(F + "sword_b.glb"),
            ["shield"] = new(F + "shield_round.glb"),
            ["kiem_dong"] = new(F + "sword_a.glb", new[] { new Paint("Steel", new Color(0.78f, 0.45f, 0.24f)), new Paint("LightSteel", new Color(0.88f, 0.55f, 0.32f)), new Paint("DarkSteel", new Color(0.55f, 0.3f, 0.16f)) }),
            ["kiem_sat"] = new(F + "sword_b.glb"),
            ["kiem_bac"] = new(F + "sword_b.glb", new[] { new Paint("Steel", new Color(0.88f, 0.9f, 0.95f)), new Paint("LightSteel", new Color(0.95f, 0.97f, 1f)) }),
            ["kiem_vang"] = new(F + "sword_a.glb", new[] { new Paint("Steel", new Color(0.95f, 0.75f, 0.25f)), new Paint("LightSteel", new Color(1f, 0.85f, 0.35f)), new Paint("DarkSteel", new Color(0.85f, 0.15f, 0.2f)) }),
            ["giap_da"] = new(F + "armor_leather.glb"),
            ["giap_sat"] = new(F + "armor_metal.glb"),
            ["giap_bac"] = new(F + "armor_metal.glb", new[] { new Paint("LightSteel", new Color(0.9f, 0.92f, 0.97f)) }),
            ["giap_vang"] = new(F + "armor_golden.glb"),
            // ---- Dat duoc / may / san pham ----
            ["to_ong"] = new(F + "crate_q.glb"),
            ["cay_giong_tao"] = new(F + "tree_c.glb"),
            ["cay_giong_le"] = new(F + "tree_a.glb"),
            ["cay_giong_anh_dao"] = new(F + "tree_c.glb", new[] { new Paint("Tree_Green", new Color(0.95f, 0.6f, 0.75f)) }),
            ["cay_giong_nho"] = new(F + "grapes.glb"),
            ["may_lam_mut"] = new(F + "jars.glb"),
            ["may_ep_nuoc"] = new(F + "juicer.glb"),
            ["bay_cua"] = new(F + "crate_k.glb"),
            ["ho_ca_canh"] = new(F + "fountain.glb"),
            ["mut_trai_cay"] = new(F + "jar.glb"),
            ["nuoc_ep"] = new(F + "jar.glb", new[] { new Paint(null, new Color(1f, 0.6f, 0.15f), 0.2f) }),
            ["cua"] = new(F + "crab.glb"),
            ["ca_than_xuan"] = new(F + "fish.glb", new[] { new Paint(null, new Color(1f, 0.6f, 0.8f), 0.4f) }),
            ["ca_than_ha"] = new(F + "fish.glb", new[] { new Paint(null, new Color(0.3f, 0.85f, 1f), 0.4f) }),
            ["ca_than_thu"] = new(F + "fish.glb", new[] { new Paint(null, new Color(1f, 0.55f, 0.15f), 0.4f) }),
            ["ca_than_dong"] = new(F + "fish.glb", new[] { new Paint(null, new Color(0.85f, 0.95f, 1f), 0.5f) }),
        };

        private const string F = "res://assets3d/features/";

        static ItemModels()
        {
            // Do trang tri + thu cung: icon = chinh model dat trong the gioi.
            foreach (var (id, (model, _)) in DecorCatalog.Models) Defs[id] = new Def(model);
            foreach (var (id, pet) in PetCatalog.Pets) Defs[id] = new Def(pet.Model);
        }

        // Hat giong = tui hat (Kenney "Bag") + cay tuong ung nho ben canh.
        private static readonly HashSet<string> SeedCrops = new() { "dau_tay", "toi", "cai_xoan", "dua_hau", "ngo", "viet_quat", "ca_tim", "cu_den", "cu_cai_dong" };

        public static bool Has(string itemId) =>
            OutfitCatalog.Get(itemId) != null || Defs.ContainsKey(itemId) || (itemId.EndsWith("_seed") && SeedCrops.Contains(itemId[..^5]));

        // Model cao ~1 don vi, tam o goc toa do (IconRenderer dat camera theo kich thuoc nay).
        public static Node3D Build(string itemId, float height = 1f)
        {
            // Trang phuc / bo giap ca bo: icon = nhan vat mau dang mac (thay vi chi 1 manh giap).
            if (OutfitCatalog.Get(itemId) != null) return OutfitRig.BuildMannequin(itemId, "male", height);
            if (itemId.EndsWith("_seed") && SeedCrops.Contains(itemId[..^5]))
            {
                var root = new Node3D();
                if (Load(I + "bag.glb", null, height * 0.85f) is Node3D bag)
                {
                    bag.Position = new Vector3(-0.18f * height, 0, 0);
                    root.AddChild(bag);
                }
                if (Build(itemId[..^5], height * 0.5f) is Node3D crop)
                {
                    crop.Position = new Vector3(0.3f * height, 0, 0.2f * height);
                    root.AddChild(crop);
                }
                return root;
            }
            if (!Defs.TryGetValue(itemId, out var def)) return null;
            var model = Load(def.Model, def.Paints, height);
            if (model == null) return null;
            var holder = new Node3D();
            holder.AddChild(model);
            return holder;
        }

        private static Node3D Load(string path, Paint[] paints, float height)
        {
            var m = ModelUtil.Instance(path);
            if (m == null) return null;
            ModelUtil.FitToHeight(m, height);
            if (paints != null)
                foreach (var p in paints) ModelUtil.Recolor(m, p.Color, p.Material, p.Glow);
            var holder = new Node3D();
            holder.AddChild(m);
            return holder;
        }
    }
}
