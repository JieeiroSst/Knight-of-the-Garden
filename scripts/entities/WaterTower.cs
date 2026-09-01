using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Thap nuoc canh ho (xem Main.BuildWaterTower) - KHONG chi la trang tri: bom nuoc sach xuong
    // ho MOI NGAY (WaterEcosystem.TowerMaintained=true), tu HONG sau 1 so ngay khong sua
    // (DaysUntilBreak) khien chat luong nuoc ho tut dan - nguoi choi bam [E] de sua (tra vang).
    public partial class WaterTower : StaticBody3D
    {
        [Export] public int RepairCost = 40;
        [Export] public int DaysUntilBreak = 6;

        private int _daysSinceRepair = 0;
        public bool IsBroken => _daysSinceRepair >= DaysUntilBreak;

        public override void _Ready()
        {
            AddToGroup("water_towers");
            GameManager.Instance.DayChanged += OnDayChanged;
            WaterEcosystem.Instance.TowerMaintained = true;
        }

        private void OnDayChanged(int day)
        {
            _daysSinceRepair++;
            WaterEcosystem.Instance.TowerMaintained = !IsBroken;
        }

        public void Interact()
        {
            if (!IsBroken)
            {
                GD.Print("Tháp nước đang hoạt động tốt, đang bơm nước sạch xuống hồ.");
                return;
            }
            if (GameManager.Instance.SpendGold(RepairCost))
            {
                _daysSinceRepair = 0;
                WaterEcosystem.Instance.TowerMaintained = true;
                GD.Print("Đã sửa tháp nước - nguồn nước sạch lại chảy xuống hồ.");
            }
            else
            {
                GD.Print($"Tháp nước đã hỏng, cần {RepairCost} vàng để sửa.");
            }
        }
    }
}
