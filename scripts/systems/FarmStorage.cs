using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Kho nong san chung cua trang trai (nha kho): dem SO LUONG san pham (trung, sua...) da
    // duoc cac NPC cham nuoi THU HOACH va cat vao kho - KHAC voi Inventory (tui do rieng cua
    // NGUOI CHOI, co gioi han o/stack). Kho nay khong gioi han so luong, chi la 1 bo dem hien thi
    // (xem UI.FarmStorageBoard) - chua dung de "rut hang" ra dung, chi de nguoi choi thay duoc
    // trang trai dang san xuat bao nhieu.
    public partial class FarmStorage : Node
    {
        public static FarmStorage Instance { get; private set; }

        private readonly Dictionary<string, int> _counts = new();

        [Signal] public delegate void StorageChangedEventHandler();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public void Add(string itemId, int amount)
        {
            if (amount <= 0) return;
            _counts.TryGetValue(itemId, out int cur);
            _counts[itemId] = cur + amount;
            EmitSignal(SignalName.StorageChanged);
        }

        public int GetCount(string itemId) => _counts.TryGetValue(itemId, out int cur) ? cur : 0;

        // Truoc day KHONG co cach nao "rut hang" ra dung (chi Add) - Utility AI can tieu thu that
        // (vd chuong an het thuc an trong kho) de "kho sap het" tro thanh 1 su that co the cham
        // diem duoc thay vi trang tri. Tra false neu khong du (khong tru am).
        public bool TryRemove(string itemId, int amount)
        {
            if (amount <= 0) return true;
            if (!_counts.TryGetValue(itemId, out int cur) || cur < amount) return false;
            _counts[itemId] = cur - amount;
            EmitSignal(SignalName.StorageChanged);
            return true;
        }

        public bool IsLow(string itemId, int threshold) => GetCount(itemId) < threshold;

        // Antoine (nguoi quan ly kho, xem WarehouseManagerNpc.cs) dung 2 ham nay de bao cao tinh
        // trang kho va de xuat "dua ra cho" khi gan day - CapacityPerItem la 1 gioi han GIA DINH
        // (kho thuc te khong gioi han so luong, xem ghi chu tren) chi dung lam moc so sanh de tao
        // canh bao % cho trong con y nghia.
        public const int CapacityPerItem = 300;
        public float GetFullness(string itemId) => Mathf.Clamp((float)GetCount(itemId) / CapacityPerItem, 0f, 1f);

        public System.Collections.Generic.IReadOnlyDictionary<string, int> AllCounts => _counts;
    }
}
