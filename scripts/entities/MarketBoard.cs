using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Bang Cho Nguoi Choi o thi tran - bam [E] mo MarketUI (mua ban giua nguoi choi).
    public partial class MarketBoard : SignBoard
    {
        public override void _Ready() => BuildSign(new Color(0.3f, 0.55f, 0.75f));

        public override void Interact() => (GetTree().GetFirstNodeInGroup("market_ui") as MarketUI)?.Open();
    }
}
