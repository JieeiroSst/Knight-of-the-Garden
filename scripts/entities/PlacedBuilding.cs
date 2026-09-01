using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Cong trinh do NGUOI CHOI tu xay (xem BuildMenuUI.cs/BuildingCatalog.cs) - la 1 StaticBody3D
    // bao quanh model GLB (instantiate rieng, KHONG gan script - model goc chi la mesh trang tri)
    // de vua co va cham that (nguoi choi khong di xuyen qua duoc) vua giu lai BuildingId de
    // luu/nap lai DUNG cong trinh sau khi thoat game (xem SaveSystem.cs/Main.RestorePlacedBuildings).
    public partial class PlacedBuilding : StaticBody3D
    {
        public string BuildingId;

        public static void Spawn(BuildingDef def, Vector3 pos, Node parent)
        {
            if (def == null || parent == null) return;
            var scene = GD.Load<PackedScene>(def.ScenePath);
            if (scene == null) return;

            var visual = scene.Instantiate<Node3D>();
            visual.Position = pos;
            visual.Scale = Vector3.One * def.ModelScale;
            parent.AddChild(visual);

            var body = new PlacedBuilding { BuildingId = def.Id, Position = pos };
            body.AddToGroup("player_buildings");
            body.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = def.FootprintRadius, Height = 300f },
                Position = Vector3.Up * 150f,
            });
            parent.AddChild(body);
        }
    }
}
