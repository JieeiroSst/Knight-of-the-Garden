using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Minigame cau ca (thay cho "60% trung" ngau nhien truoc day): tha cau -> cho ca can -> thanh
    // keo hien ra, con tro chay qua lai, bam [Space]/chuot phai DUNG LUC con tro nam trong vung
    // xanh de bat ca. Loai ca chon theo mua/gio/thoi tiet (data/fish.json); ca kho hon thi con
    // tro nhanh hon + vung xanh hep hon; cap Cau Ca va Moi Cau mo rong vung xanh.
    public partial class FishingUI : CanvasLayer
    {
        private enum State { Idle, Waiting, Reeling }
        private State _state = State.Idle;

        private const float BarWidth = 320f;
        private Panel _panel;
        private Label _label;
        private Control _bar;
        private ColorRect _zone, _cursor;

        private Node3D _player;
        private Vector3 _castPos;
        private double _timer;
        private FishDef _fish;
        private float _zoneStart, _zoneSize, _cursorPos, _cursorDir = 1f, _speed;

        public bool IsActive => _state != State.Idle;

        public override void _Ready()
        {
            AddToGroup("fishing_ui");
            _panel = UiStyle.MakePanel(new Vector2(300, 380), new Vector2(360, 100));
            AddChild(_panel);
            _label = new Label { Position = new Vector2(20, 10), CustomMinimumSize = new Vector2(320, 0), HorizontalAlignment = HorizontalAlignment.Center };
            _label.AddThemeColorOverride("font_color", UiStyle.Text);
            _panel.AddChild(_label);
            _bar = new Control { Position = new Vector2(20, 50), CustomMinimumSize = new Vector2(BarWidth, 24) };
            _panel.AddChild(_bar);
            _bar.AddChild(new ColorRect { Color = new Color(0.2f, 0.25f, 0.35f), Size = new Vector2(BarWidth, 24) });
            _zone = new ColorRect { Color = new Color(0.3f, 0.8f, 0.35f), Size = new Vector2(60, 24) };
            _bar.AddChild(_zone);
            _cursor = new ColorRect { Color = new Color(1f, 0.9f, 0.3f), Size = new Vector2(5, 32), Position = new Vector2(0, -4) };
            _bar.AddChild(_cursor);
            _panel.Visible = false;
        }

        public void Start(Node3D player)
        {
            if (IsActive) return;
            _player = player;
            _castPos = player.GlobalPosition;
            _state = State.Waiting;
            _timer = GD.RandRange(1.2, 3.5);
            _bar.Visible = false;
            _label.Text = Loc.T("fish.waiting");
            _panel.Visible = true;
        }

        private void Stop(string message)
        {
            _state = State.Idle;
            _panel.Visible = false;
            if (!string.IsNullOrEmpty(message)) Notify.Show(message);
        }

        public override void _Process(double delta)
        {
            if (_state == State.Idle) return;
            if (_player == null || !IsInstanceValid(_player) || _player.GlobalPosition.DistanceTo(_castPos) > 60f)
            {
                Stop(Loc.T("fish.moved_away"));
                return;
            }

            _timer -= delta;
            if (_state == State.Waiting)
            {
                if (_timer <= 0) BeginReel();
                return;
            }

            _cursorPos += _cursorDir * _speed * (float)delta;
            if (_cursorPos > 1f) { _cursorPos = 1f; _cursorDir = -1f; }
            if (_cursorPos < 0f) { _cursorPos = 0f; _cursorDir = 1f; }
            _cursor.Position = new Vector2(_cursorPos * BarWidth - 2.5f, -4);
            if (_timer <= 0) Stop(Loc.T("fish.escaped"));
        }

        public override void _Input(InputEvent e)
        {
            if (_state == State.Idle) return;
            if (e.IsActionPressed("use_tool"))
            {
                GetViewport().SetInputAsHandled();
                if (_state == State.Waiting) Stop(Loc.T("fish.too_early"));
                else TryCatch();
            }
            else if (e.IsActionPressed("ui_cancel"))
            {
                GetViewport().SetInputAsHandled();
                Stop(null);
            }
        }

        private void BeginReel()
        {
            var gm = GameManager.Instance;
            int hour = gm.Hour;
            int level = SkillSystem.Instance.GetLevel(SkillType.Fishing);
            string season = gm.CurrentSeason.ToString();
            var pool = ItemDatabase.Instance.Fish.Where(f =>
                (f.Seasons == null || f.Seasons.Length == 0 || f.Seasons.Contains(season))
                && (!f.RainOnly || gm.IsRaining)
                && level >= f.MinFishingLevel
                && InHourRange(hour, f.HourMin, f.HourMax)
                && ItemDatabase.Instance.GetItem(f.ItemId) != null).ToList();
            if (pool.Count == 0) { Stop(Loc.T("fish.escaped")); return; }

            int total = pool.Sum(f => f.Weight);
            int pick = (int)(GD.Randi() % (uint)total);
            _fish = pool[0];
            foreach (var f in pool) { if (pick < f.Weight) { _fish = f; break; } pick -= f.Weight; }

            bool bait = Inventory.Instance.RemoveItem("moi_cau", 1);
            _zoneSize = Mathf.Clamp(0.32f / Mathf.Sqrt(_fish.Difficulty) * (1f + SkillSystem.Instance.FishingZoneBonus) * (bait ? 1.3f : 1f), 0.08f, 0.7f);
            _zoneStart = (float)GD.RandRange(0, 1f - _zoneSize);
            _speed = 0.55f + 0.4f * _fish.Difficulty;
            _cursorPos = 0f;
            _cursorDir = 1f;
            _zone.Position = new Vector2(_zoneStart * BarWidth, 0);
            _zone.Size = new Vector2(_zoneSize * BarWidth, 24);
            _bar.Visible = true;
            _timer = 6.0;
            _state = State.Reeling;
            _label.Text = Loc.T("fish.bite") + (bait ? "  " + Loc.T("fish.bait_used") : "");
        }

        private static bool InHourRange(int hour, int min, int max) =>
            min <= max ? hour >= min && hour < max : hour >= min || hour < max;

        private void TryCatch()
        {
            if (_cursorPos < _zoneStart || _cursorPos > _zoneStart + _zoneSize)
            {
                Stop(Loc.T("fish.escaped"));
                return;
            }
            string id = _fish.ItemId;
            if (!Inventory.Instance.AddItem(id, 1, "fish")) { Stop(Loc.T("mail.inventory_full")); return; }
            FarmStorage.Instance.Add(id, 1);
            QuestSystem.Instance.OnItemCollected(id);
            WaterEcosystem.Instance.OnPlayerCatch("fish", 3f);
            SkillSystem.Instance.AddXp(SkillType.Fishing, _fish.Xp);
            AchievementSystem.Instance.Add("fish_caught");
            if (id == "ca_vang") AchievementSystem.Instance.Add("legend_fish");
            Stop(string.Format(Loc.T("fish.caught_fmt"), ItemDatabase.Instance.GetDisplayName(id)));
        }
    }
}
