using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Thanh trang thai: HP, vang, cap do, ngay, va theo doi nhiem vu.
    public partial class HUD : CanvasLayer
    {
        private ProgressBar _hpBar;
        private Label _hpLabel;
        private Label _goldLabel;
        private Label _levelLabel;
        private Label _dayLabel;
        private Label _timeLabel;
        private Label _questLabel;
        private Control _compass;
        private static readonly Vector2 CompassCenter = new(900, 40);

        // Bang ten cong trinh: hien khi nguoi choi lai gan (xem BuildingLabelZone.cs) - dung 1
        // DANH SACH (khong phai 1 co don) vi nguoi choi co the dung o vung giao nhau cua 2 cong
        // trinh gan nhau cung luc; luon hien ten cong trinh MOI VAO GAN DAY NHAT.
        private Label _buildingNameLabel;
        private readonly System.Collections.Generic.List<string> _nearbyBuildings = new();

        public override void _Ready()
        {
            AddToGroup("hud");
            BuildUI();
            GameManager.Instance.StatsChanged += Refresh;
            QuestSystem.Instance.QuestUpdated += _ => Refresh();
            QuestSystem.Instance.QuestCompleted += _ => Refresh();
            Refresh();
        }

        public void ShowBuildingName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _nearbyBuildings.Remove(name);
            _nearbyBuildings.Add(name);
            RefreshBuildingLabel();
        }

        public void HideBuildingName(string name)
        {
            _nearbyBuildings.Remove(name);
            RefreshBuildingLabel();
        }

        private void RefreshBuildingLabel()
        {
            if (_nearbyBuildings.Count == 0) { _buildingNameLabel.Visible = false; return; }
            _buildingNameLabel.Text = _nearbyBuildings[^1];
            _buildingNameLabel.Visible = true;
        }

        public override void _Process(double delta)
        {
            // Dong ho thuc: cap nhat rieng moi frame de kim phut/giay chay muot,
            // khong phu thuoc cac su kien StatsChanged/Quest o Refresh().
            var now = System.DateTime.Now;
            string dayNight = GameManager.Instance.IsNight ? "Dem" : "Ngay";
            _dayLabel.Text = $"{now:dd/MM/yyyy}";
            _timeLabel.Text = $"{now:HH:mm:ss} ({dayNight})";

            _compass.QueueRedraw();
        }

        private void BuildUI()
        {
            var panel = new PanelContainer();
            panel.Position = new Vector2(10, 10);
            panel.CustomMinimumSize = new Vector2(230, 0);
            panel.Scale = new Vector2(0.5f, 0.5f); // thu nho toan bo bang trang thai xuong con 50%
            AddChild(panel);

            var vb = new VBoxContainer();
            panel.AddChild(vb);

            _hpBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 18), ShowPercentage = false };
            vb.AddChild(_hpBar);
            _hpLabel = new Label(); vb.AddChild(_hpLabel);
            _levelLabel = new Label(); vb.AddChild(_levelLabel);
            _goldLabel = new Label(); vb.AddChild(_goldLabel);
            _dayLabel = new Label(); vb.AddChild(_dayLabel);
            _timeLabel = new Label(); vb.AddChild(_timeLabel);

            var qTitle = new Label { Text = "-- Nhiem vu --" };
            vb.AddChild(qTitle);
            _questLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            _questLabel.CustomMinimumSize = new Vector2(210, 0);
            vb.AddChild(_questLabel);

            // Danh sach phim tat CHUYEN sang man hinh Cai Dat/Huong Dan rieng (bam [H], xem
            // SettingsUI.cs) - khong con hien co dinh tren man hinh choi nua.

            // Ten cong trinh khi lai gan (xem BuildingLabelZone.cs) - hien giua man hinh, phia
            // tren, chu to de de doc thoang qua khi di ngang.
            _buildingNameLabel = new Label
            {
                Text = "",
                Position = new Vector2(360, 60),
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(400, 0),
                Visible = false
            };
            _buildingNameLabel.AddThemeFontSizeOverride("font_size", 26);
            _buildingNameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _buildingNameLabel.AddThemeConstantOverride("outline_size", 6);
            AddChild(_buildingNameLabel);

            // Mui ten la ban chi huong toi "diem den" nguoi choi bam chon tren ban do (xem
            // MapUI.Waypoint) - KHONG tu dong dan duong, chi xoay theo huong that + hien
            // khoang cach, giup nguoi choi tu chay toi.
            _compass = new Control { Position = Vector2.Zero, CustomMinimumSize = Vector2.Zero };
            _compass.Draw += DrawCompass;
            AddChild(_compass);
        }

        // Quy uoc goc xoay: dung TRUC TIEP (worldDir.X, worldDir.Z) lam vector man hinh (khop
        // dung quy uoc cua MapUI.WorldToMap: X the gioi -> X ban do/man hinh, Z the gioi tang ->
        // Y ban do/man hinh tang xuong duoi) - camera cua Player la OFFSET CO DINH (0,140,115)
        // khong xoay theo huong nhan vat va khong lech truc X, nen huong the gioi anh xa gan
        // dung sang huong man hinh theo cach nay.
        private void DrawCompass()
        {
            var mapUi = GetTree().GetFirstNodeInGroup("map_ui") as MapUI;
            var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (mapUi == null || !mapUi.HasWaypoint || player == null) return;

            Vector3 diff = mapUi.Waypoint - player.GlobalPosition;
            var dir2 = new Vector2(diff.X, diff.Z);
            float dist = dir2.Length();
            if (dist < 40f) return; // da toi noi (~2m) - an mui ten, khong con can chi huong nua

            float angle = dir2.Angle();
            Vector2[] shape =
            {
                new(18, 0), new(-9, -11), new(-3, 0), new(-9, 11)
            };
            var pts = new Vector2[shape.Length];
            for (int i = 0; i < shape.Length; i++)
                pts[i] = CompassCenter + shape[i].Rotated(angle);

            _compass.DrawColoredPolygon(pts, new Color(1f, 0.82f, 0.15f));
            _compass.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colors.Black, 1.5f);

            float distMeters = dist / 20f; // quy doi 20 don vi/met (dung chung ca game)
            _compass.DrawString(ThemeDB.FallbackFont, CompassCenter + new Vector2(-16, 30),
                $"{distMeters:0}m", HorizontalAlignment.Left, -1, 14, Colors.White);
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            _hpBar.MaxValue = gm.MaxHp;
            _hpBar.Value = gm.Hp;
            _hpLabel.Text = $"HP: {gm.Hp}/{gm.MaxHp}";
            _levelLabel.Text = $"Cap {gm.Level}  (EXP {gm.Exp}/{gm.ExpToNext})";
            _goldLabel.Text = $"Vang: {gm.Gold}";

            // Hien nhiem vu dang lam
            string qtext = "";
            foreach (var kv in QuestSystem.Instance.Active)
            {
                var def = ItemDatabase.Instance.GetQuest(kv.Key);
                if (def != null)
                    qtext += $"- {def.Title} ({kv.Value}/{def.TargetCount})\n";
            }
            _questLabel.Text = qtext == "" ? "(chua co)" : qtext;
        }
    }
}
