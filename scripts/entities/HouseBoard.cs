using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Bang Nang Cap Nha canh nha nong dan - bam [E] mo HomeUI.
    public partial class HouseBoard : SignBoard
    {
        public override void _Ready() => BuildSign(new Color(0.75f, 0.6f, 0.35f));

        public override void Interact() => (GetTree().GetFirstNodeInGroup("home_ui") as HomeUI)?.Open(HomeUI.Mode.Upgrade);
    }
}
