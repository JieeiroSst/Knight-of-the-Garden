using Godot;

namespace HiepSiVeVuon.UI
{
    // Khung bang dung chung cho cac UI nong trai moi (Che Tao/Giao Hang/Tang Qua/Nhat Ky) - cung
    // phong cach go toi + vien vang voi CookingUI.cs.
    public static class UiStyle
    {
        public static readonly Color Gold = new(0.75f, 0.62f, 0.35f);
        public static readonly Color Text = new(0.95f, 0.92f, 0.85f);
        public static readonly Color Dim = new(0.6f, 0.58f, 0.55f);
        public static readonly Color Hint = new(0.85f, 0.78f, 0.5f, 0.75f);

        public static Panel MakePanel(Vector2 pos, Vector2 size)
        {
            var panel = new Panel { Position = pos, CustomMinimumSize = size, Size = size };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.97f),
                BorderColor = Gold,
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            });
            return panel;
        }

        public static Label Title(string text)
        {
            var l = new Label { Text = text };
            l.AddThemeColorOverride("font_color", Gold);
            l.AddThemeFontSizeOverride("font_size", 18);
            return l;
        }

        public static Label Small(string text, Color? color = null)
        {
            var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Word };
            l.AddThemeColorOverride("font_color", color ?? Hint);
            l.AddThemeFontSizeOverride("font_size", 12);
            return l;
        }

        public static void Clear(Node container)
        {
            foreach (Node c in container.GetChildren()) c.QueueFree();
        }
    }
}
