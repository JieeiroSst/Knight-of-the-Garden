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
        private Label _hint;

        public override void _Ready()
        {
            BuildUI();
            GameManager.Instance.StatsChanged += Refresh;
            QuestSystem.Instance.QuestUpdated += _ => Refresh();
            QuestSystem.Instance.QuestCompleted += _ => Refresh();
            Refresh();
        }

        public override void _Process(double delta)
        {
            // Dong ho thuc: cap nhat rieng moi frame de kim phut/giay chay muot,
            // khong phu thuoc cac su kien StatsChanged/Quest o Refresh().
            var now = System.DateTime.Now;
            string dayNight = GameManager.Instance.IsNight ? "Dem" : "Ngay";
            _dayLabel.Text = $"{now:dd/MM/yyyy}";
            _timeLabel.Text = $"{now:HH:mm:ss} ({dayNight})";
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

            _hint = new Label
            {
                Text = "[WASD] Di | [J/Chuot] Danh | [Space] Cong cu | [E] Tuong tac/Cua/Cau thang | [I] Tui do | [F5] Luu"
            };
            _hint.AutowrapMode = TextServer.AutowrapMode.Word;
            _hint.Position = new Vector2(10, 500);
            _hint.CustomMinimumSize = new Vector2(940, 0);
            _hint.Scale = new Vector2(0.6f, 0.6f); // thu nho dong huong dan xuong con 60%
            AddChild(_hint);
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
