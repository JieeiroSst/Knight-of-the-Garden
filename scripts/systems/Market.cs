using Godot;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Systems
{
    // Thi truong dong: gia mua/ban 1 item TU DONG bien doi theo muc TON KHO HIEN TAI trong
    // FarmStorage (khong xay he thong theo doi lich su/xu huong san luong rieng - don gian hoa
    // hop ly, van dung tinh than "san luong tang -> ton kho nhieu -> gia giam, khan hiem -> gia
    // tang" nguoi dung yeu cau, vi hau het nong san khong co gi tieu thu bot ngoai
    // "thucan_giasuc"). CHI co y nghia cho Crop/Material (2 loai duy nhat FarmStorage thuc su
    // theo doi - xem ghi chu trong GetSupplyMultiplier) - vu khi/giap/hat giong/tool tra ve 1.0f
    // (gia co dinh), KHONG bi tinh nham la "khan hiem" chi vi chua bao gio duoc Add vao kho.
    public static class Market
    {
        public const int ReferenceStock = 40; // muc "binh thuong" gia dinh - giong tinh than CapacityPerItem cu

        public static float GetSupplyMultiplier(string itemId)
        {
            var def = ItemDatabase.Instance?.GetItem(itemId);
            if (def == null || (def.Type != ItemType.Crop && def.Type != ItemType.Material))
                return 1f;

            int count = FarmStorage.Instance.GetCount(itemId);
            float ratio = count / (float)ReferenceStock;
            // It hang (khan hiem) -> nhan gia LEN toi 1.6x; du/thua hang -> giam gia toi 0.4x.
            return Mathf.Clamp(1.6f - 0.6f * ratio, 0.4f, 1.6f);
        }
    }
}
