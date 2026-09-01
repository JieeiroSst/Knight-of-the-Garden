using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Man hinh dang nhap MOI - la scene KHOI DAU cua game (thay Main.tscn, xem project.godot
    // run/main_scene) vi tu nay moi lan choi/luu deu can dang nhap qua backend truoc (khong con
    // luu file JSON local nua - xem BackendClient.cs). Dang ky/dang nhap thanh cong -> vao Main.tscn.
    public partial class LoginUI : Control
    {
        private LineEdit _username;
        private LineEdit _password;
        private Label _status;
        private Button _loginBtn;
        private Button _registerBtn;

        public override void _Ready()
        {
            var bg = new ColorRect { Color = new Color(0.05f, 0.06f, 0.08f), AnchorRight = 1, AnchorBottom = 1 };
            AddChild(bg);

            var panel = new Panel
            {
                Position = new Vector2(280, 120),
                CustomMinimumSize = new Vector2(400, 300),
            };
            var frameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.97f),
                BorderColor = new Color(0.75f, 0.62f, 0.35f),
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            };
            panel.AddThemeStyleboxOverride("panel", frameStyle);
            AddChild(panel);

            var vb = new VBoxContainer { Position = new Vector2(24, 20), CustomMinimumSize = new Vector2(352, 0) };
            vb.AddThemeConstantOverride("separation", 6);
            panel.AddChild(vb);

            var title = new Label { Text = "HIEP SI VE VUON" };
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 22);
            vb.AddChild(title);

            var sub = new Label
            {
                Text = "Dang nhap de vao trang trai (can server backend dang chay)",
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(352, 0),
            };
            sub.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            vb.AddChild(sub);

            vb.AddChild(new HSeparator());

            vb.AddChild(new Label { Text = "Ten dang nhap (>= 3 ky tu)" });
            _username = new LineEdit { PlaceholderText = "vd: nongdan01" };
            vb.AddChild(_username);

            vb.AddChild(new Label { Text = "Mat khau (>= 6 ky tu)" });
            _password = new LineEdit { PlaceholderText = "mat khau", Secret = true };
            _password.TextSubmitted += _ => OnLogin();
            vb.AddChild(_password);

            var row = new HBoxContainer();
            vb.AddChild(row);

            _loginBtn = new Button { Text = "Dang Nhap" };
            _loginBtn.Pressed += OnLogin;
            row.AddChild(_loginBtn);

            _registerBtn = new Button { Text = "Dang Ky Tai Khoan Moi" };
            _registerBtn.Pressed += OnRegister;
            row.AddChild(_registerBtn);

            _status = new Label
            {
                Text = "",
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(352, 0),
            };
            _status.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.4f));
            vb.AddChild(_status);
        }

        private void OnLogin() => Submit(isRegister: false);
        private void OnRegister() => Submit(isRegister: true);

        private void Submit(bool isRegister)
        {
            string user = _username.Text.Trim();
            string pass = _password.Text;
            if (user.Length < 3 || pass.Length < 6)
            {
                _status.Text = "Ten dang nhap >= 3 ky tu, mat khau >= 6 ky tu.";
                return;
            }

            SetBusy(true);
            _status.Text = "Dang ket noi server...";

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
                    _status.Text = tokenOrError ?? "Loi khong ro.";
                }
            }

            if (isRegister) BackendClient.Instance.Register(user, pass, OnDone);
            else BackendClient.Instance.Login(user, pass, OnDone);
        }

        private void SetBusy(bool busy)
        {
            _loginBtn.Disabled = busy;
            _registerBtn.Disabled = busy;
        }
    }
}
