using Godot;
using System.Linq;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.UI
{
    // Nhat Ky [K]: 4 tab - Ky Nang / Ban Be / Thanh Tuu / Thu. Tab Thu cung mo tu Hop Thu (Mailbox.cs).
    public partial class JournalUI : CanvasLayer
    {
        public enum Tab { Skills, Friends, Achievements, Mail }
        private Tab _tab = Tab.Skills;

        private VBoxContainer _content;
        private HBoxContainer _tabs;
        private Label _title;
        private MailSystem.Letter _selectedLetter;

        public override void _Ready()
        {
            AddToGroup("journal_ui");
            var panel = UiStyle.MakePanel(new Vector2(170, 30), new Vector2(620, 480));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(580, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            _tabs = new HBoxContainer();
            vb.AddChild(_tabs);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(580, 340) };
            vb.AddChild(scroll);
            _content = new VBoxContainer { CustomMinimumSize = new Vector2(565, 0) };
            _content.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_content);
            var close = new Button { Text = Loc.T("journal.close_btn") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;

            MailSystem.Instance.MailChanged += () => { if (Visible) Refresh(); };
            Loc.LanguageChanged += OnLanguageChanged;
        }

        public override void _ExitTree() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged() { if (Visible) Refresh(); }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e.IsActionPressed("toggle_journal"))
            {
                if (Visible) Visible = false; else Open(_tab);
                GetViewport().SetInputAsHandled();
            }
            else if (Visible && e.IsActionPressed("ui_cancel"))
            {
                Visible = false;
                GetViewport().SetInputAsHandled();
            }
        }

        public void Open(Tab tab)
        {
            _tab = tab;
            _selectedLetter = null;
            Refresh();
            Visible = true;
        }

        private void Refresh()
        {
            _title.Text = Loc.T("journal.title");
            UiStyle.Clear(_tabs);
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                string text = Loc.T("journal.tab." + t);
                if (t == Tab.Mail && MailSystem.Instance.UnreadCount > 0) text += $" ({MailSystem.Instance.UnreadCount})";
                var b = new Button { Text = text, ToggleMode = true, ButtonPressed = t == _tab };
                var tab = t;
                b.Pressed += () => { _tab = tab; _selectedLetter = null; Refresh(); };
                _tabs.AddChild(b);
            }

            UiStyle.Clear(_content);
            switch (_tab)
            {
                case Tab.Skills: BuildSkills(); break;
                case Tab.Friends: BuildFriends(); break;
                case Tab.Achievements: BuildAchievements(); break;
                case Tab.Mail: BuildMail(); break;
            }
        }

        private void BuildSkills()
        {
            var ss = SkillSystem.Instance;
            foreach (SkillType s in System.Enum.GetValues(typeof(SkillType)))
            {
                int lvl = ss.GetLevel(s);
                var (cur, need) = ss.GetProgress(s);
                var name = new Label { Text = string.Format(Loc.T("journal.skill_fmt"), Loc.T("skill." + s), lvl, SkillSystem.MaxLevel) };
                name.AddThemeColorOverride("font_color", UiStyle.Text);
                _content.AddChild(name);
                _content.AddChild(new ProgressBar { MaxValue = need, Value = cur, CustomMinimumSize = new Vector2(540, 12), ShowPercentage = false });
                _content.AddChild(UiStyle.Small(Loc.T("skill.perk." + s)));
            }
            _content.AddChild(UiStyle.Small(string.Format(Loc.T("journal.stamina_bonus_fmt"), ss.TotalLevels * 3)));
            var home = HomeSystem.Instance;
            var adv = AdventureSystem.Instance;
            _content.AddChild(new HSeparator());
            _content.AddChild(UiStyle.Small(string.Format(Loc.T("journal.home_fmt"), home.HouseLevel, HomeSystem.MaxHouseLevel, (int)home.HerdHappiness, home.HappinessLabel), UiStyle.Text));
            _content.AddChild(UiStyle.Small(string.Format(Loc.T("journal.adventure_fmt"), adv.Discovered.Count, WorldStreamer.Regions.Count,
                adv.OpenedChests.Count, adv.DeepestDungeonFloor, adv.MuseumDonated.Count), UiStyle.Text));
        }

        private void BuildFriends()
        {
            var fs = FriendshipSystem.Instance;
            var known = fs.Points.OrderByDescending(kv => kv.Value).ToList();
            if (known.Count == 0) { _content.AddChild(UiStyle.Small(Loc.T("journal.no_friends"))); return; }
            foreach (var kv in known)
            {
                var row = new HBoxContainer();
                _content.AddChild(row);
                var name = new Label { Text = fs.GetName(kv.Key), CustomMinimumSize = new Vector2(200, 0), ClipText = true };
                name.AddThemeColorOverride("font_color", UiStyle.Text);
                row.AddChild(name);
                row.AddChild(GiftUI.HeartBar(fs.GetPoints(kv.Key)));
                row.AddChild(new Label { Text = $" {fs.GetHearts(kv.Key)}/10" });
                string extra = fs.GetBirthdayText(kv.Key);
                if (fs.GiftedToday(kv.Key)) extra += "  " + Loc.T("journal.gifted_today");
                row.AddChild(UiStyle.Small("  " + extra));
            }
        }

        private void BuildAchievements()
        {
            var ach = AchievementSystem.Instance;
            var all = ItemDatabase.Instance.Achievements;
            _content.AddChild(UiStyle.Small(string.Format(Loc.T("journal.ach_progress_fmt"), ach.Unlocked.Count, all.Count), UiStyle.Gold));
            foreach (var a in all)
            {
                bool done = ach.Unlocked.Contains(a.Id);
                var name = new Label { Text = (done ? "[x] " : "[  ] ") + AchievementSystem.GetTitle(a) };
                name.AddThemeColorOverride("font_color", done ? UiStyle.Gold : UiStyle.Dim);
                _content.AddChild(name);
                int cur = Mathf.Min(ach.GetStat(a.Stat), a.Target);
                _content.AddChild(UiStyle.Small($"{AchievementSystem.GetDescription(a)}  ({cur}/{a.Target})  +{a.RewardGold}g"));
            }
        }

        private void BuildMail()
        {
            var ms = MailSystem.Instance;
            if (ms.Letters.Count == 0) { _content.AddChild(UiStyle.Small(Loc.T("journal.no_mail"))); return; }

            if (_selectedLetter != null && ms.Letters.Contains(_selectedLetter))
            {
                var l = _selectedLetter;
                _content.AddChild(UiStyle.Title(l.Title));
                _content.AddChild(UiStyle.Small(string.Format(Loc.T("journal.mail_day_fmt"), l.Day), UiStyle.Dim));
                var body = new Label { Text = l.Body, AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(540, 0) };
                body.AddThemeColorOverride("font_color", UiStyle.Text);
                _content.AddChild(body);
                if (l.HasAttachment)
                {
                    string att = l.Gold > 0 ? $"{l.Gold}g" : "";
                    if (!string.IsNullOrEmpty(l.ItemId)) att += (att == "" ? "" : " + ") + $"{ItemDatabase.Instance.GetDisplayName(l.ItemId)} x{l.ItemCount}";
                    var claim = new Button { Text = string.Format(Loc.T("journal.claim_fmt"), att) };
                    claim.Pressed += () => { ms.Claim(l); Refresh(); };
                    _content.AddChild(claim);
                }
                var back = new Button { Text = Loc.T("journal.back_btn") };
                back.Pressed += () => { _selectedLetter = null; Refresh(); };
                _content.AddChild(back);
                return;
            }

            foreach (var l in ms.Letters)
            {
                string mark = !l.Read ? "(*) " : l.HasAttachment ? "[+] " : "";
                var b = new Button { Text = $"{mark}{l.Title}  ({string.Format(Loc.T("journal.mail_day_fmt"), l.Day)})", Alignment = HorizontalAlignment.Left };
                var letter = l;
                // MarkRead phat MailChanged -> Refresh(); dat _selectedLetter TRUOC de lan Refresh do
                // da hien dung thu (khong goi MarkRead trong luc dang dung danh sach).
                b.Pressed += () => { _selectedLetter = letter; if (letter.Read) Refresh(); else ms.MarkRead(letter); };
                _content.AddChild(b);
            }
        }
    }
}
