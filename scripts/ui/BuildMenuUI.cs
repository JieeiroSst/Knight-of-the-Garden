using Godot;
using System.Linq;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;

namespace HiepSiVeVuon.UI
{
    // Bang xay dung (nhan [N] de bat/tat) - liet ke cac cong trinh CO THE xay (xem
    // BuildingCatalog.cs), can DU vat lieu trong tui do moi xay duoc. Xay xong dat NGAY truoc
    // mat nguoi choi (khong co che do "kien truc su di chuyen ban tay ma" - don gian hoa, giong
    // tinh than dat May Tuoi Tu Dong/cuoc dat tu do da co).
    public partial class BuildMenuUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _status;

        public override void _Ready()
        {
            AddToGroup("build_menu_ui");

            var panel = new Panel { Position = new Vector2(240, 50), CustomMinimumSize = new Vector2(460, 440) };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.97f),
                BorderColor = new Color(0.75f, 0.62f, 0.35f),
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            });
            AddChild(panel);

            var vb = new VBoxContainer { Position = new Vector2(20, 16), CustomMinimumSize = new Vector2(420, 0) };
            panel.AddChild(vb);

            var title = new Label { Text = "BANG XAY DUNG" };
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 18);
            vb.AddChild(title);

            var sub = new Label { Text = "Chon cong trinh - se dat ngay truoc mat ban, can du vat lieu trong tui do." };
            sub.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            sub.AutowrapMode = TextServer.AutowrapMode.Word;
            sub.CustomMinimumSize = new Vector2(420, 0);
            vb.AddChild(sub);
            vb.AddChild(new HSeparator());

            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(420, 300) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(410, 0) };
            _list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_list);

            _status = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(420, 0) };
            _status.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.4f));
            vb.AddChild(_status);

            var close = new Button { Text = "Dong [N]" };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);

            Visible = false;
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_build_menu"))
            {
                Visible = !Visible;
                if (Visible) Refresh();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Refresh()
        {
            foreach (Node c in _list.GetChildren()) c.QueueFree();
            _status.Text = "";

            foreach (var def in BuildingCatalog.Entries)
            {
                bool canBuild = def.Cost.All(kv => Inventory.Instance.CountOf(kv.Key) >= kv.Value);
                string costText = string.Join(", ", def.Cost.Select(kv =>
                {
                    var itemDef = ItemDatabase.Instance.GetItem(kv.Key);
                    return $"{itemDef?.Name ?? kv.Key} x{kv.Value}";
                }));

                var row = new VBoxContainer();
                _list.AddChild(row);

                var nameLabel = new Label { Text = def.Name };
                nameLabel.AddThemeColorOverride("font_color", canBuild ? new Color(0.95f, 0.92f, 0.85f) : new Color(0.6f, 0.58f, 0.55f));
                row.AddChild(nameLabel);

                var costLabel = new Label { Text = $"Can: {costText}" };
                costLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.78f, 0.5f, 0.75f));
                costLabel.AddThemeFontSizeOverride("font_size", 12);
                row.AddChild(costLabel);

                var btn = new Button { Text = canBuild ? "Xay" : "Thieu vat lieu", Disabled = !canBuild };
                var d = def;
                btn.Pressed += () => Build(d);
                row.AddChild(btn);

                row.AddChild(new HSeparator());
            }
        }

        private void Build(BuildingDef def)
        {
            foreach (var kv in def.Cost)
                if (Inventory.Instance.CountOf(kv.Key) < kv.Value) { Refresh(); return; }

            var player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (player == null) { _status.Text = "Khong tim thay nguoi choi."; return; }

            foreach (var kv in def.Cost)
                Inventory.Instance.RemoveItem(kv.Key, kv.Value);

            Vector3 pos = player.GlobalPosition + player.Facing * (def.FootprintRadius + 40f);
            PlacedBuilding.Spawn(def, pos, GetTree().CurrentScene);

            _status.Text = $"Da xay: {def.Name}.";
            Refresh();
        }
    }
}
