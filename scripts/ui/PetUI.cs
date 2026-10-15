using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Trai Thu Cung (thi tran): nhan nuoi (mua the qua EconomyClient.Buy - server tru vang theo gia),
    // chon con di theo / cat di.
    public partial class PetUI : CanvasLayer
    {
        private VBoxContainer _list;

        public override void _Ready()
        {
            AddToGroup("pet_ui");
            var panel = UiStyle.MakePanel(new Vector2(240, 50), new Vector2(480, 440));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 12), CustomMinimumSize = new Vector2(444, 0) };
            panel.AddChild(vb);
            vb.AddChild(UiStyle.Title(Loc.T("pet.title")));
            vb.AddChild(UiStyle.Small(Loc.T("pet.hint")));
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(444, 320) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
            _list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            PetSystem.Instance.Changed += () => { if (Visible) Refresh(); };
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open() { Visible = true; Refresh(); }

        private void Refresh()
        {
            UiStyle.Clear(_list);
            var ps = PetSystem.Instance;
            var db = ItemDatabase.Instance;
            foreach (var (id, pet) in PetCatalog.Pets)
            {
                var def = db.GetItem(id);
                bool owned = ps.Owned.Contains(id);
                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new TextureRect { Texture = db.GetItemIcon(id), CustomMinimumSize = new Vector2(36, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(info);
                info.AddChild(new Label { Text = PetSystem.DisplayName(id) + (owned ? $"  Lv{ps.Level(id)}" : "") });
                info.AddChild(UiStyle.Small(string.Format(Loc.T("pet.stats_fmt"), pet.BaseDamage, pet.Flying ? Loc.T("pet.flying") : Loc.T("pet.walking"))));
                if (!owned)
                {
                    var buy = new Button { Text = string.Format(Loc.T("pet.adopt_fmt"), def?.BuyPrice ?? 0), Disabled = GameManager.Instance.Gold < (def?.BuyPrice ?? 0) };
                    buy.Pressed += () => { if (Inventory.Instance.CountOf(id) > 0 || EconomyClient.Instance.Buy(id, 1)) ps.Adopt(id); };
                    row.AddChild(buy);
                }
                else if (ps.Active == id)
                {
                    var off = new Button { Text = Loc.T("pet.dismiss") };
                    off.Pressed += () => ps.SetActive(null);
                    row.AddChild(off);
                }
                else
                {
                    var on = new Button { Text = Loc.T("pet.bring") };
                    on.Pressed += () => ps.SetActive(id);
                    row.AddChild(on);
                }
            }
        }
    }
}
