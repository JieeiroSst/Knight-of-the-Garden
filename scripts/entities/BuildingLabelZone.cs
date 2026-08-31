using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Vung phat hien nguoi choi lai gan 1 cong trinh - hien ten cong trinh tren HUD (xem
    // HUD.ShowBuildingName) khi nguoi choi buoc vao vung, tu an di khi buoc ra. Khac voi
    // BuildingDoor (cua ra vao that su, can bam [E]) - zone nay CHI la thong bao, khong co hanh
    // dong tuong tac nao.
    public partial class BuildingLabelZone : Area3D
    {
        [Export] public string BuildingLabel = "";

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is not Player) return;
            (GetTree().GetFirstNodeInGroup("hud") as HUD)?.ShowBuildingName(BuildingLabel);
        }

        private void OnBodyExited(Node3D body)
        {
            if (body is not Player) return;
            (GetTree().GetFirstNodeInGroup("hud") as HUD)?.HideBuildingName(BuildingLabel);
        }
    }
}
