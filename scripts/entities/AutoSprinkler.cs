using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // May tuoi tu dong (item "may_tuoi_tu_dong", xem Player.TryPlaceSprinkler) - nguoi choi dat
    // gan ruong, tu dong TUOI moi o dat trong tam MOI NGAY (GameManager.DayChanged) - khong can
    // thao tac thu cong, dung y "may moc tu dong hoa" nguoi dung yeu cau.
    public partial class AutoSprinkler : StaticBody3D
    {
        [Export] public float Radius = 100f;

        public override void _Ready()
        {
            AddToGroup("auto_sprinklers");
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void OnDayChanged(int day)
        {
            foreach (var n in GetTree().GetNodesInGroup("farm_plots"))
            {
                if (n is FarmPlot p && IsInstanceValid(p) && p.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
                    p.AutoWater();
            }
        }
    }
}
