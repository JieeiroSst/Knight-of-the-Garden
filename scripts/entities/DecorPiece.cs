using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Do trang tri da dat (model CC0 theo DecorCatalog). Bam [E] de nhat lai ve tui.
    public partial class DecorPiece : FarmFacility
    {
        public PlacementSystem.Entry Entry;

        public override void _Ready()
        {
            if (!DecorCatalog.Models.TryGetValue(Entry.ItemId, out var info)) return;
            var m = ModelUtil.Instance(info.model);
            if (m == null) return;
            ModelUtil.FitToHeight(m, info.height);
            AddChild(m);
            var box = m.Transform * ModelUtil.LocalAabb(m);
            AddCollisionBox(new Vector3(Mathf.Max(8f, box.Size.X), Mathf.Max(4f, box.Size.Y), Mathf.Max(8f, box.Size.Z)));
            if (Entry.ItemId.Contains("lamp") || Entry.ItemId.Contains("lantern") || Entry.ItemId.Contains("chandelier"))
                AddChild(new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.6f), LightEnergy = 1.4f, OmniRange = 120f, Position = Vector3.Up * info.height * 0.9f });
        }

        public override void Interact() => PlacementSystem.Instance.PickUp(Entry, this);
    }
}
