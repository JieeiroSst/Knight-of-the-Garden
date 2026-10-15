using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Quay Bao Tang o thi tran - bam [E] mo MuseumUI. Ve 1 ngoi nha co nho co cot + bac thang.
    public partial class MuseumCounter : FarmFacility
    {
        public override void _Ready()
        {
            AddCollisionBox(new Vector3(60, 40, 30));
            var marble = new Color(0.86f, 0.84f, 0.78f);
            AddBox(new Vector3(90, 6, 50), new Vector3(0, 3, 0), marble.Darkened(0.15f));
            foreach (var x in new[] { -36f, -12f, 12f, 36f })
                AddBox(new Vector3(7, 50, 7), new Vector3(x, 31, 0), marble);
            AddBox(new Vector3(96, 8, 54), new Vector3(0, 60, 0), marble.Darkened(0.05f));
            var roof = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(100, 22, 56) }, Position = new Vector3(0, 75, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.25f, 0.2f) },
            };
            AddChild(roof);
            AddBox(new Vector3(40, 18, 14), new Vector3(0, 15, 6), new Color(0.4f, 0.25f, 0.15f)); // quay
            AdventureSystem.Instance.RegisterPoi(AdventureSystem.PoiKind.Museum, "museum", GlobalPosition, "label.museum");
        }

        public override void Interact() => (GetTree().GetFirstNodeInGroup("museum_ui") as MuseumUI)?.Open();
    }
}
