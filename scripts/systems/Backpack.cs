using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Systems
{
    // Balo: tui do THU HAI, rieng biet voi Inventory (tui do chinh, 24 o, phim [I]) - kho chua
    // them 50 o, mo bang phim [B] (xem BackpackUI.cs). MOI loai vat pham (ke ca vu khi/giap/cong
    // cu) deu xep chong duoc toi da 100 - KHAC Inventory (dung ItemDef.MaxStack rieng tung loai,
    // vd vu khi chi 1), o day LUON co dinh 100 bat ke loai (balo la kho chua thu dong, khong lien
    // quan den logic trang bi - trang bi van CHI doc tu Inventory.EquippedWeapon/Armor/Tool).
    public partial class Backpack : Node
    {
        public static Backpack Instance { get; private set; }

        public const int MaxSlots = 50;
        public const int MaxStackPerItem = 100;
        public List<ItemStack> Slots = new();

        [Signal] public delegate void BackpackChangedEventHandler();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public bool AddItem(string itemId, int count = 1)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null)
            {
                GD.PushWarning($"Vật phẩm không tồn tại: {itemId}");
                return false;
            }

            foreach (var s in Slots)
            {
                if (s.ItemId == itemId && s.Count < MaxStackPerItem)
                {
                    int space = MaxStackPerItem - s.Count;
                    int add = Mathf.Min(space, count);
                    s.Count += add;
                    count -= add;
                    if (count <= 0) { EmitSignal(SignalName.BackpackChanged); return true; }
                }
            }

            while (count > 0)
            {
                if (Slots.Count >= MaxSlots)
                {
                    GD.Print("Balo đã đầy!");
                    EmitSignal(SignalName.BackpackChanged);
                    return false;
                }
                int add = Mathf.Min(MaxStackPerItem, count);
                Slots.Add(new ItemStack(itemId, add));
                count -= add;
            }
            EmitSignal(SignalName.BackpackChanged);
            return true;
        }

        public bool RemoveItem(string itemId, int count = 1)
        {
            if (CountOf(itemId) < count) return false;
            for (int i = Slots.Count - 1; i >= 0 && count > 0; i--)
            {
                if (Slots[i].ItemId == itemId)
                {
                    int take = Mathf.Min(Slots[i].Count, count);
                    Slots[i].Count -= take;
                    count -= take;
                    if (Slots[i].Count <= 0) Slots.RemoveAt(i);
                }
            }
            EmitSignal(SignalName.BackpackChanged);
            return true;
        }

        public int CountOf(string itemId)
        {
            int total = 0;
            foreach (var s in Slots) if (s.ItemId == itemId) total += s.Count;
            return total;
        }

        public void Clear()
        {
            Slots.Clear();
            EmitSignal(SignalName.BackpackChanged);
        }
    }
}
