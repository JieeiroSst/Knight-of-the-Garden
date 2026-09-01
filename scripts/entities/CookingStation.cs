using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Bep nau an (xem Main.BuildCookingStation) - nguoi choi bam [E] de mo CookingUI.cs.
    public partial class CookingStation : StaticBody3D
    {
        public override void _Ready() => AddToGroup("cooking_stations");

        public void Interact()
        {
            var ui = GetTree().GetFirstNodeInGroup("cooking_ui") as CookingUI;
            ui?.Open();
        }
    }
}
