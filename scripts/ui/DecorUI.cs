using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Trang tri [L] (V = doi goc nhin camera): danh sach do trang tri dang co trong tui -> chon huong (0/90/180/270 do) -> "Dat"
    // ngay truoc mat (trong nha hay ngoai vuon deu duoc). Bam [E] vao do da dat de nhat lai.
    public partial class DecorUI : CanvasLayer
    {
        private VBoxContainer _list;
        private int _rot;

        public override void _Ready()
        {
            AddToGroup("decor_ui");
            var panel = UiStyle.MakePanel(new Vector2(250, 50), new Vector2(460, 440));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 12), CustomMinimumSize = new Vector2(424, 0) };
            panel.AddChild(vb);
            vb.AddChild(UiStyle.Title(Loc.T("decor.title")));
            vb.AddChild(UiStyle.Small(Loc.T("decor.hint")));
            var rotRow = new HBoxContainer();
            vb.AddChild(rotRow);
            rotRow.AddChild(new Label { Text = Loc.T("decor.rotation") });
            foreach (var deg in new[] { 0, 90, 180, 270 })
            {
                var b = new Button { Text = $"{deg}°", ToggleMode = true, ButtonPressed = deg == 0 };
                int d = deg;
                b.Pressed += () => { _rot = d; foreach (Node c in rotRow.GetChildren()) if (c is Button bb) bb.ButtonPressed = bb == b; };
                rotRow.AddChild(b);
            }
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(424, 300) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(410, 0) };
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (ChatUI.IsTyping) return;
            if (e.IsActionPressed("toggle_decor")) { Visible = !Visible; if (Visible) Refresh(); GetViewport().SetInputAsHandled(); }
            else if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        private void Refresh()
        {
            UiStyle.Clear(_list);
            var db = ItemDatabase.Instance;
            var owned = Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().Where(id => DecorCatalog.Models.ContainsKey(id)).ToList();
            if (owned.Count == 0) _list.AddChild(UiStyle.Small(Loc.T("decor.none")));
            foreach (var id in owned)
            {
                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new TextureRect { Texture = db.GetItemIcon(id), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                row.AddChild(new Label { Text = $"{db.GetDisplayName(id)} x{Inventory.Instance.CountOf(id)}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
                var place = new Button { Text = Loc.T("decor.place") };
                place.Pressed += () =>
                {
                    if (GetTree().GetFirstNodeInGroup("player") is not Player p) return;
                    var pos = p.GlobalPosition + p.Facing * 55f;
                    pos.Y = p.GlobalPosition.Y;
                    PlacementSystem.Instance.Place(id, pos, Mathf.RadToDeg(p.Yaw) + _rot, GetTree().CurrentScene);
                };
                row.AddChild(place);
            }
        }
    }
}
