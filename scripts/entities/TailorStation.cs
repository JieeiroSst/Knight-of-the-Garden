using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ban May: may trang phuc nong trai tu Vai Det + thuoc nhuom (mo Tu Do o che do may) - hinh =
    // tu quan ao + ban tron + bao vai + tham (model CC0 Quaternius).
    public partial class TailorStation : FarmFacility
    {
        public override void _Ready()
        {
            AddToGroup("tailor_stations");
            AddCollisionBox(new Vector3(56, 30, 30));
            void Place(string path, Vector3 off, float h, float rot = 0f)
            {
                var m = ModelUtil.Instance(path);
                if (m == null) return;
                ModelUtil.FitToHeight(m, h);
                var holder = new Node3D { Position = off, RotationDegrees = new Vector3(0, rot, 0) };
                holder.AddChild(m);
                AddChild(holder);
            }
            const string F = "res://assets3d/quaternius/furniture/";
            Place(F + "rug.glb", new Vector3(0, 0.3f, 6), 1.5f);
            Place(F + "wardrobe.glb", new Vector3(-20, 0, -4), 40f);
            Place(F + "table_round.glb", new Vector3(14, 0, 6), 16f);
            Place(F + "bag.glb", new Vector3(30, 0, -2), 12f, 30f);
        }

        public override void Interact() => (GetTree().GetFirstNodeInGroup("wardrobe_ui") as WardrobeUI)?.Open(true);
    }
}
