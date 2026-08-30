using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Quan ly tui do: them/bo vat pham, xep chong (stack), trang bi.
    public partial class Inventory : Node
    {
        public static Inventory Instance { get; private set; }

        public const int MaxSlots = 24;
        public List<ItemStack> Slots = new();

        // Trang bi hien tai
        public string EquippedWeapon = null;
        public string EquippedArmor = null;

        [Signal] public delegate void InventoryChangedEventHandler();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public bool AddItem(string itemId, int count = 1)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null)
            {
                GD.PushWarning($"Vat pham khong ton tai: {itemId}");
                return false;
            }

            // Xep chong vao stack co san
            foreach (var s in Slots)
            {
                if (s.ItemId == itemId && s.Count < def.MaxStack)
                {
                    int space = def.MaxStack - s.Count;
                    int add = Mathf.Min(space, count);
                    s.Count += add;
                    count -= add;
                    if (count <= 0) { EmitSignal(SignalName.InventoryChanged); return true; }
                }
            }

            // Tao slot moi
            while (count > 0)
            {
                if (Slots.Count >= MaxSlots)
                {
                    GD.Print("Tui do da day!");
                    EmitSignal(SignalName.InventoryChanged);
                    return false;
                }
                int add = Mathf.Min(def.MaxStack, count);
                Slots.Add(new ItemStack(itemId, add));
                count -= add;
            }
            EmitSignal(SignalName.InventoryChanged);
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
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }

        public int CountOf(string itemId)
        {
            int total = 0;
            foreach (var s in Slots) if (s.ItemId == itemId) total += s.Count;
            return total;
        }

        public void Equip(string itemId)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null) return;
            if (def.Type == ItemType.Weapon) EquippedWeapon = itemId;
            else if (def.Type == ItemType.Armor) EquippedArmor = itemId;
            EmitSignal(SignalName.InventoryChanged);
        }

        public int GetWeaponDamage()
        {
            if (EquippedWeapon == null) return 5; // dam tay
            var def = ItemDatabase.Instance.GetItem(EquippedWeapon);
            return def != null ? def.Damage : 5;
        }

        public int GetArmorDefense()
        {
            if (EquippedArmor == null) return 0;
            var def = ItemDatabase.Instance.GetItem(EquippedArmor);
            return def != null ? def.Defense : 0;
        }

        // Dung consumable (vd potion)
        public bool UseConsumable(string itemId)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null || def.Type != ItemType.Consumable) return false;
            if (CountOf(itemId) <= 0) return false;
            RemoveItem(itemId, 1);
            if (def.HealAmount > 0) GameManager.Instance.Heal(def.HealAmount);
            return true;
        }

        public void Clear()
        {
            Slots.Clear();
            EquippedWeapon = null;
            EquippedArmor = null;
            EmitSignal(SignalName.InventoryChanged);
        }
    }
}
