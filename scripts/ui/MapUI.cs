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

        // Diem den nguoi choi tu danh dau bang cach bam vao ban do - HUD doc gia tri nay (qua
        // group "map_ui") de ve mui ten chi huong (xem HUD.DrawCompass).
        public bool HasWaypoint { get; private set; }
        public Vector3 Waypoint { get; private set; }

        // Khop voi cac hang so vi tri that trong Main.cs (FarmhousePos, VillageAnchor...) -
        // neu cac vi tri do doi thi phai cap nhat lai o day.
        private static readonly Vector2 FarmhousePos2D = new(-300, -60);
        private static readonly Vector2 BarnPos2D = new(-482, 250);
        private static readonly Vector2 CowPasturePos2D = new(-820, -250);
        private static readonly Vector2 CowherdHousePos2D = new(-1100, -250);
        private static readonly Vector2 HorseStablePos2D = new(-820, -650);
        private static readonly Vector2 StablehandHousePos2D = new(-1100, -650);
        private static readonly Vector2 ChickenCoopPos2D = new(-820, -990);
        private static readonly Vector2 PoultryKeeperHousePos2D = new(-1100, -990);
        private static readonly Vector2 VillageAnchor2D = new(9250, 3750);
        private static readonly Vector2 TownHallPos2D = new(9250, 3570);

        // 3 cao nguyen hoang da phia dong nong trai (xem Main.BuildPlateaus).
        private static readonly Vector2[] PlateauPos2D =
        {
            new(1600, -350), new(2650, 750), new(1750, 1950)
        };

        // Canh dong hoa huong duong phia tay nong trai (xem Main.BuildSunflowerField).
        private static readonly Vector2 SunflowerFieldPos2D = new(-2552, 390);

        // Vung toa do the gioi hien thi tren ban do (bao ca nong trai lan thi tran + le).
        // WorldMinX mo rong tu -2200 -> -3000 de bao ca canh dong huong duong o -2552 (xem
        // Main.SunflowerFieldCenter) sau khi doi ra 100m.
        private const float WorldMinX = -3000f, WorldMaxX = 11000f;
        private const float WorldMinZ = -2200f, WorldMaxZ = 6200f;
        private static readonly Vector2 MapSize = new(660, 400);
        private static readonly Vector2 MapOrigin = new(40, 70);

        public override void _Ready()
        {
            AddToGroup("map_ui");

            var panel = new Panel
            {
                Position = MapOrigin - new Vector2(20, 40),
                CustomMinimumSize = MapSize + new Vector2(40, 70)
            };
            AddChild(panel);

            var title = new Label
            {
                Text = "== BAN DO (nhan [M] de dong, bam de danh dau diem den) ==",
                Position = new Vector2(20, 8)
            };
            panel.AddChild(title);

            _mapArea = new Control
            {
                Position = new Vector2(20, 40),
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
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.16f, 0.28f, 0.14f));
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.9f, 0.9f, 0.8f), false, 2f);

            // Duong noi nong trai - thi tran
            _mapArea.DrawLine(WorldToMap(FarmhousePos2D), WorldToMap(VillageAnchor2D), new Color(0.65f, 0.55f, 0.35f), 3f);

            DrawLandmark(FarmhousePos2D, new Color(0.85f, 0.6f, 0.3f), "Nha nong dan");
            DrawLandmark(BarnPos2D, new Color(0.6f, 0.35f, 0.15f), "Nha kho");
            DrawLandmark(CowPasturePos2D, new Color(0.85f, 0.85f, 0.85f), "Trang trai bo");
            DrawLandmark(CowherdHousePos2D, new Color(0.7f, 0.5f, 0.3f), "Nha nguoi cham bo");
            DrawLandmark(HorseStablePos2D, new Color(0.6f, 0.42f, 0.25f), "Chuong ngua");
            DrawLandmark(StablehandHousePos2D, new Color(0.7f, 0.5f, 0.3f), "Nha nguoi cham ngua");
            DrawLandmark(ChickenCoopPos2D, new Color(0.9f, 0.8f, 0.5f), "Chuong ga");
            DrawLandmark(PoultryKeeperHousePos2D, new Color(0.7f, 0.5f, 0.3f), "Nha nguoi cham ga");
            DrawLandmark(TownHallPos2D, new Color(0.75f, 0.25f, 0.2f), "Toa Thi Chinh");
            DrawLandmark(VillageAnchor2D, new Color(0.95f, 0.85f, 0.4f), "Thi Tran");

            for (int i = 0; i < PlateauPos2D.Length; i++)
                DrawLandmark(PlateauPos2D[i], new Color(0.55f, 0.42f, 0.32f), i == 0 ? "Cao nguyen" : "");

            DrawLandmark(SunflowerFieldPos2D, new Color(0.95f, 0.78f, 0.1f), "Canh dong huong duong");

            if (HasWaypoint)
            {
                var wp = WorldToMap(new Vector2(Waypoint.X, Waypoint.Z));
                _mapArea.DrawCircle(wp, 8f, new Color(1f, 0.25f, 0.2f, 0.35f));
                _mapArea.DrawLine(wp + new Vector2(-5, -5), wp + new Vector2(5, 5), Colors.White, 2f);
                _mapArea.DrawLine(wp + new Vector2(-5, 5), wp + new Vector2(5, -5), Colors.White, 2f);
            }

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
