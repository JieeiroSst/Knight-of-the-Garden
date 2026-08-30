using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
	// Cua hang: mua hat giong/do dung, ban nong san. Kinh te co gia mua/ban.
	public partial class ShopUI : CanvasLayer
	{
		private PanelContainer _panel;
		private VBoxContainer _buyList;
		private VBoxContainer _sellList;
		private Label _status;
		private NPC _current;

		public override void _Ready()
		{
			AddToGroup("shop_ui");
			Build();
			Visible = false;
		}

		private void Build()
		{
			_panel = new PanelContainer();
			_panel.Position = new Vector2(300, 60);
			_panel.CustomMinimumSize = new Vector2(360, 400);
			AddChild(_panel);

			var vb = new VBoxContainer();
			_panel.AddChild(vb);
			var title = new Label { Text = "== CUA HANG ==" };
			vb.AddChild(title);
			_status = new Label();
			vb.AddChild(_status);

			vb.AddChild(new Label { Text = "-- Mua --" });
			_buyList = new VBoxContainer();
			vb.AddChild(_buyList);

			vb.AddChild(new Label { Text = "-- Ban nong san --" });
			_sellList = new VBoxContainer();
			vb.AddChild(_sellList);

			var close = new Button { Text = "Dong" };
			close.Pressed += () => Visible = false;
			vb.AddChild(close);
		}

		public void Open(NPC npc)
		{
			_current = npc;
			Visible = true;
			RefreshBuy();
			RefreshSell();
			UpdateStatus();
		}

		private void UpdateStatus() => _status.Text = $"Vang cua ban: {GameManager.Instance.Gold}";

		private void RefreshBuy()
		{
			foreach (Node c in _buyList.GetChildren()) c.QueueFree();
			if (_current == null) return;
			foreach (var id in _current.ShopItems)
			{
				var def = ItemDatabase.Instance.GetItem(id);
				if (def == null) continue;
				var btn = new Button { Text = $"{def.Name} - {def.BuyPrice} vang" };
				string itemId = id;
				int price = def.BuyPrice;
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
				var btn = new Button { Text = $"Ban {def.Name} x{stack.Count} ({def.SellPrice}/cai)" };
				string itemId = stack.ItemId;
				int price = def.SellPrice;
				btn.Pressed += () => Sell(itemId, price);
				_sellList.AddChild(btn);
			}
		}

		private void Buy(string itemId, int price)
		{
			if (GameManager.Instance.SpendGold(price))
			{
				Inventory.Instance.AddItem(itemId, 1);
				_status.Text = "Mua thanh cong!";
			}
			else _status.Text = "Khong du vang!";
			UpdateStatus();
			RefreshSell();
		}

		private void Sell(string itemId, int price)
		{
			if (Inventory.Instance.RemoveItem(itemId, 1))
			{
				GameManager.Instance.AddGold(price);
				_status.Text = "Ban thanh cong!";
			}
			RefreshSell();
			UpdateStatus();
		}
	}
}
