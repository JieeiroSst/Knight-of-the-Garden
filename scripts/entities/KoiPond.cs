using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ho ca canh (model dai phun nuoc CC0): bam [E] tha 1 con moi loai ca CHUA co -> ca (model Fish)
    // boi vong trong ho. Du 6 / tat ca cac loai co moc thuong (gui thu, server chan nhan 2 lan).
    public partial class KoiPond : FarmFacility
    {
        public PlacementSystem.Entry Entry;   // Data: id cac loai ca da tha
        private readonly List<(Node3D node, float phase, float radius)> _fish = new();

        public static IEnumerable<string> AllFish => ItemDatabase.Instance.Fish.Select(f => f.ItemId);

        public override void _Ready()
        {
            AddCollisionBox(new Vector3(70, 26, 70));
            var fountain = ModelUtil.Instance("res://assets3d/features/fountain.glb");
            if (fountain != null) { ModelUtil.FitToHeight(fountain, 55f); AddChild(fountain); }
            foreach (var id in Entry.Data) AddFishVisual(id);
        }

        private void AddFishVisual(string id)
        {
            var f = ItemModels.Build(id, 6f) ?? ModelUtil.Instance("res://assets3d/features/fish.glb");
            if (f == null) return;
            var holder = new Node3D();
            holder.AddChild(f);
            AddChild(holder);
            _fish.Add((holder, _fish.Count * 1.3f, 14f + (_fish.Count % 3) * 6f));
        }

        public override void _Process(double delta)
        {
            float t = (float)Time.GetTicksMsec() / 1000f;
            foreach (var (node, phase, radius) in _fish)
            {
                float a = t * 0.7f + phase;
                node.Position = new Vector3(Mathf.Cos(a) * radius, 10f + Mathf.Sin(t * 2f + phase) * 1.5f, Mathf.Sin(a) * radius);
                node.Rotation = new Vector3(0, -a, 0);
            }
        }

        public override void Interact()
        {
            var have = new HashSet<string>(Entry.Data);
            string give = AllFish.FirstOrDefault(id => !have.Contains(id) && Inventory.Instance.CountOf(id) > 0);
            int total = AllFish.Count();
            if (give == null)
            {
                Notify.Show(string.Format(Loc.T("place.pond_status_fmt"), have.Count, total));
                return;
            }
            Inventory.Instance.RemoveItem(give, 1);
            Entry.Data.Add(give);
            AddFishVisual(give);
            Notify.Show(string.Format(Loc.T("place.pond_added_fmt"), ItemDatabase.Instance.GetDisplayName(give), Entry.Data.Count, total));
            AchievementSystem.Instance.SetMax("pond_species", Entry.Data.Count);
            if (Entry.Data.Count == 6)
                MailSystem.Instance.Send("pond_6", Loc.T("place.pond_reward_title"), Loc.T("place.pond_reward6"), "moi_cau", 20, 1000);
            if (Entry.Data.Count == total)
                MailSystem.Instance.Send("pond_all", Loc.T("place.pond_reward_title"), Loc.T("place.pond_reward_all"), "kim_cuong", 2, 5000);
        }
    }
}
