using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Man hinh Cai Dat / Huong Dan - truoc day danh sach phim tat nam CO DINH tren HUD (luon
    // choan 1 dong duoi man hinh moi luc choi), nay CHUYEN VAO DAY (bam [H] de bat/tat, giong
    // MapUI/ShopUI/InventoryUI) - man hinh choi sach hon, huong dan van xem duoc bat ky luc nao.
    // Cung la NOI DUY NHAT co nut doi ngon ngu VI/EN (xem Loc.cs) - doi ngay lap tuc, khong can
    // khoi dong lai.
    public partial class SettingsUI : CanvasLayer
    {
        // Chi con KHOA phim tat (khong phai chuoi mo ta truc tiep) - noi dung that lay qua Loc.T
        // luc dung, tu doi theo ngon ngu dang chon.
        private static readonly (string key, string actionKey)[] Controls =
        {
            ("WASD", "settings.ctrl.move"),
            ("Chuot / J", "settings.ctrl.attack"),
            ("Space", "settings.ctrl.tool"),
            ("E", "settings.ctrl.interact"),
            ("R", "settings.ctrl.mount"),
            ("I", "settings.ctrl.inventory"),
            ("B", "settings.ctrl.backpack"),
            ("N", "settings.ctrl.build"),
            ("M", "settings.ctrl.map"),
            ("H", "settings.ctrl.settings"),
            ("F5", "settings.ctrl.save"),
        };

        private readonly LocalizedLabelSet _loc = new();
        private Button _viBtn, _enBtn;

        public override void _Ready()
        {
            AddToGroup("settings_ui");

            var panel = new Panel
            {
                Position = new Vector2(260, 50),
                CustomMinimumSize = new Vector2(420, 480),
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

            var title = new Label();
            _loc.Track(title, "settings.title");
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 20);
            vb.AddChild(title);

            vb.AddChild(new HSeparator());

            // Chuyen doi ngon ngu (theo yeu cau) - 2 nut, nut dang CHON duoc to sang, nut kia mo.
            var langHeader = new Label();
            _loc.Track(langHeader, "settings.language_header");
            langHeader.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            vb.AddChild(langHeader);

            var langRow = new HBoxContainer();
            vb.AddChild(langRow);
            _viBtn = new Button { Text = "Tiếng Việt" };
            _viBtn.Pressed += () => SetLanguage(Loc.Lang.VI);
            langRow.AddChild(_viBtn);
            _enBtn = new Button { Text = "English" };
            _enBtn.Pressed += () => SetLanguage(Loc.Lang.EN);
            langRow.AddChild(_enBtn);
            RefreshLanguageButtons();

            vb.AddChild(new HSeparator());

            var sub = new Label();
            _loc.Track(sub, "settings.shortcuts_header");
            sub.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            vb.AddChild(sub);

            // Danh sach phim tat gio da dai hon 420x360 goc (them Balo/Cuoc dat) - cuon RIENG
            // trong 1 vung cao co dinh thay vi de tran ra ngoai khung panel.
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(372, 280) };
            vb.AddChild(scroll);
            var listVb = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
            scroll.AddChild(listVb);

            foreach (var (key, actionKey) in Controls)
            {
                var row = new HBoxContainer();
                listVb.AddChild(row);

                var keyLabel = new Label { Text = $"[{key}]", CustomMinimumSize = new Vector2(90, 0) };
                keyLabel.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.15f));
                row.AddChild(keyLabel);

                var actionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(260, 0) };
                _loc.Track(actionLabel, actionKey);
                actionLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f));
                row.AddChild(actionLabel);
            }

            var close = new Button();
            _loc.Track(close, "settings.close_btn");
            close.Pressed += () => Visible = false;
            vb.AddChild(close);

            Loc.LanguageChanged += OnLanguageChanged;

            Visible = false;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void SetLanguage(Loc.Lang lang)
        {
            Loc.SetLanguage(lang);
            RefreshLanguageButtons();
        }

        private void OnLanguageChanged()
        {
            _loc.Refresh();
            RefreshLanguageButtons();
        }

        private void RefreshLanguageButtons()
        {
            _viBtn.Disabled = Loc.Current == Loc.Lang.VI;
            _enBtn.Disabled = Loc.Current == Loc.Lang.EN;
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
