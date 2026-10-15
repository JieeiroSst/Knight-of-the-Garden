using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Ve ICON vat pham TU MODEL 3D (khong ve tay): dung 1 SubViewport an (the gioi rieng,
    // nen trong suot), chup tung model goc 3/4 roi luu thanh ImageTexture. Chay dan luc vao game (moi
    // khung hinh 1 icon). ItemDatabase.GetItemIcon uu tien icon nay; chua ve xong thi dung PNG du phong.
    public partial class IconRenderer : Node
    {
        public static IconRenderer Instance { get; private set; }

        private const int Size = 128;
        private readonly Dictionary<string, Texture2D> _icons = new();
        private readonly Queue<string> _pending = new();
        private SubViewport _vp;
        private Node3D _stage;
        private string _current;
        private bool _waiting;
        private Camera3D _cam;

        [Signal] public delegate void IconsUpdatedEventHandler();

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            _vp = new SubViewport
            {
                Size = new Vector2I(Size, Size),
                OwnWorld3D = true,
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
                Msaa3D = Viewport.Msaa.Msaa4X,
            };
            AddChild(_vp);
            _cam = new Camera3D { Fov = 35 };
            _vp.AddChild(_cam);
            _vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, 35, 0), LightEnergy = 1.6f });
            _vp.AddChild(new WorldEnvironment
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.ClearColor,
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color(0.75f, 0.75f, 0.8f),
                    AmbientLightEnergy = 0.9f,
                },
            });
            _stage = new Node3D();
            _vp.AddChild(_stage);

            foreach (var id in ItemDatabase.Instance.Items.Keys)
                if (OreVisuals.HasIconModel(id) || ItemModels.Has(id)) _pending.Enqueue(id);
        }

        public Texture2D TryGet(string itemId) => _icons.TryGetValue(itemId, out var t) ? t : null;

        public override void _Process(double delta)
        {
            if (_waiting || _pending.Count == 0) return;
            _current = _pending.Dequeue();
            foreach (Node c in _stage.GetChildren()) c.QueueFree();
            var model = OreVisuals.BuildIconModel(_current) ?? ItemModels.Build(_current);
            if (model == null) return;
            // Xoay nghieng 1 chut cho thay khoi 3D ro hon.
            model.RotationDegrees = new Vector3(0, 35, 0);
            _stage.AddChild(model);
            // Can khung tu dong theo kich thuoc that cua model (dua hau/hat giong rong hon cao...).
            var box = model.Transform * ModelUtil.LocalAabb(model);
            var center = box.GetCenter();
            float extent = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
            var dir = new Vector3(0, 0.45f, 1f).Normalized();
            _cam.Position = center + dir * extent * 1.9f;
            _cam.LookAt(center, Vector3.Up);
            _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            _waiting = true;
            RenderingServer.FramePostDraw += Capture;
        }

        private void Capture()
        {
            RenderingServer.FramePostDraw -= Capture;
            var img = _vp.GetTexture().GetImage();
            if (img != null && !img.IsEmpty()) _icons[_current] = ImageTexture.CreateFromImage(img);
            _waiting = false;
            if (_pending.Count == 0) CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.IconsUpdated);
        }
    }
}
