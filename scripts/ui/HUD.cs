using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Thanh trang thai: HP, vang, cap do, ngay, va theo doi nhiem vu.
    public partial class HUD : CanvasLayer
    {
        private ProgressBar _hpBar;
        private ProgressBar _staminaBar;
        private Label _staminaLabel;
        private Label _seasonLabel;
        private Label _mailLabel;
        private VBoxContainer _toastBox;
        private HBoxContainer _abilityBar;
        private readonly Label[] _abilityLabels = new Label[3];
        private Control _bossPanel;
        private Label _bossName;
        private ProgressBar _bossBar;
        private Label _hpLabel;
        private Label _goldLabel;
        private Label _levelLabel;
        private Label _dayLabel;
        private Label _timeLabel;
        private Label _questLabel;
        private Label _questHeaderLabel;
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
            Loc.LanguageChanged += Refresh;
            Loc.LanguageChanged += RefreshBuildingLabel;
            MailSystem.Instance.MailChanged += Refresh;
            Refresh();
        }

        // Thanh mau trum o dinh man hinh (xem Enemy.UpdateBossHud) - hien khi dang giao chien.
        public void ShowBossBar(string name, int hp, int max)
        {
            if (_bossPanel == null) return;
            _bossName.Text = name;
            _bossBar.MaxValue = max;
            _bossBar.Value = hp;
            _bossPanel.Visible = true;
        }

        public void HideBossBar()
        {
            if (_bossPanel != null) _bossPanel.Visible = false;
        }

        // Toast: xep chong toi da 5 dong o giua-duoi man hinh, moi dong tu mo dan roi bien mat.
        public void ShowToast(string text)
        {
            if (_toastBox == null || string.IsNullOrEmpty(text)) return;
            while (_toastBox.GetChildCount() >= 5) _toastBox.GetChild(0).Free();
            var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Word };
            lbl.CustomMinimumSize = new Vector2(560, 0);
            lbl.AddThemeFontSizeOverride("font_size", 15);
            lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
            lbl.AddThemeConstantOverride("outline_size", 5);
            _toastBox.AddChild(lbl);
            var tw = lbl.CreateTween();
            tw.TweenInterval(3.0);
            tw.TweenProperty(lbl, "modulate:a", 0f, 0.8);
            tw.TweenCallback(Callable.From(lbl.QueueFree));
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= Refresh;
            Loc.LanguageChanged -= RefreshBuildingLabel;
            if (MailSystem.Instance != null) MailSystem.Instance.MailChanged -= Refresh;
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
            _buildingNameLabel.Text = Loc.T(_nearbyBuildings[^1]);
            _buildingNameLabel.Visible = true;
        }

        public override void _Process(double delta)
        {
            // Dong ho thuc: cap nhat rieng moi frame de kim phut/giay chay muot,
            // khong phu thuoc cac su kien StatsChanged/Quest o Refresh().
            var now = GameManager.Instance.GameNow; // gio game dong bo server
            string dayNight = GameManager.Instance.IsNight ? Loc.T("hud.night") : Loc.T("hud.day");
            _dayLabel.Text = $"{now:dd/MM/yyyy}";
            _timeLabel.Text = $"{now:HH:mm:ss} ({dayNight})";

            _compass.QueueRedraw();
            UpdateAbilityBar();
        }

        // Thanh ky nang (1/2/3): ten + hoi chieu con lai, mo theo cap nhan vat.
        private void UpdateAbilityBar()
        {
            var ab = GetTree().GetFirstNodeInGroup("player_abilities") as HiepSiVeVuon.Entities.PlayerAbilities;
            if (ab == null) { _abilityBar.Visible = false; return; }
            _abilityBar.Visible = true;
            for (int i = 0; i < 3; i++)
            {
                var a = HiepSiVeVuon.Entities.PlayerAbilities.All[i];
                float cd = ab.CooldownLeft(i);
                bool locked = GameManager.Instance.Level < a.Level;
                _abilityLabels[i].Text = $"[{i + 1}] {Loc.T("ability." + a.Key)}" + (locked ? $" (Lv{a.Level})" : cd > 0 ? $" {cd:0.0}s" : "");
                _abilityLabels[i].Modulate = locked ? new Color(1, 1, 1, 0.35f) : cd > 0 ? new Color(1, 1, 1, 0.6f) : Colors.White;
            }
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
            _staminaBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 14), ShowPercentage = false };
            _staminaBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.95f, 0.75f, 0.2f) });
            vb.AddChild(_staminaBar);
            _staminaLabel = new Label(); vb.AddChild(_staminaLabel);
            _levelLabel = new Label(); vb.AddChild(_levelLabel);
            _goldLabel = new Label(); vb.AddChild(_goldLabel);
            _dayLabel = new Label(); vb.AddChild(_dayLabel);
            _timeLabel = new Label(); vb.AddChild(_timeLabel);
            _seasonLabel = new Label(); vb.AddChild(_seasonLabel);
            _mailLabel = new Label { Visible = false };
            _mailLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            vb.AddChild(_mailLabel);

            _questHeaderLabel = new Label { Text = Loc.T("hud.quest_header") };
            vb.AddChild(_questHeaderLabel);
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
            _bossPanel = new VBoxContainer { Position = new Vector2(230, 8), CustomMinimumSize = new Vector2(500, 0), Visible = false };
            _bossName = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _bossName.AddThemeFontSizeOverride("font_size", 18);
            _bossName.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.5f));
            _bossName.AddThemeColorOverride("font_outline_color", Colors.Black);
            _bossName.AddThemeConstantOverride("outline_size", 6);
            _bossPanel.AddChild(_bossName);
            _bossBar = new ProgressBar { CustomMinimumSize = new Vector2(500, 16), ShowPercentage = false };
            _bossBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.8f, 0.12f, 0.15f) });
            _bossBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.05f, 0.05f, 0.85f) });
            _bossPanel.AddChild(_bossBar);
            AddChild(_bossPanel);

            _abilityBar = new HBoxContainer { Position = new Vector2(300, 512) };
            _abilityBar.AddThemeConstantOverride("separation", 14);
            for (int i = 0; i < 3; i++)
            {
                _abilityLabels[i] = new Label();
                _abilityLabels[i].AddThemeFontSizeOverride("font_size", 12);
                _abilityLabels[i].AddThemeColorOverride("font_outline_color", Colors.Black);
                _abilityLabels[i].AddThemeConstantOverride("outline_size", 4);
                _abilityBar.AddChild(_abilityLabels[i]);
            }
            AddChild(_abilityBar);

            _toastBox = new VBoxContainer { Position = new Vector2(200, 400), CustomMinimumSize = new Vector2(560, 0) };
            _toastBox.MouseFilter = Control.MouseFilterEnum.Ignore;
            _toastBox.Alignment = BoxContainer.AlignmentMode.End;
            AddChild(_toastBox);

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
            _hpLabel.Text = string.Format(Loc.T("hud.hp_fmt"), FormatHp(gm.Hp), FormatHp(gm.MaxHp));
            _levelLabel.Text = string.Format(Loc.T("hud.level_fmt"), gm.Level, gm.Exp, gm.ExpToNext);
            _goldLabel.Text = string.Format(Loc.T("hud.gold_fmt"), gm.Gold);
            _staminaBar.MaxValue = gm.MaxStamina;
            _staminaBar.Value = gm.Stamina;
            _staminaLabel.Text = string.Format(Loc.T("hud.stamina_fmt"), (int)gm.Stamina, (int)gm.MaxStamina)
                + (gm.IsExhausted ? " " + Loc.T("hud.exhausted") : "");
            string weather = WeatherSystem.Instance?.DisplayName ?? (gm.IsRaining ? Loc.T("hud.rain") : Loc.T("hud.sunny"));
            _seasonLabel.Text = string.Format(Loc.T("hud.season_fmt"), Loc.T("season." + gm.CurrentSeason), gm.DayInSeason + 1, weather)
                + (gm.IsFestivalDay ? " " + Loc.T("hud.festival") : "")
                + (FestivalSystem.Instance?.TodayName is string fest ? " [" + fest + "]" : "");
            int unread = MailSystem.Instance?.UnreadCount ?? 0;
            _mailLabel.Visible = unread > 0;
            _mailLabel.Text = string.Format(Loc.T("hud.mail_fmt"), unread);
            _questHeaderLabel.Text = Loc.T("hud.quest_header");

            // Hien nhiem vu dang lam
            string qtext = "";
            foreach (var kv in QuestSystem.Instance.Active)
            {
                var def = ItemDatabase.Instance.GetQuest(kv.Key);
                if (def != null)
                    qtext += $"- {ItemDatabase.Instance.GetQuestDisplayTitle(kv.Key)} ({kv.Value}/{def.TargetCount})\n";
            }
            _questLabel.Text = qtext == "" ? Loc.T("hud.quest_empty") : qtext;
        }

        // Rut gon so mau tu 1000 tro len thanh dang "1k"/"1.2k" (theo dung yeu cau) - mau toi da
        // tang +100/lan len cap nen se nhanh choong vuot 1000, so day du (vd "1500") chiem nhieu
        // cho tren HUD hon can thiet. Duoi 1000 giu nguyen so binh thuong.
        private static string FormatHp(int value)
        {
            if (value < 1000) return value.ToString();
            float k = value / 1000f;
            return Mathf.IsEqualApprox(k, Mathf.Round(k)) ? $"{k:0}k" : $"{k:0.#}k";
        }
    }
}
