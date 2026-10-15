using Godot;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Menu nho khi bam [E] vao nguoi choi khac: Giao dich / Nhan rieng.
    public partial class PlayerActionsUI : CanvasLayer
    {
        private Label _title;
        private string _targetName;

        public override void _Ready()
        {
            AddToGroup("player_actions_ui");
            var panel = UiStyle.MakePanel(new Vector2(360, 150), new Vector2(240, 230));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(16, 12), CustomMinimumSize = new Vector2(208, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            var trade = new Button { Text = Loc.T("pa.trade") };
            trade.Pressed += () => { Visible = false; (GetTree().GetFirstNodeInGroup("trade_ui") as TradeUI)?.InviteByName(_targetName); };
            vb.AddChild(trade);
            var duel = new Button { Text = Loc.T("pa.duel") };
            duel.Pressed += () => { Visible = false; NetworkClient.Instance.DuelInvite(_targetName); };
            vb.AddChild(duel);
            var friend = new Button { Text = Loc.T("pa.add_friend") };
            friend.Pressed += () =>
            {
                Visible = false;
                BackendClient.Instance.PostJson("/api/friends", System.Text.Json.JsonSerializer.Serialize(new { name = _targetName }),
                    (ok, st, t) => Notify.Show(ok && st == 200 ? Loc.T("social.request_sent") : BackendClient.ErrorOf(t, Loc.T("net.server_error"))));
            };
            vb.AddChild(friend);
            var whisper = new Button { Text = Loc.T("pa.whisper") };
            whisper.Pressed += () =>
            {
                Visible = false;
                (GetTree().GetFirstNodeInGroup("chat_ui") as ChatUI)?.AddLine("system", "", string.Format(Loc.T("pa.whisper_hint_fmt"), _targetName));
            };
            vb.AddChild(whisper);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
        }

        public void Open(RemotePlayer target)
        {
            _targetName = target.DisplayName;
            _title.Text = target.DisplayName;
            Visible = true;
        }
    }
}
