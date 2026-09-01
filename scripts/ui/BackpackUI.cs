using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
    // Balo (tui do thu hai, 50 o - xem Backpack.cs). Nhan [B] de bat/tat. Bam vat pham ben Tui Do
    // de CAT vao balo, bam vat pham ben Balo de LAY ra tui do - moi lan bam chuyen 1 cai, giong
    // quy uoc Mua/Ban trong ShopUI (khong tu dong chuyen ca stack).
    public partial class BackpackUI : CanvasLayer
    {
        private PanelContainer _panel;
        private GridContainer _invGrid;
        private GridContainer _bagGrid;
        private Label _info;

        public override void _Ready()
        {
            Build();
            Inventory.Instance.InventoryChanged += Refresh;
            Backpack.Instance.BackpackChanged += Refresh;
            Visible = false;
        }

        private void Build()
        {
            _panel = new PanelContainer();
            _panel.Position = new Vector2(240, 60);
            _panel.CustomMinimumSize = new Vector2(480, 460);
            AddChild(_panel);

            var vb = new VBoxContainer();
            _panel.AddChild(vb);

            vb.AddChild(new Label { Text = "== BALO ==" });

            vb.AddChild(new Label { Text = $"-- Tui do (bam de cat vao balo) --" });
            _invGrid = new GridContainer { Columns = 8 };
            vb.AddChild(_invGrid);

            vb.AddChild(new Label { Text = $"-- Balo, {Backpack.MaxSlots} o (bam de lay ra tui do) --" });
            _bagGrid = new GridContainer { Columns = 8 };
            vb.AddChild(_bagGrid);

            _info = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            _info.CustomMinimumSize = new Vector2(460, 40);
            vb.AddChild(_info);
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_backpack"))
            {
                Visible = !Visible;
                if (Visible) Refresh();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Refresh()
        {
            foreach (Node c in _invGrid.GetChildren()) c.QueueFree();
            foreach (var stack in Inventory.Instance.Slots)
            {
                string id = stack.ItemId;
                var btn = MakeSlotButton(id, stack.Count);
                btn.Pressed += () => DepositToBackpack(id);
                _invGrid.AddChild(btn);
            }

            foreach (Node c in _bagGrid.GetChildren()) c.QueueFree();
            foreach (var stack in Backpack.Instance.Slots)
            {
                string id = stack.ItemId;
                var btn = MakeSlotButton(id, stack.Count);
                btn.Pressed += () => WithdrawFromBackpack(id);
                _bagGrid.AddChild(btn);
            }
        }

        private Button MakeSlotButton(string itemId, int count)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            var btn = new Button { CustomMinimumSize = new Vector2(48, 48) };
            var tex = ItemDatabase.Instance.GetItemIcon(itemId);
            if (tex != null) { btn.Icon = tex; btn.ExpandIcon = true; }
            btn.Text = count > 1 ? $"x{count}" : "";
            btn.MouseEntered += () => ShowInfo(def, count);
            return btn;
        }

        private void ShowInfo(ItemDef def, int count)
        {
            if (def == null) return;
            _info.Text = $"{def.Name} [{def.Rarity}] x{count}\n{def.Description}";
        }

        // LUON them vao noi DEN truoc, chi bo khoi noi DI neu them thanh cong - neu lam nguoc lai
        // (bo truoc, them sau) va noi den vua luc day, vat pham se BIEN MAT khoi ca 2 tui.
        private void DepositToBackpack(string itemId)
        {
            if (!Backpack.Instance.AddItem(itemId, 1)) { _info.Text = "Balo da day!"; return; }
            Inventory.Instance.RemoveItem(itemId, 1);
        }

        private void WithdrawFromBackpack(string itemId)
        {
            if (!Inventory.Instance.AddItem(itemId, 1)) { _info.Text = "Tui do da day!"; return; }
            Backpack.Instance.RemoveItem(itemId, 1);
        }
    }
}
