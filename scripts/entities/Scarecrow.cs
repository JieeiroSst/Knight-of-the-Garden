using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Entities
{
    // Bu Nhin - xua qua trong ban kinh ProtectRadius (xem FarmPlot.OnDayChanged). Nguoi choi cam
    // bang item "bu_nhin" (Player.TryPlaceScarecrow), vi tri luu qua HomeSystem.ScarecrowPositions.
    public partial class Scarecrow : Node3D
    {
        public const float ProtectRadius = 420f;
        // Bu nhin trang tri co san trong the gioi (Main.DrawGround) cung co tac dung.
        public static readonly List<Vector3> ExtraPoints = new();

        private static PackedScene _model;

        public override void _Ready()
        {
            AddToGroup("scarecrows");
            _model ??= GD.Load<PackedScene>("res://assets3d/polybygoogle/scarecrow.glb");
            if (_model != null)
            {
                var m = _model.Instantiate<Node3D>();
                m.Scale = Vector3.One * 13f;
                AddChild(m);
            }
        }

        public static bool IsProtected(SceneTree tree, Vector3 pos)
        {
            foreach (var p in ExtraPoints)
                if (new Vector2(p.X - pos.X, p.Z - pos.Z).Length() <= ProtectRadius) return true;
            foreach (Node n in tree.GetNodesInGroup("scarecrows"))
                if (n is Node3D s && new Vector2(s.GlobalPosition.X - pos.X, s.GlobalPosition.Z - pos.Z).Length() <= ProtectRadius) return true;
            return false;
        }
    }
}
