using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ban Che Tao - dat TRUNG vi tri ban lam viec trang tri trong nha nong dan (xem
    // Main.BuildRoomForKind/Farmhouse), chi them va cham de bam [E] mo CraftingUI.
    public partial class CraftingBench : FarmFacility
    {
        // Ban dat ngoai troi (khong co mo hinh trang tri san) thi tu ve 1 cai ban don gian.
        [Export] public bool DrawVisual = false;

        public override void _Ready()
        {
            AddToGroup("crafting_benches");
            AddCollisionBox(new Vector3(40, 26, 26));
            if (DrawVisual)
            {
                var wood = new Color(0.6f, 0.42f, 0.25f);
                AddBox(new Vector3(40, 4, 26), new Vector3(0, 22, 0), wood);
                foreach (var x in new[] { -17f, 17f })
                foreach (var z in new[] { -10f, 10f })
                    AddBox(new Vector3(4, 20, 4), new Vector3(x, 10, z), wood.Darkened(0.3f));
            }
        }

        public override void Interact()
        {
            (GetTree().GetFirstNodeInGroup("crafting_ui") as CraftingUI)?.Open();
        }
    }
}
