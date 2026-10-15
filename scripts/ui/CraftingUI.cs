using Godot;
using System.Linq;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Ban Che Tao (xem CraftingBench.cs) - cong thuc nap tu data/recipes.json, mot so cong thuc
    // can cap ky nang nhat dinh moi mo khoa (RecipeDef.UnlockSkill/UnlockLevel).
    public partial class CraftingUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _title;

        public override void _Ready()
        {
            AddToGroup("crafting_ui");
            var panel = UiStyle.MakePanel(new Vector2(240, 50), new Vector2(480, 440));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 16), CustomMinimumSize = new Vector2(440, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(440, 320) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(425, 0) };
            _list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
            Inventory.Instance.InventoryChanged += () => { if (Visible) Refresh(); };
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open()
        {
            Refresh();
            Visible = true;
        }

        private static bool IsUnlocked(RecipeDef r) =>
            string.IsNullOrEmpty(r.UnlockSkill)
            || (System.Enum.TryParse<SkillType>(r.UnlockSkill, out var s) && SkillSystem.Instance.GetLevel(s) >= r.UnlockLevel);

        private void Refresh()
        {
            _title.Text = Loc.T("craft.title");
            UiStyle.Clear(_list);
            var db = ItemDatabase.Instance;
            foreach (var r in db.Recipes.Where(x => string.IsNullOrEmpty(x.Station)))
            {
                if (db.GetItem(r.OutputId) == null) continue;
                bool unlocked = IsUnlocked(r);
                bool canCraft = unlocked && r.Ingredients.All(i => Inventory.Instance.CountOf(i.ItemId) >= i.Count);

                var row = new HBoxContainer();
                _list.AddChild(row);
                var icon = new TextureRect
                {
                    Texture = db.GetItemIcon(r.OutputId), CustomMinimumSize = new Vector2(28, 28),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                row.AddChild(icon);

                var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(info);
                var name = new Label { Text = $"{db.GetDisplayName(r.OutputId)} x{r.OutputCount}" };
                name.AddThemeColorOverride("font_color", canCraft ? UiStyle.Text : UiStyle.Dim);
                info.AddChild(name);
                string need = unlocked
                    ? string.Join(", ", r.Ingredients.Select(i => $"{db.GetDisplayName(i.ItemId)} {Inventory.Instance.CountOf(i.ItemId)}/{i.Count}"))
                    : string.Format(Loc.T("craft.locked_fmt"), Loc.T("skill." + r.UnlockSkill), r.UnlockLevel);
                info.AddChild(UiStyle.Small(need));

                var btn = new Button { Text = Loc.T("craft.craft_btn"), Disabled = !canCraft, CustomMinimumSize = new Vector2(80, 0) };
                var recipe = r;
                btn.Pressed += () => Craft(recipe);
                row.AddChild(btn);
            }
        }

        private void Craft(RecipeDef r)
        {
            if (!IsUnlocked(r) || !r.Ingredients.All(i => Inventory.Instance.CountOf(i.ItemId) >= i.Count)) { Refresh(); return; }
            // Server kiem tra nguyen lieu theo recipes.json (xem EconomyClient.Craft).
            if (!EconomyClient.Instance.Craft(r, cooking: false)) { Refresh(); return; }
            AchievementSystem.Instance.Add("items_crafted");
            Notify.Show(string.Format(Loc.T("craft.crafted_fmt"), ItemDatabase.Instance.GetDisplayName(r.OutputId), r.OutputCount));
            Refresh();
        }
    }
}
