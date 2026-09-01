using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Man hinh dang nhap - la scene KHOI DAU cua game (thay Main.tscn, xem project.godot
    // run/main_scene) vi tu nay moi lan choi/luu deu can dang nhap qua backend truoc (khong con
    // luu file JSON local nua - xem BackendClient.cs). Dang ky/dang nhap thanh cong -> vao Main.tscn.
    //
    // Ban DAU dung Panel KICH THUOC CO DINH (400x300) + VBoxContainer dat vi tri thu cong ben
    // trong - noi dung (title/subtitle/2 truong nhap/2 nut) THUC TE cao hon 300, tran ra ngoai
    // khung ("be man hinh"). Viet lai dung PanelContainer TU DONG GIAN NO theo noi dung (khong
    // bao gio tran) + CenterContainer de luon can giua man hinh o moi do phan giai, thay vi
    // Position co dinh chi dung voi dung 1 kich thuoc thiet ke.
    public partial class LoginUI : Control
    {
        private LineEdit _username;
        private LineEdit _password;
        private Label _status;
        private Button _primaryBtn;
        private Button _toggleModeBtn;
        private bool _isRegisterMode = false;
        private readonly LocalizedLabelSet _loc = new();

        private static readonly Color GoldAccent = new(0.78f, 0.62f, 0.32f);
        private static readonly Color CreamText = new(0.95f, 0.92f, 0.85f);
        private static readonly Color ErrorColor = new(1f, 0.55f, 0.45f);

        public override void _Ready()
        {
            AddChild(BuildBackground());
            // Dai canh "trang trai" trang tri o day man hinh - dung LAI cac sprite pixel-art co
            // san cua game (khong ve/tai gi moi) thay vi de nen trong trai. Them TRUOC
            // CenterContainer nen ve DUOI khung dang nhap (Control ve theo thu tu con, con sau
            // de len tren con truoc).
            AddChild(BuildFarmSceneStrip());

            var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
            AddChild(center);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.08f, 0.06f, 0.97f),
                BorderColor = GoldAccent,
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
                ShadowSize = 18, ShadowColor = new Color(0, 0, 0, 0.55f),
                ContentMarginLeft = 34, ContentMarginRight = 34, ContentMarginTop = 30, ContentMarginBottom = 26,
            });
            center.AddChild(panel);

            var vb = new VBoxContainer();
            vb.AddThemeConstantOverride("separation", 10);
            panel.AddChild(vb);

            // "Chan dung" hiep si/nong dan: nhan vat chinh (player.png) giua, kiem+khien (sword.png/
            // shield.png) hai ben - dung DUNG cac sprite pixel-art game da co, dai dien ca hai chu
            // de "nong trai" va "hiep si" trong ten game ngay tren man hinh dang nhap.
            vb.AddChild(BuildHeroPortrait());

            var title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _loc.Track(title, "login.title");
            title.AddThemeColorOverride("font_color", GoldAccent);
            title.AddThemeFontSizeOverride("font_size", 26);
            vb.AddChild(title);

            var sub = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _loc.Track(sub, "login.subtitle");
            sub.AddThemeColorOverride("font_color", new Color(CreamText, 0.65f));
            sub.AddThemeFontSizeOverride("font_size", 13);
            vb.AddChild(sub);

            vb.AddChild(new HSeparator());

            vb.AddChild(_loc.Track(MakeFieldLabel(), "login.username_label"));
            _username = MakeStyledLineEdit();
            _loc.Track(_username, "login.username_placeholder");
            vb.AddChild(_username);

            vb.AddChild(_loc.Track(MakeFieldLabel(), "login.password_label"));
            _password = MakeStyledLineEdit(secret: true);
            _loc.Track(_password, "login.password_placeholder");
            _password.TextSubmitted += _ => Submit(_isRegisterMode);
            vb.AddChild(_password);

            vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) }); // dem nho truoc nut

            _primaryBtn = new Button { Text = Loc.T("login.btn_login"), CustomMinimumSize = new Vector2(0, 42) };
            StylePrimaryButton(_primaryBtn);
            _primaryBtn.Pressed += () => Submit(_isRegisterMode);
            vb.AddChild(_primaryBtn);

            _toggleModeBtn = new Button
            {
                Text = Loc.T("login.toggle_to_register"),
                Flat = true,
                CustomMinimumSize = new Vector2(0, 28),
            };
            _toggleModeBtn.AddThemeColorOverride("font_color", GoldAccent);
            _toggleModeBtn.AddThemeColorOverride("font_hover_color", new Color(0.9f, 0.75f, 0.45f));
            _toggleModeBtn.Pressed += ToggleMode;
            vb.AddChild(_toggleModeBtn);

            _status = new Label
            {
                Text = "",
                AutowrapMode = TextServer.AutowrapMode.Word,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(332, 0),
            };
            _status.AddThemeColorOverride("font_color", ErrorColor);
            vb.AddChild(_status);

            var footer = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.Word,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(332, 0),
            };
            _loc.Track(footer, "login.footer");
            footer.AddThemeColorOverride("font_color", new Color(CreamText, 0.4f));
            footer.AddThemeFontSizeOverride("font_size", 11);
            vb.AddChild(footer);

            Loc.LanguageChanged += OnLanguageChanged;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            _loc.Refresh();
            _primaryBtn.Text = Loc.T(_isRegisterMode ? "login.btn_register" : "login.btn_login");
            _toggleModeBtn.Text = Loc.T(_isRegisterMode ? "login.toggle_to_login" : "login.toggle_to_register");
        }

        // Nen gradient am (thay ColorRect 1 mau phang) - lay cam hung tu man hinh dang nhap cac
        // game nong trai/phieu luu khac (Stardew Valley, My Time at Portia...): mau AM/HOANG HON
        // goi khong khi nong trai, sang dan tu tren xuong duoi de panel giua man hinh noi bat.
        private static TextureRect BuildBackground()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(0.22f, 0.18f, 0.10f));
            gradient.AddPoint(0.55f, new Color(0.14f, 0.12f, 0.08f));
            gradient.SetColor(1, new Color(0.05f, 0.045f, 0.04f));
            var tex = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 2,
                Height = 540,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0.5f, 0f),
                FillTo = new Vector2(0.5f, 1f),
            };
            return new TextureRect
            {
                Texture = tex,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                AnchorRight = 1, AnchorBottom = 1,
            };
        }

        // Anh pixel-art dung LAI tu asset co san cua game (khong tai/ve gi moi) - Nearest filter
        // BAT BUOC de giu net rieng (mac dinh Godot dung Linear, se lam mo phong to pixel-art).
        private static TextureRect MakeSprite(string path, float size, float alpha = 1f)
        {
            return new TextureRect
            {
                Texture = GD.Load<Texture2D>(path),
                CustomMinimumSize = new Vector2(size, size),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest,
                Modulate = new Color(1f, 1f, 1f, alpha),
            };
        }

        // Dai "duong chan troi trang trai" doc theo mep duoi man hinh (cay/bu nhin/nha/lua mi/
        // lau dai) - lam nen co khong khi thay vi 1 khoang trong, dung DUNG sprite scenery co san.
        private static Control BuildFarmSceneStrip()
        {
            var row = new HBoxContainer
            {
                AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
                OffsetTop = -100, OffsetBottom = -16,
                Alignment = BoxContainer.AlignmentMode.Center,
            };
            row.AddThemeConstantOverride("separation", 46);

            (string path, float size)[] scenery =
            {
                ("res://assets/scenery/tree.png", 72f),
                ("res://assets/scenery/scarecrow.png", 62f),
                ("res://assets/scenery/house.png", 84f),
                ("res://assets/crops/wheat.png", 54f),
                ("res://assets/scenery/castle.png", 72f),
            };
            foreach (var (path, size) in scenery)
                row.AddChild(MakeSprite(path, size, 0.8f));

            return row;
        }

        private static Control BuildHeroPortrait()
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(MakeSprite("res://assets/items/sword.png", 30f));
            row.AddChild(MakeSprite("res://assets/player/player.png", 72f));
            row.AddChild(MakeSprite("res://assets/items/shield.png", 30f));
            return row;
        }

        private static Label MakeFieldLabel()
        {
            var l = new Label();
            l.AddThemeColorOverride("font_color", new Color(CreamText, 0.75f));
            l.AddThemeFontSizeOverride("font_size", 13);
            return l;
        }

        private static LineEdit MakeStyledLineEdit(bool secret = false)
        {
            var box = new StyleBoxFlat
            {
                BgColor = new Color(0.17f, 0.14f, 0.10f),
                BorderColor = new Color(GoldAccent, 0.5f),
                BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
                CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
                ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 7, ContentMarginBottom = 7,
            };
            var focusBox = (StyleBoxFlat)box.Duplicate();
            focusBox.BorderColor = GoldAccent;
            focusBox.BorderWidthTop = 2; focusBox.BorderWidthBottom = 2; focusBox.BorderWidthLeft = 2; focusBox.BorderWidthRight = 2;

            var le = new LineEdit { Secret = secret, CustomMinimumSize = new Vector2(332, 0) };
            le.AddThemeStyleboxOverride("normal", box);
            le.AddThemeStyleboxOverride("focus", focusBox);
            le.AddThemeColorOverride("font_color", CreamText);
            le.AddThemeColorOverride("font_placeholder_color", new Color(CreamText, 0.35f));
            return le;
        }

        private static void StylePrimaryButton(Button btn)
        {
            var normal = new StyleBoxFlat
            {
                BgColor = new Color(0.60f, 0.44f, 0.18f),
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            };
            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color(0.72f, 0.55f, 0.24f);
            var pressed = (StyleBoxFlat)normal.Duplicate();
            pressed.BgColor = new Color(0.50f, 0.36f, 0.14f);
            var disabled = (StyleBoxFlat)normal.Duplicate();
            disabled.BgColor = new Color(0.35f, 0.32f, 0.28f);

            btn.AddThemeStyleboxOverride("normal", normal);
            btn.AddThemeStyleboxOverride("hover", hover);
            btn.AddThemeStyleboxOverride("pressed", pressed);
            btn.AddThemeStyleboxOverride("focus", normal);
            btn.AddThemeStyleboxOverride("disabled", disabled);
            btn.AddThemeColorOverride("font_color", new Color(0.13f, 0.10f, 0.06f));
            btn.AddThemeColorOverride("font_hover_color", new Color(0.13f, 0.10f, 0.06f));
            btn.AddThemeColorOverride("font_pressed_color", new Color(0.13f, 0.10f, 0.06f));
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.6f, 0.58f, 0.55f));
            btn.AddThemeFontSizeOverride("font_size", 16);
        }

        private void ToggleMode()
        {
            _isRegisterMode = !_isRegisterMode;
            _primaryBtn.Text = Loc.T(_isRegisterMode ? "login.btn_register" : "login.btn_login");
            _toggleModeBtn.Text = Loc.T(_isRegisterMode ? "login.toggle_to_login" : "login.toggle_to_register");
            _status.Text = "";
        }

        private void Submit(bool isRegister)
        {
            string user = _username.Text.Trim();
            string pass = _password.Text;
            if (user.Length < 3 || pass.Length < 6)
            {
                _status.Text = Loc.T("login.validation_error");
                return;
            }

            SetBusy(true);
            _status.Text = Loc.T("login.connecting");

            void OnDone(bool ok, string tokenOrError)
            {
                if (!IsInstanceValid(this)) return; // scene co the da doi truoc khi callback ve
                SetBusy(false);
                if (ok)
                {
                    GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
                }
                else
                {
                    _status.Text = tokenOrError ?? Loc.T("login.unknown_error");
                }
            }

            if (isRegister) BackendClient.Instance.Register(user, pass, OnDone);
            else BackendClient.Instance.Login(user, pass, OnDone);
        }

        private void SetBusy(bool busy)
        {
            _primaryBtn.Disabled = busy;
            _toggleModeBtn.Disabled = busy;
        }
    }
}
