using Godot;

namespace HiepSiVeVuon.Entities
{
    // Cua ra vao mot cong trinh: nguoi choi dung gan + nhan [E] de vao (dua toi noi that RIENG
    // cua cong trinh nay, xem InteriorAnchor) hoac ra (neu la cua thoat ben trong noi that, dua
    // ve dung diem ngoai troi da luu).
    public partial class BuildingDoor : Area3D
    {
        [Export] public string BuildingName = "";
        [Export] public bool IsExit = false;

        // Vi tri phong noi that rieng cua cong trinh nay (moi cong trinh 1 phong khac nhau -
        // xem Main.BuildRoomForKind). Chi dung khi IsExit=false.
        public Vector3 InteriorAnchor;

        public override void _Ready()
        {
            AddToGroup(IsExit ? "exit_doors" : "building_doors");
        }
    }
}
