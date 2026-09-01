using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Cong khoa Nha Kinh (xem Main.BuildGreenhouse) - chan loi vao cho toi khi nguoi choi tra
    // vang mo khoa 1 LAN DUY NHAT (GameManager.GreenhouseUnlocked, luu qua SaveSystem) - sau do
    // tu an di vinh vien, khong con chan nua.
    public partial class GreenhouseGate : StaticBody3D
    {
        [Export] public int UnlockCost = 3000;

        public override void _Ready()
        {
            AddToGroup("greenhouse_gates");
            if (GameManager.Instance.GreenhouseUnlocked) Open();
        }

        public void Interact()
        {
            if (GameManager.Instance.GreenhouseUnlocked) return;
            if (GameManager.Instance.SpendGold(UnlockCost))
            {
                GameManager.Instance.GreenhouseUnlocked = true;
                GD.Print("Đã mở khóa Nhà Kính! Giờ có thể trồng cây quanh năm trong đó.");
                Open();
            }
            else
            {
                GD.Print($"Cổng Nhà Kính đang khóa - cần {UnlockCost} vàng để mở.");
            }
        }

        private void Open()
        {
            Visible = false;
            foreach (Node child in GetChildren())
                if (child is CollisionShape3D cs) cs.Disabled = true;
        }
    }
}
