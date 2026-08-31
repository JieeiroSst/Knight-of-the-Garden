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

        // true = day la cau thang (len/xuong tang trong CUNG 1 cong trinh) - khac voi cua chinh
        // (IsExit=false, IsFloorChange=false): cau thang KHONG dong/mo trang thai "_indoors"
        // hay ghi de vi tri tro ve ngoai troi, chi don gian doi vi tri sang tang khac.
        public bool IsFloorChange = false;

        // Vi tri phong noi that rieng cua cong trinh nay (moi cong trinh 1 phong khac nhau -
        // xem Main.BuildRoomForKind). Dung khi IsExit=false (ca cua chinh lan cau thang).
        public Vector3 InteriorAnchor;

        // true = tu kich hoat ngay khi nguoi choi CHAM VAO (giong nhat vat pham roi) thay vi
        // phai dung yen bam [E] - dung cho cau thang, vi buoc len cau thang la hanh dong tu
        // nhien khi di toi chu khong phai mot thao tac rieng nhu mo cua.
        public bool IsAutoTrigger = false;

        public override void _Ready()
        {
            AddToGroup(IsExit ? "exit_doors" : "building_doors");
            if (IsAutoTrigger) BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is Player player) player.TriggerFloorChange(InteriorAnchor);
        }
    }
}
