using Godot;
using System;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Tao nhan vat (lan dau dang nhap, hoac sua lai sau): ten hien thi (duy nhat tren server), gioi
    // tinh, kieu nhan vat theo gioi tinh, mau da/toc/trang phuc - xem truoc 3D xoay tron. Luu qua
    // PUT /api/character; nguoi choi khac nhin thay dung ngoai hinh nay.
    public partial class CharacterCreatorUI : Control
    {
        public event Action<CharacterProfile> Saved;

        private readonly Appearance _look = new();
        private LineEdit _name;
        private HFlowContainer _models;
        private Label _status;
        private Node3D _previewRoot;
        private Node3D _previewModel;
        private Button _maleBtn, _femaleBtn;

        public void Init(CharacterProfile existing)
        {
            if (existing == null) return;
            _look.Gender = existing.Look.Gender;
            _look.ModelId = existing.Look.ModelId;
            _look.SkinColor = existing.Look.SkinColor;
            _look.HairColor = existing.Look.HairColor;
            _look.OutfitColor = existing.Look.OutfitColor;
            if (_name != null) _name.Text = existing.DisplayName;
            _pendingName = existing.DisplayName;
        }

        private string _pendingName;

        public override void _Ready()
        {
            // Phai dung ...AndOffsets...: o _Ready node DA nam trong cay, SetAnchorsPreset() thuong
            // GIU NGUYEN kich thuoc cu (0x0) -> nen toi khong phu man hinh, lo form dang nhap phia
            // duoi chong len (loi cu).
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            var bg = new ColorRect { Color = new Color(0.06f, 0.05f, 0.04f, 1f) };
            AddChild(bg);
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var center = new CenterContainer();
            AddChild(center);
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var root = new HBoxContainer();
            root.AddThemeConstantOverride("separation", 20);
            center.AddChild(root);

            // ---- Xem truoc 3D ----
            var vpc = new SubViewportContainer { CustomMinimumSize = new Vector2(300, 470), Stretch = true };
            root.AddChild(vpc);
            var vp = new SubViewport { OwnWorld3D = true, TransparentBg = true, Size = new Vector2I(300, 470) };
            vpc.AddChild(vp);
            _previewRoot = new Node3D();
            vp.AddChild(_previewRoot);
            vp.AddChild(new Camera3D { Position = new Vector3(0, 26, 62), RotationDegrees = new Vector3(-8, 0, 0), Fov = 45 });
            vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, 30, 0), LightEnergy = 1.3f });
            vp.AddChild(new WorldEnvironment { Environment = new Godot.Environment { AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.7f, 0.7f, 0.75f), AmbientLightEnergy = 0.8f } });

            // ---- Form ----
            // Form nam trong khung cuon (man hinh thiet ke 960x540 - noi dung khong bao gio tran ra ngoai).
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(540, 490), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            var form = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
            form.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(form);
            form.AddChild(UiStyle.Title(Loc.T("cc.title")));
            form.AddChild(UiStyle.Small(Loc.T("cc.name")));
            _name = new LineEdit { MaxLength = 20, PlaceholderText = Loc.T("cc.name_hint"), Text = _pendingName ?? "" };
            form.AddChild(_name);

            form.AddChild(UiStyle.Small(Loc.T("cc.gender")));
            var g = new HBoxContainer();
            form.AddChild(g);
            _maleBtn = new Button { Text = Loc.T("cc.male"), ToggleMode = true };
            _femaleBtn = new Button { Text = Loc.T("cc.female"), ToggleMode = true };
            _maleBtn.Pressed += () => SetGender("male");
            _femaleBtn.Pressed += () => SetGender("female");
            g.AddChild(_maleBtn);
            g.AddChild(_femaleBtn);

            form.AddChild(UiStyle.Small(Loc.T("cc.style")));
            // Tu xuong dong khi het chieu ngang (truoc day 1 hang 6 nut tran ra ngoai man hinh).
            _models = new HFlowContainer { CustomMinimumSize = new Vector2(520, 0) };
            form.AddChild(_models);

            AddColorRow(form, Loc.T("cc.skin"), CharacterCatalog.SkinPresets, c => _look.SkinColor = c, () => _look.SkinColor);
            AddColorRow(form, Loc.T("cc.hair"), CharacterCatalog.HairPresets, c => _look.HairColor = c, () => _look.HairColor);
            AddColorRow(form, Loc.T("cc.outfit"), CharacterCatalog.OutfitPresets, c => _look.OutfitColor = c, () => _look.OutfitColor);

            _status = UiStyle.Small("", new Color(1f, 0.6f, 0.5f));
            form.AddChild(_status);
            var save = new Button { Text = Loc.T("cc.save"), CustomMinimumSize = new Vector2(0, 40) };
            save.Pressed += Save;
            form.AddChild(save);

            SetGender(_look.Gender, keepModel: true);
        }

        private void AddColorRow(VBoxContainer form, string label, string[] presets, Action<string> set, Func<string> get)
        {
            form.AddChild(UiStyle.Small(label));
            var row = new HBoxContainer();
            form.AddChild(row);
            foreach (var hex in presets)
            {
                var b = new Button { CustomMinimumSize = new Vector2(30, 26) };
                b.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(hex), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                b.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(hex).Lightened(0.2f), BorderColor = Colors.White, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
                var h = hex;
                b.Pressed += () => { set(h); RebuildPreview(); };
                row.AddChild(b);
            }
            // Mau tu chon.
            var picker = new ColorPickerButton { Color = new Color(get()), CustomMinimumSize = new Vector2(40, 26), EditAlpha = false };
            picker.ColorChanged += c => { set("#" + c.ToHtml(false)); RebuildPreview(); };
            row.AddChild(picker);
        }

        private void SetGender(string gender, bool keepModel = false)
        {
            _look.Gender = gender;
            _maleBtn.ButtonPressed = gender == "male";
            _femaleBtn.ButtonPressed = gender == "female";
            var options = CharacterCatalog.ForGender(gender).ToList();
            if (!keepModel || !options.Any(m => m.Id == _look.ModelId)) _look.ModelId = options[0].Id;
            UiStyle.Clear(_models);
            foreach (var m in options)
            {
                var b = new Button { Text = Loc.Current == Loc.Lang.EN ? m.NameEn : m.NameVi, ToggleMode = true, ButtonPressed = m.Id == _look.ModelId };
                var id = m.Id;
                b.Pressed += () => { _look.ModelId = id; SetGender(_look.Gender, keepModel: true); };
                _models.AddChild(b);
            }
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            _previewModel?.QueueFree();
            _previewModel = new Node3D();
            _previewRoot.AddChild(_previewModel);
            var def = CharacterCatalog.Get(_look.ModelId);
            var anim = CharacterCatalog.AttachModel(_previewModel, def, 0.95f);
            CharacterCatalog.ApplyColors(_previewModel, _look);
            var anims = CharacterCatalog.ResolveAnims(anim);
            if (anim != null && anims.TryGetValue("Idle", out var idle)) anim.Play(idle);
        }

        public override void _Process(double delta)
        {
            if (_previewRoot != null) _previewRoot.RotateY((float)delta * 0.6f);
        }

        private void Save()
        {
            string name = _name.Text.Trim();
            if (name.Length < 3) { _status.Text = Loc.T("cc.name_short"); return; }
            _status.Text = Loc.T("cc.saving");
            var body = JsonSerializer.Serialize(new
            {
                displayName = name, gender = _look.Gender, modelId = _look.ModelId,
                skinColor = _look.SkinColor, hairColor = _look.HairColor, outfitColor = _look.OutfitColor,
            });
            BackendClient.Instance.PutJson("/api/character", body, (ok, status, text) =>
            {
                if (!ok || status != 200)
                {
                    _status.Text = ok ? BackendClient.ErrorOf(text, Loc.T("net.server_error")) : Loc.T("net.no_connection");
                    return;
                }
                using var doc = JsonDocument.Parse(text);
                var profile = CharacterProfile.FromJson(doc.RootElement);
                CharacterProfile.Current = profile;
                Saved?.Invoke(profile);
            });
        }
    }
}
