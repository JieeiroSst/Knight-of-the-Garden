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
    }
}
