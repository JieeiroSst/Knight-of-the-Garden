using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Bang Le Hoi o quang truong thi tran - bam [E] (xem FestivalSystem.Interact).
    public partial class FestivalBoard : SignBoard
    {
        public override void _Ready()
        {
            BuildSign(new Color(0.85f, 0.35f, 0.4f));
            FestivalSystem.Instance.BoardPosition = GlobalPosition;
            AdventureSystem.Instance.RegisterPoi(AdventureSystem.PoiKind.Festival, "festival_board", GlobalPosition, "label.festival_board");
        }

        public override void Interact() => FestivalSystem.Instance.Interact();
    }
}
