using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Truoc day KHONG CO cach nao de 1 NPC tu "mua" duoc gi - ShopUI.Buy() la handler nut bam CHI
    // danh cho nguoi choi (tru vang cua NGUOI CHOI, cong vao Inventory CUA NGUOI CHOI). NPC can 1
    // duong rieng, khong qua UI: dung CHUNG 1 vi vang voi nguoi choi (GameManager.Gold - don gian
    // nhat, tranh bay them khai niem "quy trang trai" rieng khong can thiet) nhung hang mua vao
    // KHO CHUNG cua trang trai (FarmStorage) chu khong phai tui do ca nhan nguoi choi.
    public static class NpcEconomy
    {
        // Diem "nhap hang" cho NPC trang trai - CO Y KHONG dung vi tri thuong nhan that trong
        // Thi Tran (cach trang trai ~9000 don vi, ngoai pham vi navmesh cua trang trai - xem
        // Main.BuildFarmNavigation chi bake trong tuong da) vi NPC cham chuong di bo den do la phi
        // thuc te. Main.cs gan bang StorageZoneAnchor (Khu Nha Kho, xem quy hoach 5 khu vuc) -
        // hanh dong GOAP "di nhap hang" (xem UtilityAi.cs) coi day la noi nhan hang giao toi.
        public static Vector3 RestockPos;

        public static bool NpcBuy(string itemId, int qty)
        {
            var def = ItemDatabase.Instance?.GetItem(itemId);
            if (def == null) return false;
            // Cung gia dong theo cung/cau nhu nguoi choi (xem Market.cs) - NPC tra dat hon khi
            // mat hang dang khan hiem, nhat quan 1 the gioi kinh te.
            int cost = Mathf.RoundToInt(def.BuyPrice * Market.GetSupplyMultiplier(itemId) * qty);
            if (!GameManager.Instance.SpendGold(cost)) return false;
            FarmStorage.Instance.Add(itemId, qty);
            return true;
        }
    }
}
