using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Tu Do (phim C, hoac Ban May): 2 tab tach biet
    //  - Trang phuc nong trai: mac/coi; may moi (chi khi dung o Ban May) tu Vai Det + thuoc nhuom.
    //  - Ao giap Trung co: bo giap ca bo (ren o Lo Ren) - mac/coi, bat/tat hien giap (van giu phong thu).
    // Ben trai la nhan vat xem truoc 3D xoay cham, mac thu bo dang chon.
    public partial class WardrobeUI : CanvasLayer
    {
        private bool _armorTab;
        private bool _atTailor;
        private string _preview;
        private Label _title;
        private HBoxContainer _tabs;
        private VBoxContainer _list;
        private CheckBox _showArmor;
        private Node3D _turntable;
        private OutfitRig _previewRig;
        private Label _previewName;

        public override void _Ready()
        {
            AddToGroup("wardrobe_ui");
            var panel = UiStyle.MakePanel(new Vector2(110, 30), new Vector2(760, 500));
            AddChild(panel);
            var root = new HBoxContainer { Position = new Vector2(16, 12) };
            root.AddThemeConstantOverride("separation", 14);
            panel.AddChild(root);

            // ---- Xem truoc 3D ----
            var left = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
            root.AddChild(left);
            var vpc = new SubViewportContainer { CustomMinimumSize = new Vector2(240, 400), Stretch = true };
            left.AddChild(vpc);
            var vp = new SubViewport { OwnWorld3D = true, TransparentBg = true, Size = new Vector2I(240, 400), RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
            vpc.AddChild(vp);
            var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.75f, 0.8f), AmbientLightEnergy = 0.7f };
            vp.AddChild(new WorldEnvironment { Environment = env });
            vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, 30, 0), LightEnergy = 1.3f });
            var cam = new Camera3D { Position = new Vector3(0, 1.05f, 3.3f), Fov = 40f };
            vp.AddChild(cam);
            cam.LookAt(new Vector3(0, 0.95f, 0), Vector3.Up);
            _turntable = new Node3D();
            vp.AddChild(_turntable);
            BuildPreviewModel();
            _previewName = new Label { HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(240, 0) };
            left.AddChild(_previewName);

            // ---- Danh sach ----
            var right = new VBoxContainer { CustomMinimumSize = new Vector2(474, 0) };
            root.AddChild(right);
            _title = UiStyle.Title(Loc.T("wardrobe.title"));
            right.AddChild(_title);
            _tabs = new HBoxContainer();
            right.AddChild(_tabs);
            _showArmor = new CheckBox { Text = Loc.T("wardrobe.show_armor"), ButtonPressed = Inventory.Instance.ShowArmor };
            _showArmor.Toggled += on => Inventory.Instance.SetShowArmor(on);
            right.AddChild(_showArmor);
            right.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(474, 330) };
            right.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
            _list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("wardrobe.close") };
            close.Pressed += () => Visible = false;
            right.AddChild(close);

            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
            IconRenderer.Instance.IconsUpdated += () => { if (Visible) Refresh(); };
        }

        private void BuildPreviewModel()
        {
            foreach (Node c in _turntable.GetChildren()) c.QueueFree();
            var look = CharacterProfile.Current?.Look ?? new Appearance();
            var def = CharacterCatalog.Get(look.ModelId);
            var holder = new Node3D();
            _turntable.AddChild(holder);
            var ap = CharacterCatalog.AttachModel(holder, def);
            CharacterCatalog.ApplyColors(holder, look);
            ModelUtil.FitToHeight(holder, 1.9f);
            _previewRig = new OutfitRig(holder, look.Gender);
            var anims = CharacterCatalog.ResolveAnims(ap);
            if (ap != null && anims.TryGetValue("Idle", out var idle)) ap.Play(idle);
        }

        public override void _Process(double delta)
        {
            if (Visible && _turntable != null) _turntable.RotateY((float)delta * 0.6f);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (ChatUI.IsTyping) return;
            if (e.IsActionPressed("toggle_wardrobe")) { if (Visible) Visible = false; else Open(false); GetViewport().SetInputAsHandled(); }
            else if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        // atTailor: mo tu Ban May -> may duoc trang phuc nong trai.
        public void Open(bool atTailor)
        {
            _atTailor = atTailor;
            if (atTailor) _armorTab = false;
            _preview = Inventory.Instance.VisualOutfit;
            Visible = true;
            Refresh();
        }

        private void SetPreview(string id)
        {
            _preview = id;
            _previewRig?.Apply(CharacterProfile.Current?.Look ?? new Appearance(), id);
            _previewName.Text = id == null ? Loc.T("wardrobe.default_look") : ItemDatabase.Instance.GetDisplayName(id);
        }

        private void Refresh()
        {
            var db = ItemDatabase.Instance;
            _title.Text = Loc.T(_atTailor ? "wardrobe.tailor_title" : "wardrobe.title");
            _showArmor.SetPressedNoSignal(Inventory.Instance.ShowArmor);
            _showArmor.Visible = _armorTab;
            UiStyle.Clear(_tabs);
            foreach (var armor in new[] { false, true })
            {
                var b = new Button { Text = Loc.T(armor ? "wardrobe.tab_armor" : "wardrobe.tab_farm"), ToggleMode = true, ButtonPressed = armor == _armorTab };
                bool a2 = armor;
                b.Pressed += () => { _armorTab = a2; Refresh(); };
                _tabs.AddChild(b);
            }
            if (_preview != _previewRig?.Current || _previewName.Text == "") SetPreview(_preview);

            UiStyle.Clear(_list);
            _list.AddChild(UiStyle.Small(Loc.T(_armorTab ? "wardrobe.armor_hint" : (_atTailor ? "wardrobe.farm_hint_tailor" : "wardrobe.farm_hint")), UiStyle.Hint));
            string cat = _armorTab ? "armor" : "farm";
            foreach (var od in OutfitCatalog.InCategory(cat))
            {
                var def = db.GetItem(od.ItemId);
                if (def == null) continue;
                bool owned = EconomyClient.TotalOwned(od.ItemId) > 0;
                bool wearing = _armorTab ? Inventory.Instance.EquippedArmor == od.ItemId : Inventory.Instance.EquippedOutfit == od.ItemId;

                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new TextureRect { Texture = db.GetItemIcon(od.ItemId), CustomMinimumSize = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(info);
                var name = new Label { Text = db.GetDisplayName(od.ItemId) + (wearing ? "  ✔" : "") };
                name.AddThemeColorOverride("font_color", owned ? (wearing ? UiStyle.Gold : UiStyle.Text) : UiStyle.Dim);
                info.AddChild(name);
                info.AddChild(UiStyle.Small(BonusText(def)));
                var recipe = db.Recipes.FirstOrDefault(r => r.OutputId == od.ItemId);
                if (!owned && recipe != null)
                    info.AddChild(UiStyle.Small(Unlocked(recipe)
                        ? string.Join(", ", recipe.Ingredients.Select(i => $"{db.GetDisplayName(i.ItemId)} {EconomyClient.TotalOwned(i.ItemId)}/{i.Count}"))
                        : string.Format(Loc.T("craft.locked_fmt"), Loc.T("skill." + recipe.UnlockSkill), recipe.UnlockLevel)));

                var view = new Button { Text = Loc.T("wardrobe.try_on") };
                string id = od.ItemId;
                view.Pressed += () => SetPreview(id);
                row.AddChild(view);

                if (owned)
                {
                    var wear = new Button { Text = Loc.T(wearing ? "wardrobe.take_off" : "wardrobe.wear"), CustomMinimumSize = new Vector2(70, 0) };
                    wear.Pressed += () =>
                    {
                        if (wearing) Inventory.Instance.Unequip(def.Type);
                        else
                        {
                            Inventory.Instance.Equip(id);
                            if (def.Type == ItemType.Armor && !Inventory.Instance.ShowArmor) Inventory.Instance.SetShowArmor(true);
                        }
                        _preview = Inventory.Instance.VisualOutfit;
                        Refresh();
                    };
                    row.AddChild(wear);
                }
                else if (recipe != null && !_armorTab)
                {
                    bool can = _atTailor && Unlocked(recipe) && recipe.Ingredients.All(i => Inventory.Instance.CountOf(i.ItemId) >= i.Count);
                    var sew = new Button { Text = Loc.T("wardrobe.sew"), Disabled = !can, TooltipText = _atTailor ? "" : Loc.T("wardrobe.need_tailor"), CustomMinimumSize = new Vector2(70, 0) };
                    var rr = recipe;
                    sew.Pressed += () =>
                    {
                        // Server kiem tra nguyen lieu theo recipes.json (xem EconomyClient.Craft).
                        if (EconomyClient.Instance.Craft(rr, cooking: false))
                        {
                            AchievementSystem.Instance.Add("items_crafted");
                            Notify.Show(string.Format(Loc.T("wardrobe.sewn_fmt"), db.GetDisplayName(rr.OutputId)));
                            SetPreview(rr.OutputId);
                        }
                        Refresh();
                    };
                    row.AddChild(sew);
                }
                else if (_armorTab)
                {
                    row.AddChild(UiStyle.Small(Loc.T("wardrobe.at_forge"), UiStyle.Dim));
                }
            }
        }

        private static bool Unlocked(RecipeDef r) =>
            string.IsNullOrEmpty(r.UnlockSkill)
            || (System.Enum.TryParse<SkillType>(r.UnlockSkill, out var s) && SkillSystem.Instance.GetLevel(s) >= r.UnlockLevel);

        private static string BonusText(ItemDef def)
        {
            if (def.Type == ItemType.Armor) return string.Format(Loc.T("wardrobe.defense_fmt"), def.Defense);
            if (def.OutfitSpeedBonus > 0) return string.Format(Loc.T("wardrobe.speed_fmt"), Mathf.RoundToInt(def.OutfitSpeedBonus * 100));
            if (!string.IsNullOrEmpty(def.OutfitSkill))
                return string.Format(Loc.T("wardrobe.stamina_fmt"), Loc.T("skill." + def.OutfitSkill), Mathf.RoundToInt(def.OutfitStaminaDiscount * 100));
            return "";
        }
    }
}
