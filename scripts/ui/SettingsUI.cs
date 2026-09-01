using Godot;

namespace HiepSiVeVuon.UI
{
    // Man hinh Cai Dat / Huong Dan - truoc day danh sach phim tat nam CO DINH tren HUD (luon
    // choan 1 dong duoi man hinh moi luc choi), nay CHUYEN VAO DAY (bam [H] de bat/tat, giong
    // MapUI/ShopUI/InventoryUI) - man hinh choi sach hon, huong dan van xem duoc bat ky luc nao.
    public partial class SettingsUI : CanvasLayer
    {
        private static readonly (string key, string action)[] Controls =
        {
            ("WASD", "Di chuyen"),
            ("Chuot / J", "Tan cong"),
            ("Space", "Dung cong cu (cuoc dat moi/trong/tuoi/thu hoach/cuoc quang/cau ca/dat may tuoi tu dong - cuoc bac/vang tac dong ca vung)"),
            ("E", "Tuong tac / Mo cua / Cau thang / Sua thap nuoc / Cho vit an / May che bien / Bep / Cong Nha Kinh"),
            ("R", "Cuoi / Xuong ngua hoac thuyen"),
            ("I", "Tui do"),
            ("B", "Balo (kho chua them 50 o - chuyen do qua lai voi tui do)"),
            ("N", "Bang Xay Dung (xay nha/chuong/thap canh... can vat lieu go/da/sat/dong)"),
            ("M", "Ban do the gioi"),
            ("H", "Cai dat / Huong dan (man hinh nay)"),
            ("F5", "Luu game"),
        };

        public override void _Ready()
        {
            AddToGroup("settings_ui");

            var panel = new Panel
            {
                Position = new Vector2(260, 60),
                CustomMinimumSize = new Vector2(420, 440),
            };
            var frameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.95f),
                BorderColor = new Color(0.75f, 0.62f, 0.35f),
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            };
            panel.AddThemeStyleboxOverride("panel", frameStyle);
            AddChild(panel);

            var vb = new VBoxContainer { Position = new Vector2(24, 20), CustomMinimumSize = new Vector2(372, 0) };
            panel.AddChild(vb);

            var title = new Label { Text = "CAI DAT  /  HUONG DAN" };
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 20);
            vb.AddChild(title);

            vb.AddChild(new HSeparator());

            var sub = new Label { Text = "Phim tat" };
            sub.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            vb.AddChild(sub);

            // Danh sach phim tat gio da dai hon 420x360 goc (them Balo/Cuoc dat) - cuon RIENG
            // trong 1 vung cao co dinh thay vi de tran ra ngoai khung panel.
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(372, 280) };
            vb.AddChild(scroll);
            var listVb = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
            scroll.AddChild(listVb);

            foreach (var (key, action) in Controls)
            {
                var row = new HBoxContainer();
                listVb.AddChild(row);

                var keyLabel = new Label { Text = $"[{key}]", CustomMinimumSize = new Vector2(90, 0) };
                keyLabel.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.15f));
                row.AddChild(keyLabel);

                var actionLabel = new Label { Text = action, AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(260, 0) };
                actionLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f));
                row.AddChild(actionLabel);
            }

            var close = new Button { Text = "Dong [H]" };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);

            Visible = false;
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_settings"))
            {
                Visible = !Visible;
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
