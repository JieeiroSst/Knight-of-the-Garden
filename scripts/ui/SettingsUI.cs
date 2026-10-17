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
            ("G", "settings.ctrl.gift"),
            ("K", "settings.ctrl.journal"),
            ("I", "settings.ctrl.inventory"),
            ("B", "settings.ctrl.backpack"),
            ("N", "settings.ctrl.build"),
            ("M", "settings.ctrl.map"),
            ("H", "settings.ctrl.settings"),
            ("F1", "settings.ctrl.guide"),
            ("F5", "settings.ctrl.save"),
            ("Enter", "settings.ctrl.chat"),
            ("O", "settings.ctrl.social"),
            ("L", "settings.ctrl.decor"),
            ("C", "settings.ctrl.wardrobe"),
            ("1 / 2 / 3", "settings.ctrl.abilities"),
            ("T", "settings.ctrl.voice"),
            ("Chuot / V", "settings.ctrl.camera"),
            ("Space", "settings.ctrl.gallop"),
            ("/toado", "settings.ctrl.coords"),
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
            BuildGraphicsSection(listVb);
            BuildVoiceSection(listVb);
            listVb.AddChild(new HSeparator());

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

        // ==== Do hoa ====
        private readonly Button[] _qualityBtns = new Button[4];
        private Label _fpsLabel;
        private static readonly (string key, int fps, bool vsync)[] FpsOptions =
        {
            ("gfx.fps_vsync", 0, true), ("gfx.fps_30", 30, false), ("gfx.fps_60", 60, false), ("gfx.fps_120", 120, false), ("gfx.fps_unlimited", 0, false),
        };

        private Label Header(string key)
        {
            var l = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(350, 0) };
            _loc.Track(l, key);
            l.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            return l;
        }

        private void BuildGraphicsSection(VBoxContainer vb)
        {
            vb.AddChild(Header("gfx.header"));
            var row = new HBoxContainer();
            vb.AddChild(row);
            string[] keys = { "gfx.low", "gfx.medium", "gfx.high", "gfx.ultra" };
            for (int i = 0; i < 4; i++)
            {
                var q = (Core.GraphicsSettings.Quality)i;
                var b = new Button { ToggleMode = true };
                _loc.Track(b, keys[i]);
                b.Pressed += () => { Core.GraphicsSettings.Set(q, GetTree()); RefreshGraphics(); };
                row.AddChild(b);
                _qualityBtns[i] = b;
            }

            var fpsRow = new HBoxContainer();
            vb.AddChild(fpsRow);
            var fpsLbl = new Label { CustomMinimumSize = new Vector2(120, 0) };
            _loc.Track(fpsLbl, "gfx.fps_label");
            fpsRow.AddChild(fpsLbl);
            var fps = new OptionButton();
            foreach (var (key, _, _) in FpsOptions) fps.AddItem(Loc.T(key));
            int sel = System.Array.FindIndex(FpsOptions, o => o.fps == Core.GraphicsSettings.MaxFps && o.vsync == Core.GraphicsSettings.VSync);
            fps.Selected = sel < 0 ? 0 : sel;
            fps.ItemSelected += idx => Core.GraphicsSettings.SetFrameLimit(FpsOptions[idx].fps, FpsOptions[idx].vsync, GetTree());
            fpsRow.AddChild(fps);
            _fpsLabel = new Label();
            _fpsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
            fpsRow.AddChild(_fpsLabel);
            RefreshGraphics();
        }

        private void RefreshGraphics()
        {
            for (int i = 0; i < 4; i++) _qualityBtns[i].ButtonPressed = (int)Core.GraphicsSettings.Current == i;
        }

        // ==== Giong noi ====
        private ProgressBar _micBar;

        private void BuildVoiceSection(VBoxContainer vb)
        {
            var voice = VoiceChat.Instance;
            if (voice == null) return;
            vb.AddChild(new HSeparator());
            vb.AddChild(Header("voice.header"));

            var mode = new OptionButton();
            mode.AddItem(Loc.T("voice.mode_off"));
            mode.AddItem(Loc.T("voice.mode_ptt"));
            mode.AddItem(Loc.T("voice.mode_open"));
            mode.Selected = (int)voice.CurrentMode;
            mode.ItemSelected += idx => voice.SetMode((VoiceChat.Mode)idx);
            vb.AddChild(mode);

            vb.AddChild(Header("voice.volume"));
            var vol = new HSlider { MinValue = 0, MaxValue = 1.5, Step = 0.05, Value = voice.OutputVolume, CustomMinimumSize = new Vector2(330, 0) };
            vol.ValueChanged += v => voice.SetVolume((float)v);
            vb.AddChild(vol);

            vb.AddChild(Header("voice.threshold"));
            var thr = new HSlider { MinValue = 0.002, MaxValue = 0.12, Step = 0.002, Value = voice.MicThreshold, CustomMinimumSize = new Vector2(330, 0) };
            thr.ValueChanged += v => voice.SetThreshold((float)v);
            vb.AddChild(thr);

            var levelRow = new HBoxContainer();
            vb.AddChild(levelRow);
            var lvlLbl = new Label { CustomMinimumSize = new Vector2(80, 0) };
            _loc.Track(lvlLbl, "voice.mic_level");
            levelRow.AddChild(lvlLbl);
            _micBar = new ProgressBar { MinValue = 0, MaxValue = 0.12, ShowPercentage = false, CustomMinimumSize = new Vector2(250, 14) };
            levelRow.AddChild(_micBar);

            var muteAll = new CheckBox { ButtonPressed = voice.MuteAll };
            _loc.Track(muteAll, "voice.mute_all");
            muteAll.Toggled += on => voice.SetMuteAll(on);
            vb.AddChild(muteAll);
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if (_fpsLabel != null) _fpsLabel.Text = "  " + string.Format(Loc.T("gfx.fps_show_fmt"), Engine.GetFramesPerSecond());
            if (_micBar != null && VoiceChat.Instance != null) _micBar.Value = VoiceChat.Instance.MicLevel;
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
