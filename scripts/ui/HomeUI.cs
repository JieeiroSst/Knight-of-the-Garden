using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Nha cua toi: 2 che do - Nang Cap Nha (cap/chi phi/loi ich + do vui vat nuoi) va Ruong Chua Do
    // (trai = tui do -> ruong, phai = ruong -> tui do).
    public partial class HomeUI : CanvasLayer
    {
        public enum Mode { Upgrade, Chest }
        private Mode _mode;
        private VBoxContainer _left, _right;
        private Label _title, _info;
        private HBoxContainer _cols;

        public override void _Ready()
        {
            AddToGroup("home_ui");
            var panel = UiStyle.MakePanel(new Vector2(150, 40), new Vector2(660, 460));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(620, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            _info = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(620, 0) };
            _info.AddThemeColorOverride("font_color", UiStyle.Text);
            vb.AddChild(_info);
            vb.AddChild(new HSeparator());
            _cols = new HBoxContainer();
            _cols.AddThemeConstantOverride("separation", 16);
            vb.AddChild(_cols);
            _left = Column(); _right = Column();
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
        }

        private VBoxContainer Column()
        {
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(302, 300) };
            _cols.AddChild(scroll);
            var list = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
            scroll.AddChild(list);
            return list;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open(Mode mode)
        {
            _mode = mode;
            Refresh();
            Visible = true;
        }

        private void Refresh()
        {
            UiStyle.Clear(_left);
            UiStyle.Clear(_right);
            var home = HomeSystem.Instance;
            if (_mode == Mode.Upgrade) BuildUpgrade(home); else BuildChest(home);
        }

        private void BuildUpgrade(HomeSystem home)
        {
            _title.Text = Loc.T("home.title");
            _info.Text = string.Format(Loc.T("home.level_fmt"), home.HouseLevel, HomeSystem.MaxHouseLevel, (int)home.StaminaBonus, home.ChestCapacity)
                + "\n" + string.Format(Loc.T("home.herd_fmt"), (int)home.HerdHappiness, home.HappinessLabel);
            if (home.HouseLevel >= HomeSystem.MaxHouseLevel) { _left.AddChild(UiStyle.Small(Loc.T("home.max_level"), UiStyle.Gold)); return; }
            var (gold, mats) = HomeSystem.Upgrades[home.HouseLevel];
            _left.AddChild(UiStyle.Small(string.Format(Loc.T("home.next_fmt"), home.HouseLevel + 1), UiStyle.Gold));
            _left.AddChild(UiStyle.Small($"- {gold}g ({GameManager.Instance.Gold}g)", UiStyle.Text));
            foreach (var (id, count) in mats)
                _left.AddChild(UiStyle.Small($"- {ItemDatabase.Instance.GetDisplayName(id)} {Inventory.Instance.CountOf(id)}/{count}", UiStyle.Text));
            _left.AddChild(UiStyle.Small(string.Format(Loc.T("home.benefit_fmt"), (int)HomeSystem.StaminaPerLevel, HomeSystem.ChestSlotsPerLevel)));
            var btn = new Button { Text = Loc.T("home.upgrade_btn"), Disabled = !home.CanUpgrade(out _) };
            btn.Pressed += () => { home.Upgrade(); Refresh(); };
            _left.AddChild(btn);
        }

        private void BuildChest(HomeSystem home)
        {
            _title.Text = Loc.T("home.chest_title");
            _info.Text = string.Format(Loc.T("home.chest_info_fmt"), home.Chest.Count, home.ChestCapacity);
            var db = ItemDatabase.Instance;
            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
            {
                var row = new HBoxContainer();
                _left.AddChild(row);
                row.AddChild(new Label { Text = $"{db.GetDisplayName(id)} x{Inventory.Instance.CountOf(id)}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true });
                var b = new Button { Text = Loc.T("home.store_btn") };
                b.Pressed += () => { home.Deposit(id, Inventory.Instance.CountOf(id)); Refresh(); };
                row.AddChild(b);
            }
            foreach (var e in home.Chest.ToList())
            {
                var row = new HBoxContainer();
                _right.AddChild(row);
                row.AddChild(new Label { Text = $"{db.GetDisplayName(e.Id)} x{e.Count}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true });
                var b = new Button { Text = Loc.T("home.take_btn") };
                string id = e.Id;
                b.Pressed += () => { home.Withdraw(id); Refresh(); };
                row.AddChild(b);
            }
        }
    }
}
