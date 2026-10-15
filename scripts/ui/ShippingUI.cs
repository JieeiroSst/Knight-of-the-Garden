using Godot;
using System.Linq;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Thung Giao Hang (xem ShippingSystem.cs): trai = hang trong tui do co the giao, phai = hang
    // da bo vao thung (lay lai duoc truoc khi qua ngay). Sang ngay moi tu dong ban het.
    public partial class ShippingUI : CanvasLayer
    {
        private VBoxContainer _invList, _binList;
        private Label _title, _total;

        public override void _Ready()
        {
            AddToGroup("shipping_ui");
            var panel = UiStyle.MakePanel(new Vector2(150, 40), new Vector2(660, 460));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(620, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            vb.AddChild(new HSeparator());

            var cols = new HBoxContainer();
            cols.AddThemeConstantOverride("separation", 16);
            vb.AddChild(cols);
            _invList = MakeColumn(cols);
            _binList = MakeColumn(cols);

            _total = new Label();
            _total.AddThemeColorOverride("font_color", UiStyle.Gold);
            vb.AddChild(_total);

            var buttons = new HBoxContainer();
            vb.AddChild(buttons);
            var shipCrops = new Button { Text = Loc.T("ship.all_crops_btn") };
            shipCrops.Pressed += ShipAllCrops;
            buttons.AddChild(shipCrops);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            buttons.AddChild(close);

            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
            ShippingSystem.Instance.BinChanged += () => { if (Visible) Refresh(); };
        }

        private static VBoxContainer MakeColumn(HBoxContainer parent)
        {
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(302, 330) };
            parent.AddChild(scroll);
            var list = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
            scroll.AddChild(list);
            return list;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open()
        {
            Refresh();
            Visible = true;
        }

        private void ShipAllCrops()
        {
            var ids = Inventory.Instance.Slots
                .Select(s => s.ItemId).Distinct()
                .Where(id => ItemDatabase.Instance.GetItem(id)?.Type == ItemType.Crop)
                .ToList();
            foreach (var id in ids) ShippingSystem.Instance.Ship(id, Inventory.Instance.CountOf(id));
        }

        private void Refresh()
        {
            var db = ItemDatabase.Instance;
            var ship = ShippingSystem.Instance;
            _title.Text = Loc.T("ship.title");
            UiStyle.Clear(_invList);
            UiStyle.Clear(_binList);
            _invList.AddChild(UiStyle.Small(Loc.T("ship.inventory_header"), UiStyle.Gold));
            _binList.AddChild(UiStyle.Small(Loc.T("ship.bin_header"), UiStyle.Gold));

            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
            {
                if (!ShippingSystem.CanShip(db.GetItem(id))) continue;
                int count = Inventory.Instance.CountOf(id);
                var row = new HBoxContainer();
                _invList.AddChild(row);
                var lbl = new Label { Text = $"{db.GetDisplayName(id)} x{count} ({ship.PriceOf(id)}g)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
                row.AddChild(lbl);
                var one = new Button { Text = "+1" };
                one.Pressed += () => ship.Ship(id, 1);
                row.AddChild(one);
                var all = new Button { Text = Loc.T("ship.all_btn") };
                all.Pressed += () => ship.Ship(id, Inventory.Instance.CountOf(id));
                row.AddChild(all);
            }

            foreach (var e in ship.Bin.ToList())
            {
                var row = new HBoxContainer();
                _binList.AddChild(row);
                row.AddChild(new Label { Text = $"{db.GetDisplayName(e.Id)} x{e.Count} = {ship.PriceOf(e.Id) * e.Count}g", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true });
                var back = new Button { Text = Loc.T("ship.take_back_btn") };
                string id = e.Id;
                back.Pressed += () => ship.TakeBack(id);
                row.AddChild(back);
            }

            _total.Text = string.Format(Loc.T("ship.total_fmt"), ship.EstimatedTotal);
        }
    }
}
