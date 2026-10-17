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
		public string EquippedTool = null; // vd pickaxe - xem GetToolPower/OreNode.cs
		// Trang phuc nong trai dang mac (Type Outfit) va co hien bo giap hay khong - khi tat, nhan vat
		// hien trang phuc nong trai nhung VAN duoc phong thu cua giap (xem VisualOutfit).
		public string EquippedOutfit = null;
		public bool ShowArmor = true;

		// Trang phuc dang hien thi: bo giap (neu bat hien giap) > trang phuc nong trai > do mac dinh.
		public string VisualOutfit =>
			ShowArmor && EquippedArmor != null && HiepSiVeVuon.Core.OutfitCatalog.Get(EquippedArmor) != null ? EquippedArmor : EquippedOutfit;

		public static bool IsEquippable(ItemDef def) =>
			def != null && def.Type is ItemType.Weapon or ItemType.Armor or ItemType.Tool or ItemType.Outfit;

		public bool IsEquipped(string id) =>
			id != null && (id == EquippedWeapon || id == EquippedArmor || id == EquippedTool || id == EquippedOutfit);

		public void Unequip(ItemType type)
		{
			if (type == ItemType.Weapon) EquippedWeapon = null;
			else if (type == ItemType.Armor) EquippedArmor = null;
			else if (type == ItemType.Tool) EquippedTool = null;
			else if (type == ItemType.Outfit) EquippedOutfit = null;
			LastEquippedType = null;
			EmitSignal(SignalName.InventoryChanged);
		}

		public void SetShowArmor(bool show)
		{
			ShowArmor = show;
			EmitSignal(SignalName.InventoryChanged);
		}

		// Trang phuc nong trai dang mac co giam the luc cho ky nang nay khong.
		public float OutfitStaminaMult(SkillType skill)
		{
			var def = EquippedOutfit == null ? null : ItemDatabase.Instance.GetItem(EquippedOutfit);
			return def != null && def.OutfitSkill == skill.ToString() ? 1f - def.OutfitStaminaDiscount : 1f;
		}

		public float OutfitSpeedMult()
		{
			var def = EquippedOutfit == null ? null : ItemDatabase.Instance.GetItem(EquippedOutfit);
			return 1f + (def?.OutfitSpeedBonus ?? 0f);
		}

		// Loai vat pham vua duoc Equip() gan day nhat - Player.cs doc gia tri nay khi nhan
		// InventoryChanged de biet NEN UU TIEN hien vu khi hay cong cu tren tay ngay lap tuc (vd
		// nguoi choi vao Balo bam chon 1 cong cu - can thay đổi tren tay NGAY, khong phu thuoc vao
		// hanh dong choi gan nhat la tan cong hay dung cong cu).
		public ItemType? LastEquippedType = null;

		[Signal] public delegate void InventoryChangedEventHandler();

		public override void _EnterTree()
		{
			Instance = this;
		}

		// source/key: nguon nhan do cho server kiem tra (harvest/fish/loot/... - xem backend
		// economyRules), key = ma phan thuong 1 lan (ruong/thu/nhiem vu). Bao so luong THUC TE da them
		// (tui day co the chi them duoc 1 phan) cho EconomyClient.
		public bool AddItem(string itemId, int count = 1, string source = null, string key = null)
		{
			int before = CountOf(itemId);
			bool ok = AddItemCore(itemId, count);
			EconomyClient.Instance?.OnItemDelta(itemId, CountOf(itemId) - before, source, key);
			return ok;
		}

		private bool AddItemCore(string itemId, int count)
		{
			var def = ItemDatabase.Instance.GetItem(itemId);
			if (def == null)
			{
				GD.PushWarning($"Vật phẩm không tồn tại: {itemId}");
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
					GD.Print("Túi đồ đã đầy!");
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
			bool ok = RemoveItemCore(itemId, count);
			if (ok) EconomyClient.Instance?.OnItemDelta(itemId, -count, null, null);
			return ok;
		}

		private bool RemoveItemCore(string itemId, int count)
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
			else if (def.Type == ItemType.Tool) EquippedTool = itemId;
			else if (def.Type == ItemType.Outfit) EquippedOutfit = itemId;
			LastEquippedType = def.Type;
			EmitSignal(SignalName.InventoryChanged);
		}

		public int GetWeaponDamage()
		{
			if (EquippedWeapon == null) return 5; // dam tay
			if (!ItemConditions.IsUsable(EquippedWeapon)) return 5; // vu khi hong (<40% do ben) = danh tay khong
			var def = ItemDatabase.Instance.GetItem(EquippedWeapon);
			// Cuong hoa +N (server quyet dinh, xem EnhanceClient): +10% moi cap.
			return def != null ? Mathf.RoundToInt(def.Damage * EnhanceClient.Mult(EquippedWeapon)) : 5;
		}

		// Suc cuoc (dao quang, xem OreNode.cs) - mirror y het GetWeaponDamage() o tren.
		public int GetToolPower()
		{
			if (EquippedTool == null) return 3; // tay khong cuoc yeu
			if (!ItemConditions.IsUsable(EquippedTool)) return 3;
			var def = ItemDatabase.Instance.GetItem(EquippedTool);
			return def != null ? Mathf.RoundToInt(def.Damage * EnhanceClient.Mult(EquippedTool)) : 3;
		}

		public int GetArmorDefense()
		{
			if (EquippedArmor == null) return 0;
			if (!ItemConditions.IsUsable(EquippedArmor)) return 0; // giap hong khong con tac dung
			var def = ItemDatabase.Instance.GetItem(EquippedArmor);
			return def != null ? Mathf.RoundToInt(def.Defense * EnhanceClient.Mult(EquippedArmor)) : 0;
		}

		// Dung consumable (vd potion)
		public bool UseConsumable(string itemId)
		{
			var def = ItemDatabase.Instance.GetItem(itemId);
			if (def == null || def.Type != ItemType.Consumable) return false;
			if (CountOf(itemId) <= 0) return false;
			RemoveItem(itemId, 1);
			if (def.HealAmount > 0) GameManager.Instance.Heal(def.HealAmount);
			if (def.StaminaAmount > 0) GameManager.Instance.RestoreStamina(def.StaminaAmount);
			SurvivalSystem.Instance?.Consume(def); // no/nuoc (xem SurvivalSystem)
			return true;
		}

		public void Clear()
		{
			Slots.Clear();
			EquippedWeapon = null;
			EquippedArmor = null;
			EquippedTool = null;
			EquippedOutfit = null;
			EmitSignal(SignalName.InventoryChanged);
		}
	}
}
