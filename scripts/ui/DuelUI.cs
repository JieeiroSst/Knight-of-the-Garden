using Godot;
using System.Text.Json;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Dau truong: loi moi thach dau (dong y/tu choi), 2 thanh mau trong tran (server tinh), ket qua +/- Elo.
    // Trong tran, bam tan cong khi dung gan doi thu -> gui duel_hit (server kiem tam danh/hoi chieu).
    public partial class DuelUI : CanvasLayer
    {
        public static string ActiveDuelId { get; private set; }
        public static int OpponentId { get; private set; }

        private Panel _invite;
        private Label _inviteText;
        private string _inviteId;
        private Control _bars;
        private ProgressBar _myBar, _foeBar;
        private Label _myName, _foeName;

        public override void _Ready()
        {
            AddToGroup("duel_ui");
            _invite = UiStyle.MakePanel(new Vector2(330, 120), new Vector2(300, 100));
            AddChild(_invite);
            var vb = new VBoxContainer { Position = new Vector2(14, 10), CustomMinimumSize = new Vector2(272, 0) };
            _invite.AddChild(vb);
            _inviteText = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(272, 0) };
            vb.AddChild(_inviteText);
            var hb = new HBoxContainer();
            vb.AddChild(hb);
            var yes = new Button { Text = Loc.T("duel.accept") };
            yes.Pressed += () => { _invite.Visible = false; NetworkClient.Instance.DuelAccept(_inviteId); };
            var no = new Button { Text = Loc.T("trade.decline") };
            no.Pressed += () => _invite.Visible = false;
            hb.AddChild(yes);
            hb.AddChild(no);
            _invite.Visible = false;

            _bars = new VBoxContainer { Position = new Vector2(280, 60), CustomMinimumSize = new Vector2(400, 0), Visible = false };
            AddChild(_bars);
            (_myName, _myBar) = Bar(new Color(0.3f, 0.8f, 0.4f));
            (_foeName, _foeBar) = Bar(new Color(0.9f, 0.25f, 0.2f));
            NetworkClient.Instance.SocialEvent += OnEvent;
        }

        public override void _ExitTree() => NetworkClient.Instance.SocialEvent -= OnEvent;

        private (Label, ProgressBar) Bar(Color c)
        {
            var l = new Label();
            l.AddThemeColorOverride("font_outline_color", Colors.Black);
            l.AddThemeConstantOverride("outline_size", 5);
            var b = new ProgressBar { CustomMinimumSize = new Vector2(400, 14), ShowPercentage = false, MaxValue = 400 };
            b.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = c });
            _bars.AddChild(l);
            _bars.AddChild(b);
            return (l, b);
        }

        private void OnEvent(string type, string json)
        {
            using var doc = JsonDocument.Parse(json);
            var e = doc.RootElement;
            int me = NetworkClient.Instance.MyId;
            switch (type)
            {
                case "duel_invite":
                    _inviteId = e.GetProperty("duelId").GetString();
                    _inviteText.Text = string.Format(Loc.T("duel.invite_fmt"), e.GetProperty("fromName").GetString());
                    _invite.Visible = true;
                    break;
                case "duel_invited":
                    Notify.Show(string.Format(Loc.T("duel.invited_fmt"), e.GetProperty("toName").GetString()));
                    break;
                case "duel_start":
                    ActiveDuelId = e.GetProperty("duelId").GetString();
                    int a = e.GetProperty("aId").GetInt32(), b = e.GetProperty("bId").GetInt32();
                    OpponentId = a == me ? b : a;
                    _myName.Text = (a == me ? e.GetProperty("aName") : e.GetProperty("bName")).GetString();
                    _foeName.Text = (a == me ? e.GetProperty("bName") : e.GetProperty("aName")).GetString();
                    _myBar.MaxValue = _foeBar.MaxValue = e.GetProperty("hp").GetInt32();
                    _myBar.Value = _foeBar.Value = _myBar.MaxValue;
                    _bars.Visible = true;
                    Notify.Show(Loc.T("duel.start"));
                    break;
                case "duel_hp":
                    var hp = e.GetProperty("hp");
                    foreach (var p in hp.EnumerateObject())
                        if (int.Parse(p.Name) == me) _myBar.Value = p.Value.GetInt32(); else _foeBar.Value = p.Value.GetInt32();
                    int by = e.GetProperty("by").GetInt32();
                    var hitTarget = NetworkClient.Instance.FindRemote(by == me ? OpponentId : by);
                    if (by == me && hitTarget != null) HiepSiVeVuon.Entities.EnemyVisuals.HitFlash(hitTarget);
                    break;
                case "duel_end":
                    int winner = e.GetProperty("winnerId").GetInt32();
                    int delta = e.GetProperty("delta").GetInt32();
                    Notify.Show(winner == 0 ? Loc.T("duel.draw") : winner == me ? string.Format(Loc.T("duel.win_fmt"), delta) : string.Format(Loc.T("duel.lose_fmt"), delta));
                    ActiveDuelId = null;
                    OpponentId = 0;
                    _bars.Visible = false;
                    break;
            }
        }
    }
}
