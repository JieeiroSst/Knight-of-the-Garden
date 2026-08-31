using Godot;

namespace HiepSiVeVuon.Entities
{
    // Truc quay cua canh quat coi xay gio (xem Main.AddWindmill) - chi xoay LIEN TUC quanh 1
    // truc cuc bo co dinh (SpinAxis, Main.cs tinh tu AABB rieng cua mesh canh quat luc dat, chon
    // chieu MONG NHAT cua dia canh quat lam truc), khong co logic AI/vat ly gi khac. La CHA cua
    // mesh canh quat, dat tai DUNG TAM HUB (khong phai tam ca coi xay) de canh quat quay tai cho
    // giong coi xay gio that, khong "vay" quanh chan thap cua coi.
    public partial class WindmillBlades : Node3D
    {
        [Export] public Vector3 SpinAxis = Vector3.Back;
        [Export] public float SpinSpeedDegPerSec = 35f;

        public override void _Process(double delta)
        {
            RotateObjectLocal(SpinAxis.Normalized(), Mathf.DegToRad(SpinSpeedDegPerSec) * (float)delta);
        }
    }
}
