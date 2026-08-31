using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // "Cam bien" vo hinh theo doi do ben (Hp) cua 1 khu vuc hang rao chinh trong nong trai - hang
    // rao 3D THAT (MeshInstance3D tinh, dat qua AddFenceLine/AddStoneWallLine) khong doi hinh
    // dang theo Hp nay (khong co he thong doi model hu hong/lanh lan theo thoi gian thuc), day
    // chi la du lieu de Marcel (tho sua chua, xem RepairmanNpc.cs) biet KHU VUC NAO can den sua,
    // theo dung y tuong "Fence HP: 100 -> Mua/Dong vat/Thoi gian -> 70 -> Can sua".
    public partial class FenceMarker : Node3D
    {
        [Export] public string FenceName = "Hang rao";
        [Export] public int Hp = 100;
        [Export] public int MinHp = 20; // khong bao gio "sup do" hoan toan, luon con it nhat 1 phan de Marcel sua

        public override void _Ready()
        {
            AddToGroup("fence_markers");
            GameManager.Instance.DayChanged += _ => Decay();
        }

        // Moi ngay THAT: mua/dong vat co xat/thoi gian lam hao mon 1 khoang ngau nhien.
        private void Decay()
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            Hp = Mathf.Max(MinHp, Hp - rng.RandiRange(5, 15));
        }

        public void Repair(int amount) => Hp = Mathf.Min(100, Hp + amount);
    }
}
