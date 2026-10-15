using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Thung Giao Hang canh nha nong dan - bam [E] mo ShippingUI (xem ShippingSystem.cs).
    public partial class ShippingBin : FarmFacility
    {
        public override void _Ready()
        {
            AddToGroup("shipping_bins");
            AddCollisionBox(new Vector3(40, 26, 28));
            var wood = new Color(0.55f, 0.36f, 0.2f);
            AddBox(new Vector3(40, 22, 28), new Vector3(0, 11, 0), wood);
            AddBox(new Vector3(42, 4, 30), new Vector3(0, 24, 0), wood.Darkened(0.25f)); // nap
            AddBox(new Vector3(44, 3, 3), new Vector3(0, 14, 15), wood.Darkened(0.4f));   // dai sat
        }

        public override void Interact()
        {
            (GetTree().GetFirstNodeInGroup("shipping_ui") as ShippingUI)?.Open();
        }
    }
}
