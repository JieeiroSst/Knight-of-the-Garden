using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
    // Balo ([B]) - giao dien kho do kieu game sinh ton:
    //  - 2 khu: TUI CHINH 24 o (ROI HET khi bi nguoi choi khac ha - xem PvP) va BALO 50 o (an toan).
    //    Moi o hien ca o trong, vien mau theo do hiem, so luong goc duoi, dau "E" = dang trang bi.
    //  - Bam 1 lan = chon (xem chi tiet o duoi), bam DUP = chuyen ca chong sang ben kia, chuot PHAI =
    //    chuyen 1 cai. Nut "Cat het vao balo" cat moi thu (tru do dang trang bi) de khoi mat khi bi ha.
    //  - Tim kiem + loc theo nhom + sap xep; o khong khop bi lam mo (giu nguyen vi tri o).
    //  - Chi tiet: icon lon, ten (mau do hiem), loai, mo ta, chi so (sat thuong/tam ban/phong thu/hoi
    //    mau/gia ban), nut Trang bi/Thao/Dung/Chuyen.
    // Tui chinh + balo la 1 kho chung tren server -> chuyen qua lai KHONG tao op kinh te.
    public partial class BackpackUI : CanvasLayer
    {
        private const float Slot = 46f;
        private static readonly Vector2 PanelSize = new(900, 500);

        private enum Filter { All, Gear, Farm, Material, Food }
        private enum Side { Inventory, Backpack }

        private Panel _panel;
        private GridContainer _invGrid, _bagGrid;
        private Label _invHeader, _bagHeader, _capacity;
        private LineEdit _search;
        private readonly Dictionary<Filter, Button> _filterBtns = new();
        private Filter _filter = Filter.All;

        // Chi tiet o dang chon.
        private TextureRect _detailIcon;
        private Label _detailName, _detailMeta, _detailDesc, _detailStats, _status;
        private Button _moveOne, _moveAll, _equipBtn, _useBtn, _repairBtn;
        private Label _detailCond;
        private Side _selSide;
        private string _selId;

        private Tween _tween;

        public override void _Ready()
        {
            AddToGroup("backpack_ui");
            Layer = 15;
            Build();
            Inventory.Instance.InventoryChanged += OnChanged;
            Backpack.Instance.BackpackChanged += OnChanged;
            Loc.LanguageChanged += Rebuild;
            Visible = false;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= Rebuild;
        }

        private void OnChanged() { if (Visible) Refresh(); }

        private void Rebuild()
        {
            foreach (Node c in GetChildren()) c.QueueFree();
            _filterBtns.Clear();
            Build();
            if (Visible) Refresh();
        }

        // ==== Dung khung ====
        private void Build()
        {
            _panel = UiStyle.MakePanel(new Vector2((960 - PanelSize.X) / 2f, (540 - PanelSize.Y) / 2f), PanelSize);
            _panel.PivotOffset = PanelSize / 2f;
            AddChild(_panel);

            var title = UiStyle.Title(Loc.T("bp.title"));
            title.Position = new Vector2(18, 10);
            _panel.AddChild(title);
            _capacity = UiStyle.Small("");
            _capacity.Position = new Vector2(120, 16);
            _panel.AddChild(_capacity);

            _search = new LineEdit { PlaceholderText = Loc.T("bp.search"), Position = new Vector2(560, 8), CustomMinimumSize = new Vector2(200, 28), ClearButtonEnabled = true };
            _search.TextChanged += _ => Refresh();
            _panel.AddChild(_search);
            var sort = new Button { Text = Loc.T("bp.sort"), Position = new Vector2(768, 8), CustomMinimumSize = new Vector2(78, 28) };
            sort.Pressed += SortAll;
            _panel.AddChild(sort);
            var close = new Button { Text = "X", Position = new Vector2(PanelSize.X - 42, 8), CustomMinimumSize = new Vector2(28, 28) };
            close.Pressed += Close;
            _panel.AddChild(close);

            var filters = new HBoxContainer { Position = new Vector2(18, 42) };
            filters.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(filters);
            foreach (var (f, key) in new[] { (Filter.All, "bp.f_all"), (Filter.Gear, "bp.f_gear"), (Filter.Farm, "bp.f_farm"), (Filter.Material, "bp.f_mat"), (Filter.Food, "bp.f_food") })
            {
                var b = new Button { Text = Loc.T(key), ToggleMode = true, CustomMinimumSize = new Vector2(0, 26) };
                var ff = f;
                b.Pressed += () => { _filter = ff; Refresh(); };
                filters.AddChild(b);
                _filterBtns[f] = b;
            }

            // Tui chinh (trai) - 6 x 4 o.
            _invHeader = new Label { Position = new Vector2(18, 76) };
            _invHeader.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
            _invHeader.AddThemeFontSizeOverride("font_size", 13);
            _panel.AddChild(_invHeader);
            _invGrid = new GridContainer { Columns = 6, Position = new Vector2(18, 98) };
            _invGrid.AddThemeConstantOverride("h_separation", 4);
            _invGrid.AddThemeConstantOverride("v_separation", 4);
            _panel.AddChild(_invGrid);
            var stash = new Button { Text = Loc.T("bp.stash_all"), Position = new Vector2(18, 304), CustomMinimumSize = new Vector2(296, 30) };
            stash.Pressed += StashAll;
            _panel.AddChild(stash);

            // Balo (phai) - 10 cot, cuon.
            _bagHeader = new Label { Position = new Vector2(338, 76) };
            _bagHeader.AddThemeColorOverride("font_color", new Color(0.55f, 1f, 0.6f));
            _bagHeader.AddThemeFontSizeOverride("font_size", 13);
            _panel.AddChild(_bagHeader);
            var scroll = new ScrollContainer { Position = new Vector2(338, 98), CustomMinimumSize = new Vector2(546, 236), Size = new Vector2(546, 236) };
            _panel.AddChild(scroll);
            _bagGrid = new GridContainer { Columns = 11 };
            _bagGrid.AddThemeConstantOverride("h_separation", 4);
            _bagGrid.AddThemeConstantOverride("v_separation", 4);
            scroll.AddChild(_bagGrid);

            // Chi tiet (duoi).
            var detail = new Panel { Position = new Vector2(18, 344), Size = new Vector2(PanelSize.X - 36, 142) };
            detail.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.3f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 });
            _panel.AddChild(detail);
            _detailIcon = new TextureRect { Position = new Vector2(12, 12), Size = new Vector2(72, 72), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            detail.AddChild(_detailIcon);
            _detailName = new Label { Position = new Vector2(96, 8) };
            _detailName.AddThemeFontSizeOverride("font_size", 17);
            detail.AddChild(_detailName);
            _detailMeta = UiStyle.Small("");
            _detailMeta.Position = new Vector2(96, 32);
            detail.AddChild(_detailMeta);
            _detailDesc = new Label { Position = new Vector2(96, 50), Size = new Vector2(470, 46), AutowrapMode = TextServer.AutowrapMode.Word, ClipText = true };
            _detailDesc.AddThemeFontSizeOverride("font_size", 12);
            _detailDesc.AddThemeColorOverride("font_color", UiStyle.Text);
            detail.AddChild(_detailDesc);
            _detailStats = new Label { Position = new Vector2(96, 98) };
            _detailStats.AddThemeFontSizeOverride("font_size", 12);
            _detailStats.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
            detail.AddChild(_detailStats);
            _status = UiStyle.Small("", new Color(1f, 0.6f, 0.5f));
            _status.Position = new Vector2(380, 116);
            detail.AddChild(_status);

            var actions = new GridContainer { Columns = 2, Position = new Vector2(PanelSize.X - 36 - 270, 14) };
            actions.AddThemeConstantOverride("h_separation", 6);
            actions.AddThemeConstantOverride("v_separation", 6);
            detail.AddChild(actions);
            _moveOne = ActionButton(actions, () => Move(_selSide, _selId, 1));
            _moveAll = ActionButton(actions, () => Move(_selSide, _selId, int.MaxValue));
            _equipBtn = ActionButton(actions, ToggleEquip);
            _useBtn = ActionButton(actions, UseSelected);
            _repairBtn = ActionButton(actions, RepairSelected);
            _detailCond = new Label { Position = new Vector2(96, 116) };
            _detailCond.AddThemeFontSizeOverride("font_size", 12);
            detail.AddChild(_detailCond);
            ShowDetail(null, 0);
        }

        private static Button ActionButton(Control parent, System.Action act)
        {
            var b = new Button { CustomMinimumSize = new Vector2(130, 32) };
            b.Pressed += act;
            parent.AddChild(b);
            return b;
        }

        // ==== Mo / dong ====
        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_backpack") && !ChatUI.IsTyping && !(_search?.HasFocus() ?? false))
            {
                if (Visible) Close(); else Open();
                GetViewport().SetInputAsHandled();
            }
            else if (Visible && e.IsActionPressed("ui_cancel"))
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }

        public void Open()
        {
            Visible = true;
            Refresh();
            _panel.Scale = Vector2.One * 0.9f;
            _panel.Modulate = new Color(1, 1, 1, 0);
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(_panel, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(_panel, "modulate:a", 1f, 0.14f);
        }

        private void Close()
        {
            _search?.ReleaseFocus();
            _tween?.Kill();
            _tween = CreateTween();
            _tween.TweenProperty(_panel, "modulate:a", 0f, 0.1f);
            _tween.TweenCallback(Callable.From(() => Visible = false));
        }

        // ==== Ve lai ====
        private void Refresh()
        {
            foreach (var (f, b) in _filterBtns) b.ButtonPressed = f == _filter;
            int invUsed = Inventory.Instance.Slots.Count, bagUsed = Backpack.Instance.Slots.Count;
            _capacity.Text = string.Format(Loc.T("bp.capacity_fmt"), invUsed, Inventory.MaxSlots, bagUsed, Backpack.MaxSlots);
            _invHeader.Text = Loc.T("bp.inv_header");
            _bagHeader.Text = Loc.T("bp.bag_header");
            FillGrid(_invGrid, Inventory.Instance.Slots, Inventory.MaxSlots, Side.Inventory);
            FillGrid(_bagGrid, Backpack.Instance.Slots, Backpack.MaxSlots, Side.Backpack);
            // O dang chon co the vua het (chuyen het/dung het) -> bo chon.
            var list = _selSide == Side.Inventory ? Inventory.Instance.Slots : Backpack.Instance.Slots;
            if (_selId != null && list.All(s => s.ItemId != _selId)) _selId = null;
            ShowDetail(_selId == null ? null : ItemDatabase.Instance.GetItem(_selId), CountIn(_selSide, _selId));
        }

        private void FillGrid(GridContainer grid, List<ItemStack> slots, int capacity, Side side)
        {
            foreach (Node c in grid.GetChildren()) c.QueueFree();
            for (int i = 0; i < capacity; i++)
                grid.AddChild(i < slots.Count ? MakeSlot(slots[i], side) : EmptySlot());
        }

        private static Control EmptySlot()
        {
            var p = new Panel { CustomMinimumSize = new Vector2(Slot, Slot) };
            p.AddThemeStyleboxOverride("panel", SlotStyle(new Color(0.3f, 0.27f, 0.22f, 0.6f), new Color(0, 0, 0, 0.25f), 1));
            return p;
        }

        private static StyleBoxFlat SlotStyle(Color border, Color bg, int width) => new()
        {
            BgColor = bg, BorderColor = border,
            BorderWidthTop = width, BorderWidthBottom = width, BorderWidthLeft = width, BorderWidthRight = width,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Uncommon => new Color(0.45f, 0.85f, 0.4f),
            Rarity.Rare => new Color(0.35f, 0.6f, 1f),
            Rarity.Epic => new Color(0.75f, 0.45f, 1f),
            Rarity.Legendary => new Color(1f, 0.7f, 0.2f),
            _ => new Color(0.6f, 0.58f, 0.52f),
        };

        private Control MakeSlot(ItemStack stack, Side side)
        {
            var def = ItemDatabase.Instance.GetItem(stack.ItemId);
            bool selected = stack.ItemId == _selId && side == _selSide;
            var btn = new Button { CustomMinimumSize = new Vector2(Slot, Slot), ExpandIcon = true, FocusMode = Control.FocusModeEnum.None };
            var border = selected ? UiStyle.Gold : RarityColor(def?.Rarity ?? Rarity.Common);
            var normal = SlotStyle(border, new Color(0.08f, 0.07f, 0.06f, 0.9f), selected ? 3 : 2);
            btn.AddThemeStyleboxOverride("normal", normal);
            btn.AddThemeStyleboxOverride("hover", SlotStyle(UiStyle.Gold, new Color(0.16f, 0.13f, 0.09f, 0.95f), 2));
            btn.AddThemeStyleboxOverride("pressed", normal);
            if (ItemDatabase.Instance.GetItemIcon(stack.ItemId) is Texture2D tex) btn.Icon = tex;
            else btn.Text = (ItemDatabase.Instance.GetDisplayName(stack.ItemId) ?? "?")[..1];
            btn.TooltipText = ItemDatabase.Instance.GetDisplayName(stack.ItemId);

            if (stack.Count > 1)
            {
                var count = new Label { Text = stack.Count.ToString(), HorizontalAlignment = HorizontalAlignment.Right, Position = new Vector2(0, Slot - 18), Size = new Vector2(Slot - 4, 16) };
                count.AddThemeFontSizeOverride("font_size", 11);
                count.AddThemeColorOverride("font_outline_color", Colors.Black);
                count.AddThemeConstantOverride("outline_size", 4);
                count.MouseFilter = Control.MouseFilterEnum.Ignore;
                btn.AddChild(count);
            }
            // Thanh tinh trang duoi o: do ben (vu khi/giap/nong cu) hoac do tuoi (thit/nong san/mon an).
            float? cond = null;
            if (ItemConditions.HasDurability(stack.ItemId)) { var (cur, max) = ItemConditions.Durability(stack.ItemId); cond = (float)cur / max; }
            else if (ItemConditions.Perishable(stack.ItemId)) cond = ItemConditions.FreshRatio(stack.ItemId);
            if (cond is float c)
            {
                var barBg = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Position = new Vector2(4, Slot - 6), Size = new Vector2(Slot - 8, 3), MouseFilter = Control.MouseFilterEnum.Ignore };
                btn.AddChild(barBg);
                var col = c >= 0.6f ? new Color(0.4f, 0.9f, 0.4f) : c >= ItemConditions.UsableRatio ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.3f, 0.25f);
                btn.AddChild(new ColorRect { Color = col, Position = new Vector2(4, Slot - 6), Size = new Vector2((Slot - 8) * Mathf.Clamp(c, 0f, 1f), 3), MouseFilter = Control.MouseFilterEnum.Ignore });
            }
            if (ItemConditions.HasDurability(stack.ItemId) && !ItemConditions.IsUsable(stack.ItemId))
            {
                var broken = new Label { Text = "!", Position = new Vector2(Slot - 12, 1) };
                broken.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.25f));
                broken.AddThemeColorOverride("font_outline_color", Colors.Black);
                broken.AddThemeConstantOverride("outline_size", 4);
                broken.MouseFilter = Control.MouseFilterEnum.Ignore;
                btn.AddChild(broken);
            }
            if (Inventory.Instance.IsEquipped(stack.ItemId))
            {
                var eq = new Label { Text = "E", Position = new Vector2(3, 1) };
                eq.AddThemeFontSizeOverride("font_size", 11);
                eq.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
                eq.AddThemeColorOverride("font_outline_color", Colors.Black);
                eq.AddThemeConstantOverride("outline_size", 4);
                eq.MouseFilter = Control.MouseFilterEnum.Ignore;
                btn.AddChild(eq);
            }
            if (!Matches(def)) btn.Modulate = new Color(1, 1, 1, 0.22f);

            string id = stack.ItemId;
            btn.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton mb || !mb.Pressed) return;
                if (mb.ButtonIndex == MouseButton.Right) { Move(side, id, 1); return; }
                if (mb.ButtonIndex != MouseButton.Left) return;
                if (mb.DoubleClick) { Move(side, id, int.MaxValue); return; }
                _selSide = side;
                _selId = id;
                Refresh();
            };
            return btn;
        }

        private bool Matches(ItemDef def)
        {
            if (def == null) return true;
            string q = _search?.Text?.Trim().ToLowerInvariant() ?? "";
            if (q.Length > 0 && !(ItemDatabase.Instance.GetDisplayName(def.Id) ?? "").ToLowerInvariant().Contains(q) && !def.Id.Contains(q)) return false;
            return _filter switch
            {
                Filter.Gear => def.Type is ItemType.Weapon or ItemType.Armor or ItemType.Tool or ItemType.Outfit,
                Filter.Farm => def.Type is ItemType.Crop or ItemType.Seed,
                Filter.Material => def.Type is ItemType.Material or ItemType.Quest,
                Filter.Food => def.Type == ItemType.Consumable,
                _ => true,
            };
        }

        // ==== Chi tiet ====
        private void ShowDetail(ItemDef def, int count)
        {
            bool has = def != null && count > 0;
            foreach (var b in new[] { _moveOne, _moveAll, _equipBtn, _useBtn, _repairBtn }) b.Visible = has;
            _detailCond.Text = "";
            if (!has)
            {
                _detailIcon.Texture = null;
                _detailName.Text = Loc.T("bp.pick_hint");
                _detailName.AddThemeColorOverride("font_color", UiStyle.Dim);
                _detailMeta.Text = Loc.T("bp.controls_hint");
                _detailDesc.Text = "";
                _detailStats.Text = "";
                return;
            }
            _detailIcon.Texture = ItemDatabase.Instance.GetItemIcon(def.Id);
            _detailName.Text = $"{ItemDatabase.Instance.GetDisplayName(def.Id)}  x{count}";
            _detailName.AddThemeColorOverride("font_color", RarityColor(def.Rarity));
            _detailMeta.Text = $"{Loc.T("bp.type." + def.Type)} - {Loc.T("bp.rarity." + def.Rarity)} - {Loc.T(_selSide == Side.Inventory ? "bp.in_inv" : "bp.in_bag")}";
            _detailDesc.Text = ItemDatabase.Instance.GetDisplayDescription(def.Id);
            var stats = new List<string>();
            if (def.Damage > 0) stats.Add(string.Format(Loc.T(def.Type == ItemType.Tool ? "bp.stat_power_fmt" : "bp.stat_dmg_fmt"), def.Damage));
            if (def.Ranged) stats.Add(string.Format(Loc.T("bp.stat_range_fmt"), Mathf.RoundToInt(def.Range / MapCoords.UnitsPerMeter)));
            if (def.Defense > 0) stats.Add(string.Format(Loc.T("bp.stat_def_fmt"), def.Defense));
            if (def.HealAmount > 0) stats.Add(string.Format(Loc.T("bp.stat_heal_fmt"), def.HealAmount));
            if (def.StaminaAmount > 0) stats.Add(string.Format(Loc.T("bp.stat_stamina_fmt"), def.StaminaAmount));
            if (def.SellPrice > 0) stats.Add(string.Format(Loc.T("bp.stat_sell_fmt"), def.SellPrice));
            _detailStats.Text = string.Join("   ", stats);

            bool toBag = _selSide == Side.Inventory;
            _moveOne.Text = Loc.T(toBag ? "bp.store_one" : "bp.take_one");
            _moveAll.Text = Loc.T(toBag ? "bp.store_all" : "bp.take_all");
            bool equippable = Inventory.IsEquippable(def);
            _equipBtn.Visible = equippable;
            _equipBtn.Text = Loc.T(Inventory.Instance.IsEquipped(def.Id) ? "bp.unequip" : "bp.equip");
            _useBtn.Visible = def.Type == ItemType.Consumable;
            _useBtn.Text = Loc.T("bp.use");

            // Tinh trang: do ben / han su dung.
            _repairBtn.Visible = false;
            if (def.MaxDurability > 0)
            {
                var (cur, max) = ItemConditions.Durability(def.Id);
                bool ok = ItemConditions.IsUsable(def.Id);
                _detailCond.Text = string.Format(Loc.T(ok ? "dur.detail_fmt" : "dur.detail_broken_fmt"), Mathf.RoundToInt(100f * cur / max), cur, max);
                _detailCond.AddThemeColorOverride("font_color", ok ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.4f, 0.35f));
                if (cur < max)
                {
                    _repairBtn.Visible = true;
                    _repairBtn.Text = string.Format(Loc.T("dur.repair_btn_fmt"), ItemConditions.RepairCostText(def.Id));
                    bool near = ItemConditions.NearRepairStation(GetTree().GetFirstNodeInGroup("player") as Node3D);
                    _repairBtn.Disabled = !near || !ItemConditions.CanAffordRepair(def.Id);
                    _repairBtn.TooltipText = Loc.T(near ? "dur.repair_need_mats" : "dur.repair_need_station");
                }
            }
            else if (def.ShelfHours > 0 && ItemConditions.SoonestSpoil(def.Id) is var sp && sp != null)
            {
                var left = System.TimeSpan.FromMilliseconds(Mathf.Max(0, sp.Value.msLeft));
                string t = left.TotalHours >= 1 ? $"{(int)left.TotalHours}h{left.Minutes:00}m" : $"{left.Minutes}m";
                _detailCond.Text = string.Format(Loc.T("fresh.detail_fmt"), t, sp.Value.n);
                _detailCond.AddThemeColorOverride("font_color", ItemConditions.FreshRatio(def.Id) > 0.3f ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.7f, 0.3f));
            }
        }

        private static int CountIn(Side side, string id) =>
            id == null ? 0 : side == Side.Inventory ? Inventory.Instance.CountOf(id) : Backpack.Instance.CountOf(id);

        // ==== Hanh dong ====
        // LUON them vao noi DEN truoc, chi bo khoi noi DI phan da them duoc - noi den day giua chung thi
        // phan con lai van o cho cu (khong bao gio mat do).
        private void Move(Side from, string id, int want)
        {
            if (id == null) return;
            int have = CountIn(from, id);
            int n = Mathf.Min(want, have);
            if (n <= 0) return;
            using var _ = EconomyClient.Suppress();
            int moved;
            if (from == Side.Inventory)
            {
                int before = Backpack.Instance.CountOf(id);
                Backpack.Instance.AddItem(id, n);
                moved = Backpack.Instance.CountOf(id) - before;
                if (moved > 0) Inventory.Instance.RemoveItem(id, moved);
                if (moved < n) _status.Text = Loc.T("backpack.full");
                // Chuyen het do dang trang bi vao balo -> van giu trang bi (Inventory.Equip khong doi hoi o tui).
            }
            else
            {
                int before = Inventory.Instance.CountOf(id);
                Inventory.Instance.AddItem(id, n);
                moved = Inventory.Instance.CountOf(id) - before;
                if (moved > 0) Backpack.Instance.RemoveItem(id, moved);
                if (moved < n) _status.Text = Loc.T("backpack.inventory_full");
            }
            if (moved >= n) _status.Text = "";
        }

        // Cat moi thu trong tui chinh (TRU do dang trang bi) vao balo - de khong bi cuop khi bi ha.
        private void StashAll()
        {
            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
                if (!Inventory.Instance.IsEquipped(id)) Move(Side.Inventory, id, int.MaxValue);
        }

        private void ToggleEquip()
        {
            if (_selId == null || ItemDatabase.Instance.GetItem(_selId) is not ItemDef def) return;
            if (Inventory.Instance.IsEquipped(def.Id)) Inventory.Instance.Unequip(def.Type);
            else Inventory.Instance.Equip(def.Id);
            Refresh();
        }

        private void RepairSelected()
        {
            if (_selId == null) return;
            if (!EconomyClient.Instance.Repair(_selId)) _status.Text = Loc.T("dur.repair_failed");
            Refresh();
        }

        private void UseSelected()
        {
            if (_selId == null) return;
            // Do an/thuoc trong balo: lay 1 ra tui roi dung (UseConsumable chi doc tui chinh).
            if (_selSide == Side.Backpack && Inventory.Instance.CountOf(_selId) == 0) Move(Side.Backpack, _selId, 1);
            if (!Inventory.Instance.UseConsumable(_selId)) _status.Text = Loc.T("bp.cannot_use");
        }

        // Sap xep ca 2 ben: theo nhom (trang bi > do an > nong san > nguyen lieu) roi ten.
        private void SortAll()
        {
            int Rank(ItemStack s) => ItemDatabase.Instance.GetItem(s.ItemId)?.Type switch
            {
                ItemType.Weapon => 0, ItemType.Armor => 1, ItemType.Tool => 2, ItemType.Outfit => 3, ItemType.Consumable => 4,
                ItemType.Crop => 5, ItemType.Seed => 6, ItemType.Material => 7, _ => 8,
            };
            foreach (var list in new[] { Inventory.Instance.Slots, Backpack.Instance.Slots })
            {
                var sorted = list.OrderBy(Rank).ThenBy(s => ItemDatabase.Instance.GetDisplayName(s.ItemId)).ThenByDescending(s => s.Count).ToList();
                list.Clear();
                list.AddRange(sorted);
            }
            Refresh();
        }
    }
}
