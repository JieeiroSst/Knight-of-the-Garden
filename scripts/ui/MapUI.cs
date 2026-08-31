using Godot;

namespace HiepSiVeVuon.UI
{
    // Ban do toan canh (nong trai + thi tran + duong noi giua 2 khu) - nhan [M] de bat/tat.
    // Ve truc tiep bang Control._Draw(), khong can asset rieng - danh dau vi tri nguoi choi,
    // tu cap nhat lien tuc khi ban do dang mo.
    public partial class MapUI : CanvasLayer
    {
        private Control _mapArea;
        private Node3D _player;

        // Khop voi cac hang so vi tri that trong Main.cs (FarmhousePos, VillageAnchor...) -
        // neu cac vi tri do doi thi phai cap nhat lai o day.
        private static readonly Vector2 FarmhousePos2D = new(-300, -60);
        private static readonly Vector2 BarnPos2D = new(-482, 250);
        private static readonly Vector2 CowPasturePos2D = new(-820, -250);
        private static readonly Vector2 VillageAnchor2D = new(9250, 3750);
        private static readonly Vector2 TownHallPos2D = new(9250, 3570);

        // Vung toa do the gioi hien thi tren ban do (bao ca nong trai lan thi tran + le).
        private const float WorldMinX = -2200f, WorldMaxX = 11000f;
        private const float WorldMinZ = -2200f, WorldMaxZ = 6200f;
        private static readonly Vector2 MapSize = new(660, 400);
        private static readonly Vector2 MapOrigin = new(40, 70);

        public override void _Ready()
        {
            var panel = new Panel
            {
                Position = MapOrigin - new Vector2(20, 40),
                CustomMinimumSize = MapSize + new Vector2(40, 70)
            };
            AddChild(panel);

            var title = new Label
            {
                Text = "== BAN DO (nhan [M] de dong) ==",
                Position = new Vector2(20, 8)
            };
            panel.AddChild(title);

            _mapArea = new Control
            {
                Position = new Vector2(20, 40),
                CustomMinimumSize = MapSize
            };
            _mapArea.Draw += DrawMap;
            panel.AddChild(_mapArea);

            Visible = false;
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
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.16f, 0.28f, 0.14f));
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.9f, 0.9f, 0.8f), false, 2f);

            // Duong noi nong trai - thi tran
            _mapArea.DrawLine(WorldToMap(FarmhousePos2D), WorldToMap(VillageAnchor2D), new Color(0.65f, 0.55f, 0.35f), 3f);

            DrawLandmark(FarmhousePos2D, new Color(0.85f, 0.6f, 0.3f), "Nha nong dan");
            DrawLandmark(BarnPos2D, new Color(0.6f, 0.35f, 0.15f), "Nha kho");
            DrawLandmark(CowPasturePos2D, new Color(0.85f, 0.85f, 0.85f), "Trang trai bo");
            DrawLandmark(TownHallPos2D, new Color(0.75f, 0.25f, 0.2f), "Toa Thi Chinh");
            DrawLandmark(VillageAnchor2D, new Color(0.95f, 0.85f, 0.4f), "Thi Tran");

            if (_player != null)
            {
                var p = WorldToMap(new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z));
                _mapArea.DrawCircle(p, 6f, new Color(0.2f, 0.85f, 1f));
                _mapArea.DrawCircle(p, 6f, Colors.White, false, 1.5f);
                _mapArea.DrawString(ThemeDB.FallbackFont, p + new Vector2(9, -4), "Ban", HorizontalAlignment.Left, -1, 13, Colors.White);
            }
        }

        private void DrawLandmark(Vector2 worldXZ, Color color, string label)
        {
            var p = WorldToMap(worldXZ);
            _mapArea.DrawCircle(p, 5f, color);
            _mapArea.DrawCircle(p, 5f, Colors.Black, false, 1f);
            _mapArea.DrawString(ThemeDB.FallbackFont, p + new Vector2(8, 4), label, HorizontalAlignment.Left, -1, 13, Colors.White);
        }
    }
}
