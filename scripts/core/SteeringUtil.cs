using Godot;

namespace HiepSiVeVuon.Core
{
    // Ham dung chung cho kieu di chuyen "quay dau roi moi buoc toi" (non-holonomic steering) ma
    // moi loai vat nuoi/NPC trong game deu dung (Cow/Horse/Dog/FarmDog/FarmCat/Chicken/
    // FarmhandNpc/StablehandNpc/PoultryKeeperNpc): xoay dan huong than THAT (_facing) ve huong
    // muon den, gioi han toc do quay theo dt.
    public static class SteeringUtil
    {
        // Dung NLERP (lerp roi chuan hoa lai) thay vi Godot Vector3.Slerp - Slerp NEM LOI
        // ArgumentException("Argument is not normalized") khi 2 vector NGUOC HUONG HOAN TOAN
        // (180 do - truc xoay noi suy ra la vector khong/degenerate, khong the chuan hoa). Day
        // la tinh huong HOAN TOAN BINH THUONG trong game (vd vat nuoi chon muc tieu wander moi
        // dung nguoc huong dang dung) va da gay crash that (xem FarmCat.cs). Voi buoc xoay NHO
        // moi khung hinh (t << 1, dung cho steering muot moi frame) nlerp va slerp gan nhu khong
        // the phan biet bang mat, nhung nlerp khong bao gio nem loi.
        public static Vector3 SmoothTurn(Vector3 current, Vector3 desired, float t)
        {
            var blended = current.Lerp(desired, Mathf.Clamp(t, 0f, 1f));
            return blended.LengthSquared() > 0.0001f ? blended.Normalized() : desired;
        }

        // Dung chung cho MOI NPC co lich trinh "ve nha ngu" (FarmhandNpc/StablehandNpc/
        // PoultryKeeperNpc/EstateWorkerNpc/FarmWorkerNpc/TownCitizenNpc/ScheduledFarmNpc): khi
        // dang ngu, model duoc XOAY NAM NGANG (thay vi dung nguyen dang dung) de trong giong dang
        // nam tren giuong that su, dong thoi BO QUA logic xoay theo huong di chuyen (neu khong,
        // khoi if (_facing != Vector3.Zero) moi frame se lien tuc ep model dung day lai). Khi
        // thuc/di lam, goi lai voi isAsleep=false de tro ve dung logic xoay-theo-huong-di-chuyen
        // nhu cu.
        public static void ApplyStandingOrLyingPose(Node3D model, bool isAsleep, Vector3 facing, bool flipModelFacing, float turnAmount)
        {
            if (model == null) return;
            if (isAsleep)
            {
                // Nam ngua, xoay -90 do quanh truc X (than nam ngang thay vi dung), giu nguyen
                // huong Y=90 do co dinh de khop voi huong dat giuong trong AddBuildingEntrance
                // (xem bedRotationY trong Main.cs).
                model.RotationDegrees = new Vector3(-90f, 90f, 0f);
                return;
            }
            if (facing == Vector3.Zero) return;
            var lookDir = flipModelFacing ? -facing : facing;
            var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
            model.Basis = model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(turnAmount, 0f, 1f));
        }
    }
}
