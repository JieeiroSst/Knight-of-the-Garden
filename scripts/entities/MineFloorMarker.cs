using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Danh dau 1 tang ham mo: khi nguoi choi buoc vao -> bao ten mo/tang + loai quang + cuoc chim can
    // dung; xuong toi tang SAU NHAT lan dau -> ghi nhan cho thanh tuu "Chinh Phuc Long Dat".
    public partial class MineFloorMarker : Node3D
    {
        public string MineId = "";
        public string MineLabelKey = "";
        public int Floor = 1;
        public int FloorCount = 3;
        public string OreSummary = "";
        public float RoomHalf = 200f;

        private bool _inside;
        private double _check;

        public override void _Process(double delta)
        {
            _check -= delta;
            if (_check > 0) return;
            _check = 0.5;
            if (GetTree().GetFirstNodeInGroup("player") is not Node3D p) return;
            var d = p.GlobalPosition - GlobalPosition;
            bool inside = Mathf.Abs(d.X) < RoomHalf && Mathf.Abs(d.Z) < RoomHalf && Mathf.Abs(d.Y) < 150f;
            if (inside && !_inside) OnEnter();
            _inside = inside;
        }

        private void OnEnter()
        {
            Notify.Show(string.Format(Loc.T("mine.floor_fmt"), Loc.T(MineLabelKey), Floor, FloorCount, OreSummary));
            if (Floor < FloorCount) return;
            var ach = AchievementSystem.Instance;
            string key = $"deep_mine_{MineId}";
            if (ach.GetStat(key) > 0) return;
            ach.Add(key);
            int reached = 0;
            foreach (var kv in ach.Stats) if (kv.Key.StartsWith("deep_mine_") && kv.Value > 0) reached++;
            ach.SetMax("deep_mines", reached);
        }
    }
}
