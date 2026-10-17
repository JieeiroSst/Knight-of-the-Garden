using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Phia client cua su kien the gioi (server: backend src/realtime/worldEvents.js):
    //  - Ruong tiep te: roi tu troi xuong toa do co dinh (bao truoc 2 phut), ai toi truoc [E] mo duoc.
    //  - Doan thuong nhan: dung 15 phut tai 1 diem, ban do hiem (so luong co han, ai mua truoc duoc truoc).
    //  - Dem Trang Mau: 1/5 so dem - troi nhuom do, quai manh + dong hon.
    public partial class WorldEventsClient : Node
    {
        public static WorldEventsClient Instance { get; private set; }
        public static bool BloodMoon { get; private set; }

        public class Drop { public string Id; public float X, Z; public long LandsAt, ExpiresAt; public bool Looted; }
        public class Offer { public string Item; public int Price, Left; }
        public class Caravan { public string Id; public float X, Z; public long Until; public List<Offer> Stock = new(); }
        // Trum The Gioi: mau CHUNG tren server, ai cung danh duoc, thuong chia theo dong gop.
        public class WorldBoss { public string Id, Kind; public float X, Z; public int Hp, MaxHp; public long WakeAt, Until; }
        public WorldBoss Boss { get; private set; }
        private Enemy _bossNode;
        private const float BossSpawnDist = 2200f;

        public readonly Dictionary<string, Drop> Drops = new();
        public Caravan CurrentCaravan { get; private set; }
        private readonly Dictionary<string, SupplyCrate> _crates = new();
        private CaravanSite _caravanNode;
        private ColorRect _bloodTint;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            var layer = new CanvasLayer { Layer = 1 };
            AddChild(layer);
            _bloodTint = new ColorRect { Color = new Color(0.7f, 0.05f, 0.05f, 0.12f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _bloodTint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(_bloodTint);
        }

        private static NetworkClient Net => NetworkClient.Instance;
        public static long ServerNow => (long)GameManager.Instance.ServerNowMs;

        public void OnMessage(string type, JsonElement m)
        {
            switch (type)
            {
                case "world": ApplyState(m.GetProperty("state")); break;
                case "drop_looted":
                    if (Drops.TryGetValue(m.GetProperty("id").GetString(), out var d))
                    {
                        d.Looted = true;
                        Notify.Show(string.Format(Loc.T("world.drop_looted_fmt"), m.GetProperty("byName").GetString(), MapCoords.Format(d.X, d.Z)));
                    }
                    break;
                case "boss_hp":
                    if (Boss != null && Boss.Id == m.GetProperty("id").GetString())
                    {
                        Boss.Hp = m.GetProperty("hp").GetInt32();
                        if (_bossNode != null && IsInstanceValid(_bossNode)) _bossNode.SetNetHp(Boss.Hp, Boss.MaxHp);
                    }
                    break;
                case "boss_end":
                    if (Boss != null && Boss.Id == m.GetProperty("id").GetString())
                    {
                        bool killed = m.GetProperty("killed").GetBoolean();
                        var top = m.GetProperty("top").EnumerateArray().Select(x => $"{x.GetProperty("name").GetString()} ({x.GetProperty("share").GetInt32()}%)").ToList();
                        Notify.Show(killed ? string.Format(Loc.T("wboss.killed_fmt"), BossName(Boss.Kind), string.Join(", ", top)) : string.Format(Loc.T("wboss.left_fmt"), BossName(Boss.Kind)));
                        if (_bossNode != null && IsInstanceValid(_bossNode)) { if (killed) _bossNode.SetNetHp(0, Boss.MaxHp); else _bossNode.NetVanish(); }
                        _bossNode = null;
                        Boss = null;
                    }
                    break;
                case "caravan_stock":
                    var offer = CurrentCaravan?.Stock.FirstOrDefault(o => o.Item == m.GetProperty("item").GetString());
                    if (offer != null) offer.Left = m.GetProperty("left").GetInt32();
                    CaravanUI.Instance?.Refresh();
                    break;
            }
            SyncNodes();
        }

        public void OnEvent(string type, JsonElement e)
        {
            if (type == "drop_opened")
            {
                var parts = e.GetProperty("items").EnumerateObject().Select(p => $"{ItemDatabase.Instance.GetDisplayName(p.Name)} x{p.Value.GetInt32()}").ToList();
                Notify.Show(string.Format(Loc.T("world.drop_opened_fmt"), string.Join(", ", parts), e.GetProperty("gold").GetInt32()));
            }
            else if (type == "boss_reward")
                Notify.Show(string.Format(Loc.T("wboss.reward_fmt"), BossName(e.GetProperty("kind").GetString()), e.GetProperty("rank").GetInt32(),
                    e.GetProperty("share").GetInt32(), e.GetProperty("gold").GetInt32(), ItemsText(e.GetProperty("items"))));
            else if (type == "caravan_bought")
                Notify.Show(string.Format(Loc.T("world.caravan_bought_fmt"), ItemDatabase.Instance.GetDisplayName(e.GetProperty("item").GetString()), e.GetProperty("n").GetInt32(), e.GetProperty("cost").GetInt32()));
        }

        public static string BossName(string kind)
        {
            var def = ItemDatabase.Instance.GetEnemy(kind);
            if (def == null) return kind;
            return Loc.Current == Loc.Lang.EN && !string.IsNullOrEmpty(def.BossTitleEn) ? def.BossTitleEn : (def.BossTitle ?? def.Name);
        }

        public static string ItemsText(JsonElement items) =>
            string.Join(", ", items.EnumerateObject().Select(p => $"{ItemDatabase.Instance.GetDisplayName(p.Name)} x{p.Value.GetInt32()}"));

        private void ApplyState(JsonElement s)
        {
            var seen = new HashSet<string>();
            foreach (var d in s.GetProperty("drops").EnumerateArray())
            {
                string id = d.GetProperty("id").GetString();
                seen.Add(id);
                bool isNew = !Drops.ContainsKey(id);
                Drops[id] = new Drop
                {
                    Id = id, X = d.GetProperty("x").GetSingle(), Z = d.GetProperty("z").GetSingle(),
                    LandsAt = d.GetProperty("landsAt").GetInt64(), ExpiresAt = d.GetProperty("expiresAt").GetInt64(), Looted = d.GetProperty("looted").GetBoolean(),
                };
                if (isNew && !Drops[id].Looted && Drops[id].LandsAt > ServerNow)
                    Notify.Show(string.Format(Loc.T("world.drop_incoming_fmt"), MapCoords.Format(Drops[id].X, Drops[id].Z)));
            }
            foreach (var id in Drops.Keys.Where(k => !seen.Contains(k)).ToList()) Drops.Remove(id);

            if (s.TryGetProperty("caravan", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                bool isNew = CurrentCaravan?.Id != c.GetProperty("id").GetString();
                CurrentCaravan = new Caravan
                {
                    Id = c.GetProperty("id").GetString(), X = c.GetProperty("x").GetSingle(), Z = c.GetProperty("z").GetSingle(), Until = c.GetProperty("until").GetInt64(),
                    Stock = c.GetProperty("stock").EnumerateArray().Select(o => new Offer { Item = o.GetProperty("item").GetString(), Price = o.GetProperty("price").GetInt32(), Left = o.GetProperty("left").GetInt32() }).ToList(),
                };
                if (isNew) Notify.Show(string.Format(Loc.T("world.caravan_arrived_fmt"), MapCoords.Format(CurrentCaravan.X, CurrentCaravan.Z)));
            }
            else CurrentCaravan = null;

            if (s.TryGetProperty("boss", out var b) && b.ValueKind == JsonValueKind.Object)
            {
                string id = b.GetProperty("id").GetString();
                bool isNew = Boss?.Id != id;
                if (isNew && _bossNode != null && IsInstanceValid(_bossNode)) _bossNode.NetVanish();
                if (isNew) _bossNode = null;
                Boss = new WorldBoss
                {
                    Id = id, Kind = b.GetProperty("kind").GetString(), X = b.GetProperty("x").GetSingle(), Z = b.GetProperty("z").GetSingle(),
                    Hp = b.GetProperty("hp").GetInt32(), MaxHp = b.GetProperty("maxHp").GetInt32(),
                    WakeAt = b.GetProperty("wakeAt").GetInt64(), Until = b.GetProperty("until").GetInt64(),
                };
                if (isNew) Notify.Show(string.Format(Loc.T("wboss.spawn_fmt"), BossName(Boss.Kind), MapCoords.Format(Boss.X, Boss.Z)));
            }
            else
            {
                if (_bossNode != null && IsInstanceValid(_bossNode)) _bossNode.NetVanish();
                _bossNode = null;
                Boss = null;
            }

            bool bm = s.GetProperty("bloodMoon").GetBoolean();
            if (bm != BloodMoon) Notify.Show(Loc.T(bm ? "world.blood_moon_start" : "world.blood_moon_end"));
            BloodMoon = bm;
            _bloodTint.Visible = bm;
        }

        // Dung/xoa thuc the theo trang thai.
        private void SyncNodes()
        {
            var scene = GetTree().CurrentScene;
            if (scene == null || scene.GetNodeOrNull("WorldEnvironment") == null) return;
            foreach (var (id, d) in Drops)
                if (!_crates.ContainsKey(id)) { var crate = new SupplyCrate { Data = d }; scene.AddChild(crate); _crates[id] = crate; }
            foreach (var id in _crates.Keys.Where(k => !Drops.ContainsKey(k)).ToList())
            {
                if (IsInstanceValid(_crates[id])) _crates[id].QueueFree();
                _crates.Remove(id);
            }
            if (CurrentCaravan != null && (_caravanNode == null || !IsInstanceValid(_caravanNode) || _caravanNode.Data.Id != CurrentCaravan.Id))
            {
                if (_caravanNode != null && IsInstanceValid(_caravanNode)) _caravanNode.QueueFree();
                _caravanNode = new CaravanSite { Data = CurrentCaravan };
                scene.AddChild(_caravanNode);
            }
            else if (CurrentCaravan == null && _caravanNode != null && IsInstanceValid(_caravanNode)) { _caravanNode.QueueFree(); _caravanNode = null; }
            else if (_caravanNode != null && IsInstanceValid(_caravanNode)) _caravanNode.Data = CurrentCaravan;
        }

        public void OpenDrop(Drop d) => Net?.SendMsg(new { t = "drop_open", id = d.Id });
        public void Buy(string item, int n) => Net?.SendMsg(new { t = "caravan_buy", item, n });

        public override void _Process(double delta)
        {
            // Vua vao the gioi (scene Main moi nap) -> dung lai thuc the.
            if (_crates.Count < Drops.Count || (CurrentCaravan != null && _caravanNode == null)) SyncNodes();
            _bossCheckIn -= delta;
            if (_bossCheckIn <= 0) { _bossCheckIn = 0.5; SyncBoss(); }
        }

        private double _bossCheckIn;
        private static PackedScene _enemyScene;

        // Chi dung trum khi nguoi choi o gan (va da thuc day); ra xa -> go bo (server van giu mau).
        private void SyncBoss()
        {
            var scene = GetTree().CurrentScene;
            var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (scene == null || player == null || scene.GetNodeOrNull("WorldEnvironment") == null) return;
            bool alive = _bossNode != null && IsInstanceValid(_bossNode) && !_bossNode.IsDead;
            if (Boss == null || Boss.Hp <= 0 || ServerNow < Boss.WakeAt || ServerNow > Boss.Until)
            {
                if (alive && (Boss == null || ServerNow > Boss.Until)) _bossNode.NetVanish();
                if (Boss == null || ServerNow > Boss.Until) _bossNode = null;
                return;
            }
            var home = new Vector3(Boss.X, 0, Boss.Z);
            float d = new Vector2(player.GlobalPosition.X - home.X, player.GlobalPosition.Z - home.Z).Length();
            if (alive && d > BossSpawnDist * 1.3f) { _bossNode.QueueFree(); _bossNode = null; return; }
            if (alive || d > BossSpawnDist) return;
            _enemyScene ??= GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            var e = _enemyScene.Instantiate<Enemy>();
            e.EnemyId = Boss.Kind;
            e.StatMultiplier = 1.6f * Enemy.SeasonalMultiplier();
            e.NetHome = home;
            string id = Boss.Id;
            e.NetHit = dmg => Net?.SendMsg(new { t = "boss_hit", id, dmg });
            e.Position = home + Vector3.Up * 20f;
            scene.AddChild(e);
            e.SetNetHp(Boss.Hp, Boss.MaxHp);
            _bossNode = e;
        }
    }
}
