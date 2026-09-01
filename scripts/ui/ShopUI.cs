using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
	// Cua hang: mua hat giong/do dung, ban nong san, ky/theo doi Hop dong dai han (xem
	// ContractSystem.cs). Kinh te co gia mua/ban DONG theo cung/cau (xem Market.cs) + theo mua/le
	// hoi (xem GameManager.GetSeasonalPriceMultiplier) - gia CHI DONG that su cho Crop/Material.
	public partial class ShopUI : CanvasLayer
	{
		private PanelContainer _panel;
		private VBoxContainer _buyList;
		private VBoxContainer _sellList;
		private VBoxContainer _contractList;
		private Label _status;
		private NPC _current;
		private readonly LocalizedLabelSet _loc = new();

		public override void _Ready()
		{
			AddToGroup("shop_ui");
			Build();
			Visible = false;

			// Gia/hop dong THAT SU "dong" ngay ca khi dang mo panel xem (truoc day chi lam moi
			// luc Open()) - chi lam moi khi panel dang hien (Visible), tranh lam viec thua khi
			// nguoi choi dang o xa/panel dang dong.
			FarmStorage.Instance.StorageChanged += () => { if (Visible) { RefreshBuy(); RefreshSell(); } };
			GameManager.Instance.DayChanged += _ => { if (Visible) { RefreshBuy(); RefreshSell(); RefreshContracts(); UpdateStatus(); } };
			ContractSystem.Instance.ContractUpdated += _ => { if (Visible) RefreshContracts(); };
			Loc.LanguageChanged += OnLanguageChanged;
		}

		public override void _ExitTree()
		{
			Loc.LanguageChanged -= OnLanguageChanged;
		}

		private void Build()
		{
			_panel = new PanelContainer();
			_panel.Position = new Vector2(300, 60);
			_panel.CustomMinimumSize = new Vector2(360, 480);
			AddChild(_panel);

			var vb = new VBoxContainer();
			_panel.AddChild(vb);
			vb.AddChild(_loc.Track(new Label(), "shop.title"));
			_status = new Label();
			vb.AddChild(_status);

			vb.AddChild(_loc.Track(new Label(), "shop.buy_header"));
			_buyList = new VBoxContainer();
			vb.AddChild(_buyList);

			vb.AddChild(_loc.Track(new Label(), "shop.sell_header"));
			_sellList = new VBoxContainer();
			vb.AddChild(_sellList);

			vb.AddChild(_loc.Track(new Label(), "shop.contract_header"));
			_contractList = new VBoxContainer();
			vb.AddChild(_contractList);

			var close = _loc.Track(new Button(), "common.close");
			close.Pressed += () => Visible = false;
			vb.AddChild(close);
		}

		private void OnLanguageChanged()
		{
			_loc.Refresh();
			UpdateStatus();
			RefreshBuy();
			RefreshSell();
			RefreshContracts();
		}

		public void Open(NPC npc)
		{
			_current = npc;
			Visible = true;
			RefreshBuy();
			RefreshSell();
			RefreshContracts();
			UpdateStatus();
		}

		private void UpdateStatus() => _status.Text = string.Format(Loc.T("shop.gold_fmt"), GameManager.Instance.Gold);

		private void RefreshBuy()
		{
			foreach (Node c in _buyList.GetChildren()) c.QueueFree();
			if (_current == null) return;
			foreach (var id in _current.ShopItems)
			{
				var def = ItemDatabase.Instance.GetItem(id);
				if (def == null) continue;
				// Gia mua theo cung/cau (xem Market.cs) - CHI dong voi Crop/Material, cac loai
				// khac (hat giong/vu khi/tool...) tra ve he so 1.0f (gia co dinh nhu cu).
				int price = Mathf.RoundToInt(def.BuyPrice * Market.GetSupplyMultiplier(id));
				var btn = new Button { Text = string.Format(Loc.T("shop.buy_item_fmt"), ItemDatabase.Instance.GetDisplayName(id), price) };
				string itemId = id;
				btn.Pressed += () => Buy(itemId, price);
				_buyList.AddChild(btn);
			}
		}

		private void RefreshSell()
		{
			foreach (Node c in _sellList.GetChildren()) c.QueueFree();
			foreach (var stack in Inventory.Instance.Slots)
			{
				var def = ItemDatabase.Instance.GetItem(stack.ItemId);
				if (def == null || def.SellPrice <= 0) continue;
				if (def.Type != ItemType.Crop && def.Type != ItemType.Material) continue;
				// Gia ban theo mua/le hoi (GetSeasonalPriceMultiplier) NHAN THEM theo cung/cau
				// (Market.GetSupplyMultiplier) - 2 he so DOC LAP, ca nhan hien thi label lan logic
				// AddGold deu dung chung bien "price" nay nen tu dong dong bo.
				int price = Mathf.RoundToInt(def.SellPrice * GameManager.Instance.GetSeasonalPriceMultiplier(def) * Market.GetSupplyMultiplier(def.Id));
				var btn = new Button { Text = string.Format(Loc.T("shop.sell_item_fmt"), ItemDatabase.Instance.GetDisplayName(stack.ItemId), stack.Count, price) };
				string itemId = stack.ItemId;
				btn.Pressed += () => Sell(itemId, price);
				_sellList.AddChild(btn);
			}
		}

		private void RefreshContracts()
		{
			foreach (Node c in _contractList.GetChildren()) c.QueueFree();

			foreach (var def in ContractSystem.Catalog)
			{
				if (ContractSystem.Instance.IsActive(def.Id) || ContractSystem.Instance.IsCompleted(def.Id)) continue;
				var itemDef = ItemDatabase.Instance.GetItem(def.ItemId);
				var btn = new Button
				{
					Text = string.Format(Loc.T("shop.contract_sign_fmt"), def.Title, def.AmountPerDelivery, itemDef != null ? ItemDatabase.Instance.GetDisplayName(def.ItemId) : null, def.IntervalDays, def.TotalDeliveries, def.RewardGold)
				};
				string contractId = def.Id;
				btn.Pressed += () => { ContractSystem.Instance.SignContract(contractId); };
				_contractList.AddChild(btn);
			}

			foreach (var kv in ContractSystem.Instance.Active)
			{
				var def = ContractSystem.GetDef(kv.Key);
				if (def == null) continue;
				int daysLeft = Mathf.Max(0, kv.Value.NextDueDay - GameManager.Instance.Day);
				_contractList.AddChild(new Label
				{
					Text = string.Format(Loc.T("shop.contract_active_fmt"), def.Title, kv.Value.DeliveriesDone, def.TotalDeliveries, daysLeft)
				});
			}
		}

		private void Buy(string itemId, int price)
		{
			if (GameManager.Instance.SpendGold(price))
			{
				Inventory.Instance.AddItem(itemId, 1);
				_status.Text = Loc.T("shop.buy_success");
			}
			else _status.Text = Loc.T("shop.buy_fail");
			UpdateStatus();
			RefreshBuy();
			RefreshSell();
		}

		private void Sell(string itemId, int price)
		{
			if (Inventory.Instance.RemoveItem(itemId, 1))
			{
				GameManager.Instance.AddGold(price);
				_status.Text = Loc.T("shop.sell_success");
			}
			RefreshBuy();
			RefreshSell();
			UpdateStatus();
		}
	}
}
