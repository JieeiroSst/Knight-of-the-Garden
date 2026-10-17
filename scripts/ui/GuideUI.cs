using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // HUONG DAN CHOI ([F1]): danh sach chu de ben trai, noi dung ben phai (BBCode, song ngu - xem LocGuide).
    // Lan dau vao game tu mo chu de "Bat dau" + bang VIEC CAN LAM cho nguoi moi (GuideTracker).
    public partial class GuideUI : CanvasLayer
    {
        public static GuideUI Instance { get; private set; }
        private const string CfgPath = "user://guide.cfg";

        private Panel _panel;
        private VBoxContainer _topicList;
        private RichTextLabel _body;
        private Label _title, _hint;
        private Button _close, _stepsBtn;
        private string _topic = "start";
        private readonly Dictionary<string, Button> _topicBtns = new();
        private GuideTracker _tracker;

        public override void _EnterTree() => Instance = this;
        public override void _ExitTree() { if (Instance == this) Instance = null; Loc.LanguageChanged -= Rebuild; }

        public override void _Ready()
        {
            Layer = 22;
            var vp = GetViewport().GetVisibleRect().Size;
            var size = new Vector2(Mathf.Min(900, vp.X - 40), Mathf.Min(600, vp.Y - 40));
            _panel = UiStyle.MakePanel((vp - size) / 2f, size);
            AddChild(_panel);

            _title = UiStyle.Title("");
            _title.Position = new Vector2(20, 12);
            _panel.AddChild(_title);
            _hint = UiStyle.Small("");
            _hint.Position = new Vector2(220, 18);
            _panel.AddChild(_hint);

            var scrollL = new ScrollContainer { Position = new Vector2(16, 50), Size = new Vector2(200, size.Y - 110) };
            _panel.AddChild(scrollL);
            _topicList = new VBoxContainer { CustomMinimumSize = new Vector2(188, 0) };
            scrollL.AddChild(_topicList);

            _body = new RichTextLabel
            {
                BbcodeEnabled = true, ScrollActive = true, FitContent = false,
                Position = new Vector2(232, 50), Size = new Vector2(size.X - 252, size.Y - 110),
            };
            _body.AddThemeFontSizeOverride("normal_font_size", 15);
            _body.AddThemeFontSizeOverride("bold_font_size", 15);
            _panel.AddChild(_body);

            _stepsBtn = new Button { Position = new Vector2(16, size.Y - 48) };
            _stepsBtn.Pressed += () => { _tracker?.SetHidden(!_tracker.Hidden); RefreshStepsBtn(); };
            _panel.AddChild(_stepsBtn);
            _close = new Button { Position = new Vector2(size.X - 110, size.Y - 48), CustomMinimumSize = new Vector2(90, 30) };
            _close.Pressed += () => _panel.Visible = false;
            _panel.AddChild(_close);
            _panel.Visible = false;

            _tracker = new GuideTracker();
            AddChild(_tracker);

            Rebuild();
            Loc.LanguageChanged += Rebuild;

            // Lan dau choi: mo huong dan "Bat dau".
            var cfg = new ConfigFile();
            bool seen = cfg.Load(CfgPath) == Error.Ok && (bool)cfg.GetValue("guide", "seen", false);
            if (!seen)
            {
                GetTree().CreateTimer(4.0).Timeout += () => { if (IsInstanceValid(this)) Open("start"); };
                cfg.SetValue("guide", "seen", true);
                cfg.Save(CfgPath);
            }
        }

        public bool IsOpen => _panel.Visible;

        public void Open(string topic = null)
        {
            if (topic != null) _topic = topic;
            ShowTopic(_topic);
            _panel.Visible = true;
            _tracker?.Mark("guide");
        }

        private void Rebuild()
        {
            _title.Text = Loc.T("guide.title");
            _hint.Text = Loc.T("guide.hint");
            _close.Text = Loc.T("guide.close");
            RefreshStepsBtn();
            UiStyle.Clear(_topicList);
            _topicBtns.Clear();
            foreach (var t in LocGuide.Topics)
            {
                string id = t;
                var b = new Button { Text = Loc.T("guide.t." + id), ToggleMode = true, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(186, 30) };
                b.Pressed += () => ShowTopic(id);
                _topicList.AddChild(b);
                _topicBtns[id] = b;
            }
            ShowTopic(_topic);
        }

        private void RefreshStepsBtn()
        {
            if (_stepsBtn != null) _stepsBtn.Text = Loc.T(_tracker != null && _tracker.Hidden ? "guide.reset_steps" : "guide.hide_steps");
        }

        private void ShowTopic(string id)
        {
            _topic = id;
            foreach (var (k, b) in _topicBtns) b.SetPressedNoSignal(k == id);
            _body.Text = $"[font_size=19][color=#e8c060]{Loc.T("guide.t." + id)}[/color][/font_size]\n\n{Loc.T("guide.b." + id)}";
            _body.ScrollToLine(0);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.F1 && !ChatUI.IsTyping)
            {
                if (_panel.Visible) _panel.Visible = false; else Open();
                GetViewport().SetInputAsHandled();
            }
            else if (_panel.Visible && e.IsActionPressed("ui_cancel"))
            {
                _panel.Visible = false;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // Bang VIEC CAN LAM cho nguoi moi (goc tren ben phai, khong chan chuot): tu danh dau khi nguoi choi
    // lam dung viec do. Xong het (hoac tat trong huong dan) -> an han, nho qua cac lan choi.
    public partial class GuideTracker : Control
    {
        private const string CfgPath = "user://guide_steps.cfg";
        private static readonly string[] Steps = { "move", "look", "interact", "tool", "bag", "map", "guide" };
        private readonly HashSet<string> _done = new();
        private VBoxContainer _list;
        private Panel _bg;
        private Vector3? _start;
        private float _startYaw;
        private bool _finished;
        public bool Hidden { get; private set; }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.TopRight);
            Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 330, 150);
            _bg = new Panel { Size = new Vector2(310, 40), MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.92f) };
            _bg.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.07f, 0.05f, 0.78f), BorderColor = UiStyle.Gold,
                BorderWidthLeft = 3, CornerRadiusTopRight = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusBottomLeft = 6,
            });
            AddChild(_bg);
            _list = new VBoxContainer { Position = new Vector2(12, 8), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_list);
            var cfg = new ConfigFile();
            if (cfg.Load(CfgPath) == Error.Ok)
            {
                foreach (var s in (string[])cfg.GetValue("steps", "done", new string[0])) _done.Add(s);
                Hidden = (bool)cfg.GetValue("steps", "hidden", false);
            }
            _finished = AllDone();
            Loc.LanguageChanged += Redraw;
            Redraw();
        }

        public override void _ExitTree() => Loc.LanguageChanged -= Redraw;

        private bool AllDone() { foreach (var s in Steps) if (!_done.Contains(s)) return false; return true; }

        public void SetHidden(bool hidden)
        {
            Hidden = hidden;
            if (!hidden && _finished) { _done.Clear(); _finished = false; }
            Save();
            Redraw();
        }

        public void Mark(string step)
        {
            if (_finished || !_done.Add(step)) return;
            Save();
            if (AllDone())
            {
                _finished = true;
                Notify.Show(Loc.T("steps.done"));
            }
            Redraw();
        }

        private void Save()
        {
            var cfg = new ConfigFile();
            cfg.SetValue("steps", "done", new List<string>(_done).ToArray());
            cfg.SetValue("steps", "hidden", Hidden);
            cfg.Save(CfgPath);
        }

        private void Redraw()
        {
            Visible = !Hidden && !_finished;
            if (!Visible) return;
            UiStyle.Clear(_list);
            var title = UiStyle.Title(Loc.T("steps.title"));
            title.AddThemeFontSizeOverride("font_size", 14);
            title.MouseFilter = MouseFilterEnum.Ignore;
            _list.AddChild(title);
            foreach (var s in Steps)
            {
                bool ok = _done.Contains(s);
                var l = new Label { Text = (ok ? "✔ " : "○ ") + Loc.T("steps." + s), MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(286, 0), AutowrapMode = TextServer.AutowrapMode.Word };
                l.AddThemeFontSizeOverride("font_size", 13);
                l.AddThemeColorOverride("font_color", ok ? UiStyle.Dim : UiStyle.Text);
                _list.AddChild(l);
            }
            var hint = UiStyle.Small(Loc.T("steps.skip"));
            hint.MouseFilter = MouseFilterEnum.Ignore;
            hint.CustomMinimumSize = new Vector2(286, 0);
            _list.AddChild(hint);
            _bg.Size = new Vector2(310, 26 + 22 * (Steps.Length + 2));
        }

        public override void _Process(double delta)
        {
            if (_finished || Hidden) return;
            if (ChatUI.IsTyping) return;
            if (GetTree().GetFirstNodeInGroup("player") is Node3D p)
            {
                _start ??= p.GlobalPosition;
                if (p.GlobalPosition.DistanceTo(_start.Value) > 250f) Mark("move");
            }
            if (CameraRig.Current != null)
            {
                if (_startYaw == 0f) _startYaw = CameraRig.Current.Yaw + 0.0001f;
                if (Mathf.Abs(CameraRig.Current.Yaw - _startYaw) > 1.2f) Mark("look");
            }
            if (Input.IsActionJustPressed("interact")) Mark("interact");
            if (Input.IsActionJustPressed("use_tool")) Mark("tool");
            if (Input.IsActionJustPressed("toggle_inventory") || Input.IsActionJustPressed("toggle_backpack")) Mark("bag");
            if (Input.IsActionJustPressed("toggle_map")) Mark("map");
        }
    }
}
