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
    }
}
