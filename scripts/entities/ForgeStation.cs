using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Lo Ren (ren vu khi/giap + cuong hoa) - hinh = ban lam viec + thung + cuoc/kiem (model CC0).
    public partial class ForgeStation : FarmFacility
    {
        public override void _Ready()
        {
            AddToGroup("forge_stations");
            AddCollisionBox(new Vector3(50, 30, 30));
            void Place(string path, Vector3 off, float h, float rot = 0f)
            {
                var m = ModelUtil.Instance(path);
                if (m == null) return;
                ModelUtil.FitToHeight(m, h);
                var holder = new Node3D { Position = off, RotationDegrees = new Vector3(0, rot, 0) };
                holder.AddChild(m);
                AddChild(holder);
            }
            Place("res://assets3d/quaternius/furniture/workbench.glb", Vector3.Zero, 26f);
            Place("res://assets3d/quaternius/furniture/barrel.glb", new Vector3(32, 0, 4), 22f);
            Place("res://assets3d/features/sword_b.glb", new Vector3(-6, 26, 0), 16f, 90f);
            Place("res://assets3d/features/shield_round.glb", new Vector3(-30, 0, 6), 18f, 20f);
            AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 1.6f, OmniRange = 80f, Position = new Vector3(0, 30, 10) });
        }

        public override void Interact() => (GetTree().GetFirstNodeInGroup("forge_ui") as ForgeUI)?.Open();
    }
}
