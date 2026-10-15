using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // To ong tu dat: thung go (model Crate) + vai con ong bay quanh (model Bee). Cu 2 ngay co mat ong;
    // gan hoa/cay an qua (<=300) cho them sap ong. Bam [E] lay mat.
    public partial class PlacedBeehive : FarmFacility
    {
        public PlacementSystem.Entry Entry;
        private readonly System.Collections.Generic.List<Node3D> _bees = new();
        private Label3D _ready;
        public const int Days = 2;

        public override void _Ready()
        {
            AddCollisionBox(new Vector3(26, 30, 26));
            var box = ModelUtil.Instance("res://assets3d/features/crate_q.glb");
            if (box != null) { ModelUtil.FitToHeight(box, 26f); AddChild(box); }
            for (int i = 0; i < 3; i++)
            {
                var bee = ModelUtil.Instance("res://assets3d/features/bee.glb");
                if (bee == null) break;
                ModelUtil.FitToHeight(bee, 5f);
                var holder = new Node3D();
                holder.AddChild(bee);
                AddChild(holder);
                _bees.Add(holder);
            }
            _ready = new Label3D { Text = Loc.T("place.honey_ready"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.2f, FontSize = 30, OutlineSize = 6, Position = Vector3.Up * 42f, Modulate = new Color(1f, 0.85f, 0.3f), NoDepthTest = true };
            AddChild(_ready);
        }

        private bool HoneyReady => PlacementSystem.DaysSince(Entry) >= Days;

        public override void _Process(double delta)
        {
            float t = (float)Time.GetTicksMsec() / 1000f;
            for (int i = 0; i < _bees.Count; i++)
            {
                float a = t * 1.6f + i * 2.1f;
                _bees[i].Position = new Vector3(Mathf.Cos(a) * 22f, 28f + Mathf.Sin(t * 3f + i) * 4f, Mathf.Sin(a) * 22f);
                _bees[i].Rotation = new Vector3(0, -a, 0);
            }
            _ready.Visible = HoneyReady;
        }

        public override void Interact()
        {
            if (!HoneyReady) { Notify.Show(string.Format(Loc.T("place.honey_wait_fmt"), Days - PlacementSystem.DaysSince(Entry))); return; }
            Entry.Day = GameManager.Instance.Day;
            Inventory.Instance.AddItem("honey", 1, "animal");
            bool flowers = false;
            foreach (var e in PlacementSystem.Instance.Entries)
                if (e.Kind == "tree" && new Vector2(e.X - Entry.X, e.Z - Entry.Z).Length() < 300f) { flowers = true; break; }
            if (flowers) Inventory.Instance.AddItem("beeswax", 1, "animal");
            SkillSystem.Instance.AddXp(Data.SkillType.Animal, 6);
            Notify.Show(string.Format(Loc.T("pickup.got_fmt"), ItemDatabase.Instance.GetDisplayName("honey"), 1));
        }
    }
}
