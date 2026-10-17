using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HiepSiVeVuon.Core
{
    // Ngoai hinh nhan vat nguoi choi (tu tao o CharacterCreatorUI, luu tren server /api/character,
    // nguoi khac nhin thay qua gateway). Ma ngoai hinh gon: g=gioi tinh, m=mau, s/h/o = mau da/toc/
    // trang phuc (hex) - cung dinh dang voi truong "app" gateway gui.
    public class Appearance
    {
        public string Gender = "male";
        public string ModelId = "Farmer";
        public string SkinColor = "#e8b894";
        public string HairColor = "#3b2414";
        public string OutfitColor = "#4a78c0";
        // Trang phuc/bo giap dang hien thi (id vat pham, xem OutfitCatalog) - null = do mac dinh cua model.
        public string Outfit;
    }

    public class CharacterProfile
    {
        public int PlayerId;
        public string DisplayName;
        public Appearance Look = new();

        // Nhan vat cua nguoi choi dang dang nhap (nap o man hinh dang nhap/tao nhan vat).
        public static CharacterProfile Current;

        public static CharacterProfile FromJson(System.Text.Json.JsonElement e) => new()
        {
            PlayerId = e.GetProperty("playerId").GetInt32(),
            DisplayName = e.GetProperty("displayName").GetString(),
            Look = new Appearance
            {
                Gender = e.GetProperty("gender").GetString(),
                ModelId = e.GetProperty("modelId").GetString(),
                SkinColor = e.GetProperty("skinColor").GetString(),
                HairColor = e.GetProperty("hairColor").GetString(),
                OutfitColor = e.GetProperty("outfitColor").GetString(),
            },
        };
    }

    public static class CharacterCatalog
    {
        public class ModelDef
        {
            public string Id;
            public string Gender;
            public string Path;          // model goc: cung cap BO XUONG + HOAT CANH (+ dau neu KeepBaseHead)
            public float Scale;          // tuong duong Player.ModelScale cua Farmer (22) - cao xap xi nhau
            public string[] OutfitMaterials;
            public string NameVi, NameEn;
            // Ghep tu nhieu file (cung bo xuong 62 xuong cua Farmer, da bake san - tools/outfits):
            // Parts = (file, ten cac luoi). KeepBaseHead = giu dau cua model goc, bo than goc.
            public (string path, string[] meshes)[] Parts;
            public bool KeepBaseHead;
            public string[] ExtraHair = System.Array.Empty<string>();
            // Kieu cu (hien dai) - khong con cho chon khi tao nhan vat, chi de hien dung nhan vat da tao truoc day.
            public bool Legacy;
        }

        private const string Ch = "res://assets3d/quaternius/characters/";
        private const string Ou = "res://assets3d/outfits/";
        private static readonly string[] MedievalBody = { "Medieval_Body", "Medieval_Legs", "Medieval_Feet" };
        private static readonly string[] KnightBody = { "King_Body", "King_Legs", "King_Feet" };

        // BOI CANH TRUNG CO: chi cac kieu ao quan thoi Trung co (nong dan, tham hiem, lu khach, hiep si,
        // lanh chua...). Tat ca dung CHUNG bo xuong + hoat canh Farmer -> giap, trang phuc, vat cam tay
        // va dong tac vung tay deu khop. PHAI khop danh sach MODELS ben backend (services/characterService.js).
        public static readonly ModelDef[] Models =
        {
            // ---- Nam ----
            new() { Id = "Farmer", Gender = "male", Path = Ch + "Farmer.gltf", Scale = 22f, OutfitMaterials = new[] { "LightBlue" }, NameVi = "Nông dân", NameEn = "Farmer" },
            new() { Id = "Adventurer", Gender = "male", Path = Ch + "Adventurer.gltf", Scale = 22f, OutfitMaterials = new[] { "Green" }, NameVi = "Nhà thám hiểm", NameEn = "Adventurer" },
            new() { Id = "Ranger", Gender = "male", Path = Ch + "Casual_2.gltf", Scale = 22f, KeepBaseHead = true, Parts = new[] { (Ou + "hooded_parts.glb", MedievalBody) },
                OutfitMaterials = new[] { "LightBrown" }, NameVi = "Lữ khách", NameEn = "Wanderer" },
            new() { Id = "Knight", Gender = "male", Path = Ch + "Casual_2.gltf", Scale = 22f, KeepBaseHead = true, Parts = new[] { (Ou + "knight_m.glb", KnightBody) },
                OutfitMaterials = new[] { "Blue" }, NameVi = "Hiệp sĩ", NameEn = "Knight" },
            new() { Id = "Lord", Gender = "male", Path = Ch + "Farmer.gltf", Scale = 22f, Parts = new[] { (Ou + "king_parts.glb", (string[])null) },
                OutfitMaterials = new[] { "Blue" }, NameVi = "Lãnh chúa", NameEn = "Lord" },
            // ---- Nu (cung bo xuong nam, ti le hoi nho hon) ----
            new() { Id = "HuntressF", Gender = "female", Path = Ch + "Farmer.gltf", Scale = 21f, Parts = new[] { (Ou + "adventurerf_parts.glb", (string[])null) },
                OutfitMaterials = new[] { "Green" }, NameVi = "Nữ thám hiểm", NameEn = "Huntress" },
            new() { Id = "HoodedF", Gender = "female", Path = Ch + "Farmer.gltf", Scale = 21f, Parts = new[] { (Ou + "hooded_parts.glb", (string[])null) },
                OutfitMaterials = new[] { "LightBrown" }, ExtraHair = new[] { "White" }, NameVi = "Nữ lữ khách", NameEn = "Hooded wanderer" },
            new() { Id = "PeasantF", Gender = "female", Path = Ch + "Farmer.gltf", Scale = 21f,
                Parts = new[] { (Ou + "hooded_parts.glb", MedievalBody), (Ou + "adventurerf_parts.glb", new[] { "Adventurer_Head" }) },
                OutfitMaterials = new[] { "LightBrown" }, NameVi = "Thôn nữ", NameEn = "Village girl" },
            new() { Id = "KnightF", Gender = "female", Path = Ch + "Farmer.gltf", Scale = 21f,
                Parts = new[] { (Ou + "knight_m.glb", KnightBody), (Ou + "adventurerf_parts.glb", new[] { "Adventurer_Head" }) },
                OutfitMaterials = new[] { "Blue" }, NameVi = "Nữ hiệp sĩ", NameEn = "Lady knight" },
            // ---- Kieu cu (hien dai) - giu de nhan vat tao truoc day van hien dung ----
            new() { Id = "Worker", Gender = "male", Legacy = true, Path = Ch + "Worker.gltf", Scale = 22f, OutfitMaterials = new[] { "Worker_Vest", "Worker_Yellow" }, NameVi = "Thợ", NameEn = "Worker" },
            new() { Id = "Casual_2", Gender = "male", Legacy = true, Path = Ch + "Casual_2.gltf", Scale = 22f, OutfitMaterials = new[] { "Red_Dark" }, NameVi = "Thường ngày", NameEn = "Casual" },
            new() { Id = "Suit", Gender = "male", Legacy = true, Path = Ch + "Suit.gltf", Scale = 22f, OutfitMaterials = new[] { "Suit" }, NameVi = "Lịch lãm", NameEn = "Suit" },
            new() { Id = "Swat", Gender = "male", Legacy = true, Path = Ch + "Swat.gltf", Scale = 23f, OutfitMaterials = new[] { "Swat" }, NameVi = "Hiệp sĩ giáp", NameEn = "Armored" },
            // Bo nu cu (Animated Women Pack - rig rieng, armature scale 100 -> ti le 8.0, cao ~37).
            new() { Id = "Woman_Casual", Gender = "female", Legacy = true, Path = Ch + "Woman_Casual.glb", Scale = 8.0f, OutfitMaterials = new[] { "Shirt" }, NameVi = "Thường ngày", NameEn = "Casual" },
            new() { Id = "Woman_Jacket", Gender = "female", Legacy = true, Path = Ch + "Woman_Jacket.glb", Scale = 8.0f, OutfitMaterials = new[] { "Jacket", "LightJacket" }, NameVi = "Áo khoác", NameEn = "Jacket" },
            new() { Id = "Woman_TankTop", Gender = "female", Legacy = true, Path = Ch + "Woman_TankTop.glb", Scale = 8.0f, OutfitMaterials = new[] { "Shirt" }, NameVi = "Năng động", NameEn = "Sporty" },
            new() { Id = "Woman_Dress", Gender = "female", Legacy = true, Path = Ch + "Woman_Dress.glb", Scale = 8.0f, OutfitMaterials = new[] { "Dress" }, NameVi = "Váy", NameEn = "Dress" },
        };

        // Vu khi/do vat hien dai di kem model goc (sung luc cua Suit/Swat, kiem lien than cua Hooded) - bo di.
        private static readonly HashSet<string> StripMeshes = new() { "Pistol", "Sword" };
        private static readonly Dictionary<string, PackedScene> PartScenes = new();

        // Gan model nhan vat (ke ca kieu ghep nhieu file) - dung cho nguoi choi, nguoi choi khac, NPC, xem truoc.
        public static AnimationPlayer AttachModel(Node3D parent, ModelDef def, float scaleMult = 1f)
        {
            var ap = CharacterRig.Attach(parent, def.Path, def.Scale * scaleMult);
            var skel = CharacterRig.FindSkeleton(parent);
            if (skel == null) return ap;
            foreach (var mi in AllMeshes(skel).ToList())
                if (StripMeshes.Contains(mi.Name)) { mi.GetParent().RemoveChild(mi); mi.QueueFree(); }
            if (def.Parts == null) return ap;

            var own = skel.GetChildren().OfType<MeshInstance3D>().Where(m => m.Skin != null).ToList();
            var reference = own.Count > 0 ? own[0].Transform : Transform3D.Identity;
            foreach (var m in own)
            {
                if (def.KeepBaseHead && m.Name.ToString().EndsWith("_Head")) continue;
                skel.RemoveChild(m);
                m.QueueFree();
            }
            foreach (var (path, names) in def.Parts)
            {
                if (!PartScenes.TryGetValue(path, out var scene)) PartScenes[path] = scene = GD.Load<PackedScene>(path);
                if (scene == null) continue;
                var src = scene.Instantiate<Node3D>();
                foreach (var mi in AllMeshes(src).ToList())
                {
                    if (mi.Skin == null || (names != null && !names.Contains(mi.Name.ToString())) || StripMeshes.Contains(mi.Name)) continue;
                    mi.GetParent().RemoveChild(mi);
                    mi.Owner = null; // bo owner cua scene goc (tranh canh bao "owner inconsistent")
                    skel.AddChild(mi);
                    mi.Transform = reference;
                    mi.Skeleton = new NodePath("..");
                }
                src.QueueFree();
            }
            return ap;
        }

        private static IEnumerable<MeshInstance3D> AllMeshes(Node n)
        {
            if (n is MeshInstance3D mi && mi.Mesh != null) yield return mi;
            foreach (Node c in n.GetChildren())
                foreach (var x in AllMeshes(c)) yield return x;
        }

        public static ModelDef Get(string id) => Models.FirstOrDefault(m => m.Id == id) ?? Models[0];
        public static IEnumerable<ModelDef> ForGender(string gender) => Models.Where(m => m.Gender == gender && !m.Legacy);

        public static readonly string[] SkinPresets = { "#f6d5bd", "#e8b894", "#c98e64", "#a06a42", "#6e4428", "#4a2c1a" };
        public static readonly string[] HairPresets = { "#1c1410", "#3b2414", "#7a4a22", "#c8963c", "#e6d08a", "#a83226", "#d0d0d0", "#3a5fb0" };
        public static readonly string[] OutfitPresets = { "#4a78c0", "#3c8a4a", "#b03a3a", "#d6a23a", "#7a4ab0", "#e07ab0", "#3a3a3a", "#e8e8e8" };

        private static readonly string[] SkinMats = { "Skin", "Skin_Darker" };
        private static readonly string[] HairMats = { "Hair", "HairBase", "Eyebrows", "Moustache", "Hair_Brown", "Hair_White" };

        // Doi mau tung nhom vat lieu theo ten (nhan ban vat lieu - khong sua vat lieu goc dung chung).
        public static void ApplyColors(Node root, Appearance look)
        {
            var def = Get(look.ModelId);
            var skin = new Color(look.SkinColor);
            var hair = new Color(look.HairColor);
            var outfit = new Color(look.OutfitColor);
            Walk(root, def, skin, hair, outfit);
        }

        // Nhu ApplyColors nhung voi danh sach vat lieu trang phuc rieng (phan trang phuc gan them - xem OutfitRig).
        public static void Paint(Node root, Appearance look, string[] outfitMaterials)
        {
            var def = new ModelDef { OutfitMaterials = outfitMaterials ?? System.Array.Empty<string>() };
            Walk(root, def, new Color(look.SkinColor), new Color(look.HairColor), new Color(look.OutfitColor));
        }

        private static void Walk(Node n, ModelDef def, Color skin, Color hair, Color outfit)
        {
            if (n is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is not StandardMaterial3D mat) continue;
                    string name = mat.ResourceName ?? "";
                    Color? c = SkinMats.Contains(name) ? (name == "Skin_Darker" ? skin.Darkened(0.15f) : skin)
                        : HairMats.Contains(name) || def.ExtraHair.Contains(name) ? (name == "Eyebrows" ? hair.Darkened(0.2f) : hair)
                        : def.OutfitMaterials.Contains(name) ? outfit
                        : null;
                    if (c == null) continue;
                    var dup = (StandardMaterial3D)mat.Duplicate();
                    dup.AlbedoColor = c.Value;
                    mi.SetSurfaceOverrideMaterial(s, dup);
                }
            }
            foreach (Node child in n.GetChildren()) Walk(child, def, skin, hair, outfit);
        }

        // Ten hoat canh theo VAI TRO - bo nam (rig Quaternius nhan vat) va bo nu (HumanArmature,
        // ten "Female_*") dat ten khac nhau. Tra ve ten THAT co trong model, hoac "" neu khong co.
        private static readonly Dictionary<string, string[]> AnimRoles = new()
        {
            ["Idle"] = new[] { "Idle", "Female_Idle" },
            ["Walk"] = new[] { "Walk", "Female_Walk" },
            ["Run"] = new[] { "Run", "Female_Run" },
            ["Sword_Slash"] = new[] { "Sword_Slash", "Female_SwordSlash" },
            ["Punch_Right"] = new[] { "Punch_Right", "Female_Punch" },
            ["Punch_Left"] = new[] { "Punch_Left", "Female_Punch" },
            ["Interact"] = new[] { "Interact", "Female_Standing", "Female_Clapping" },
            ["Wave"] = new[] { "Wave", "Female_Clapping" },
            ["Death"] = new[] { "Death", "Female_Death" },
            ["Idle_Sword"] = new[] { "Idle_Sword" },
            ["Roll"] = new[] { "Roll", "Female_RunningJump" },
            ["HitRecieve"] = new[] { "HitRecieve" },
            ["HitRecieve_2"] = new[] { "HitRecieve_2" },
            ["Kick_Right"] = new[] { "Kick_Right", "Female_Punch" },
            ["Kick_Left"] = new[] { "Kick_Left", "Female_Punch" },
        };

        public static Dictionary<string, string> ResolveAnims(AnimationPlayer ap)
        {
            var map = new Dictionary<string, string>();
            if (ap == null) return map;
            var list = ap.GetAnimationList();
            foreach (var (role, names) in AnimRoles)
                foreach (var n in names)
                {
                    var hit = list.FirstOrDefault(a => a == n || a.EndsWith("|" + n));
                    if (hit != null) { map[role] = hit; break; }
                }
            foreach (var role in new[] { "Idle", "Walk", "Run", "Idle_Sword" })
                if (map.TryGetValue(role, out var a) && ap.GetAnimation(a) is Animation anim) anim.LoopMode = Animation.LoopModeEnum.Linear;
            return map;
        }

        // Xuong tay phai de cam vat pham (rig nam: Wrist.R, rig nu: MiddleHand.R).
        public static readonly string[] RightHandBones = { "Wrist.R", "Palm.R", "Hand.R" };
    }
}
