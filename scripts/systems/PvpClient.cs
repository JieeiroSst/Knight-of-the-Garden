using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Phia client cua PvP the gioi mo / can cu / to doi (server: backend src/realtime/pvp.js). SERVER
    // quyet dinh moi thu (mau PvP, ai chet, do roi, mau cong trinh) - lop nay chi:
    //  - bao mau hien tai + danh sach tui chinh len server (de server biet chot do roi khi chet),
    //  - gui y dinh (danh nguoi/cong trinh, nhat tui, xay, sua, ruong, to doi, ho tro),
    //  - dung/xoa thuc the tui do (LootBag) va cong trinh (BaseStructure) theo tin server,
    //  - xu ly chet PvP: day het op kinh te -> gui tui -> nhan danh sach mat -> bo khoi tui.
    public partial class PvpClient : Node
    {
        public static PvpClient Instance { get; private set; }

        public static readonly HashSet<string> BaseKits = new() { "bo_tuong_go", "bo_leu_trai", "bo_ruong_can_cu", "bo_lua_trai", "bo_giuong",
            "bo_ban_che_tac", "bo_ban_moc", "bo_lo_ren", "bo_xuong" };
        public static bool IsBaseKit(string itemId) => itemId != null && BaseKits.Contains(itemId);

        public class StructureInfo
        {
            public long Id; public int OwnerId; public string OwnerName = ""; public string Kind = "";
            public float X, Z, Rot; public int Hp, MaxHp;
        }

        public class PartyMember { public int Id; public string Name; public Vector3 Pos; public string Zone; public int Hp, Max; public ulong SeenMs; }

        public string PartyId { get; private set; }
        public readonly List<int> PartyMembers = new();
        public readonly Dictionary<int, PartyMember> PartyPositions = new();
        public readonly Dictionary<long, LootBag> Bags = new();
        public readonly Dictionary<long, BaseStructure> Structures = new();
        // Moi cong trinh CUA MINH (ke ca o xa, khong tai) - ve ban do + chon diem hoi sinh (leu).
        public readonly Dictionary<long, StructureInfo> MyStructures = new();

        private double _hpTimer, _hpResend, _bagTimer, _regenTimer;
        private int _lastHpSent = -1, _lastMaxSent = -1;
        private bool _bagDirty = true;
        private ulong _lastPvpHurtMs;
        // Vua trung don PvP (server da tu tru do ben giap) - client khong tru them.
        public bool RecentPvpHurt => Time.GetTicksMsec() - _lastPvpHurtMs < 150;
        private string _pendingPartyInvite;
        private ConfirmDialog _confirm;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Inventory.Instance.InventoryChanged += () => _bagDirty = true;
            _confirm = new ConfirmDialog();
            AddChild(_confirm);
        }

        private static NetworkClient Net => NetworkClient.Instance;
        private bool Online => Net != null && Net.Connected && Net.Zone != null;

        // ==== Dong doi ====
        public bool IsAlly(int playerId) => playerId == Net?.MyId || PartyMembers.Contains(playerId);
        public bool IsFriendly(RemotePlayer r) =>
            r != null && (PartyMembers.Contains(r.PlayerId) || (r.GuildId != 0 && r.GuildId == Net?.MyGuildId));

        // ==== Gui y dinh ====
        public void Hit(RemotePlayer target, bool finisher)
        {
            NoteAttack();
            if (!Online || target == null || IsFriendly(target)) return;
            if (CurrentMarket != null || MarketAt(target.GlobalPosition) != null) return; // vung an toan
            Net.SendMsg(new { t = "pvp_hit", target = target.PlayerId, fin = finisher });
        }

        public void HitStructure(BaseStructure s)
        {
            NoteAttack();
            if (!Online || s == null || IsAlly(s.Info.OwnerId)) return;
            Net.SendMsg(new { t = "struct_hit", id = s.Info.Id });
        }

        public void Loot(LootBag bag) { if (Online) Net.SendMsg(new { t = "bag_loot", id = bag.BagId }); }
        public void Repair(long id) { if (Online) Net.SendMsg(new { t = "struct_repair", id }); }
        public void Remove(long id) { if (Online) Net.SendMsg(new { t = "struct_remove", id }); }
        public void ChestOpen(long id) { if (Online) Net.SendMsg(new { t = "chest_open", id }); }
        public void ChestMove(long id, string item, int n) { if (Online) Net.SendMsg(new { t = "chest_move", id, item, n }); }
        public void PartyInvite(string name) { if (Online) Net.SendMsg(new { t = "party_invite", to = name }); }
        public void PartyLeave() { if (Online) Net.SendMsg(new { t = "party_leave" }); }
        public void HealAlly(RemotePlayer r) { if (Online && r != null) Net.SendMsg(new { t = "heal_ally", target = r.PlayerId }); }

        // Dat bo xay dung o vi tri pos (Player goi khi cam bo xay dung + [Space]).
        public bool Place(string itemId, Vector3 pos, float rotDeg)
        {
            if (!Online) { Notify.Show(Loc.T("pvp.need_online")); return false; }
            if (Inventory.Instance.CountOf(itemId) <= 0) return false;
            if (MarketAt(pos) != null) { Notify.Show(Loc.T("market.no_build")); return false; }
            Net.SendMsg(new { t = "struct_place", item = itemId, x = pos.X, z = pos.Z, rot = rotDeg });
            return true;
        }

        // ==== Hoi sinh (SERVER quyet dinh: giuong gan nhat / nong trai / doi 1 gio neu vua mat giuong) ====
        // onDecided(vi tri hoi sinh, readyAtMs gio server - 0 = ngay).
        private System.Action<Vector3, long> _respawnCallback;
        private bool _pvpDeathPending;
        private long _forcedWaitUntil;
        private ulong _respawnAskedMs;

        public void ResolveRespawn(Vector3 deathPos, System.Action<Vector3, long> onDecided)
        {
            if (_forcedWaitUntil > (long)GameManager.Instance.ServerNowMs)
            {
                long until = _forcedWaitUntil;
                _forcedWaitUntil = 0;
                onDecided(Vector3.Zero, until);
                return;
            }
            if (!Online) { onDecided(Vector3.Zero, 0); return; }
            _respawnCallback = onDecided;
            _respawnAskedMs = Time.GetTicksMsec();
            // Chet PvP: quyet dinh hoi sinh di kem "pvp_dead". Chet vi quai/roi: tu hoi server.
            if (!_pvpDeathPending) Net.SendMsg(new { t = "died" });
        }

        private void ApplyRespawn(JsonElement r)
        {
            string mode = r.GetProperty("mode").GetString();
            long readyAt = r.TryGetProperty("readyAt", out var ra) ? ra.GetInt64() : 0;
            var pos = mode == "bed" ? new Vector3(r.GetProperty("x").GetSingle(), 0, r.GetProperty("z").GetSingle()) + new Vector3(0, 0, 45) : Vector3.Zero;
            if (mode == "bed") Notify.Show(Loc.T("respawn.at_bed"));
            var cb = _respawnCallback;
            _respawnCallback = null;
            _pvpDeathPending = false;
            if (cb != null) { cb(pos, mode == "wait" ? readyAt : 0); return; }
            // Dang nhap lai khi van con han cho (vd thoat game luc dang cho) -> guc ngay, hien dem nguoc.
            if (mode == "wait")
            {
                _forcedWaitUntil = readyAt;
                if (!(Player?.IsDowned ?? false)) GameManager.Instance.ForceDeath();
            }
        }

        // ==== An nap ====
        private ulong _lastAttackMs;
        public bool HiddenLocal { get; private set; }
        public void NoteAttack() { _lastAttackMs = Time.GetTicksMsec(); HiddenLocal = false; }

        // Dang dung trong bui co cao (HideSpots - giong het server) hoac trong leu, va 3s khong tan cong.
        public bool WantsHide(Vector3 p)
        {
            string zone = Net?.Zone;
            bool can = zone != null && zone != "town" && !zone.StartsWith("farm_") && Time.GetTicksMsec() - _lastAttackMs > 3000
                && !(Player?.IsDowned ?? false);
            bool inside = can && (HideSpots.InBush(p) || Structures.Values.Any(s => IsInstanceValid(s) && s.Info.Kind == "tent"
                && new Vector2(s.Info.X - p.X, s.Info.Z - p.Z).Length() < 34f));
            HiddenLocal = inside;
            return inside;
        }

        // ==== Cung ten ====
        // Ban 1 mui ten toi diem `to` (muc tieu nguoi choi neu co) - server kiem tam/cung/mui ten that.
        public void Shoot(Vector3 to, RemotePlayer target)
        {
            NoteAttack();
            if (!Online) return;
            Net.SendMsg(new { t = "shot", tx = to.X, ty = to.Y, tz = to.Z });
            if (target != null && !IsFriendly(target) && CurrentMarket == null && MarketAt(target.GlobalPosition) == null)
                Net.SendMsg(new { t = "pvp_hit", target = target.PlayerId, ranged = true });
        }

        private Dictionary<string, int> Manifest()
        {
            var m = new Dictionary<string, int>();
            foreach (var s in Inventory.Instance.Slots) m[s.ItemId] = (m.TryGetValue(s.ItemId, out var v) ? v : 0) + s.Count;
            return m;
        }

        // ==== Khu cho (vung an toan) ====
        public Data.MarketDef CurrentMarket { get; private set; }
        private double _marketTimer;

        public static Data.MarketDef MarketAt(Vector3 p)
        {
            foreach (var m in ItemDatabase.Instance.Markets)
                if (new Vector2(p.X - m.X, p.Z - m.Z).Length() <= m.SafeRadius) return m;
            return null;
        }

        public static Data.MarketDef NearestMarket(Vector3 p) =>
            ItemDatabase.Instance.Markets.OrderBy(m => new Vector2(p.X - m.X, p.Z - m.Z).LengthSquared()).FirstOrDefault();

        private void UpdateMarket(double delta)
        {
            _marketTimer -= delta;
            if (_marketTimer > 0) return;
            _marketTimer = 0.5;
            var p = Player;
            var m = p != null ? MarketAt(p.GlobalPosition) : null;
            if (m == CurrentMarket) return;
            if (m != null) Notify.Show(string.Format(Loc.T("market.enter_fmt"), Loc.Current == Loc.Lang.VI ? m.Name : m.NameEn));
            else if (CurrentMarket != null) Notify.Show(Loc.T("market.leave"));
            CurrentMarket = m;
        }

        public override void _Process(double delta)
        {
            UpdateMarket(delta);
            // Server khong tra loi quyet dinh hoi sinh (mat mang...) -> ve nong trai sau 5s.
            if (_respawnCallback != null && Time.GetTicksMsec() - _respawnAskedMs > 5000)
            {
                var cb = _respawnCallback;
                _respawnCallback = null;
                _pvpDeathPending = false;
                cb(Vector3.Zero, 0);
            }
            if (!Online) return;
            var gm = GameManager.Instance;
            // Bao mau khi doi (toi da ~3 lan/giay) VA nhac lai moi giay: server chi cho mau tang toi da 25%
            // moi lan bao (chong hoi mau ao), nen sau khi hoi sinh/uong thuoc can vai lan bao moi khop.
            _hpTimer -= delta;
            _hpResend -= delta;
            bool changed = gm.Hp != _lastHpSent || gm.MaxHp != _lastMaxSent;
            if (_hpTimer <= 0 && (changed || _hpResend <= 0) && !(Player?.IsDowned ?? false))
            {
                _hpTimer = 0.3;
                _hpResend = 1.0;
                _lastHpSent = gm.Hp; _lastMaxSent = gm.MaxHp;
                Net.SendMsg(new { t = "hp", hp = gm.Hp, max = gm.MaxHp });
            }
            _bagTimer -= delta;
            if (_bagTimer <= 0 && _bagDirty)
            {
                _bagTimer = 1.2;
                _bagDirty = false;
                Net.SendMsg(new { t = "bag", items = Manifest() });
            }
            CampfireRegen(delta);
        }

        private Player Player => GetTree().GetFirstNodeInGroup("player") as Player;

        // Lua trai cua minh/dong doi: dung gan (khong giao tranh 8s) -> hoi 4 mau/giay.
        private void CampfireRegen(double delta)
        {
            _regenTimer -= delta;
            if (_regenTimer > 0) return;
            _regenTimer = 1.0;
            var p = Player;
            var gm = GameManager.Instance;
            if (p == null || gm.Hp <= 0 || gm.Hp >= gm.MaxHp || Time.GetTicksMsec() - _lastPvpHurtMs < 8000) return;
            foreach (var s in Structures.Values)
                if (IsInstanceValid(s) && s.Info.Kind == "campfire" && IsAlly(s.Info.OwnerId)
                    && s.GlobalPosition.DistanceTo(p.GlobalPosition) < 170f)
                { gm.Heal(4); return; }
        }

        // ==== Tin tu server (NetworkClient chuyen sang) ====
        public void OnMessage(string type, JsonElement m)
        {
            switch (type)
            {
                case "bags":
                    ClearBags();
                    foreach (var b in m.GetProperty("list").EnumerateArray()) SpawnBag(b);
                    break;
                case "bag": SpawnBag(m.GetProperty("bag")); break;
                case "bag_gone": RemoveBag(m.GetProperty("id").GetInt64()); break;
                case "structs":
                    var keep = new HashSet<long>();
                    foreach (var s in m.GetProperty("list").EnumerateArray()) keep.Add(UpsertStructure(ParseStructure(s)));
                    foreach (var id in Structures.Keys.Where(k => !keep.Contains(k)).ToList()) RemoveStructure(id, false);
                    break;
                case "struct": UpsertStructure(ParseStructure(m.GetProperty("s"))); break;
                case "struct_gone": RemoveStructure(m.GetProperty("id").GetInt64(), true); break;
                case "structs_mine":
                    MyStructures.Clear();
                    foreach (var s in m.GetProperty("list").EnumerateArray()) { var info = ParseStructure(s); MyStructures[info.Id] = info; }
                    break;
                case "hpbar":
                    Net.FindRemote(m.GetProperty("id").GetInt32())?.ShowHp(m.GetProperty("hp").GetInt32(), m.GetProperty("max").GetInt32());
                    break;
                case "shot":
                    if (Net.FindRemote(m.GetProperty("id").GetInt32()) is RemotePlayer shooter && Scene != null)
                        ArrowProjectile.Spawn(Scene, shooter.GlobalPosition + Vector3.Up * 30f,
                            new Vector3(m.GetProperty("tx").GetSingle(), m.GetProperty("ty").GetSingle(), m.GetProperty("tz").GetSingle()), null);
                    break;
                case "chest":
                    BaseUI.Instance?.ShowChest(m.GetProperty("id").GetInt64(),
                        m.GetProperty("items").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()));
                    break;
            }
        }

        public void OnConnected()
        {
            _lastHpSent = -1;
            _bagDirty = true;
            Net.SendMsg(new { t = "structs_mine" });
        }

        public void OnZoneLeft()
        {
            ClearBags();
            foreach (var id in Structures.Keys.ToList()) RemoveStructure(id, false);
        }

        public void OnEvent(string type, JsonElement e)
        {
            var gm = GameManager.Instance;
            switch (type)
            {
                case "pvp_hurt":
                    _lastPvpHurtMs = Time.GetTicksMsec();
                    gm.ApplyServerDamage(e.GetProperty("dmg").GetInt32(), e.GetProperty("hp").GetInt32());
                    _lastHpSent = gm.Hp;
                    if (e.TryGetProperty("charge", out var ch) && ch.GetBoolean())
                        Notify.Show(string.Format(Loc.T("horse.charged_by_fmt"), e.GetProperty("byName").GetString()));
                    if (e.TryGetProperty("knock", out var kn) && kn.GetBoolean()) Player?.KnockOffHorse();
                    break;
                case "respawn":
                    ApplyRespawn(e.GetProperty("respawn"));
                    break;
                case "pvp_dead_pending":
                    _pvpDeathPending = true;
                    // Server da chot: minh guc. Day het op kinh te dang cho roi moi gui tui -> server tru
                    // dung so do that (khong mat/khong nhan doi do vua nhat chua kip dong bo).
                    Notify.Show(string.Format(Loc.T("pvp.downed_by_fmt"), e.GetProperty("byName").GetString()));
                    if (!(Player?.IsDowned ?? false)) gm.ForceDeath();
                    EconomyClient.Instance.FlushNow(_ => Net.SendMsg(new { t = "death_ack", items = Manifest() }));
                    break;
                case "pvp_dead":
                    ApplyLoss(e.GetProperty("lost"));
                    if (e.TryGetProperty("bountyLost", out var bl) && bl.GetInt64() > 0)
                        Notify.Show(string.Format(Loc.T("bounty.lost_fmt"), bl.GetInt64()));
                    if (e.TryGetProperty("respawn", out var rsp)) ApplyRespawn(rsp);
                    break;
                case "pvp_kill":
                    Notify.Show(string.Format(Loc.T("pvp.kill_fmt"), e.GetProperty("victimName").GetString()));
                    AchievementSystem.Instance?.Add("pvp_kills");
                    if (e.TryGetProperty("bountyClaimed", out var bc) && bc.GetInt64() > 0)
                        Notify.Show(string.Format(Loc.T("bounty.claimed_fmt"), bc.GetInt64(), e.GetProperty("victimName").GetString()));
                    else if (e.TryGetProperty("bountyAdded", out var ba) && ba.GetInt64() > 0)
                        Notify.Show(string.Format(Loc.T("bounty.wanted_fmt"), e.GetProperty("bountyNow").GetInt64()));
                    BountyBoard.Instance?.Refresh();
                    break;
                case "bag_looted":
                    Notify.Show(string.Format(Loc.T("pvp.looted_fmt"), DescribeItems(e.GetProperty("items"))));
                    EconomyClient.Instance.RequestSync();
                    break;
                case "healed":
                    gm.SetHpFromServer(e.GetProperty("hp").GetInt32());
                    _lastHpSent = gm.Hp;
                    Notify.Show(string.Format(Loc.T("pvp.healed_fmt"), e.GetProperty("byName").GetString(), e.GetProperty("amount").GetInt32()));
                    break;
                case "heal_done":
                    Notify.Show(Loc.T("pvp.heal_done"));
                    break;
                case "econ_sync":
                    EconomyClient.Instance.RequestSync();
                    Net.SendMsg(new { t = "structs_mine" });
                    break;
                case "party_invite":
                    _pendingPartyInvite = e.GetProperty("partyId").GetString();
                    string from = e.GetProperty("fromName").GetString();
                    _confirm.Ask(string.Format(Loc.T("party.invite_fmt"), from), () => Net.SendMsg(new { t = "party_accept", partyId = _pendingPartyInvite }));
                    break;
                case "party_invited":
                    Notify.Show(string.Format(Loc.T("party.invited_fmt"), e.GetProperty("toName").GetString()));
                    break;
                case "party_changed":
                    PartyMembers.Clear();
                    PartyId = null;
                    if (e.TryGetProperty("party", out var party) && party.ValueKind == JsonValueKind.Object)
                    {
                        PartyId = party.GetProperty("id").GetString();
                        foreach (var id in party.GetProperty("members").EnumerateArray()) PartyMembers.Add(id.GetInt32());
                    }
                    foreach (var id in PartyPositions.Keys.Where(k => !PartyMembers.Contains(k)).ToList()) PartyPositions.Remove(id);
                    Notify.Show(PartyId == null ? Loc.T("party.left") : string.Format(Loc.T("party.size_fmt"), PartyMembers.Count));
                    break;
                case "party_pos":
                    int pid = e.GetProperty("id").GetInt32();
                    PartyPositions[pid] = new PartyMember
                    {
                        Id = pid, Name = e.GetProperty("name").GetString(),
                        Pos = new Vector3(e.GetProperty("x").GetSingle(), 0, e.GetProperty("z").GetSingle()),
                        Zone = e.GetProperty("zone").GetString(), Hp = e.GetProperty("hp").GetInt32(), Max = e.GetProperty("max").GetInt32(),
                        SeenMs = Time.GetTicksMsec(),
                    };
                    break;
                case "base_attacked":
                case "base_destroyed":
                    Notify.Show(string.Format(Loc.T(type == "base_attacked" ? "base.attacked_fmt" : "base.destroyed_fmt"),
                        Loc.T("base.kind." + e.GetProperty("kind").GetString()), e.GetProperty("byName").GetString(),
                        MapCoords.Format(e.GetProperty("x").GetSingle(), e.GetProperty("z").GetSingle())));
                    if (type == "base_destroyed") Net.SendMsg(new { t = "structs_mine" });
                    break;
            }
        }

        // Server da tru do trong tui chinh -> bo dung so do do khoi tui o client (khong tao op moi).
        private void ApplyLoss(JsonElement lost)
        {
            var parts = new List<string>();
            using (EconomyClient.Suppress())
            {
                foreach (var p in lost.EnumerateObject())
                {
                    int n = Mathf.Min(p.Value.GetInt32(), Inventory.Instance.CountOf(p.Name));
                    if (n > 0) Inventory.Instance.RemoveItem(p.Name, n);
                    if (Inventory.Instance.CountOf(p.Name) == 0 && Inventory.Instance.IsEquipped(p.Name)
                        && ItemDatabase.Instance.GetItem(p.Name) is Data.ItemDef def)
                        Inventory.Instance.Unequip(def.Type);
                    parts.Add($"{ItemDatabase.Instance.GetDisplayName(p.Name)} x{p.Value.GetInt32()}");
                }
            }
            EconomyClient.Instance.RequestSync();
            Notify.Show(parts.Count == 0 ? Loc.T("pvp.lost_nothing") : string.Format(Loc.T("pvp.lost_fmt"), string.Join(", ", parts)));
            _bagDirty = true;
        }

        private static string DescribeItems(JsonElement items)
        {
            var parts = items.EnumerateObject().Select(p => $"{ItemDatabase.Instance.GetDisplayName(p.Name)} x{p.Value.GetInt32()}").ToList();
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }

        // ==== Thuc the ====
        private Node Scene => GetTree().CurrentScene;

        private void SpawnBag(JsonElement b)
        {
            long id = b.GetProperty("id").GetInt64();
            RemoveBag(id);
            if (Scene == null) return;
            var bag = new LootBag
            {
                BagId = id,
                OwnerName = b.GetProperty("ownerName").GetString(),
                KillerId = b.TryGetProperty("killerId", out var k) && k.ValueKind == JsonValueKind.Number ? k.GetInt32() : 0,
                PriorityUntilMs = b.GetProperty("priorityUntil").GetInt64(),
                ExpiresAtMs = b.GetProperty("expiresAt").GetInt64(),
                Position = new Vector3(b.GetProperty("x").GetSingle(), 0, b.GetProperty("z").GetSingle()),
            };
            Scene.AddChild(bag);
            Bags[id] = bag;
        }

        private void RemoveBag(long id)
        {
            if (Bags.Remove(id, out var b) && IsInstanceValid(b)) b.QueueFree();
        }

        private void ClearBags()
        {
            foreach (var b in Bags.Values) if (IsInstanceValid(b)) b.QueueFree();
            Bags.Clear();
        }

        private static StructureInfo ParseStructure(JsonElement s) => new()
        {
            Id = s.GetProperty("id").GetInt64(),
            OwnerId = s.GetProperty("ownerId").GetInt32(),
            OwnerName = s.TryGetProperty("ownerName", out var on) && on.ValueKind == JsonValueKind.String ? on.GetString() : "",
            Kind = s.GetProperty("kind").GetString(),
            X = s.GetProperty("x").GetSingle(), Z = s.GetProperty("z").GetSingle(), Rot = s.GetProperty("rot").GetSingle(),
            Hp = s.GetProperty("hp").GetInt32(), MaxHp = s.GetProperty("maxHp").GetInt32(),
        };

        private long UpsertStructure(StructureInfo info)
        {
            if (info.OwnerId == Net?.MyId) MyStructures[info.Id] = info;
            if (Structures.TryGetValue(info.Id, out var node) && IsInstanceValid(node))
            {
                if (string.IsNullOrEmpty(info.OwnerName)) info.OwnerName = node.Info.OwnerName;
                node.UpdateInfo(info);
                return info.Id;
            }
            if (Scene == null) return info.Id;
            var s = new BaseStructure { Info = info, Position = new Vector3(info.X, 0, info.Z), RotationDegrees = new Vector3(0, info.Rot, 0) };
            Scene.AddChild(s);
            Structures[info.Id] = s;
            return info.Id;
        }

        private void RemoveStructure(long id, bool destroyed)
        {
            if (destroyed) MyStructures.Remove(id);
            if (Structures.Remove(id, out var s) && IsInstanceValid(s))
            {
                if (destroyed) s.Shatter();
                else s.QueueFree();
            }
        }
    }

    // Hop thoai Co/Khong don gian (moi to doi...).
    public partial class ConfirmDialog : CanvasLayer
    {
        private Panel _panel;
        private Label _text;
        private System.Action _onYes;

        public override void _Ready()
        {
            Layer = 70;
            _panel = UiStyle.MakePanel(new Vector2(300, 190), new Vector2(360, 120));
            AddChild(_panel);
            _text = new Label { Position = new Vector2(16, 14), CustomMinimumSize = new Vector2(328, 50), AutowrapMode = TextServer.AutowrapMode.Word };
            _panel.AddChild(_text);
            var yes = new Button { Text = Loc.T("common.yes"), Position = new Vector2(60, 76), CustomMinimumSize = new Vector2(100, 30) };
            yes.Pressed += () => { Visible = false; _onYes?.Invoke(); };
            _panel.AddChild(yes);
            var no = new Button { Text = Loc.T("common.no"), Position = new Vector2(200, 76), CustomMinimumSize = new Vector2(100, 30) };
            no.Pressed += () => Visible = false;
            _panel.AddChild(no);
            Visible = false;
        }

        public void Ask(string text, System.Action onYes)
        {
            _text.Text = text;
            _onYes = onYes;
            Visible = true;
        }
    }

    // Dinh dang toa do dung chung (met, 20 don vi the gioi = 1 m): "X 123 · Z -456".
    public static class MapCoords
    {
        public const float UnitsPerMeter = 20f;
        public static int M(float units) => Mathf.RoundToInt(units / UnitsPerMeter);
        public static string Format(float x, float z) => $"[{M(x)}, {M(z)}]";
        public static Vector3 FromMeters(float mx, float mz) => new(mx * UnitsPerMeter, 0, mz * UnitsPerMeter);

        // Tim toa do dang "[123, -456]" trong 1 dong chat.
        private static readonly System.Text.RegularExpressions.Regex Pattern = new(@"\[\s*(-?\d{1,7})\s*,\s*(-?\d{1,7})\s*\]");
        public static bool TryParse(string text, out Vector3 world)
        {
            world = Vector3.Zero;
            var m = Pattern.Match(text ?? "");
            if (!m.Success) return false;
            world = FromMeters(float.Parse(m.Groups[1].Value), float.Parse(m.Groups[2].Value));
            return true;
        }
    }
}
