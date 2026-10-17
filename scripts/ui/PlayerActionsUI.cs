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
        private int _targetId;
        private Button _mute;
        private Button _partyLeave;
        private RemotePlayer _target;

        public override void _Ready()
        {
            AddToGroup("player_actions_ui");
            var panel = UiStyle.MakePanel(new Vector2(360, 90), new Vector2(240, 360));
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
            // Ho tro: moi to doi (khong danh nhau, thay vi tri tren ban do), hoi mau bang thuoc cua minh.
            var party = new Button { Text = Loc.T("pa.party_invite") };
            party.Pressed += () => { Visible = false; PvpClient.Instance?.PartyInvite(_targetName); };
            vb.AddChild(party);
            var heal = new Button { Text = Loc.T("pa.heal") };
            heal.Pressed += () => { Visible = false; if (_target != null && IsInstanceValid(_target)) PvpClient.Instance?.HealAlly(_target); };
            vb.AddChild(heal);
            _partyLeave = new Button { Text = Loc.T("pa.party_leave") };
            _partyLeave.Pressed += () => { Visible = false; PvpClient.Instance?.PartyLeave(); };
            vb.AddChild(_partyLeave);
            _mute = new Button();
            _mute.Pressed += () => { VoiceChat.Instance?.ToggleMute(_targetId); RefreshMute(); };
            vb.AddChild(_mute);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
        }

        public void Open(RemotePlayer target)
        {
            _targetName = target.DisplayName;
            _targetId = target.PlayerId;
            _target = target;
            _partyLeave.Visible = PvpClient.Instance?.PartyId != null;
            _title.Text = target.DisplayName;
            RefreshMute();
            Visible = true;
        }

        private void RefreshMute() =>
            _mute.Text = Loc.T(VoiceChat.Instance?.Muted.Contains(_targetId) == true ? "pa.unmute_voice" : "pa.mute_voice");
    }
}
