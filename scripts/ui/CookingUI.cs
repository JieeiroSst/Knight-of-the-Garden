using Godot;
using System.Linq;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
    // Danh sach cong thuc nau an (xem CookingStation.cs, mo bang [E] tai bep) - ket hop nguyen
    // lieu THU HOACH/SAN BAT duoc thanh mon an hoi phuc sinh luc, khac voi thuoc (potion) mua san.
    public partial class CookingUI : CanvasLayer
    {
        // Cong thuc nau an nap tu data/cooking.json (dung CHUNG voi backend de server kiem tra
        // nguyen lieu khi nau - xem EconomyClient/backend economyService).
        private static System.Collections.Generic.IEnumerable<RecipeDef> Recipes => ItemDatabase.Instance.Cooking;

        private VBoxContainer _list;
        private readonly LocalizedLabelSet _loc = new();

        public override void _Ready()
        {
            AddToGroup("cooking_ui");

            var panel = new Panel { Position = new Vector2(260, 60), CustomMinimumSize = new Vector2(440, 420) };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.97f),
                BorderColor = new Color(0.75f, 0.62f, 0.35f),
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            });
            AddChild(panel);

            var vb = new VBoxContainer { Position = new Vector2(20, 16), CustomMinimumSize = new Vector2(400, 0) };
            panel.AddChild(vb);

            var title = new Label();
            _loc.Track(title, "cooking.title");
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 18);
            vb.AddChild(title);
            vb.AddChild(new HSeparator());

            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(400, 300) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
            _list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_list);

            var close = new Button();
            _loc.Track(close, "common.close");
            close.Pressed += () => Visible = false;
            vb.AddChild(close);

            Loc.LanguageChanged += OnLanguageChanged;

            Visible = false;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            _loc.Refresh();
            if (Visible) Refresh();
        }

        public void Open()
        {
            Refresh();
            Visible = true;
        }

        private void Refresh()
        {
            foreach (Node c in _list.GetChildren()) c.QueueFree();

            foreach (var recipe in Recipes)
            {
                var outDef = ItemDatabase.Instance.GetItem(recipe.OutputId);
                if (outDef == null) continue;

                bool canCook = recipe.Ingredients.All(ing => Inventory.Instance.CountOf(ing.ItemId) >= ing.Count);
                string ingText = string.Join(", ", recipe.Ingredients.Select(ing => $"{ItemDatabase.Instance.GetDisplayName(ing.ItemId)} x{ing.Count}"));

                var row = new VBoxContainer();
                _list.AddChild(row);

                var nameLabel = new Label { Text = string.Format(Loc.T("cooking.dish_fmt"), ItemDatabase.Instance.GetDisplayName(recipe.OutputId), outDef.HealAmount)
                    + (outDef.StaminaAmount > 0 ? string.Format(Loc.T("cooking.stamina_fmt"), outDef.StaminaAmount) : "") };
                nameLabel.AddThemeColorOverride("font_color", canCook ? new Color(0.95f, 0.92f, 0.85f) : new Color(0.6f, 0.58f, 0.55f));
                row.AddChild(nameLabel);

                var reqLabel = new Label { Text = string.Format(Loc.T("cooking.need_fmt"), ingText) };
                reqLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.78f, 0.5f, 0.75f));
                reqLabel.AddThemeFontSizeOverride("font_size", 12);
                row.AddChild(reqLabel);

                var btn = new Button { Text = canCook ? Loc.T("cooking.cook_btn") : Loc.T("cooking.missing_ingredients"), Disabled = !canCook };
                var r = recipe;
                btn.Pressed += () => Cook(r);
                row.AddChild(btn);

                row.AddChild(new HSeparator());
            }
        }

        private void Cook(RecipeDef recipe)
        {
            // Server kiem tra nguyen lieu theo cooking.json (xem EconomyClient.Craft).
            if (!EconomyClient.Instance.Craft(recipe, cooking: true)) { Refresh(); return; }
            AchievementSystem.Instance.Add("dishes_cooked");
            GD.Print(string.Format(Loc.T("cooking.cooked_fmt"), ItemDatabase.Instance.GetDisplayName(recipe.OutputId)));
            Refresh();
        }
    }
}
