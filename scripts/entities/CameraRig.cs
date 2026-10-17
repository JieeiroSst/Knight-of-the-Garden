using Godot;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Camera kieu GAME SINH TON (Valheim/ARK/DayZ/Rust):
    //  - Chuot xoay camera TU DO (khoa con tro giua man hinh, co tam ngam) - khong can giu chuot phai.
    //    Mo bat ky bang/giao dien nao (tui do, ban do, cua hang...), giu [Alt] hoac bam [Esc] -> hien con tro.
    //  - [V]: doi goc QUA VAI (thu ba, camera sat sau vai phai) <-> GOC NHIN THU NHAT (mat nhan vat; than
    //    an voi camera nhung van do bong, chi thay vu khi/cong cu dang cam). Lan chuot: phong to/thu nho goc
    //    qua vai; thu het co -> vao goc thu nhat, lan ra -> ve qua vai.
    //  - Phim di chuyen theo HUONG CAMERA; goc thu nhat nhan vat luon quay theo huong nhin, goc qua vai quay
    //    theo huong chay va quay ve huong nhin khi danh/dung cong cu (xem Player/PlayerCombat).
    //
    // Cau truc: rig (TopLevel, bam theo vi tri co do tre nhe) -> SpringArm3D (pitch/yaw, co va cham: tuong
    // chan giua thi camera tu keo gan, khong nhin xuyen tuong) -> Camera3D cua Player.tscn.
    public partial class CameraRig : Node3D
    {
        public enum Profile { Outdoor, Indoor, Mounted }
        public enum ViewMode { Shoulder, FirstPerson }

        public static CameraRig Current { get; private set; }
        public static ViewMode Mode { get; private set; } = ViewMode.Shoulder;

        private const float Sensitivity = 0.0032f;
        private const float FollowSharpness = 18f;
        private const float ZoomSharpness = 10f;
        private const float EyeHeight = 36f;       // capsule nhan vat cao 40 (~1.8m)
        private const float ShoulderOffset = 24f;   // lech sang phai -> nhan vat lech trai man hinh, tam ngam thoang
        // Lop hien thi rieng cho than nhan vat - camera goc thu nhat khong ve lop nay (van do bong).
        private const uint BodyLayer = 1u << 19;
        private const string CfgPath = "user://camera.cfg";

        private Node3D _target;
        private SpringArm3D _arm;
        private Camera3D _camera;
        private float _yaw;
        private float _pitch = Mathf.DegToRad(-12f);
        private float _distance = 150f;
        private float _wantDistance = 150f;
        private float _minDist = 80f, _maxDist = 320f;
        private float _height = 50f;
        private Profile _profile = Profile.Outdoor;
        private float _outdoorDistance = 150f;
        private bool _captured;
        private bool _cursorLatch;   // [Esc] khi khong co giao dien -> tha con tro cho toi lan bam chuot vao game
        private double _uiCheckIn;
        private bool _uiOpen;
        private double _bodyCheckIn;
        private CanvasLayer _hudLayer;
        private Control _crosshair;

        // Goc xoay ngang hien tai cua camera (0 = nhin ve -Z, huong "Bac" cua ban do).
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public static bool Captured => Current?._captured ?? false;
        // Huong nhin tren mat phang ngang (nhan vat quay theo khi danh / o goc thu nhat).
        public Vector3 FlatForward => new Vector3(0, 0, -1).Rotated(Vector3.Up, _yaw);
        public static bool FacesCamera => Current != null && Mode == ViewMode.FirstPerson;

        public void Setup(Node3D target, Camera3D camera)
        {
            Current = this;
            _target = target;
            _camera = camera;
            TopLevel = true;
            Name = "CameraRig";
            _arm = new SpringArm3D
            {
                SpringLength = _distance,
                Shape = new SphereShape3D { Radius = 6f },
                CollisionMask = 1,
                Margin = 4f,
            };
            AddChild(_arm);
            if (target is CollisionObject3D body) _arm.AddExcludedObject(body.GetRid());
            if (_camera != null)
            {
                _camera.Reparent(_arm, false);
                _camera.Position = Vector3.Zero;
                _camera.Rotation = Vector3.Zero;
                _camera.Current = true;
            }
            var cfg = new ConfigFile();
            if (cfg.Load(CfgPath) == Error.Ok)
            {
                Mode = (int)cfg.GetValue("camera", "mode", 0) == 1 ? ViewMode.FirstPerson : ViewMode.Shoulder;
                _outdoorDistance = _wantDistance = _distance = Mathf.Clamp((float)cfg.GetValue("camera", "distance", 150f), _minDist, _maxDist);
            }
            BuildCrosshair();
            GlobalPosition = target.GlobalPosition + Vector3.Up * _height;
            ApplyMode();
            ApplyArm();
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
            if (_captured) Input.MouseMode = Input.MouseModeEnum.Visible;
            SetBodyHidden(false);
        }

        private void SaveCfg()
        {
            var cfg = new ConfigFile();
            cfg.SetValue("camera", "mode", Mode == ViewMode.FirstPerson ? 1 : 0);
            cfg.SetValue("camera", "distance", _outdoorDistance);
            cfg.Save(CfgPath);
        }

        // Doi tu the camera theo hoan canh (trong nha: gan hon de khong cham tuong).
        public void SetProfile(Profile p)
        {
            if (p == _profile) return;
            if (_profile == Profile.Outdoor) _outdoorDistance = _wantDistance;
            _profile = p;
            switch (p)
            {
                case Profile.Indoor:
                    _minDist = 45f; _maxDist = 120f; _wantDistance = 75f; _height = 44f;
                    break;
                case Profile.Mounted:
                    _minDist = 110f; _maxDist = 420f; _wantDistance = Mathf.Max(_outdoorDistance, 200f); _height = 64f;
                    break;
                default:
                    _minDist = 80f; _maxDist = 320f; _wantDistance = _outdoorDistance; _height = 50f;
                    break;
            }
        }

        public void ToggleView()
        {
            Mode = Mode == ViewMode.Shoulder ? ViewMode.FirstPerson : ViewMode.Shoulder;
            ApplyMode();
            SaveCfg();
            Notify.Show(Systems.Loc.T(Mode == ViewMode.FirstPerson ? "camera.fp" : "camera.shoulder"));
        }

        private void ApplyMode()
        {
            bool fp = Mode == ViewMode.FirstPerson;
            if (_camera != null)
            {
                _camera.CullMask = fp ? 0xFFFFFu & ~BodyLayer : 0xFFFFFu;
                _camera.Near = fp ? 1.2f : 0.05f;
                _camera.Fov = fp ? 80f : 70f;
            }
            SetBodyHidden(fp);
        }

        // Than nhan vat sang lop rieng (camera goc thu nhat khong ve) - TRU do dang cam tren tay.
        private void SetBodyHidden(bool hidden)
        {
            if (_target == null || !IsInstanceValid(_target)) return;
            var model = _target.GetNodeOrNull<Node3D>("Model");
            if (model != null) SetLayers(model, hidden);
        }

        private static void SetLayers(Node n, bool hidden)
        {
            if (n is HeldItemVisual) return;
            if (n is VisualInstance3D vi) vi.Layers = hidden ? BodyLayer : 1u;
            foreach (Node c in n.GetChildren()) SetLayers(c, hidden);
        }

        // Doi huong phim (trai/phai, len/xuong tren ban phim) thanh huong THE GIOI theo camera.
        public static Vector3 RelativeMove(Vector2 input)
        {
            var dir = new Vector3(input.X, 0f, input.Y);
            if (Current == null || dir == Vector3.Zero) return dir;
            return dir.Rotated(Vector3.Up, Current._yaw);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (k.PhysicalKeycode == Key.V && !ChatUI.IsTyping && !_uiOpen) { ToggleView(); GetViewport().SetInputAsHandled(); return; }
                if (k.PhysicalKeycode == Key.Escape && _captured) { _cursorLatch = true; GetViewport().SetInputAsHandled(); return; }
            }
            if (e is InputEventMouseButton mb && mb.Pressed)
            {
                // Bam chuot vao game sau khi da tha con tro bang Esc -> khoa lai.
                if (_cursorLatch && !_uiOpen && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right)) _cursorLatch = false;
                if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
                {
                    if (!_captured) return;
                    bool zoomIn = mb.ButtonIndex == MouseButton.WheelUp;
                    if (Mode == ViewMode.FirstPerson)
                    {
                        if (!zoomIn) { Mode = ViewMode.Shoulder; _wantDistance = _distance = _minDist; ApplyMode(); SaveCfg(); }
                    }
                    else if (zoomIn && _wantDistance <= _minDist + 0.5f) { Mode = ViewMode.FirstPerson; ApplyMode(); SaveCfg(); }
                    else
                    {
                        _wantDistance = Mathf.Clamp(_wantDistance * (zoomIn ? 0.88f : 1.14f), _minDist, _maxDist);
                        if (_profile == Profile.Outdoor) { _outdoorDistance = _wantDistance; SaveCfg(); }
                    }
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (e is InputEventMouseMotion mm && _captured)
            {
                _yaw -= mm.Relative.X * Sensitivity;
                float minP = Mode == ViewMode.FirstPerson ? -1.45f : -1.2f;
                float maxP = Mode == ViewMode.FirstPerson ? 1.45f : 0.9f;
                _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * Sensitivity, minP, maxP);
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (_target == null || !IsInstanceValid(_target)) return;
            float dt = (float)delta;

            _uiCheckIn -= delta;
            if (_uiCheckIn <= 0) { _uiCheckIn = 0.1; _uiOpen = AnyUiOpen(); }
            bool want = !_uiOpen && !_cursorLatch && !ChatUI.IsTyping && !Input.IsKeyPressed(Key.Alt) && GetWindow().HasFocus();
            if (want != _captured)
            {
                _captured = want;
                Input.MouseMode = want ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            }
            if (_crosshair != null) _crosshair.Visible = _captured;

            // Do moi mac/doi vu khi tao mesh moi -> goc thu nhat kiem tra lai lop an than.
            if (Mode == ViewMode.FirstPerson)
            {
                _bodyCheckIn -= delta;
                if (_bodyCheckIn <= 0) { _bodyCheckIn = 0.5; SetBodyHidden(true); }
            }

            bool fp = Mode == ViewMode.FirstPerson;
            var pivot = _target.GlobalPosition + Vector3.Up * (fp ? EyeHeight + (_profile == Profile.Mounted ? 14f : 0f) : _height);
            if (!fp) pivot += new Vector3(1, 0, 0).Rotated(Vector3.Up, _yaw) * ShoulderOffset;
            // Dich chuyen/qua cua (nhay xa) -> dat thang, khong "bay" camera qua ca ban do. Goc thu nhat bam sat.
            if (fp || GlobalPosition.DistanceTo(pivot) > 400f) GlobalPosition = pivot;
            else GlobalPosition = GlobalPosition.Lerp(pivot, 1f - Mathf.Exp(-FollowSharpness * dt));
            float wantLen = fp ? 0f : _wantDistance;
            _distance = fp ? 0f : Mathf.Lerp(_distance, wantLen, 1f - Mathf.Exp(-ZoomSharpness * dt));
            ApplyArm();
            if (_camera != null) _camera.Position = fp ? new Vector3(0, 0, -5f) : Vector3.Zero;
        }

        private void ApplyArm()
        {
            if (_arm == null) return;
            _arm.Rotation = new Vector3(_pitch, _yaw, 0f);
            _arm.SpringLength = _distance;
        }

        // ==== Con tro / tam ngam ====
        private void BuildCrosshair()
        {
            _hudLayer = new CanvasLayer { Layer = 4 };
            AddChild(_hudLayer);
            _crosshair = new Crosshair { MouseFilter = Control.MouseFilterEnum.Ignore };
            _crosshair.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _hudLayer.AddChild(_crosshair);
        }

        private partial class Crosshair : Control
        {
            public override void _Draw()
            {
                var c = Size / 2f;
                var col = new Color(1f, 1f, 1f, 0.85f);
                var sh = new Color(0f, 0f, 0f, 0.5f);
                foreach (var (a, b) in new[] { (new Vector2(-9, 0), new Vector2(-3, 0)), (new Vector2(3, 0), new Vector2(9, 0)), (new Vector2(0, -9), new Vector2(0, -3)), (new Vector2(0, 3), new Vector2(0, 9)) })
                {
                    DrawLine(c + a + Vector2.One, c + b + Vector2.One, sh, 2f);
                    DrawLine(c + a, c + b, col, 2f);
                }
                DrawCircle(c, 1.4f, col);
            }

            public override void _Notification(int what)
            {
                if (what == NotificationResized) QueueRedraw();
            }
        }

        // Co bang/giao dien nao dang mo can con tro khong: Control hien, chan chuot (MouseFilter.Stop) va du
        // lon (bang, cua so) tren cac CanvasLayer - BO QUA HUD (thanh do, la ban...) va moi thu nam duoi node 3D
        // (man den chuyen canh cua Player, tam ngam...). Chi duyet Node/CanvasLayer/Control - khong di vao the gioi 3D.
        public static string LastBlocker = "";
        private bool AnyUiOpen()
        {
            foreach (Node n in GetTree().Root.GetChildren()) if (ScanForUi(n, 0)) return true;
            LastBlocker = "";
            return false;
        }

        private static bool ScanForUi(Node n, int depth)
        {
            if (depth > 14 || n.IsInGroup("hud")) return false;
            if (n is CanvasLayer cl && !cl.Visible) return false;
            if (n is Control c)
            {
                if (!c.Visible) return false;
                if (c.MouseFilter == Control.MouseFilterEnum.Stop && c.Size.X * c.Size.Y >= 60000f) { LastBlocker = c.GetPath(); return true; }
            }
            // Node 3D (vd Main - goc scene, cha cua moi bang giao dien): chi xet con KHONG phai 3D (CanvasLayer/
            // Control/Node) - khong lan vao the gioi 3D (World, nguoi choi, man den chuyen canh...).
            bool is3D = n is Node3D;
            foreach (Node k in n.GetChildren())
            {
                if (is3D && k is Node3D) continue;
                if (ScanForUi(k, depth + 1)) return true;
            }
            return false;
        }
    }
}
