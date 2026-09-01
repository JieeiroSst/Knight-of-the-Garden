using Godot;

namespace HiepSiVeVuon.UI
{
    // Ban do toan canh (nong trai + thi tran + toan bo 11 khu vuc the gioi mo) - nhan [M] de
    // bat/tat. Ve truc tiep bang Control._Draw() (khong can asset rieng) - danh dau vi tri nguoi
    // choi, tu cap nhat lien tuc khi ban do dang mo.
    //
    // Cap nhat lai (sau khi the gioi mo rong ra ~11 khu vuc, xem Main.cs): vung toa do hien thi
    // MO RONG rat nhieu (tu ~14000x8400 don vi len ~19900x17800) de bao het cac khu moi - o quy mo
    // nay, cac tieu khu NHO ben trong trang trai (Khu Chan Nuoi/Nha O/Trong Trot/Nha Kho/San
    // Xuat...) qua nho de con doc duoc rieng biet, nen GOM LAI thanh 1 diem "Nong Trai" duy nhat
    // (giu Nha Kho rieng vi la cong trinh lon/de nhan). Giao dien ve lai theo huong "ban do phieu
    // luu" (khung vang tren nen toi, icon rieng theo loai dia diem, vong tron do canh bao noi co
    // quai, la ban chi huong) thay vi cac cham tron dong mau don dieu nhu truoc.
    public partial class MapUI : CanvasLayer
    {
        private Control _mapArea;
        private Node3D _player;

        // Diem den nguoi choi tu danh dau bang cach bam vao ban do - HUD doc gia tri nay (qua
        // group "map_ui") de ve mui ten chi huong (xem HUD.DrawCompass).
        public bool HasWaypoint { get; private set; }
        public Vector3 Waypoint { get; private set; }

        private enum Icon { Dot, House, Triangle, Tree, Water, Diamond, Cross, Flower }

        private readonly struct Landmark
        {
            public readonly Vector2 Pos;
            public readonly Color Color;
            public readonly string Label;
            public readonly Icon Icon;
            public readonly bool Danger;
            public Landmark(Vector2 pos, Color color, string label, Icon icon, bool danger)
            {
                Pos = pos; Color = color; Label = label; Icon = icon; Danger = danger;
            }
        }

        // Khop voi cac hang so vi tri that trong Main.cs - neu cac vi tri do doi thi phai cap
        // nhat lai o day. Nhom theo LOAI (loi/nong trai, thi tran, 11 khu the gioi mo) thay vi
        // liet ke phang nhu truoc, de de doi chieu khi Main.cs doi vi tri.
        private static readonly Landmark[] Landmarks =
        {
            // ---- Loi trang trai (gom lai o quy mo ban do the gioi, xem ghi chu tren) ----
            new(new Vector2(-300, -60), new Color(0.85f, 0.6f, 0.3f), "Nong Trai", Icon.House, false),
            new(new Vector2(-482, 250), new Color(0.6f, 0.35f, 0.15f), "Nha Kho", Icon.House, false),

            // ---- Thi tran (khu do thi cu, VillageAnchor) ----
            new(new Vector2(9250, 3750), new Color(0.95f, 0.85f, 0.4f), "Thi Tran", Icon.House, false),

            // ---- Dia hinh/cong trinh phu gan nong trai (da co truoc) ----
            new(new Vector2(1600, -350), new Color(0.55f, 0.42f, 0.32f), "Cao Nguyen", Icon.Triangle, false),
            new(new Vector2(2650, 750), new Color(0.55f, 0.42f, 0.32f), "", Icon.Triangle, false),
            new(new Vector2(1750, 1950), new Color(0.55f, 0.42f, 0.32f), "", Icon.Triangle, false),
            new(new Vector2(-2552, 390), new Color(0.95f, 0.78f, 0.1f), "Dong Hoa Huong Duong", Icon.Flower, false),

            // ---- 11 khu vuc "the gioi mo" (xem Main.cs cac hang so *RegionCenter/MineEntrancePos) ----
            new(new Vector2(2500, -400), new Color(0.65f, 0.5f, 0.25f), "Mo", Icon.Diamond, true),
            new(new Vector2(200, -6800), new Color(0.6f, 0.6f, 0.62f), "Nui", Icon.Triangle, true),
            new(new Vector2(-3600, -5700), new Color(0.25f, 0.5f, 0.22f), "Rung", Icon.Tree, true),
            new(new Vector2(-5200, 5100), new Color(0.75f, 0.78f, 0.35f), "Dong Ruong", Icon.Dot, false),
            new(new Vector2(-6300, -2650), new Color(0.25f, 0.5f, 0.7f), "Ho", Icon.Water, true),
            new(new Vector2(2300, 7000), new Color(0.3f, 0.55f, 0.68f), "Song", Icon.Water, false),
            new(new Vector2(5700, 5000), new Color(0.8f, 0.68f, 0.45f), "Lang", Icon.House, false),
            new(new Vector2(-1800, 7300), new Color(0.7f, 0.68f, 0.62f), "Thanh Pho", Icon.House, false),
            new(new Vector2(6700, -2650), new Color(0.55f, 0.4f, 0.6f), "Phe Tich", Icon.Diamond, true),
            new(new Vector2(7000, 1400), new Color(0.75f, 0.75f, 0.72f), "Nghia Dia", Icon.Cross, true),
            new(new Vector2(-6900, 1400), new Color(0.3f, 0.42f, 0.28f), "Dam Lay", Icon.Water, true),
            new(new Vector2(4100, -5650), new Color(0.4f, 0.4f, 0.42f), "Hang Dong", Icon.Diamond, true),
        };

        // Vung toa do THE GIOI hien thi tren ban do - MO RONG de bao het 11 khu vuc moi (truoc
        // day chi ~14000x8400, chi du cho nong trai+thi tran). Bien luon rong hon toa do xa nhat
        // (~8300-8900) mot khoang du de khong bi cham vien.
        private const float WorldMinX = -8700f, WorldMaxX = 11200f;
        private const float WorldMinZ = -8600f, WorldMaxZ = 9200f;
        private static readonly Vector2 MapSize = new(600, 536); // ti le gan dung ty le vung the gioi o tren
        private static readonly Vector2 MapOrigin = new(40, 70);

        private static readonly Color FrameGold = new(0.75f, 0.62f, 0.35f);
        private static readonly Color TextCream = new(0.95f, 0.92f, 0.82f);

        public override void _Ready()
        {
            AddToGroup("map_ui");

            var panel = new Panel
            {
                Position = MapOrigin - new Vector2(20, 40),
                CustomMinimumSize = MapSize + new Vector2(40, 70)
            };
            // Khung "ban do phieu luu": nen toi + vien vang + goc bo tron + do bong, thay cho
            // Panel xam mac dinh cua Godot.
            var frameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.09f, 0.07f, 0.94f),
                BorderColor = FrameGold,
                BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f),
            };
            panel.AddThemeStyleboxOverride("panel", frameStyle);
            AddChild(panel);

            var title = new Label
            {
                Text = "B A N   D O   T H E   G I O I",
                Position = new Vector2(20, 8),
            };
            title.AddThemeColorOverride("font_color", FrameGold);
            panel.AddChild(title);

            var hint = new Label
            {
                Text = "[M] dong  -  bam de danh dau diem den  -  vong do = khu co quai",
                Position = new Vector2(20, 28),
            };
            hint.AddThemeColorOverride("font_color", new Color(TextCream, 0.6f));
            hint.AddThemeFontSizeOverride("font_size", 12);
            panel.AddChild(hint);

            _mapArea = new Control
            {
                Position = MapOrigin,
                CustomMinimumSize = MapSize
            };
            _mapArea.Draw += DrawMap;
            _mapArea.GuiInput += OnMapInput;
            panel.AddChild(_mapArea);

            Visible = false;
        }

        // Bam chuot trai vao ban do -> quy doi vi tri (nguoc lai voi WorldToMap) thanh toa do
        // THE GIOI THAT, dat lam "diem den" - HUD se ve mui ten chi huong toi day (xem
        // HUD.DrawCompass), khong tu dong dan duong, chi chi huong.
        private void OnMapInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                Vector2 world = MapToWorld(mb.Position);
                Waypoint = new Vector3(world.X, 0, world.Y);
                HasWaypoint = true;
                _mapArea.QueueRedraw();
            }
        }

        private static Vector2 MapToWorld(Vector2 mapPos)
        {
            float tx = mapPos.X / MapSize.X;
            float tz = mapPos.Y / MapSize.Y;
            return new Vector2(WorldMinX + tx * (WorldMaxX - WorldMinX), WorldMinZ + tz * (WorldMaxZ - WorldMinZ));
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            _mapArea.QueueRedraw();
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_map"))
            {
                Visible = !Visible;
                GetViewport().SetInputAsHandled();
            }
        }

        private static Vector2 WorldToMap(Vector2 worldXZ)
        {
            float tx = (worldXZ.X - WorldMinX) / (WorldMaxX - WorldMinX);
            float tz = (worldXZ.Y - WorldMinZ) / (WorldMaxZ - WorldMinZ);
            return new Vector2(tx * MapSize.X, tz * MapSize.Y);
        }

        private void DrawMap()
        {
            // Nen: sac xanh rung tham (thay vi 1 mau xanh phang truoc day) + luoi mo nhat kieu
            // "ban do giay" + 2 vung tint rieng cho khu vuc van minh (trang trai/thi tran) de
            // noi bat giua bien wilderness rong lon moi.
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.1f, 0.18f, 0.1f));
            DrawGrid();

            // Tint nhe quanh trang trai + thi tran (khu "an toan", da thuan hoa) - hinh tron mo
            // phong pham vi tuong da/khu do thi, KHONG chinh xac tuyet doi, chi mang tinh dinh
            // huong thi giac.
            DrawSafeZoneTint(new Vector2(202, 390), 3400f, new Color(0.35f, 0.42f, 0.22f, 0.35f));
            DrawSafeZoneTint(new Vector2(9250, 3750), 2000f, new Color(0.42f, 0.38f, 0.22f, 0.35f));

            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), FrameGold, false, 2f);

            // Duong noi nong trai - thi tran (da co truoc).
            _mapArea.DrawLine(WorldToMap(new Vector2(-300, -60)), WorldToMap(new Vector2(9250, 3750)), new Color(0.65f, 0.55f, 0.35f, 0.8f), 2.5f);

            foreach (var lm in Landmarks)
                DrawLandmark(lm);

            // Vung que nuoc Phap (xem Main.FrenchRegionCenter) cach xa toi 10km - qua xa de ve
            // dung ty le. Thay bang 1 mui ten chi huong o mep trai ban do.
            var arrowTip = new Vector2(6, MapSize.Y * 0.5f);
            _mapArea.DrawLine(arrowTip + new Vector2(26, 0), arrowTip, FrameGold, 3f);
            _mapArea.DrawLine(arrowTip, arrowTip + new Vector2(9, -7), FrameGold, 3f);
            _mapArea.DrawLine(arrowTip, arrowTip + new Vector2(9, 7), FrameGold, 3f);
            _mapArea.DrawString(ThemeDB.FallbackFont, arrowTip + new Vector2(2, -12),
                "Vung que Phap (~10km)", HorizontalAlignment.Left, -1, 12, TextCream);

            DrawCompassRose(new Vector2(MapSize.X - 34, MapSize.Y - 34), 16f);

            if (HasWaypoint)
            {
                var wp = WorldToMap(new Vector2(Waypoint.X, Waypoint.Z));
                _mapArea.DrawCircle(wp, 9f, new Color(1f, 0.25f, 0.2f, 0.3f));
                _mapArea.DrawLine(wp + new Vector2(-5, -5), wp + new Vector2(5, 5), Colors.White, 2f);
                _mapArea.DrawLine(wp + new Vector2(-5, 5), wp + new Vector2(5, -5), Colors.White, 2f);
            }

            if (_player != null)
            {
                var p = WorldToMap(new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z));
                _mapArea.DrawCircle(p, 8f, new Color(0.2f, 0.85f, 1f, 0.25f)); // hao quang nhe
                _mapArea.DrawCircle(p, 5f, new Color(0.2f, 0.85f, 1f));
                _mapArea.DrawCircle(p, 5f, Colors.White, false, 1.5f);
                DrawLabelPlate(p + new Vector2(9, -4), "Ban");
            }
        }

        private void DrawGrid()
        {
            var gridColor = new Color(1f, 1f, 1f, 0.045f);
            for (float x = 0; x <= MapSize.X; x += 40f)
                _mapArea.DrawLine(new Vector2(x, 0), new Vector2(x, MapSize.Y), gridColor, 1f);
            for (float y = 0; y <= MapSize.Y; y += 40f)
                _mapArea.DrawLine(new Vector2(0, y), new Vector2(MapSize.X, y), gridColor, 1f);
        }

        private void DrawSafeZoneTint(Vector2 worldXZ, float worldRadius, Color color)
        {
            var p = WorldToMap(worldXZ);
            float rx = worldRadius / (WorldMaxX - WorldMinX) * MapSize.X;
            _mapArea.DrawCircle(p, rx, color);
        }

        private void DrawCompassRose(Vector2 center, float radius)
        {
            _mapArea.DrawCircle(center, radius, new Color(0, 0, 0, 0.3f));
            _mapArea.DrawCircle(center, radius, FrameGold, false, 1.4f);
            _mapArea.DrawLine(center + new Vector2(0, -radius), center + new Vector2(0, radius), FrameGold, 1.2f);
            _mapArea.DrawLine(center + new Vector2(-radius, 0), center + new Vector2(radius, 0), FrameGold, 1.2f);
            var font = ThemeDB.FallbackFont;
            _mapArea.DrawString(font, center + new Vector2(-4, -radius - 5), "B", HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(-4, radius + 15), "N", HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(radius + 5, 4), "D", HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(-radius - 15, 4), "T", HorizontalAlignment.Left, -1, 12, TextCream);
        }

        private void DrawLandmark(Landmark lm)
        {
            var p = WorldToMap(lm.Pos);

            // Vong tron do canh bao mo, mem (khong dung nhon/dash de tranh phuc tap) quanh cac
            // khu co quai vat - giup nguoi choi nhan biet NGAY tren ban do truoc khi di toi.
            if (lm.Danger)
                _mapArea.DrawArc(p, 12f, 0f, Mathf.Tau, 28, new Color(0.85f, 0.15f, 0.15f, 0.55f), 1.6f, true);

            switch (lm.Icon)
            {
                case Icon.House: DrawHouseIcon(p, lm.Color); break;
                case Icon.Triangle: DrawTriangleIcon(p, lm.Color); break;
                case Icon.Tree: DrawTreeIcon(p, lm.Color); break;
                case Icon.Water: DrawWaterIcon(p, lm.Color); break;
                case Icon.Diamond: DrawDiamondIcon(p, lm.Color); break;
                case Icon.Cross: DrawCrossIcon(p, lm.Color); break;
                case Icon.Flower: DrawFlowerIcon(p, lm.Color); break;
                default:
                    _mapArea.DrawCircle(p, 5f, lm.Color);
                    _mapArea.DrawCircle(p, 5f, Colors.Black, false, 1f);
                    break;
            }

            if (!string.IsNullOrEmpty(lm.Label))
                DrawLabelPlate(p + new Vector2(9, 4), lm.Label);
        }

        // Nhan co nen mo phia sau (thay vi chu trang tho tren nen ban do da mau, de doc duoc du
        // dung tren nen sang hay toi) - 1 kieu thuong thay o ban do cac game phieu luu khac.
        private void DrawLabelPlate(Vector2 anchorLeft, string text)
        {
            var font = ThemeDB.FallbackFont;
            const int fontSize = 12;
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
            var rectPos = anchorLeft + new Vector2(-2, -size.Y * 0.75f);
            _mapArea.DrawRect(new Rect2(rectPos, size + new Vector2(4, 4)), new Color(0, 0, 0, 0.5f));
            _mapArea.DrawString(font, anchorLeft + new Vector2(0, size.Y * 0.3f), text, HorizontalAlignment.Left, -1, fontSize, TextCream);
        }

        private void DrawHouseIcon(Vector2 p, Color color)
        {
            const float s = 6f;
            var baseRect = new Rect2(p + new Vector2(-s, 0), new Vector2(s * 2f, s * 1.3f));
            _mapArea.DrawRect(baseRect, color);
            _mapArea.DrawRect(baseRect, Colors.Black, false, 1f);
            Vector2[] roof = { p + new Vector2(-s * 1.25f, 0), p + new Vector2(0, -s * 1.2f), p + new Vector2(s * 1.25f, 0) };
            _mapArea.DrawColoredPolygon(roof, color.Darkened(0.25f));
            _mapArea.DrawPolyline(new[] { roof[0], roof[1], roof[2] }, Colors.Black, 1f, true);
        }

        private void DrawTriangleIcon(Vector2 p, Color color)
        {
            const float s = 7f;
            Vector2[] pts = { p + new Vector2(0, -s), p + new Vector2(s * 0.87f, s * 0.6f), p + new Vector2(-s * 0.87f, s * 0.6f) };
            _mapArea.DrawColoredPolygon(pts, color);
            _mapArea.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[0] }, Colors.Black, 1f, true);
        }

        private void DrawTreeIcon(Vector2 p, Color color)
        {
            const float s = 7f;
            _mapArea.DrawRect(new Rect2(p + new Vector2(-1.4f, s * 0.3f), new Vector2(2.8f, s * 0.7f)), new Color(0.4f, 0.28f, 0.16f));
            Vector2[] pts = { p + new Vector2(0, -s), p + new Vector2(s * 0.8f, s * 0.35f), p + new Vector2(-s * 0.8f, s * 0.35f) };
            _mapArea.DrawColoredPolygon(pts, color);
            _mapArea.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[0] }, Colors.Black, 1f, true);
        }

        private void DrawWaterIcon(Vector2 p, Color color)
        {
            const float s = 7f;
            _mapArea.DrawCircle(p, s, color);
            _mapArea.DrawCircle(p, s, Colors.Black, false, 1f);
            for (int i = -1; i <= 1; i += 2)
            {
                float y = p.Y + i * s * 0.32f;
                _mapArea.DrawLine(new Vector2(p.X - s * 0.5f, y), new Vector2(p.X + s * 0.5f, y), Colors.White, 1.3f, true);
            }
        }

        private void DrawDiamondIcon(Vector2 p, Color color)
        {
            const float s = 7f;
            Vector2[] pts = { p + new Vector2(0, -s), p + new Vector2(s, 0), p + new Vector2(0, s), p + new Vector2(-s, 0) };
            _mapArea.DrawColoredPolygon(pts, color);
            _mapArea.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colors.Black, 1f, true);
        }

        private void DrawCrossIcon(Vector2 p, Color color)
        {
            const float s = 7f;
            _mapArea.DrawLine(p + new Vector2(0, -s), p + new Vector2(0, s * 0.6f), color, 2.6f, true);
            _mapArea.DrawLine(p + new Vector2(-s * 0.55f, -s * 0.2f), p + new Vector2(s * 0.55f, -s * 0.2f), color, 2.6f, true);
        }

        private void DrawFlowerIcon(Vector2 p, Color color)
        {
            const float s = 4.5f;
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Tau * i / 5f;
                _mapArea.DrawCircle(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, s * 0.75f, color);
            }
            _mapArea.DrawCircle(p, s * 0.6f, new Color(0.5f, 0.35f, 0.1f));
        }
    }
}
