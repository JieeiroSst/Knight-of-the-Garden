using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Theo doi vi tri nguoi choi de ghi nhan KHAM PHA vung moi (WorldStreamer.Regions) - lan dau
    // buoc vao 1 vung: thong bao + EXP + bo suong mu vung do tren ban do (xem MapUI).
    public partial class AdventureTracker : Node
    {
        private double _checkIn = 0.5;

        public override void _Process(double delta)
        {
            _checkIn -= delta;
            if (_checkIn > 0) return;
            _checkIn = 0.5;
            if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;
            var p = player.GlobalPosition;
            foreach (var r in WorldStreamer.Regions)
                if (Mathf.Abs(p.X - r.Center.X) <= r.HalfSize && Mathf.Abs(p.Z - r.Center.Z) <= r.HalfSize)
                {
                    AdventureSystem.Instance.Discover(r.Name);
                    return;
                }
        }
    }
}
