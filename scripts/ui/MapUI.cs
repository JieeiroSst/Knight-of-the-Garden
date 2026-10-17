using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;

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
        private Panel _panel;
        private Control _mapArea;
        private Node3D _player;
        private Tween _toggleTween;
        private Texture2D _vignetteTex;
        private readonly LocalizedLabelSet _loc = new();

        // Diem den nguoi choi tu danh dau bang cach bam vao ban do - HUD doc gia tri nay (qua
        // group "map_ui") de ve mui ten chi huong (xem HUD.DrawCompass).
        public bool HasWaypoint { get; private set; }
        public Vector3 Waypoint { get; private set; }

        public void SetWaypoint(Vector3 world)
        {
            Waypoint = new Vector3(world.X, 0, world.Z);
            HasWaypoint = true;
            _mapArea?.QueueRedraw();
        }

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
            new(new Vector2(-300, -60), new Color(0.85f, 0.6f, 0.3f), "map.landmark.farm", Icon.House, false),
            new(new Vector2(-482, 250), new Color(0.6f, 0.35f, 0.15f), "map.landmark.storage", Icon.House, false),

            // ---- Thi tran (khu do thi cu, VillageAnchor) ----
            new(new Vector2(9250, 3750), new Color(0.95f, 0.85f, 0.4f), "map.landmark.town", Icon.House, false),

            // ---- Dia hinh/cong trinh phu gan nong trai (da co truoc) ----
            new(new Vector2(2350, -1350), new Color(0.55f, 0.42f, 0.32f), "map.landmark.plateau", Icon.Triangle, false),
            new(new Vector2(2650, 750), new Color(0.55f, 0.42f, 0.32f), "", Icon.Triangle, false),
            new(new Vector2(1750, 1950), new Color(0.55f, 0.42f, 0.32f), "", Icon.Triangle, false),
            new(new Vector2(-1150, 1000), new Color(0.95f, 0.78f, 0.1f), "map.landmark.sunflower_field", Icon.Flower, false),

            // ---- 11 khu vuc "the gioi mo" (xem Main.cs cac hang so *RegionCenter/MineEntrancePos) ----
            new(new Vector2(2500, -400), new Color(0.65f, 0.5f, 0.25f), "map.landmark.mine", Icon.Diamond, true),
            new(new Vector2(200, -6800), new Color(0.6f, 0.6f, 0.62f), "map.landmark.mountain", Icon.Triangle, true),
            new(new Vector2(-3600, -5700), new Color(0.25f, 0.5f, 0.22f), "map.landmark.forest", Icon.Tree, true),
            new(new Vector2(-5200, 5100), new Color(0.75f, 0.78f, 0.35f), "map.landmark.fields", Icon.Dot, false),
            new(new Vector2(-6300, -2650), new Color(0.25f, 0.5f, 0.7f), "map.landmark.lake", Icon.Water, true),
            new(new Vector2(2300, 7000), new Color(0.3f, 0.55f, 0.68f), "map.landmark.river", Icon.Water, false),
            new(new Vector2(5700, 5000), new Color(0.8f, 0.68f, 0.45f), "map.landmark.village", Icon.House, false),
            new(new Vector2(-1800, 7300), new Color(0.7f, 0.68f, 0.62f), "map.landmark.city", Icon.House, false),
            new(new Vector2(6700, -2650), new Color(0.55f, 0.4f, 0.6f), "map.landmark.ruins", Icon.Diamond, true),
            new(new Vector2(7000, 1400), new Color(0.75f, 0.75f, 0.72f), "map.landmark.cemetery", Icon.Cross, true),
            new(new Vector2(-6900, 1400), new Color(0.3f, 0.42f, 0.28f), "map.landmark.swamp", Icon.Water, true),
            new(new Vector2(4100, -5650), new Color(0.4f, 0.4f, 0.42f), "map.landmark.cave", Icon.Diamond, true),
        };

        // The gioi KHONG co bien (xem WorldStreamer/WorldBiomes) nen ban do la 1 "khung nhin" di
        // chuyen duoc: tam (_viewCenter) + ti le (_zoom = don vi the gioi / 1 pixel ban do). Mac
        // dinh vua khit 11 khu vuc dat tay quanh nong trai; lan chuot de phong to/thu nho, keo
        // chuot phai de di chuyen, chuot giua/nut "◎" de quay ve vi tri nguoi choi.
        private const float DefaultZoom = 33f;
        private const float MinZoom = 2f, MaxZoom = 600f;
        private const float UnitsPerMeter = 20f;
        private Vector2 _viewCenter = new(1250f, 300f);
        private float _zoom = DefaultZoom;
        private bool _dragging;
        private Vector2? _hover;
        private Label _coordsLabel;

        // Cache sinh canh/trai cua tung chunk da kham pha (tinh nhieu moi khung hinh qua ton).
        private readonly System.Collections.Generic.Dictionary<Vector2I, (Color color, bool camp)> _chunkInfo = new();
        private static readonly Vector2 MapSize = new(600, 536); // ti le gan dung ty le vung the gioi o tren
        private static readonly Vector2 MapOrigin = new(40, 70);

        private static readonly Color FrameGold = new(0.75f, 0.62f, 0.35f);
        private static readonly Color TextCream = new(0.95f, 0.92f, 0.82f);

        public override void _Ready()
        {
            AddToGroup("map_ui");

            _panel = new Panel
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
            _panel.AddThemeStyleboxOverride("panel", frameStyle);
            // Tam xoay/phong to o CHINH GIUA khung (khong phai goc trai-tren mac dinh) - de hieu
            // ung "bung mo" luc bam [M] no dan tu tam ra, khong bi lech ve 1 goc.
            _panel.PivotOffset = (MapSize + new Vector2(40, 70)) / 2f;
            AddChild(_panel);

            var title = new Label { Position = new Vector2(20, 8) };
            _loc.Track(title, "map.title");
            title.AddThemeColorOverride("font_color", FrameGold);
            _panel.AddChild(title);

            var hint = new Label { Position = new Vector2(20, 28) };
            _loc.Track(hint, "map.hint");
            hint.AddThemeColorOverride("font_color", new Color(TextCream, 0.6f));
            hint.AddThemeFontSizeOverride("font_size", 12);
            _panel.AddChild(hint);

            _mapArea = new Control
            {
                Position = MapOrigin,
                CustomMinimumSize = MapSize,
                Size = MapSize,
                ClipContents = true,
            };
            _mapArea.Draw += DrawMap;
            _mapArea.GuiInput += OnMapInput;
            _mapArea.MouseExited += () => { _hover = null; _mapArea.QueueRedraw(); };
            _panel.AddChild(_mapArea);

            var recenter = new Button { Text = "◎", Position = new Vector2(MapSize.X + 40 - 46, 8), CustomMinimumSize = new Vector2(30, 26), FocusMode = Control.FocusModeEnum.None };
            recenter.Pressed += CenterOnPlayer;
            _panel.AddChild(recenter);

            _coordsLabel = new Label { Position = new Vector2(20, MapSize.Y + 72) };
            _coordsLabel.AddThemeColorOverride("font_color", new Color(TextCream, 0.8f));
            _coordsLabel.AddThemeFontSizeOverride("font_size", 12);
            _panel.AddChild(_coordsLabel);
            _panel.CustomMinimumSize += new Vector2(0, 22);

            Loc.LanguageChanged += _loc.Refresh;

            Visible = false;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= _loc.Refresh;
        }

        // Bam chuot trai vao ban do -> quy doi vi tri (nguoc lai voi WorldToMap) thanh toa do
        // THE GIOI THAT, dat lam "diem den" - HUD se ve mui ten chi huong toi day (xem
        // HUD.DrawCompass), khong tu dong dan duong, chi chi huong.
        private void OnMapInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    Vector2 world = MapToWorld(mb.Position);
                    Waypoint = new Vector3(world.X, 0, world.Y);
                    HasWaypoint = true;
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                    _dragging = mb.Pressed;
                else if (mb.Pressed && mb.ButtonIndex == MouseButton.Middle)
                    CenterOnPlayer();
                else if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
                {
                    // Phong to quanh con tro: diem the gioi duoi chuot giu nguyen vi tri.
                    Vector2 before = MapToWorld(mb.Position);
                    float factor = mb.ButtonIndex == MouseButton.WheelUp ? 1f / 1.2f : 1.2f;
                    _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
                    _viewCenter += before - MapToWorld(mb.Position);
                }
                _mapArea.AcceptEvent();
                _mapArea.QueueRedraw();
            }
            else if (e is InputEventMouseMotion hover && !_dragging)
            {
                _hover = hover.Position;
                _mapArea.QueueRedraw();
            }
            else if (e is InputEventMouseMotion mm && _dragging)
            {
                _viewCenter -= mm.Relative * _zoom;
                _mapArea.AcceptEvent();
                _mapArea.QueueRedraw();
            }
        }

        private void CenterOnPlayer()
        {
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player != null) _viewCenter = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
            _mapArea.QueueRedraw();
        }

        private Vector2 MapToWorld(Vector2 mapPos) => _viewCenter + (mapPos - MapSize / 2f) * _zoom;

        public override void _Process(double delta)
        {
            if (!Visible) return;
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player != null)
            {
                var pp = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
                string biome = WorldBiomes.Wildness(pp) < 0.5f ? Loc.T("biome.core") : Loc.T(WorldBiomes.NameKey(WorldBiomes.At(pp)));
                _coordsLabel.Text = string.Format(Loc.T("map.coords_fmt"), Mathf.RoundToInt(pp.X / UnitsPerMeter), Mathf.RoundToInt(pp.Y / UnitsPerMeter),
                    biome, WorldBiomes.Danger(pp).ToString("0.0"));
            }
            _mapArea.QueueRedraw();
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_map"))
            {
                if (!Visible) OpenAnimated();
                else CloseAnimated();
                GetViewport().SetInputAsHandled();
            }
        }

        // "Bung mo"/thu lai tu tam khung (thay vi bat/tat tuc thi truoc day) - bam [M] gio co
        // cam giac 1 cuon ban do that su duoc mo ra chu khong phai 1 o vuong xuat hien dot ngot.
        private void OpenAnimated()
        {
            Visible = true;
            _dragging = false;
            // Mo ban do khi dang o xa ngoai vung loi -> nhay toi ngay cho nguoi choi dang dung.
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (_player != null)
            {
                var pp = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
                if (WorldToMap(pp) is var mp && (mp.X < 40 || mp.Y < 40 || mp.X > MapSize.X - 40 || mp.Y > MapSize.Y - 40))
                    _viewCenter = pp;
            }
            _panel.Scale = Vector2.One * 0.88f;
            _panel.Modulate = new Color(1f, 1f, 1f, 0f);
            _toggleTween?.Kill();
            _toggleTween = CreateTween().SetParallel(true);
            _toggleTween.TweenProperty(_panel, "scale", Vector2.One, 0.22f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _toggleTween.TweenProperty(_panel, "modulate:a", 1f, 0.16f);
        }

        private void CloseAnimated()
        {
            _toggleTween?.Kill();
            _toggleTween = CreateTween();
            _toggleTween.TweenProperty(_panel, "scale", Vector2.One * 0.9f, 0.13f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            _toggleTween.Parallel().TweenProperty(_panel, "modulate:a", 0f, 0.13f);
            _toggleTween.TweenCallback(Callable.From(() => Visible = false));
        }

        private Vector2 WorldToMap(Vector2 worldXZ) => (worldXZ - _viewCenter) / _zoom + MapSize / 2f;

        private void DrawMap()
        {
            // Nen: sac xanh rung tham (thay vi 1 mau xanh phang truoc day) + luoi mo nhat kieu
            // "ban do giay" + 2 vung tint rieng cho khu vuc van minh (trang trai/thi tran) de
            // noi bat giua bien wilderness rong lon moi.
            // Vung chua kham pha = suong mu toi; vung loi quanh nong trai (da biet) = xanh rung.
            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), new Color(0.05f, 0.06f, 0.07f));
            _mapArea.DrawCircle(WorldToMap(Vector2.Zero), (WorldBiomes.CoreRadius + WorldBiomes.BlendWidth * 0.5f) / _zoom, new Color(0.1f, 0.18f, 0.1f));
            DrawExplored();
            DrawGrid();

            // Tint nhe quanh trang trai + thi tran (khu "an toan", da thuan hoa) - hinh tron mo
            // phong pham vi tuong da/khu do thi, KHONG chinh xac tuyet doi, chi mang tinh dinh
            // huong thi giac.
            var farmC = HiepSiVeVuon.Core.FarmSpace.ToWorld(HiepSiVeVuon.Core.FarmSpace.LocalWallCenter);
            float wallR = HiepSiVeVuon.Core.FarmSpace.WallHalfSize * 1.1f;
            DrawSafeZoneTint(new Vector2(farmC.X, farmC.Z), wallR, new Color(0.35f, 0.42f, 0.22f, 0.35f));
            foreach (var f in HiepSiVeVuon.Core.FarmReplicas.Known.Values)
                DrawSafeZoneTint(new Vector2(f.Center.X, f.Center.Z), wallR, new Color(0.35f, 0.42f, 0.22f, 0.25f));
            DrawSafeZoneTint(new Vector2(9250, 3750), 2000f, new Color(0.42f, 0.38f, 0.22f, 0.35f));

            _mapArea.DrawRect(new Rect2(Vector2.Zero, MapSize), FrameGold, false, 2f);

            // Duong noi nong trai - thi tran (da co truoc).
            if (HiepSiVeVuon.Core.FarmSpace.Origin == Vector3.Zero)
                _mapArea.DrawLine(WorldToMap(new Vector2(-300, -60)), WorldToMap(new Vector2(9250, 3750)), new Color(0.65f, 0.55f, 0.35f, 0.8f), 2.5f);

            foreach (var lm in Landmarks)
                DrawLandmark(lm);

            DrawAdventureOverlay();
            DrawMines();
            DrawOtherFarms();
            DrawMarkets();
            DrawOnlineMarkers();
            DrawWorldEvents();

            // Vung que nuoc Phap (xem Main.FrenchRegionCenter) - ban do gio cuon/thu nho duoc nen
            // ve dung vi tri that thay cho mui ten chi huong cu.
            if (WorldStreamer.FrenchRegionCenter is Vector3 fc)
            {
                var fp = WorldToMap(new Vector2(fc.X, fc.Z));
                float fr = WorldStreamer.FrenchRegionHalfSize / _zoom;
                _mapArea.DrawRect(new Rect2(fp - Vector2.One * fr, Vector2.One * fr * 2f), new Color(0.5f, 0.6f, 0.3f, 0.35f));
                DrawHouseIcon(fp, new Color(0.85f, 0.75f, 0.55f));
                DrawLabelPlate(fp + new Vector2(9, 4), Loc.T("map.landmark.french"));
            }

            DrawCompassRose(new Vector2(MapSize.X - 34, MapSize.Y - 34), 16f);
            DrawScaleBar(new Vector2(14, MapSize.Y - 16));

            if (HasWaypoint)
            {
                var wp = WorldToMap(new Vector2(Waypoint.X, Waypoint.Z));
                _mapArea.DrawCircle(wp, 9f, new Color(1f, 0.25f, 0.2f, 0.3f));
                _mapArea.DrawLine(wp + new Vector2(-5, -5), wp + new Vector2(5, 5), Colors.White, 2f);
                _mapArea.DrawLine(wp + new Vector2(-5, 5), wp + new Vector2(5, -5), Colors.White, 2f);
                DrawLabelPlate(wp + new Vector2(10, -6), MapCoords.Format(Waypoint.X, Waypoint.Z));
            }

            if (_player != null)
            {
                var p = WorldToMap(new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z));
                // Hao quang "tho" theo nhip sin (thay vi 1 vong co dinh truoc day) - dam bao mat
                // nguoi choi tim ra vi tri ban than ngay lap tuc tren 1 ban do rong 11 khu vuc.
                float pulse = (Mathf.Sin(Time.GetTicksMsec() / 260f) + 1f) * 0.5f;
                _mapArea.DrawCircle(p, 8f + pulse * 3f, new Color(0.2f, 0.85f, 1f, 0.22f - pulse * 0.1f));
                _mapArea.DrawCircle(p, 5f, new Color(0.2f, 0.85f, 1f));
                _mapArea.DrawCircle(p, 5f, Colors.White, false, 1.5f);
                DrawLabelPlate(p + new Vector2(9, -4), Loc.T("map.player_label"));
            }

            DrawVignette();

            // Toa do duoi con tro chuot.
            if (_hover is Vector2 hv && hv.X >= 0 && hv.Y >= 0 && hv.X <= MapSize.X && hv.Y <= MapSize.Y)
            {
                var w = MapToWorld(hv);
                _mapArea.DrawLine(hv + new Vector2(-6, 0), hv + new Vector2(6, 0), new Color(TextCream, 0.7f), 1f);
                _mapArea.DrawLine(hv + new Vector2(0, -6), hv + new Vector2(0, 6), new Color(TextCream, 0.7f), 1f);
                DrawLabelPlate(hv + new Vector2(10, 16), MapCoords.Format(w.X, w.Y));
            }
        }

        // Suong mu tren vung CHUA kham pha (xem AdventureSystem.Discover) + diem phieu luu: cot da
        // dich chuyen (xanh = da kich hoat), hang trum (do; xam = da ha, cho hoi sinh), ham nguc,
        // bao tang, bang le hoi.
        private void DrawAdventureOverlay()
        {
            var adv = AdventureSystem.Instance;
            if (adv == null) return;
            float scale = 1f / _zoom;
            foreach (var r in WorldStreamer.Regions)
            {
                if (adv.Discovered.Contains(r.Name)) continue;
                var c = WorldToMap(new Vector2(r.Center.X, r.Center.Z));
                _mapArea.DrawCircle(c, r.HalfSize * scale, new Color(0.05f, 0.06f, 0.08f, 0.72f));
                _mapArea.DrawString(ThemeDB.FallbackFont, c + new Vector2(-5, 5), "?", HorizontalAlignment.Left, -1, 16, new Color(TextCream, 0.7f));
            }
            foreach (var poi in adv.Pois)
            {
                var p = WorldToMap(new Vector2(poi.pos.X, poi.pos.Z));
                switch (poi.kind)
                {
                    case AdventureSystem.PoiKind.Waystone:
                        bool on = adv.Waystones.Contains(poi.id);
                        DrawDiamond(p, 4.5f, on ? new Color(0.35f, 0.8f, 1f) : new Color(0.45f, 0.5f, 0.55f));
                        break;
                    case AdventureSystem.PoiKind.Boss:
                        bool alive = adv.IsBossAvailable(poi.id);
                        var bc = alive ? new Color(0.95f, 0.2f, 0.15f) : new Color(0.5f, 0.45f, 0.45f);
                        _mapArea.DrawCircle(p, 6f, new Color(bc, 0.35f));
                        _mapArea.DrawLine(p + new Vector2(-4, -4), p + new Vector2(4, 4), bc, 2.5f);
                        _mapArea.DrawLine(p + new Vector2(-4, 4), p + new Vector2(4, -4), bc, 2.5f);
                        if (!alive)
                            _mapArea.DrawString(ThemeDB.FallbackFont, p + new Vector2(7, 4), string.Format(Loc.T("map.boss_respawn_fmt"), adv.BossRespawnIn(poi.id)),
                                HorizontalAlignment.Left, -1, 10, new Color(TextCream, 0.7f));
                        break;
                    case AdventureSystem.PoiKind.Dungeon:
                        DrawDiamond(p, 6f, new Color(0.7f, 0.35f, 1f));
                        DrawLabelPlate(p + new Vector2(8, -4), Loc.T(poi.labelKey));
                        break;
                    default:
                        _mapArea.DrawCircle(p, 4f, poi.kind == AdventureSystem.PoiKind.Museum ? new Color(0.95f, 0.85f, 0.5f) : new Color(1f, 0.45f, 0.6f));
                        break;
                }
            }
        }

        // Ham mo (vi tri lay thang tu Main.MineLocations) + cac bai quang lo thien nguoi choi da di qua.
        private void DrawMines()
        {
            foreach (var (pos, ore) in HiepSiVeVuon.Entities.OreFields.Known.Values)
            {
                var p = WorldToMap(new Vector2(pos.X, pos.Z));
                if (p.X < 0 || p.Y < 0 || p.X > MapSize.X || p.Y > MapSize.Y) continue;
                _mapArea.DrawCircle(p, 2.2f, new Color(0.85f, 0.7f, 0.4f, 0.9f));
            }
            foreach (var (labelKey, pos) in HiepSiVeVuon.Core.Main.MineLocations)
            {
                var p = WorldToMap(Disp(new Vector2(pos.X, pos.Z)));
                DrawDiamond(p, 5f, new Color(0.75f, 0.55f, 0.3f));
                _mapArea.DrawLine(p + new Vector2(-3, 3), p + new Vector2(3, -3), Colors.Black, 1.5f);
            }
        }

        // Nong trai cua nguoi choi khac (moi nguoi 1 toa do rieng, xem FarmReplicas) - nha + ten chu.
        private void DrawOtherFarms()
        {
            foreach (var f in HiepSiVeVuon.Core.FarmReplicas.Known.Values)
            {
                var p = WorldToMap(new Vector2(f.House.X, f.House.Z));
                if (p.X < -20 || p.Y < -20 || p.X > MapSize.X + 20 || p.Y > MapSize.Y + 20) continue;
                DrawHouseIcon(p, new Color(0.55f, 0.75f, 0.95f));
                if (_zoom < 120f) DrawLabelPlate(p + new Vector2(9, 4), string.Format(Loc.T("farm.owner_fmt"), f.Name));
            }
        }

        private static Vector2 Disp(Vector2 v)
        {
            var w = HiepSiVeVuon.Core.FarmSpace.Display(new Vector3(v.X, 0, v.Y));
            return new Vector2(w.X, w.Z);
        }

        private void DrawDiamond(Vector2 c, float r, Color col)
        {
            var pts = new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) };
            _mapArea.DrawColoredPolygon(pts, col);
            _mapArea.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colors.Black, 1f);
        }

        // Lam toi 4 goc ban do (khong dong vao vung giua) - tao chieu sau/cam giac "tam ban do
        // cu" thay vi 1 hinh chu nhat mau phang deu tap. Dung 1 GradientTexture2D toa tron cache
        // lai (chi tao 1 lan) ve DE LEN TREN CUNG moi thu khac.
        private void DrawVignette()
        {
            if (_vignetteTex == null)
            {
                var gradient = new Gradient();
                gradient.SetColor(0, new Color(0, 0, 0, 0f));
                gradient.AddPoint(0.7f, new Color(0, 0, 0, 0f));
                gradient.SetColor(1, new Color(0, 0, 0, 0.55f));
                _vignetteTex = new GradientTexture2D
                {
                    Gradient = gradient,
                    Width = 256,
                    Height = 256,
                    Fill = GradientTexture2D.FillEnum.Radial,
                    FillFrom = new Vector2(0.5f, 0.5f),
                    FillTo = new Vector2(1f, 1f),
                };
            }
            _mapArea.DrawTextureRect(_vignetteTex, new Rect2(Vector2.Zero, MapSize), false);
        }

        // Cac chunk da kham pha o the gioi vo han: to mau theo sinh canh + danh dau trai hoang.
        private void DrawExplored()
        {
            float cs = WorldStreamer.ExploreChunkSize;
            float px = cs / _zoom;
            var tl = MapToWorld(Vector2.Zero);
            var br = MapToWorld(MapSize);
            foreach (var key in WorldStreamer.Explored)
            {
                float wx = key.X * cs, wz = key.Y * cs;
                if (wx + cs < tl.X || wz + cs < tl.Y || wx > br.X || wz > br.Y) continue;
                if (!_chunkInfo.TryGetValue(key, out var info))
                {
                    var c = new Vector2(wx + cs / 2f, wz + cs / 2f);
                    var col = WorldBiomes.Wildness(c) < 0.5f
                        ? WorldBiomes.MapColors[0]
                        : WorldBiomes.MapColors[(int)WorldBiomes.At(c)];
                    info = (col, WorldStreamer.HasCamp(key.X, key.Y));
                    _chunkInfo[key] = info;
                }
                // +0.6px che khe ho giua cac o lien ke.
                _mapArea.DrawRect(new Rect2(WorldToMap(new Vector2(wx, wz)), Vector2.One * (px + 0.6f)), info.color);
            }
            // Trai ve sau cung de khong bi o ke ben de len.
            foreach (var (key, info) in _chunkInfo)
            {
                if (!info.camp) continue;
                var p = WorldToMap(new Vector2(key.X * cs + cs / 2f, key.Y * cs + cs / 2f));
                if (p.X < -10 || p.Y < -10 || p.X > MapSize.X + 10 || p.Y > MapSize.Y + 10) continue;
                DrawTriangleIcon(p, new Color(0.85f, 0.35f, 0.2f));
                if (_zoom < 60f) DrawLabelPlate(p + new Vector2(9, 4), Loc.T("map.landmark.camp"));
            }
        }

        // Thuoc ti le: chon do dai "tron" (10/20/50/100... m) gan ~90px nhat.
        private void DrawScaleBar(Vector2 left)
        {
            float targetMeters = 90f * _zoom / UnitsPerMeter;
            float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log(targetMeters) / Mathf.Log(10f)));
            float meters = pow;
            foreach (float m in new[] { 1f, 2f, 5f, 10f })
                if (m * pow <= targetMeters) meters = m * pow;
            float px = meters * UnitsPerMeter / _zoom;
            _mapArea.DrawLine(left, left + new Vector2(px, 0), TextCream, 2f);
            _mapArea.DrawLine(left + new Vector2(0, -4), left + new Vector2(0, 4), TextCream, 2f);
            _mapArea.DrawLine(left + new Vector2(px, -4), left + new Vector2(px, 4), TextCream, 2f);
            string text = meters >= 1000f ? $"{meters / 1000f:0.#} km" : string.Format(Loc.T("map.scale_fmt"), meters);
            _mapArea.DrawString(ThemeDB.FallbackFont, left + new Vector2(px + 6, 4), text, HorizontalAlignment.Left, -1, 11, TextCream);
        }

        // Luoi TOA DO THE GIOI (met, giong /toado va HUD): buoc luoi tu chon theo muc phong to (~80px/o),
        // nhan so o mep tren (X) va mep trai (Z) - de doc/hen "gap nhau o [1500, -2000]".
        private static readonly float[] GridStepsMeters = { 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000 };

        private void DrawGrid()
        {
            float stepM = GridStepsMeters[^1];
            foreach (float st in GridStepsMeters)
                if (st * UnitsPerMeter / _zoom >= 80f) { stepM = st; break; }
            float step = stepM * UnitsPerMeter;
            var tl = MapToWorld(Vector2.Zero);
            var br = MapToWorld(MapSize);
            var gridColor = new Color(1f, 1f, 1f, 0.07f);
            var axisColor = new Color(1f, 0.85f, 0.5f, 0.25f);
            var labelColor = new Color(TextCream, 0.55f);
            var font = ThemeDB.FallbackFont;
            for (float x = Mathf.Ceil(tl.X / step) * step; x <= br.X; x += step)
            {
                float px = WorldToMap(new Vector2(x, 0)).X;
                _mapArea.DrawLine(new Vector2(px, 0), new Vector2(px, MapSize.Y), Mathf.IsZeroApprox(x) ? axisColor : gridColor, 1f);
                _mapArea.DrawString(font, new Vector2(px + 2, 11), $"{Mathf.RoundToInt(x / UnitsPerMeter)}", HorizontalAlignment.Left, -1, 9, labelColor);
            }
            for (float z = Mathf.Ceil(tl.Y / step) * step; z <= br.Y; z += step)
            {
                float py = WorldToMap(new Vector2(0, z)).Y;
                _mapArea.DrawLine(new Vector2(0, py), new Vector2(MapSize.X, py), Mathf.IsZeroApprox(z) ? axisColor : gridColor, 1f);
                _mapArea.DrawString(font, new Vector2(2, py - 2), $"{Mathf.RoundToInt(z / UnitsPerMeter)}", HorizontalAlignment.Left, -1, 9, labelColor);
            }
        }

        // Dong doi to doi, cong trinh cua minh, tui do roi (PvpClient).
        private void DrawOnlineMarkers()
        {
            var pvp = PvpClient.Instance;
            if (pvp == null) return;
            foreach (var s in pvp.MyStructures.Values)
            {
                var p = WorldToMap(new Vector2(s.X, s.Z));
                if (!InView(p)) continue;
                var col = s.Kind == "bed" ? new Color(1f, 0.45f, 0.4f) : s.Kind == "chest" ? new Color(0.75f, 0.5f, 0.25f) : new Color(0.6f, 0.5f, 0.4f);
                _mapArea.DrawRect(new Rect2(p - Vector2.One * 3f, Vector2.One * 6f), col);
                if (s.Kind == "bed" && _zoom < 120f) DrawLabelPlate(p + new Vector2(6, 4), Loc.T("base.kind.bed"));
            }
            foreach (var b in pvp.Bags.Values)
            {
                if (!IsInstanceValid(b)) continue;
                var p = WorldToMap(new Vector2(b.GlobalPosition.X, b.GlobalPosition.Z));
                if (InView(p)) DrawDiamond(p, 4f, new Color(1f, 0.85f, 0.3f));
            }
            foreach (var m in pvp.PartyPositions.Values)
            {
                if (Time.GetTicksMsec() - m.SeenMs > 5000) continue;
                var p = WorldToMap(new Vector2(m.Pos.X, m.Pos.Z));
                // Ngoai khung -> ghim o mep ban do theo huong (van biet dong doi o dau).
                var c = MapSize / 2f;
                var d = p - c;
                float k = Mathf.Max(Mathf.Abs(d.X) / (c.X - 8f), Mathf.Abs(d.Y) / (c.Y - 8f));
                if (k > 1f) p = c + d / k;
                _mapArea.DrawCircle(p, 5f, new Color(0.35f, 0.9f, 1f));
                _mapArea.DrawCircle(p, 5f, Colors.Black, false, 1.2f);
                DrawLabelPlate(p + new Vector2(8, -4), $"{m.Name} {MapCoords.Format(m.Pos.X, m.Pos.Z)}");
            }
        }

        // Khu cho: vong vung an toan + bieu tuong + ten + toa do (diem hen gap nhau/giao dich).
        private void DrawMarkets()
        {
            var font = ThemeDB.FallbackFont;
            foreach (var m in ItemDatabase.Instance.Markets)
            {
                var p = WorldToMap(new Vector2(m.X, m.Z));
                if (!InView(p)) continue;
                _mapArea.DrawArc(p, Mathf.Max(6f, m.SafeRadius / _zoom), 0f, Mathf.Tau, 32, new Color(0.4f, 1f, 0.5f, 0.55f), 1.4f, true);
                _mapArea.DrawCircle(p, 6.5f, new Color(0.95f, 0.78f, 0.25f));
                _mapArea.DrawCircle(p, 6.5f, Colors.Black, false, 1.2f);
                _mapArea.DrawString(font, p + new Vector2(-3.5f, 4.5f), "$", HorizontalAlignment.Left, -1, 12, new Color(0.3f, 0.2f, 0.05f));
                string name = Loc.Current == Loc.Lang.VI ? m.Name : m.NameEn;
                DrawLabelPlate(p + new Vector2(10, 4), _zoom < 90f ? $"{name} {MapCoords.Format(m.X, m.Z)}" : name);
            }
        }

        // Su kien the gioi: ruong tiep te + doan thuong nhan (ghim mep ban do neu o ngoai khung).
        private void DrawWorldEvents()
        {
            var we = WorldEventsClient.Instance;
            if (we == null) return;
            long now = WorldEventsClient.ServerNow;
            foreach (var d in we.Drops.Values)
            {
                var p = PinToEdge(WorldToMap(new Vector2(d.X, d.Z)));
                var col = d.Looted ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.3f, 0.2f);
                _mapArea.DrawRect(new Rect2(p - Vector2.One * 5f, Vector2.One * 10f), col);
                _mapArea.DrawRect(new Rect2(p - Vector2.One * 5f, Vector2.One * 10f), Colors.Black, false, 1.2f);
                long left = (d.LandsAt - now) / 1000;
                string st = d.Looted ? Loc.T("world.map_looted") : left > 0 ? $"{left / 60}:{left % 60:00}" : Loc.T("world.map_ready");
                DrawLabelPlate(p + new Vector2(9, 4), $"{Loc.T("world.map_drop")} {MapCoords.Format(d.X, d.Z)} {st}");
            }
            if (we.CurrentCaravan is WorldEventsClient.Caravan cv)
            {
                var p = PinToEdge(WorldToMap(new Vector2(cv.X, cv.Z)));
                _mapArea.DrawCircle(p, 6.5f, new Color(0.95f, 0.75f, 0.2f));
                _mapArea.DrawCircle(p, 6.5f, Colors.Black, false, 1.2f);
                long left = Mathf.Max(0, (int)((cv.Until - now) / 1000));
                DrawLabelPlate(p + new Vector2(9, 4), $"{Loc.T("world.map_caravan")} {MapCoords.Format(cv.X, cv.Z)} {left / 60}:{left % 60:00}");
            }
            if (we.Boss is WorldEventsClient.WorldBoss b)
            {
                var p = PinToEdge(WorldToMap(new Vector2(b.X, b.Z)));
                float pulse = (Mathf.Sin(Time.GetTicksMsec() / 250f) + 1f) * 0.5f;
                _mapArea.DrawCircle(p, 9f + pulse * 3f, new Color(0.9f, 0.1f, 0.1f, 0.35f));
                DrawCrossIcon(p, new Color(0.95f, 0.2f, 0.15f));
                long wake = (b.WakeAt - now) / 1000;
                string st = wake > 0 ? string.Format(Loc.T("wboss.map_wake_fmt"), wake / 60, wake % 60) : $"{Mathf.RoundToInt(100f * b.Hp / Mathf.Max(1, b.MaxHp))}%";
                DrawLabelPlate(p + new Vector2(11, 4), $"{Loc.T("wboss.map")} {WorldEventsClient.BossName(b.Kind)} {MapCoords.Format(b.X, b.Z)} {st}");
            }
        }

        private static Vector2 PinToEdge(Vector2 p)
        {
            var c = MapSize / 2f;
            var d = p - c;
            float k = Mathf.Max(Mathf.Abs(d.X) / (c.X - 10f), Mathf.Abs(d.Y) / (c.Y - 10f));
            return k > 1f ? c + d / k : p;
        }

        private static bool InView(Vector2 p) => p.X >= -10 && p.Y >= -10 && p.X <= MapSize.X + 10 && p.Y <= MapSize.Y + 10;

        private void DrawSafeZoneTint(Vector2 worldXZ, float worldRadius, Color color)
        {
            var p = WorldToMap(worldXZ);
            float rx = worldRadius / _zoom;
            _mapArea.DrawCircle(p, rx, color);
        }

        private void DrawCompassRose(Vector2 center, float radius)
        {
            _mapArea.DrawCircle(center, radius, new Color(0, 0, 0, 0.3f));
            _mapArea.DrawCircle(center, radius, FrameGold, false, 1.4f);
            _mapArea.DrawLine(center + new Vector2(0, -radius), center + new Vector2(0, radius), FrameGold, 1.2f);
            _mapArea.DrawLine(center + new Vector2(-radius, 0), center + new Vector2(radius, 0), FrameGold, 1.2f);
            var font = ThemeDB.FallbackFont;
            _mapArea.DrawString(font, center + new Vector2(-4, -radius - 5), Loc.T("map.compass_n"), HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(-4, radius + 15), Loc.T("map.compass_s"), HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(radius + 5, 4), Loc.T("map.compass_e"), HorizontalAlignment.Left, -1, 12, TextCream);
            _mapArea.DrawString(font, center + new Vector2(-radius - 15, 4), Loc.T("map.compass_w"), HorizontalAlignment.Left, -1, 12, TextCream);
        }

        private void DrawLandmark(Landmark lm)
        {
            var p = WorldToMap(Disp(lm.Pos));

            // Vong tron do canh bao mo, mem, "tho" nhe theo nhip sin (thay vi 1 vong tinh truoc
            // day) - de mat nguoi choi tu nhien bi hut ve cac khu nguy hiem tren ban do rong.
            if (lm.Danger)
            {
                float pulse = (Mathf.Sin(Time.GetTicksMsec() / 400f + lm.Pos.X) + 1f) * 0.5f;
                _mapArea.DrawArc(p, 12f + pulse * 2f, 0f, Mathf.Tau, 28, new Color(0.85f, 0.15f, 0.15f, 0.4f + pulse * 0.25f), 1.6f, true);
            }

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
                DrawLabelPlate(p + new Vector2(9, 4), Loc.T(lm.Label));
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
