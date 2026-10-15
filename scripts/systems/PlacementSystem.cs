using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Moi thu nguoi choi TU DAT trong the gioi (do trang tri, to ong, cay an qua, may lam
    // mut/ep nuoc, bay cua, ho ca canh) - 1 danh sach chung, luu qua SaveSystem, dung lai khi vao game.
    public partial class PlacementSystem : Node
    {
        public static PlacementSystem Instance { get; private set; }

        public class Entry
        {
            public string Kind { get; set; }      // decor | beehive | tree | jam | juice | crabpot | pond
            public string ItemId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float RotY { get; set; }
            public int Day { get; set; }          // ngay dat / moc thoi gian rieng tung loai
            public int Stage { get; set; }        // vd cay: so ngay da lon; bay cua: da co moi
            public List<string> Data { get; set; } = new(); // vd ho ca: cac loai ca da tha
        }

        public List<Entry> Entries = new();

        public override void _EnterTree() => Instance = this;

        public static string KindOf(string itemId) => itemId switch
        {
            "to_ong" => "beehive",
            "may_lam_mut" => "jam",
            "may_ep_nuoc" => "juice",
            "bay_cua" => "crabpot",
            "ho_ca_canh" => "pond",
            _ when itemId.StartsWith("cay_giong_") => "tree",
            _ when itemId.StartsWith("decor_") => "decor",
            _ => null,
        };

        public void Reset() => Entries.Clear();

        // Dat moi tu tui do (Player/DecorUI goi). Tra ve node da tao hoac null.
        public Node3D Place(string itemId, Vector3 pos, float rotY, Node parent)
        {
            string kind = KindOf(itemId);
            if (kind == null) return null;
            if (kind == "crabpot" && !NearWater(pos)) { Notify.Show(Loc.T("place.need_water")); return null; }
            if (!Inventory.Instance.RemoveItem(itemId, 1)) return null;
            var e = new Entry { Kind = kind, ItemId = itemId, X = pos.X, Y = pos.Y, Z = pos.Z, RotY = rotY, Day = GameManager.Instance.Day };
            Entries.Add(e);
            Notify.Show(string.Format(Loc.T("place.placed_fmt"), ItemDatabase.Instance.GetDisplayName(itemId)));
            return Spawn(e, parent);
        }

        // Nhat lai (chi do trang tri + may moc) -> tra vat pham ve tui.
        public void PickUp(Entry e, Node3D node)
        {
            Entries.Remove(e);
            node.QueueFree();
            Inventory.Instance.AddItem(e.ItemId, 1, e.Kind == "decor" ? "decor" : "misc");
            Notify.Show(string.Format(Loc.T("place.picked_fmt"), ItemDatabase.Instance.GetDisplayName(e.ItemId)));
        }

        public static bool NearWater(Vector3 pos)
        {
            if (WaterEcosystem.Instance.IsNearLake(pos, 160f)) return true;
            var r = WorldStreamer.FindRegion(pos);
            return r != null && (r.Name == "Sông" || r.Name == "Đầm Lầy" || r.Name == "Hồ") && new Vector2(pos.X - r.Center.X, pos.Z - r.Center.Z).Length() < 1000f;
        }

        public void SpawnAll(Node parent)
        {
            foreach (Node n in GetTree().GetNodesInGroup("placed_objects")) n.QueueFree();
            foreach (var e in Entries.ToList()) Spawn(e, parent);
        }

        private Node3D Spawn(Entry e, Node parent)
        {
            Node3D node = e.Kind switch
            {
                "decor" => new DecorPiece { Entry = e },
                "beehive" => new PlacedBeehive { Entry = e },
                "tree" => new PlacedFruitTree { Entry = e },
                "crabpot" => new CrabPot { Entry = e },
                "pond" => new KoiPond { Entry = e },
                "jam" or "juice" => MakeMachine(e),
                _ => null,
            };
            if (node == null) return null;
            node.Position = new Vector3(e.X, e.Y, e.Z);
            node.RotationDegrees = new Vector3(0, e.RotY, 0);
            node.AddToGroup("placed_objects");
            parent.AddChild(node);
            return node;
        }

        private static readonly string[] Fruits = { "apple", "pear", "cherry", "peach", "grape", "dau_tay", "viet_quat", "dua_hau" };
        private static readonly string[] JuiceInputs = { "apple", "pear", "cherry", "peach", "grape", "dau_tay", "viet_quat", "dua_hau", "carrot", "tomato", "cu_den", "cai_xoan" };

        private static Node3D MakeMachine(Entry e)
        {
            bool jam = e.Kind == "jam";
            var m = new ProcessingMachine
            {
                MachineName = ItemDatabase.Instance.GetDisplayName(e.ItemId),
                AcceptIds = jam ? Fruits : JuiceInputs,
                FixedOutputId = jam ? "mut_trai_cay" : "nuoc_ep",
                ProcessDays = 1,
            };
            m.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(28, 30, 28) }, Position = Vector3.Up * 15f });
            var model = ModelUtil.Instance(jam ? "res://assets3d/features/jars.glb" : "res://assets3d/features/juicer.glb");
            if (model != null) { ModelUtil.FitToHeight(model, jam ? 26f : 30f); m.AddChild(model); }
            m.SetMeta("placement_entry", true);
            return m;
        }

        // Ap dung 1 lan moi ngay (goi tu tung entity qua DayChanged).
        public static int DaysSince(Entry e) => GameManager.Instance.Day - e.Day;
    }
}
