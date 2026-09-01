using Godot;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Danh sach hat giong dang co trong tui do de Player TU CHON khi bam Space tren 1 o dat
    // trong (xem FarmPlot.RequestPlant) - thay the co che "moi o tu dong trong 1 giong co dinh
    // theo khu" truoc day (DefaultSeedId gio chi con la fallback an toan).
    public partial class SeedSelectUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _empty;
        private FarmPlot _targetPlot;

        public override void _Ready()
        {
            AddToGroup("seed_select_ui");

            var panel = new Panel
            {
                Position = new Vector2(300, 140),
                CustomMinimumSize = new Vector2(360, 300),
            };
            var frameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.97f),
                BorderColor = new Color(0.75f, 0.62f, 0.35f),
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            };
            panel.AddThemeStyleboxOverride("panel", frameStyle);
            AddChild(panel);

            var vb = new VBoxContainer { Position = new Vector2(20, 16), CustomMinimumSize = new Vector2(320, 0) };
            panel.AddChild(vb);

            var title = new Label { Text = "CHON GIONG DE TRONG" };
            title.AddThemeColorOverride("font_color", new Color(0.75f, 0.62f, 0.35f));
            title.AddThemeFontSizeOverride("font_size", 18);
            vb.AddChild(title);

            vb.AddChild(new HSeparator());

            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(320, 190) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(310, 0) };
            scroll.AddChild(_list);

            _empty = new Label
            {
                Text = "Ban khong co hat giong nao. Mua o cua hang!",
                AutowrapMode = TextServer.AutowrapMode.Word,
                CustomMinimumSize = new Vector2(310, 0),
                Visible = false,
            };
            _empty.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.82f, 0.7f));
            vb.AddChild(_empty);

            var cancel = new Button { Text = "Huy" };
            cancel.Pressed += () => Visible = false;
            vb.AddChild(cancel);

            Visible = false;
        }

        public void Open(FarmPlot plot)
        {
            _targetPlot = plot;
            RefreshList();
            Visible = true;
        }

        private void RefreshList()
        {
            foreach (Node c in _list.GetChildren()) c.QueueFree();

            int shown = 0;
            foreach (var stack in Inventory.Instance.Slots)
            {
                var def = ItemDatabase.Instance.GetItem(stack.ItemId);
                if (def == null || def.Type != ItemType.Seed || stack.Count <= 0) continue;

                shown++;
                var btn = new Button { Text = $"{def.Name} (dang co {stack.Count})" };
                string seedId = stack.ItemId;
                btn.Pressed += () => ChooseSeed(seedId);
                _list.AddChild(btn);
            }
            _empty.Visible = shown == 0;
        }

        private void ChooseSeed(string seedId)
        {
            if (_targetPlot != null && IsInstanceValid(_targetPlot))
                _targetPlot.Plant(seedId);
            Visible = false;
        }
    }
}
