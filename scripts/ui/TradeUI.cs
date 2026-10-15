using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Giao dich truc tiep giua 2 nguoi choi (backend tradeService): loi moi -> chap nhan -> moi ben
    // dat vat pham/vang -> Khoa -> Xac nhan -> server chuyen do nguyen tu. Sua de nghi sau khi khoa se
    // mo khoa ca 2 ben (chong doi do phut chot). Moi thay doi deu qua server; ben kia nhan su kien WS.
    public partial class TradeUI : CanvasLayer
    {
        private int _tradeId;
        private Panel _invitePanel;
        private Label _inviteLabel;
        private int _inviteId;

        private Label _title, _status;
        private VBoxContainer _myList, _theirList, _invList;
        private SpinBox _gold;
        private Button _lockBtn, _confirmBtn;
        private readonly Dictionary<string, int> _myItems = new();
        private bool _locked, _theirLocked, _confirmed;

        public override void _Ready()
        {
            AddToGroup("trade_ui");
            BuildInvitePopup();
            BuildWindow();
            NetworkClient.Instance.TradeEvent += OnTradeEvent;
        }

        public override void _ExitTree() => NetworkClient.Instance.TradeEvent -= OnTradeEvent;

        private Panel _window;

        private void BuildInvitePopup()
        {
            _invitePanel = UiStyle.MakePanel(new Vector2(330, 20), new Vector2(300, 100));
            AddChild(_invitePanel);
            var vb = new VBoxContainer { Position = new Vector2(14, 10), CustomMinimumSize = new Vector2(272, 0) };
            _invitePanel.AddChild(vb);
            _inviteLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(272, 0) };
            _inviteLabel.AddThemeColorOverride("font_color", UiStyle.Text);
            vb.AddChild(_inviteLabel);
            var hb = new HBoxContainer();
            vb.AddChild(hb);
            var yes = new Button { Text = Loc.T("trade.accept") };
            yes.Pressed += () => { _invitePanel.Visible = false; Call("POST", $"/api/trades/{_inviteId}/accept", null, _ => Open(_inviteId)); };
            hb.AddChild(yes);
            var no = new Button { Text = Loc.T("trade.decline") };
            no.Pressed += () => { _invitePanel.Visible = false; Call("POST", $"/api/trades/{_inviteId}/cancel", null, null); };
            hb.AddChild(no);
            _invitePanel.Visible = false;
        }

        private void BuildWindow()
        {
            _window = UiStyle.MakePanel(new Vector2(110, 30), new Vector2(740, 480));
            AddChild(_window);
            var vb = new VBoxContainer { Position = new Vector2(16, 12), CustomMinimumSize = new Vector2(708, 0) };
            _window.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            _status = UiStyle.Small("");
            vb.AddChild(_status);
            var cols = new HBoxContainer();
            cols.AddThemeConstantOverride("separation", 12);
            vb.AddChild(cols);
            _invList = Column(cols, Loc.T("trade.your_bag"));
            _myList = Column(cols, Loc.T("trade.you_offer"));
            _theirList = Column(cols, Loc.T("trade.they_offer"));
            var goldRow = new HBoxContainer();
            vb.AddChild(goldRow);
            goldRow.AddChild(new Label { Text = Loc.T("trade.gold") });
            _gold = new SpinBox { MinValue = 0, MaxValue = 1_000_000_000, Step = 1, CustomMinimumSize = new Vector2(140, 0) };
            goldRow.AddChild(_gold);
            var send = new Button { Text = Loc.T("trade.update_offer") };
            send.Pressed += SendOffer;
            goldRow.AddChild(send);
            var btns = new HBoxContainer();
            vb.AddChild(btns);
            _lockBtn = new Button { Text = Loc.T("trade.lock") };
            // Gui het thay doi chua dong bo truoc (vd vua thu hoach) de server thay dung tui do.
            _lockBtn.Pressed += () => EconomyClient.Instance.FlushNow(_ => Call("POST", $"/api/trades/{_tradeId}/lock", null, Apply));
            btns.AddChild(_lockBtn);
            _confirmBtn = new Button { Text = Loc.T("trade.confirm") };
            _confirmBtn.Pressed += () => Call("POST", $"/api/trades/{_tradeId}/confirm", null, Apply);
            btns.AddChild(_confirmBtn);
            var cancel = new Button { Text = Loc.T("trade.cancel") };
            cancel.Pressed += () => { Call("POST", $"/api/trades/{_tradeId}/cancel", null, null); _window.Visible = false; };
            btns.AddChild(cancel);
            _window.Visible = false;
        }

        private static VBoxContainer Column(HBoxContainer parent, string header)
        {
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(228, 0) };
            parent.AddChild(box);
            box.AddChild(UiStyle.Small(header, UiStyle.Gold));
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(228, 280) };
            box.AddChild(scroll);
            var list = new VBoxContainer { CustomMinimumSize = new Vector2(215, 0) };
            scroll.AddChild(list);
            return list;
        }

        // ==== API ====
        private void Call(string method, string path, object body, System.Action<JsonElement> onOk)
        {
            void Done(bool ok, int status, string text)
            {
                if (ok && status >= 200 && status < 300)
                {
                    if (onOk != null && !string.IsNullOrEmpty(text))
                    {
                        using var doc = JsonDocument.Parse(text);
                        onOk(doc.RootElement.Clone());
                    }
                }
                else Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error")));
            }
            var b = BackendClient.Instance;
            string json = body == null ? null : JsonSerializer.Serialize(body);
            if (method == "POST") b.PostJson(path, json, Done);
            else if (method == "PUT") b.PutJson(path, json, Done);
            else b.GetJson(path, Done);
        }

        public void InviteByName(string name)
        {
            Call("POST", "/api/trades", new { toName = name }, e =>
            {
                Notify.Show(string.Format(Loc.T("trade.invited_fmt"), name));
                _tradeId = e.GetProperty("id").GetInt32();
                _partner = name;
                Apply(e);
            });
        }

        private string _partner = "";

        private void OnTradeEvent(string type, int tradeId, string fromName)
        {
            switch (type)
            {
                case "trade_invite":
                    _inviteId = tradeId;
                    _partner = fromName;
                    _inviteLabel.Text = string.Format(Loc.T("trade.invite_fmt"), fromName);
                    _invitePanel.Visible = true;
                    break;
                case "trade_update":
                    if (tradeId == _tradeId || !_window.Visible) Open(tradeId);
                    break;
                case "trade_done":
                    _window.Visible = false;
                    Notify.Show(Loc.T("trade.done"));
                    break;
                case "trade_cancelled":
                    if (tradeId == _tradeId) { _window.Visible = false; Notify.Show(Loc.T("trade.cancelled")); }
                    break;
            }
        }

        private void Open(int id)
        {
            _tradeId = id;
            Call("GET", $"/api/trades/{id}", null, Apply);
        }

        private void SendOffer()
        {
            Call("PUT", $"/api/trades/{_tradeId}/offer", new { gold = (long)_gold.Value, items = _myItems }, Apply);
        }

        // Ve lai cua so theo trang thai server.
        private void Apply(JsonElement t)
        {
            string status = t.GetProperty("status").GetString();
            if (status is "done" or "cancelled") { _window.Visible = false; return; }
            _window.Visible = status == "open" || _window.Visible || status == "pending";
            _locked = t.GetProperty("myLocked").GetBoolean();
            _theirLocked = t.GetProperty("theirLocked").GetBoolean();
            _confirmed = t.GetProperty("myConfirmed").GetBoolean();
            _title.Text = string.Format(Loc.T("trade.title_fmt"), _partner);
            _status.Text = status == "pending" ? Loc.T("trade.waiting_accept")
                : string.Format(Loc.T("trade.state_fmt"),
                    _locked ? Loc.T("trade.locked") : Loc.T("trade.editing"),
                    _theirLocked ? Loc.T("trade.locked") : Loc.T("trade.editing"),
                    t.GetProperty("theirConfirmed").GetBoolean() ? Loc.T("trade.they_confirmed") : "");

            _myItems.Clear();
            var mine = t.GetProperty("myOffer");
            foreach (var p in mine.GetProperty("items").EnumerateObject()) _myItems[p.Name] = p.Value.GetInt32();
            _gold.Value = mine.GetProperty("gold").GetInt64();

            UiStyle.Clear(_theirList);
            var theirs = t.GetProperty("theirOffer");
            _theirList.AddChild(new Label { Text = $"{Loc.T("trade.gold")} {theirs.GetProperty("gold").GetInt64()}" });
            foreach (var p in theirs.GetProperty("items").EnumerateObject())
                _theirList.AddChild(new Label { Text = $"{ItemDatabase.Instance.GetDisplayName(p.Name)} x{p.Value.GetInt32()}" });

            _lockBtn.Disabled = status != "open" || _locked;
            _confirmBtn.Disabled = status != "open" || !_locked || !_theirLocked || _confirmed;
            RefreshLists();
        }

        private void RefreshLists()
        {
            UiStyle.Clear(_myList);
            foreach (var (id, n) in _myItems.ToList())
            {
                var b = new Button { Text = $"{ItemDatabase.Instance.GetDisplayName(id)} x{n}  (-)", Alignment = HorizontalAlignment.Left };
                b.Pressed += () => { if (--_myItems[id] <= 0) _myItems.Remove(id); RefreshLists(); };
                _myList.AddChild(b);
            }
            UiStyle.Clear(_invList);
            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
            {
                var def = ItemDatabase.Instance.GetItem(id);
                if (def == null || def.Type == Data.ItemType.Quest) continue;
                int have = EconomyClient.TotalOwned(id) - (_myItems.TryGetValue(id, out var used) ? used : 0);
                if (have <= 0) continue;
                var b = new Button { Text = $"{ItemDatabase.Instance.GetDisplayName(id)} x{have}  (+)", Alignment = HorizontalAlignment.Left };
                b.Pressed += () => { _myItems[id] = (_myItems.TryGetValue(id, out var c) ? c : 0) + 1; RefreshLists(); };
                _invList.AddChild(b);
            }
        }
    }
}
