using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Cay an qua tu trong (cay giong): model cay CC0 lon dan trong GrowDays ngay, sau do cu RegrowDays
    // ngay lai co qua (model trai cay that treo tren tan). Bam [E] hoac [Space] hai.
    public partial class PlacedFruitTree : FarmFacility
    {
        public PlacementSystem.Entry Entry;
        private Node3D _tree;
        private Node3D _fruits;

        private (string fruit, string tree, string fruitModel, int grow, int regrow, Color? leaf) Spec => Entry.ItemId switch
        {
            "cay_giong_le" => ("pear", "tree_a.glb", "pear.glb", 6, 3, null),
            "cay_giong_anh_dao" => ("cherry", "tree_c.glb", "cherries.glb", 7, 3, new Color(0.95f, 0.62f, 0.75f)),
            "cay_giong_nho" => ("grape", "tree_c.glb", "grapes.glb", 5, 2, new Color(0.35f, 0.55f, 0.25f)),
            _ => ("apple", "apple_tree.glb", "apple.glb", 6, 3, null),
        };

        public override void _Ready()
        {
            AddToGroup("fruit_trees_placed");
            AddCollisionBox(new Vector3(14, 50, 14));
            var model = ModelUtil.Instance("res://assets3d/features/" + Spec.tree);
            if (model != null)
            {
                ModelUtil.FitToHeight(model, 80f);
                if (Spec.leaf is Color c) ModelUtil.Recolor(model, c, "Tree_Green");
                // Boc trong 1 node rieng de phong to/thu nho quanh GOC cay (cay lon dan tu mat dat).
                _tree = new Node3D();
                _tree.AddChild(model);
                AddChild(_tree);
            }
            _fruits = new Node3D();
            AddChild(_fruits);
            var rng = new RandomNumberGenerator { Seed = (ulong)(Entry.X * 31 + Entry.Z * 17) };
            for (int i = 0; i < 6; i++)
            {
                var f = ModelUtil.Instance("res://assets3d/features/" + Spec.fruitModel);
                if (f == null) break;
                ModelUtil.FitToHeight(f, 7f);
                float a = Mathf.Tau * i / 6f + rng.Randf();
                var holder = new Node3D { Position = new Vector3(Mathf.Cos(a) * 18f, 46f + rng.RandfRange(-6f, 8f), Mathf.Sin(a) * 18f) };
                holder.AddChild(f);
                _fruits.AddChild(holder);
            }
            GameManager.Instance.DayChanged += OnDay;
            UpdateVisual();
        }

        private int Grow => Spec.grow;
        private bool Mature => Entry.Stage >= Grow;
        private bool Ripe => Mature && PlacementSystem.DaysSince(Entry) >= Spec.regrow;

        private void OnDay(int day)
        {
            if (!Mature && WeatherSystem.Instance?.Today != WeatherSystem.Kind.Snow) Entry.Stage++;
            if (Entry.Stage == Grow) Entry.Day = day - Spec.regrow + 1; // vua truong thanh: qua dau tien sau 1 ngay
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_tree == null) return;
            float s = Mature ? 1f : Mathf.Lerp(0.3f, 0.9f, Entry.Stage / (float)Grow);
            _tree.Scale = Vector3.One * s;
            _fruits.Scale = Vector3.One * s;
            _fruits.Visible = Ripe;
        }

        public override void _Process(double delta)
        {
            if (_fruits.Visible != Ripe) _fruits.Visible = Ripe;
        }

        public override void Interact()
        {
            if (!Mature) { Notify.Show(string.Format(Loc.T("place.tree_growing_fmt"), Grow - Entry.Stage)); return; }
            if (!Ripe) { Notify.Show(string.Format(Loc.T("place.tree_wait_fmt"), Spec.regrow - PlacementSystem.DaysSince(Entry))); return; }
            Entry.Day = GameManager.Instance.Day;
            Inventory.Instance.AddItem(Spec.fruit, 3, "harvest");
            SkillSystem.Instance.AddXp(Data.SkillType.Farming, 8);
            AchievementSystem.Instance.Add("crops_harvested", 3);
            Notify.Show(string.Format(Loc.T("farm.harvested_fmt"), ItemDatabase.Instance.GetDisplayName(Spec.fruit), 3));
            UpdateVisual();
        }
    }
}
