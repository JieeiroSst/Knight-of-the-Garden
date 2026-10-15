using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HiepSiVeVuon.Core
{
    // Trang phuc nhin thay tren nhan vat - 2 loai:
    //  - "farm": trang phuc nong trai (vat pham loai Outfit, may o Ban May tu Vai Det + thuoc nhuom).
    //    Nam: lay bo than/quan/giay cua 1 model Quaternius khac (CUNG bo xuong 62 xuong) gan vao xuong
    //    cua nguoi choi, giu nguyen dau/mat/toc nguoi choi da tao. Nu: thay ca luoi Female cung rig.
    //  - "armor": bo giap Trung co chau Au CA BO (vat pham loai Armor, ren o Lo Ren): giap than + tay
    //    + quan + ung (tu model King - Quaternius, CC0) + mu tru (Helmet - CreativeTechLab, CC-BY 3.0),
    //    da duoc chuyen sang dung bo xuong nam (Farmer) / nu (Woman) - xem assets3d/outfits/README.md.
    //    Moi bo doi mau kim loai/ao choang theo dung TEN vat lieu trong file .glb.
    public static class OutfitCatalog
    {
        public record Paint(string Material, Color Color, float Metallic = 0f, float Glow = 0f);

        public class OutfitDef
        {
            public string ItemId;
            public string Category;            // "farm" | "armor"
            public string MaleSource;          // file .gltf/.glb co luoi gan vao rig nam
            public string FemaleSource;        // file .glb co luoi gan vao rig nu
            public string[] OutfitMaterials = System.Array.Empty<string>(); // nhuom theo mau trang phuc nguoi choi
            public Paint[] Paints = System.Array.Empty<Paint>();
        }

        private const string C = "res://assets3d/quaternius/characters/";
        private const string O = "res://assets3d/outfits/";

        private static Paint[] Armor(Color metal, Color metalDark, Color cloth, Color trim, Color pants, Color helm, Color crest, float metallic = 0.75f, float glow = 0f) => new[]
        {
            new Paint("Metal", metal, metallic, glow), new Paint("Metal_Dark", metalDark, metallic),
            new Paint("Blue", cloth), new Paint("Beige", trim), new Paint("DarkBrown", pants),
            new Paint("Helmet_iron", helm, metallic, glow), new Paint("Helmet_redPaint", crest),
        };

        public static readonly OutfitDef[] All =
        {
            // ---- Trang phuc nong trai ----
            new() { ItemId = "do_yem_nong_dan", Category = "farm", MaleSource = C + "Farmer.gltf", FemaleSource = C + "Woman_Casual.glb", OutfitMaterials = new[] { "LightBlue", "Shirt" } },
            new() { ItemId = "do_tho_vuon", Category = "farm", MaleSource = O + "hooded_parts.glb", FemaleSource = C + "Woman_Jacket.glb", OutfitMaterials = new[] { "LightBrown", "Jacket" } },
            new() { ItemId = "do_tham_hiem", Category = "farm", MaleSource = C + "Adventurer.gltf", FemaleSource = C + "Woman_TankTop.glb", OutfitMaterials = new[] { "Green", "Shirt" } },
            new() { ItemId = "do_thuong_ngay", Category = "farm", MaleSource = O + "adventurerf_parts.glb", FemaleSource = C + "Woman_Casual.glb", OutfitMaterials = new[] { "Green", "Pants" } },
            // Le phuc di hoi: ao choang qui toc (than Lanh chua) nhuom do tham - vien vang.
            new() { ItemId = "do_le_hoi", Category = "farm", MaleSource = O + "king_parts.glb", FemaleSource = C + "Woman_Dress.glb", OutfitMaterials = new[] { "Dress" },
                Paints = new[] { new Paint("Blue", new Color(0.62f, 0.08f, 0.14f)), new Paint("Metal", new Color(0.9f, 0.72f, 0.3f), 0.7f), new Paint("Metal_Dark", new Color(0.55f, 0.4f, 0.15f), 0.6f), new Paint("Beige", new Color(0.95f, 0.88f, 0.65f)) } },
            // ---- Bo giap Trung co (giap_* da co tu truoc + 2 bo moi) ----
            new() { ItemId = "giap_da", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.42f, 0.27f, 0.15f), new Color(0.28f, 0.17f, 0.09f), new Color(0.22f, 0.36f, 0.2f), new Color(0.7f, 0.58f, 0.38f), new Color(0.3f, 0.22f, 0.14f), new Color(0.45f, 0.3f, 0.17f), new Color(0.2f, 0.32f, 0.18f), 0.05f) },
            new() { ItemId = "giap_sat", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.46f, 0.47f, 0.5f), new Color(0.24f, 0.25f, 0.27f), new Color(0.42f, 0.3f, 0.2f), new Color(0.55f, 0.45f, 0.3f), new Color(0.25f, 0.2f, 0.15f), new Color(0.45f, 0.46f, 0.5f), new Color(0.35f, 0.22f, 0.12f)) },
            new() { ItemId = "giap_bac", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.82f, 0.85f, 0.9f), new Color(0.5f, 0.53f, 0.58f), new Color(0.12f, 0.22f, 0.55f), new Color(0.85f, 0.75f, 0.4f), new Color(0.18f, 0.18f, 0.22f), new Color(0.84f, 0.86f, 0.92f), new Color(0.12f, 0.22f, 0.6f)) },
            new() { ItemId = "giap_thap_tu", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.7f, 0.72f, 0.76f), new Color(0.42f, 0.44f, 0.48f), new Color(0.93f, 0.92f, 0.88f), new Color(0.72f, 0.08f, 0.08f), new Color(0.3f, 0.26f, 0.22f), new Color(0.74f, 0.76f, 0.8f), new Color(0.75f, 0.06f, 0.06f)) },
            new() { ItemId = "giap_vang", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.95f, 0.74f, 0.28f), new Color(0.6f, 0.42f, 0.14f), new Color(0.6f, 0.06f, 0.1f), new Color(0.98f, 0.9f, 0.6f), new Color(0.25f, 0.08f, 0.08f), new Color(0.96f, 0.76f, 0.3f), new Color(0.95f, 0.95f, 0.95f), 0.85f, 0.08f) },
            new() { ItemId = "giap_hac_ky_si", Category = "armor", MaleSource = O + "knight_m.glb", FemaleSource = O + "knight_f.glb",
                Paints = Armor(new Color(0.12f, 0.12f, 0.15f), new Color(0.05f, 0.05f, 0.07f), new Color(0.22f, 0.06f, 0.28f), new Color(0.55f, 0.12f, 0.12f), new Color(0.08f, 0.07f, 0.1f), new Color(0.13f, 0.13f, 0.16f), new Color(0.6f, 0.05f, 0.05f), 0.85f) },
        };

        private static readonly Dictionary<string, OutfitDef> ById = All.ToDictionary(d => d.ItemId);
        public static OutfitDef Get(string itemId) => itemId != null && ById.TryGetValue(itemId, out var d) ? d : null;
        public static IEnumerable<OutfitDef> InCategory(string cat) => All.Where(d => d.Category == cat);
    }

    // Mac/coi trang phuc tren 1 model nhan vat da gan (CharacterRig.Attach) - dung chung cho nguoi
    // choi, nguoi choi khac (RemotePlayer), xem truoc trong Tu Do va icon vat pham.
    public class OutfitRig
    {
        private readonly Node3D _root;
        private readonly bool _female; // rig nu cu (Animated Women) - nhan vat nu Trung co dung rig nam
        private readonly List<Node> _parts = new();
        private readonly List<MeshInstance3D> _hidden = new();
        public string Current { get; private set; }

        // Mu cua dau nhan vat nam (nam trong luoi *_Head) - an di khi doi mu tru.
        // (mu Farmer, mu bao ho cu, vuong mien Lanh chua, mu trum cua Lu khach).
        private static readonly HashSet<string> HatMaterials = new() { "Beige", "Red", "Worker_Yellow", "Gold", "DarkBrown" };
        // Nu mac giap: chi giu da/mat/toc cua luoi Female, an quan ao ben duoi.
        private static readonly HashSet<string> FemaleKeepUnderArmor = new() { "Skin", "Eyes", "Hair", "HairBase" };
        private static StandardMaterial3D _invisible;
        private static readonly Dictionary<string, PackedScene> Scenes = new();

        public OutfitRig(Node3D modelRoot, string gender)
        {
            _root = modelRoot;
            // Xet theo BO XUONG, khong theo gioi tinh: nhan vat nu Trung co (Nu tham hiem, Thon nu...)
            // dung chung rig 62 xuong voi nam -> mac giap/trang phuc cua rig nam.
            var sk = CharacterRig.FindSkeleton(modelRoot);
            _female = sk != null ? sk.FindBone("Wrist.R") < 0 : gender == "female";
        }

        public void Apply(Appearance look, string outfitId)
        {
            var def = OutfitCatalog.Get(outfitId);
            Current = def?.ItemId;
            foreach (var p in _parts) if (GodotObject.IsInstanceValid(p)) { p.GetParent()?.RemoveChild(p); p.QueueFree(); }
            _parts.Clear();
            foreach (var m in _hidden) if (GodotObject.IsInstanceValid(m)) m.Visible = true;
            _hidden.Clear();
            var skeleton = CharacterRig.FindSkeleton(_root);
            // Chi cac luoi skinned NAM TRUC TIEP duoi Skeleton3D (than nguoi) - khong dung vao vat cam tay
            // (HeldItemVisual gan qua BoneAttachment3D) hay phan trang phuc vua go ra.
            var own = skeleton == null ? new List<MeshInstance3D>()
                : skeleton.GetChildren().OfType<MeshInstance3D>().Where(m => m.Mesh != null && m.Skin != null && !_parts.Contains(m)).ToList();
            foreach (var m in own)
                for (int s = 0; s < m.GetSurfaceOverrideMaterialCount(); s++) m.SetSurfaceOverrideMaterial(s, null);
            if (look != null) CharacterCatalog.ApplyColors(_root, look);
            if (def == null || skeleton == null || own.Count == 0) return;

            string path = _female ? def.FemaleSource : def.MaleSource;
            if (!Scenes.TryGetValue(path, out var scene)) Scenes[path] = scene = GD.Load<PackedScene>(path);
            if (scene == null) return;
            var src = scene.Instantiate<Node3D>();
            var reference = (own.FirstOrDefault(m => m.Skin != null) ?? own[0]).Transform;
            bool armor = def.Category == "armor";
            foreach (var mi in Meshes(src).ToList())
            {
                if (!WantPart(mi.Name, armor)) continue;
                mi.GetParent().RemoveChild(mi);
                skeleton.AddChild(mi);
                mi.Transform = reference;
                mi.Skeleton = new NodePath("..");
                _parts.Add(mi);
            }
            src.QueueFree();

            // An phan than cu tuong ung.
            foreach (var m in own)
            {
                string n = m.Name;
                if (_female)
                {
                    if (!armor) { m.Visible = false; _hidden.Add(m); }
                    else HideSurfaces(m, mat => !FemaleKeepUnderArmor.Contains(mat));
                }
                else if (n.EndsWith("_Head"))
                {
                    if (armor) HideSurfaces(m, mat => HatMaterials.Contains(mat));
                }
                else { m.Visible = false; _hidden.Add(m); }
            }

            // To mau: da/toc nguoi choi cho phan moi (tay, mat cua luoi nu...), mau trang phuc / bo giap.
            if (look != null)
                foreach (var p in _parts) CharacterCatalog.Paint(p, look, def.OutfitMaterials);
            foreach (var paint in def.Paints)
                foreach (var p in _parts) ModelUtil.Recolor(p, paint.Color, paint.Material, paint.Glow, paint.Metallic > 0 ? paint.Metallic : -1f);
        }

        private bool WantPart(string name, bool armor)
        {
            if (armor) return true;
            if (_female) return true; // luoi nu (ten "Female" hoac "BaseHuman.000" tuy file)
            return name.EndsWith("_Body") || name.EndsWith("_Legs") || name.EndsWith("_Pants") || name.EndsWith("_Feet") || name == "Backpack";
        }

        private static void HideSurfaces(MeshInstance3D m, System.Func<string, bool> hide)
        {
            _invisible ??= new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0, 0, 0, 0),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false,
            };
            for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++)
            {
                var mat = m.Mesh.SurfaceGetMaterial(s);
                string name = mat?.ResourceName ?? "";
                if (hide(name)) m.SetSurfaceOverrideMaterial(s, _invisible);
            }
        }

        private static IEnumerable<MeshInstance3D> Meshes(Node n)
        {
            if (n is MeshInstance3D mi && mi.Mesh != null) yield return mi;
            foreach (Node c in n.GetChildren())
                foreach (var x in Meshes(c)) yield return x;
        }

        // Nhan vat mau (nam, dau Thuong ngay) mac san trang phuc - dung lam icon vat pham/xem truoc.
        public static Node3D BuildMannequin(string outfitId, string gender = "male", float height = 1f)
        {
            var look = new Appearance { Gender = gender, ModelId = gender == "female" ? "Woman_Casual" : "Casual_2", OutfitColor = "#4a78c0" };
            var def = CharacterCatalog.Get(look.ModelId);
            var holder = new Node3D();
            var ap = CharacterCatalog.AttachModel(holder, def);
            var rig = new OutfitRig(holder, gender);
            rig.Apply(look, outfitId);
            // Dung tu the dung yen (frame dau cua Idle) ngay khi vao cay - tranh tu the "chu T".
            var anims = CharacterCatalog.ResolveAnims(ap);
            if (ap != null && anims.TryGetValue("Idle", out var idle))
                holder.Ready += () => { ap.Play(idle); ap.Seek(0.3, true); ap.Pause(); };
            var wrap = new Node3D();
            wrap.AddChild(holder);
            ModelUtil.FitToHeight(holder, height);
            return wrap;
        }
    }
}
