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
        private static readonly (string outputId, (string ing, int qty)[] ingredients)[] Recipes =
        {
            ("sup_rau", new[] { ("carrot", 1), ("cabbage", 1) }),
            ("trung_chien", new[] { ("egg", 2) }),
            ("banh_bi_ngo", new[] { ("pumpkin", 1), ("milk", 1) }),
            ("salad_ca_chua", new[] { ("tomato", 2), ("pho_mai", 1) }),
            ("banh_mi_trung", new[] { ("wheat", 1), ("egg", 1) }),
            ("sup_ca", new[] { ("ca", 1), ("carrot", 1) }),
        };

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
                var outDef = ItemDatabase.Instance.GetItem(recipe.outputId);
                if (outDef == null) continue;

                bool canCook = recipe.ingredients.All(ing => Inventory.Instance.CountOf(ing.ing) >= ing.qty);
                string ingText = string.Join(", ", recipe.ingredients.Select(ing =>
                {
                    var d = ItemDatabase.Instance.GetItem(ing.ing);
                    return $"{(d != null ? ItemDatabase.Instance.GetDisplayName(ing.ing) : ing.ing)} x{ing.qty}";
                }));

                var row = new VBoxContainer();
                _list.AddChild(row);

                var nameLabel = new Label { Text = string.Format(Loc.T("cooking.dish_fmt"), ItemDatabase.Instance.GetDisplayName(recipe.outputId), outDef.HealAmount) };
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

        private void Cook((string outputId, (string ing, int qty)[] ingredients) recipe)
        {
            foreach (var ing in recipe.ingredients)
                if (Inventory.Instance.CountOf(ing.ing) < ing.qty) { Refresh(); return; }

            foreach (var ing in recipe.ingredients)
                Inventory.Instance.RemoveItem(ing.ing, ing.qty);
            Inventory.Instance.AddItem(recipe.outputId, 1);

            var def = ItemDatabase.Instance.GetItem(recipe.outputId);
            GD.Print(string.Format(Loc.T("cooking.cooked_fmt"), def != null ? ItemDatabase.Instance.GetDisplayName(recipe.outputId) : null));
            Refresh();
        }
    }
}
