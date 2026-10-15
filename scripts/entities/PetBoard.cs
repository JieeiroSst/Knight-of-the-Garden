using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Bang Trai Thu Cung o thi tran - bam [E] mo PetUI.
    public partial class PetBoard : SignBoard
    {
        public override void _Ready() => BuildSign(new Color(0.45f, 0.7f, 0.4f));
        public override void Interact() => (GetTree().GetFirstNodeInGroup("pet_ui") as PetUI)?.Open();
    }
}
