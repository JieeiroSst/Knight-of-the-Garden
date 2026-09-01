using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
    // Tui do dang luoi. Nhan I de bat/tat. Bam vao vat pham de trang bi/dung.
    public partial class InventoryUI : CanvasLayer
    {
        private PanelContainer _panel;
        private GridContainer _grid;
        private Label _info;

        public override void _Ready()
        {
            Build();
            Inventory.Instance.InventoryChanged += Refresh;
            Visible = false;
        }

        private void Build()
        {
            _panel = new PanelContainer();
            _panel.Position = new Vector2(280, 90);
            _panel.CustomMinimumSize = new Vector2(400, 340);
            AddChild(_panel);

            var vb = new VBoxContainer();
            _panel.AddChild(vb);
            var title = new Label { Text = "== TUI DO ==  (bam de trang bi / dung)" };
            vb.AddChild(title);
            _grid = new GridContainer { Columns = 6 };
            vb.AddChild(_grid);
            _info = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            _info.CustomMinimumSize = new Vector2(380, 40);
            vb.AddChild(_info);
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_inventory"))
            {
                Visible = !Visible;
                if (Visible) Refresh();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Refresh()
        {
            foreach (Node c in _grid.GetChildren()) c.QueueFree();

            foreach (var stack in Inventory.Instance.Slots)
            {
                var def = ItemDatabase.Instance.GetItem(stack.ItemId);
                var btn = new Button();
                btn.CustomMinimumSize = new Vector2(56, 56);
                var tex = ItemDatabase.Instance.GetItemIcon(stack.ItemId);
                if (tex != null)
                {
                    btn.Icon = tex;
                    btn.ExpandIcon = true;
                }
                btn.Text = stack.Count > 1 ? $"x{stack.Count}" : "";
                string id = stack.ItemId;
                btn.Pressed += () => OnSlotClicked(id);
                btn.MouseEntered += () => ShowInfo(def);
                _grid.AddChild(btn);
            }
        }

        private void ShowInfo(ItemDef def)
        {
            if (def == null) return;
            _info.Text = $"{def.Name} [{def.Rarity}]\n{def.Description}";
            if (def.Damage > 0) _info.Text += $"\nSat thuong: {def.Damage}";
            if (def.Defense > 0) _info.Text += $"\nPhong thu: {def.Defense}";
            if (def.HealAmount > 0) _info.Text += $"\nHoi mau: {def.HealAmount}";
        }

        private void OnSlotClicked(string itemId)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (def == null) return;
            if (def.Type == ItemType.Weapon || def.Type == ItemType.Armor || def.Type == ItemType.Tool)
            {
                Inventory.Instance.Equip(itemId);
                _info.Text = $"Da trang bi: {def.Name}";
            }
            else if (def.Type == ItemType.Consumable)
            {
                Inventory.Instance.UseConsumable(itemId);
                _info.Text = $"Da dung: {def.Name}";
            }
            else
            {
                _info.Text = $"{def.Name}: khong the dung truc tiep.";
            }
        }
    }
}
