using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Khung chat goc duoi-trai: [Enter] mo o nhap, [Enter] gui, [Esc] dong.
    //   noi dung          -> chat kenh (moi nguoi cung vung + kenh)
    //   /w Ten noi dung   -> nhan rieng
    //   /kenh 2           -> chuyen kenh trong vung hien tai
    // Dong tren cung hien vung/kenh/trang thai ket noi.
    public partial class ChatUI : CanvasLayer
    {
        public static bool IsTyping { get; private set; }

        private VBoxContainer _lines;
        private LineEdit _input;
        private Label _status;
        private readonly Queue<Label> _history = new();
        private const int MaxLines = 9;

        public override void _Ready()
        {
            AddToGroup("chat_ui");
            var root = new VBoxContainer { Position = new Vector2(8, 330), CustomMinimumSize = new Vector2(380, 0) };
            root.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(root);
            _status = UiStyle.Small("", new Color(0.6f, 0.9f, 1f));
            root.AddChild(_status);
            _lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.AddChild(_lines);
            _input = new LineEdit { Visible = false, MaxLength = 200, CustomMinimumSize = new Vector2(380, 0), PlaceholderText = Loc.T("chat.placeholder") };
            _input.TextSubmitted += OnSubmit;
            root.AddChild(_input);

            NetworkClient.Instance.ChatReceived += AddLine;
            NetworkClient.Instance.StatusChanged += RefreshStatus;
            RefreshStatus();
        }

        public override void _ExitTree()
        {
            NetworkClient.Instance.ChatReceived -= AddLine;
            NetworkClient.Instance.StatusChanged -= RefreshStatus;
            IsTyping = false;
        }

        private double _statusTimer;
        public override void _Process(double delta)
        {
            _statusTimer -= delta;
            if (_statusTimer > 0) return;
            _statusTimer = 1.0;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var net = NetworkClient.Instance;
            _status.Text = !net.Connected ? Loc.T("chat.offline")
                : net.Zone == null ? Loc.T("chat.private_farm")
                : string.Format(Loc.T("chat.status_fmt"), ZoneMap.DisplayName(net.Zone), net.Channel, net.RemoteCount + 1);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Enter && !_input.Visible)
            {
                _input.Visible = true;
                _input.GrabFocus();
                IsTyping = true;
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Input(InputEvent e)
        {
            if (_input.Visible && e.IsActionPressed("ui_cancel"))
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Close()
        {
            _input.Text = "";
            _input.Visible = false;
            _input.ReleaseFocus();
            IsTyping = false;
        }

        private void OnSubmit(string text)
        {
            text = text.Trim();
            Close();
            if (text.Length == 0) return;
            var net = NetworkClient.Instance;
            if (text.StartsWith("/w ") || text.StartsWith("/W "))
            {
                var parts = text.Substring(3).Trim().Split(' ', 2);
                if (parts.Length == 2) net.Whisper(parts[0], parts[1]);
                else AddLine("system", "", Loc.T("chat.whisper_usage"));
            }
            else if (text.StartsWith("/g ") || text.StartsWith("/G "))
            {
                net.GuildChat(text.Substring(3).Trim());
            }
            else if (text.StartsWith("/kenh ") || text.StartsWith("/ch "))
            {
                if (int.TryParse(text.Split(' ', 2)[1], out int ch) && ch > 0) net.SwitchChannel(ch);
            }
            else if (net.Zone == null) AddLine("system", "", Loc.T("chat.no_zone"));
            else net.SendChat(text);
        }

        public void AddLine(string channel, string from, string text)
        {
            var color = channel switch
            {
                "whisper" => new Color(1f, 0.6f, 0.9f),
                "guild" => new Color(0.55f, 1f, 0.6f),
                "system" => new Color(1f, 0.85f, 0.4f),
                _ => new Color(0.95f, 0.95f, 0.95f),
            };
            var lbl = new Label
            {
                Text = string.IsNullOrEmpty(from) ? text : channel == "guild" ? $"[{Loc.T("chat.guild_tag")}][{from}] {text}" : $"[{from}] {text}",
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(380, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            lbl.AddThemeColorOverride("font_color", color);
            lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
            lbl.AddThemeConstantOverride("outline_size", 4);
            lbl.AddThemeFontSizeOverride("font_size", 12);
            _lines.AddChild(lbl);
            _history.Enqueue(lbl);
            while (_history.Count > MaxLines) _history.Dequeue().QueueFree();
            RefreshStatus();
        }
    }
}
