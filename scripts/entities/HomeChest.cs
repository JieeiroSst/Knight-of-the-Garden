using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ruong Chua Do trong nha (dat trung ruong trang tri tang tret) - bam [E] mo HomeUI tab ruong.
    public partial class HomeChest : FarmFacility
    {
        public override void _Ready() => AddCollisionBox(new Vector3(34, 26, 26));

        public override void Interact() => (GetTree().GetFirstNodeInGroup("home_ui") as HomeUI)?.Open(HomeUI.Mode.Chest);
    }
}
