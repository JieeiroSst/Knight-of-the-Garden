using Godot;
using System.Linq;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Bao Tang: danh sach hien vat suu tam (ItemDef.Collectible) - da quyen / co trong tui (quyen
    // duoc) / chua tim thay (an ten). Moc thuong gui qua thu (xem AdventureSystem.Donate).
    public partial class MuseumUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _title, _progress;

        public override void _Ready()
        {
            AddToGroup("museum_ui");
            var panel = UiStyle.MakePanel(new Vector2(220, 40), new Vector2(520, 460));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(480, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            _progress = UiStyle.Small("");
            vb.AddChild(_progress);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(480, 320) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(465, 0) };
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
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

        private void Refresh()
        {
            var adv = AdventureSystem.Instance;
            var db = ItemDatabase.Instance;
            var all = AdventureSystem.AllCollectibles.ToList();
            _title.Text = Loc.T("museum.title");
            int next = adv.MuseumRewardsClaimed < AdventureSystem.MuseumRewards.Length ? AdventureSystem.MuseumRewards[adv.MuseumRewardsClaimed].count : -1;
            _progress.Text = string.Format(Loc.T("museum.progress_fmt"), adv.MuseumDonated.Count, all.Count)
                + (next > 0 ? "  " + string.Format(Loc.T("museum.next_reward_fmt"), next) : "");
            UiStyle.Clear(_list);
            foreach (var d in all)
            {
                bool donated = adv.MuseumDonated.Contains(d.Id);
                bool have = Inventory.Instance.CountOf(d.Id) > 0;
                var row = new HBoxContainer();
                _list.AddChild(row);
                var icon = new TextureRect
                {
                    Texture = donated || have ? db.GetItemIcon(d.Id) : null, CustomMinimumSize = new Vector2(28, 28),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                row.AddChild(icon);
                var name = new Label { Text = donated || have ? db.GetDisplayName(d.Id) : "???", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                name.AddThemeColorOverride("font_color", donated ? UiStyle.Gold : have ? UiStyle.Text : UiStyle.Dim);
                row.AddChild(name);
                if (donated) row.AddChild(UiStyle.Small(Loc.T("museum.on_display"), UiStyle.Gold));
                else if (have)
                {
                    var b = new Button { Text = Loc.T("museum.donate_btn") };
                    var id = d.Id;
                    b.Pressed += () => { adv.Donate(id); Refresh(); };
                    row.AddChild(b);
                }
            }
        }
    }
}
