using Godot;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Vung chia se ma nguoi choi dang dung - quyet dinh vao kenh nao cua gateway:
    //  - Nong trai (quanh nha) = RIENG TU, khong vao vung chung (khong ai thay).
    //  - Thi tran = "town"; moi khu vuc the gioi mo (WorldStreamer.Regions) = "r<so thu tu>";
    //    vung hoang da con lai = "wild". Moi vung chia kenh <= 80 nguoi (server tu xep).
    public static class ZoneMap
    {
        public static Vector3 FarmCenter;
        public static float FarmRadius = 1700f;
        public static Vector3 TownCenter;
        public static float TownRadius = 1750f;

        public static string ZoneAt(Vector3 p)
        {
            var flat = new Vector2(p.X, p.Z);
            // Nong trai: zone rieng farm_<chu> - chi chu + ban be vao duoc (gateway kiem tra).
            if (flat.DistanceTo(new Vector2(FarmCenter.X, FarmCenter.Z)) < FarmRadius)
            {
                int owner = VisitSystem.Active ? VisitSystem.HostId : NetworkClient.Instance?.MyId ?? 0;
                return owner > 0 ? $"farm_{owner}" : null;
            }
            if (flat.DistanceTo(new Vector2(TownCenter.X, TownCenter.Z)) < TownRadius) return "town";
            var regions = WorldStreamer.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                if (Mathf.Abs(p.X - r.Center.X) <= r.HalfSize && Mathf.Abs(p.Z - r.Center.Z) <= r.HalfSize) return $"r{i}";
            }
            return "wild";
        }

        public static string DisplayName(string zone) => zone switch
        {
            null => Loc.T("net.zone.farm"),
            _ when zone.StartsWith("farm_") => VisitSystem.Active ? string.Format(Loc.T("net.zone.friend_farm_fmt"), VisitSystem.HostName) : Loc.T("net.zone.farm"),
            "town" => Loc.T("map.landmark.town"),
            "wild" => Loc.T("net.zone.wild"),
            _ when zone.StartsWith("r") && int.TryParse(zone[1..], out var i) && i < WorldStreamer.Regions.Count => WorldStreamer.Regions[i].Name,
            _ => zone,
        };
    }

    // [autoload] Ket noi WebSocket toi gateway (backend src/realtime/gateway.js): hien dien, vi tri
    // nguoi choi khac, chat kenh/nhan rieng, thong bao giao dich/cho. Tu ket noi lai khi mat mang.
    public partial class NetworkClient : Node
    {
        public static NetworkClient Instance { get; private set; }

        private WebSocketPeer _ws;
        private bool _wantConnected;
        private bool _authed;
        private double _reconnectIn;
        private double _backoff = 1;
        private double _sendTimer;
        private double _zoneTimer;
        private Vector3 _lastSentPos;
        private int _lastSentAnim = -1;
        private float _lastSentYaw;

        public int MyId { get; private set; }
        public string Zone { get; private set; }
        public int Channel { get; private set; }
        public bool Connected => _authed;
        private string _joinedZone;
        private readonly Dictionary<int, RemotePlayer> _remotes = new();

        [Signal] public delegate void ChatReceivedEventHandler(string channel, string from, string text);
        [Signal] public delegate void TradeEventEventHandler(string type, int tradeId, string fromName);
        [Signal] public delegate void StatusChangedEventHandler();
        // Su kien xa hoi/dau truong tu server (friend_request, guild_invite, farm_helped, duel_*...) -
        // json = noi dung su kien day du.
        [Signal] public delegate void SocialEventEventHandler(string type, string json);

        public override void _EnterTree() => Instance = this;

        public void Start()
        {
            _wantConnected = true;
            _reconnectIn = 0;
        }

        public void Stop()
        {
            _wantConnected = false;
            _ws?.Close();
            _ws = null;
            _authed = false;
            ClearRemotes();
        }

        public override void _Process(double delta)
        {
            if (!_wantConnected || BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn) return;

            if (_ws == null)
            {
                _reconnectIn -= delta;
                if (_reconnectIn > 0) return;
                _ws = new WebSocketPeer();
                if (_ws.ConnectToUrl(BackendClient.Instance.WsUrl) != Error.Ok) { Fail(); return; }
            }

            _ws.Poll();
            var state = _ws.GetReadyState();
            if (state == WebSocketPeer.State.Open)
            {
                if (!_authed && !_authSent)
                {
                    Send(new { t = "auth", token = BackendClient.Instance.AuthToken });
                    _authSent = true;
                }
                while (_ws.GetAvailablePacketCount() > 0)
                    Handle(_ws.GetPacket().GetStringFromUtf8());
                if (_authed) Tick(delta);
            }
            else if (state == WebSocketPeer.State.Closed)
            {
                int code = _ws.GetCloseCode();
                // 4002 = chua tao nhan vat, 4003 = dang nhap noi khac -> khong tu ket noi lai.
                if (code == 4002 || code == 4003)
                {
                    Notify.Show(Loc.T(code == 4003 ? "net.kicked_other_login" : "net.need_character"));
                    _wantConnected = false;
                }
                Fail();
            }
        }

        private bool _authSent;

        private void Fail()
        {
            _ws = null;
            _authed = false;
            _authSent = false;
            _joinedZone = null;
            ClearRemotes();
            _reconnectIn = _backoff;
            _backoff = Mathf.Min(30, _backoff * 2);
            EmitSignal(SignalName.StatusChanged);
        }

        private void Send(object msg)
        {
            if (_ws?.GetReadyState() == WebSocketPeer.State.Open) _ws.SendText(JsonSerializer.Serialize(msg));
        }

        private Node3D LocalPlayer => GetTree().GetFirstNodeInGroup("player") as Node3D;

        private void Tick(double delta)
        {
            var player = LocalPlayer;
            if (player == null) return;

            // Doi vung theo vi tri (moi 0.5s).
            _zoneTimer -= delta;
            if (_zoneTimer <= 0)
            {
                _zoneTimer = 0.5;
                string zone = ZoneMap.ZoneAt(player.GlobalPosition);
                if (zone != _joinedZone)
                {
                    _joinedZone = zone;
                    ClearRemotes();
                    if (zone == null) { Send(new { t = "leave" }); Zone = null; Channel = 0; EmitSignal(SignalName.StatusChanged); }
                    else Send(new { t = "join", zone });
                }
            }

            // Gui vi tri 5 lan/giay KHI co thay doi (dung yen thi khong gui gi).
            _sendTimer -= delta;
            if (_sendTimer > 0 || _joinedZone == null) return;
            _sendTimer = 0.2;
            var p = player.GlobalPosition;
            int anim = (player as Player)?.NetAnimCode ?? 0;
            float yaw = (player as Player)?.Yaw ?? 0f;
            if (p.DistanceTo(_lastSentPos) < 0.5f && anim == _lastSentAnim && Mathf.Abs(yaw - _lastSentYaw) < 0.05f) return;
            _lastSentPos = p;
            _lastSentAnim = anim;
            _lastSentYaw = yaw;
            Send(new { t = "pos", x = p.X, y = p.Y, z = p.Z, r = yaw, a = anim });
        }

        public void SendChat(string text) => Send(new { t = "chat", text });
        // Trang phuc dang mac (server kiem tra so huu roi bao lai cho nguoi cung kenh - xem gateway "outfit").
        private string _outfit;
        public void SendOutfit(string itemId)
        {
            _outfit = itemId;
            if (_joinedZone != null) Send(new { t = "outfit", id = itemId ?? "" });
        }
        public void GuildChat(string text) => Send(new { t = "gchat", text });
        public void DuelInvite(string to) => Send(new { t = "duel_invite", to });
        public void DuelAccept(string duelId) => Send(new { t = "duel_accept", duelId });
        public void DuelHit(string duelId) => Send(new { t = "duel_hit", duelId });
        public void DuelForfeit() => Send(new { t = "duel_forfeit" });
        public RemotePlayer FindRemote(int playerId) => _remotes.TryGetValue(playerId, out var r) && IsInstanceValid(r) ? r : null;
        public void Whisper(string to, string text) => Send(new { t = "whisper", to, text });
        public void SwitchChannel(int channel)
        {
            if (_joinedZone != null) { ClearRemotes(); Send(new { t = "join", zone = _joinedZone, channel }); }
        }

        private void Handle(string raw)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(raw); } catch { return; }
            using (doc)
            {
                var m = doc.RootElement;
                switch (m.GetProperty("t").GetString())
                {
                    case "welcome":
                        _authed = true;
                        _backoff = 1;
                        MyId = m.GetProperty("id").GetInt32();
                        _joinedZone = "\0"; // ep gui join lai o nhip ke tiep
                        _zoneTimer = 0;
                        EmitSignal(SignalName.StatusChanged);
                        break;
                    case "joined":
                        Zone = m.GetProperty("zone").GetString();
                        Channel = m.GetProperty("channel").GetInt32();
                        ClearRemotes();
                        foreach (var p in m.GetProperty("players").EnumerateArray()) Spawn(p);
                        if (_outfit != null) Send(new { t = "outfit", id = _outfit });
                        EmitSignal(SignalName.StatusChanged);
                        EmitSignal(SignalName.ChatReceived, "system", "", string.Format(Loc.T("net.joined_fmt"), ZoneMap.DisplayName(Zone), Channel));
                        break;
                    case "spawn": Spawn(m.GetProperty("p")); break;
                    case "despawn":
                        int gone = m.GetProperty("id").GetInt32();
                        if (_remotes.Remove(gone, out var rp) && IsInstanceValid(rp)) rp.QueueFree();
                        break;
                    case "moves":
                        foreach (var row in m.GetProperty("m").EnumerateArray())
                        {
                            int id = row[0].GetInt32();
                            if (!_remotes.TryGetValue(id, out var r) || !IsInstanceValid(r)) continue;
                            r.SetState(new Vector3(row[1].GetSingle(), row[2].GetSingle(), row[3].GetSingle()), row[4].GetSingle(), row[5].GetInt32());
                        }
                        break;
                    case "chat":
                        string ch = m.GetProperty("ch").GetString();
                        string from = m.GetProperty("from").GetString();
                        string text = m.GetProperty("text").GetString();
                        if (ch == "channel" && m.TryGetProperty("id", out var cid) && _remotes.TryGetValue(cid.GetInt32(), out var speaker)) speaker.Say(text);
                        EmitSignal(SignalName.ChatReceived, ch, from, text);
                        break;
                    case "event":
                        HandleEvent(m.GetProperty("e"));
                        break;
                    case "error":
                        EmitSignal(SignalName.ChatReceived, "system", "", m.GetProperty("msg").GetString());
                        break;
                }
            }
        }

        private void HandleEvent(JsonElement e)
        {
            string type = e.GetProperty("type").GetString();
            switch (type)
            {
                case "trade_invite":
                case "trade_update":
                case "trade_done":
                case "trade_cancelled":
                    int tradeId = e.GetProperty("tradeId").GetInt32();
                    string fromName = e.TryGetProperty("fromName", out var fn) ? fn.GetString() : "";
                    EmitSignal(SignalName.TradeEvent, type, tradeId, fromName);
                    if (type == "trade_done") EconomyClient.Instance.RequestSync();
                    break;
                case "friend_request":
                case "friend_accepted":
                case "guild_invite":
                case "guild_changed":
                case "duel_invite":
                case "duel_invited":
                case "duel_start":
                case "duel_hp":
                case "duel_end":
                    EmitSignal(SignalName.SocialEvent, type, e.GetRawText());
                    break;
                case "farm_helped":
                    EmitSignal(SignalName.SocialEvent, type, e.GetRawText());
                    VisitSystem.TakeHelps(GetTree());
                    break;
                case "market_sold":
                    Notify.Show(string.Format(Loc.T("market.sold_fmt"), ItemDatabase.Instance.GetDisplayName(e.GetProperty("itemId").GetString()),
                        e.GetProperty("count").GetInt32(), e.GetProperty("gold").GetInt32()));
                    EconomyClient.Instance.RequestSync();
                    break;
            }
        }

        private void Spawn(JsonElement p)
        {
            int id = p.GetProperty("id").GetInt32();
            if (id == MyId) return;
            var app = p.GetProperty("app");
            if (_remotes.TryGetValue(id, out var old) && IsInstanceValid(old)) old.QueueFree();
            var rp = new RemotePlayer
            {
                PlayerId = id,
                DisplayName = p.GetProperty("name").GetString(),
                Look = new Appearance
                {
                    Gender = app.GetProperty("g").GetString(),
                    ModelId = app.GetProperty("m").GetString(),
                    SkinColor = app.GetProperty("s").GetString(),
                    HairColor = app.GetProperty("h").GetString(),
                    OutfitColor = app.GetProperty("o").GetString(),
                    Outfit = app.TryGetProperty("f", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null,
                },
            };
            var scene = GetTree().CurrentScene;
            if (scene == null) return;
            scene.AddChild(rp);
            rp.SetState(new Vector3(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle(), p.GetProperty("z").GetSingle()),
                p.GetProperty("r").GetSingle(), p.GetProperty("a").GetInt32(), snap: true);
            _remotes[id] = rp;
        }

        private void ClearRemotes()
        {
            foreach (var r in _remotes.Values) if (IsInstanceValid(r)) r.QueueFree();
            _remotes.Clear();
        }

        public int RemoteCount => _remotes.Count;
    }
}
