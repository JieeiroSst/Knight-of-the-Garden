using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // 1 tang Ham Nguc Co (xem Main.BuildDungeon): khi nguoi choi buoc vao tang, sinh quai theo do
    // sau (manh dan theo tang). Tang cuoi co Rong Co Dai (hoi sinh moi tuan) + ruong bau tuan.
    // Danh sach quai/tang rong hon -> sinh lai sau RespawnSeconds ke tu luc don sach.
    public partial class DungeonFloor : Node3D
    {
        public int FloorIndex = 1;
        public int FloorCount = 5;
        public float RoomHalf = 200f;
        public const double RespawnSeconds = 180;

        private static PackedScene _enemyScene;
        private readonly List<Enemy> _alive = new();
        private bool _playerInside = false;
        private double _checkIn = 0.5;
        private ulong _clearedAt = 0;
        private Enemy _boss;
        private TreasureChest _chest;

        private static readonly (string id, int minFloor)[] Pool =
        {
            ("dungeon_mushnub", 1), ("dungeon_tribal", 2), ("dungeon_armabee", 2), ("dungeon_alien", 3), ("dungeon_ninja", 4),
        };

        private bool IsBossFloor => FloorIndex == FloorCount;
        private string WeeklyChestId => $"dungeon_chest_w{GameManager.Instance.Day / 7}";

        public override void _Process(double delta)
        {
            _checkIn -= delta;
            if (_checkIn > 0) return;
            _checkIn = 0.5;

            _alive.RemoveAll(e => e == null || !IsInstanceValid(e) || e.IsDead);
            if (_alive.Count == 0 && _clearedAt == 0 && _hadSpawn) _clearedAt = Time.GetTicksMsec();

            var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (player == null) return;
            var d = player.GlobalPosition - GlobalPosition;
            bool inside = Mathf.Abs(d.X) < RoomHalf && Mathf.Abs(d.Z) < RoomHalf && Mathf.Abs(d.Y) < 150f;
            if (inside && !_playerInside) OnEnter();
            _playerInside = inside;

            if (IsBossFloor && _boss != null && (!IsInstanceValid(_boss) || _boss.IsDead))
            {
                _boss = null;
                SpawnChestIfNeeded();
            }
        }

        private bool _hadSpawn = false;

        // Online: Rong Co Dai la TRUM MANG cua to doi (mau chung tren server, tang theo so thanh vien,
        // ca doi nhan thuong 1 lan/ngay - xem backend worldEvents dungeonJoin). Offline: trum tai cho nhu cu.
        private static bool Online => NetworkClient.Instance is NetworkClient n && n.Connected && n.Zone != null;
        private static DungeonFloor _netFloor;
        private string _netBossId;

        private void OnEnter()
        {
            Notify.Show(string.Format(Loc.T("dungeon.floor_fmt"), FloorIndex, FloorCount));
            AdventureSystem.Instance.OnDungeonFloorReached(FloorIndex);
            bool cooledDown = !_hadSpawn || (_clearedAt > 0 && Time.GetTicksMsec() - _clearedAt > RespawnSeconds * 1000);
            if (_alive.Count == 0 && cooledDown) SpawnWave();
            if (IsBossFloor && Online)
            {
                _netFloor = this;
                // Doi vi tri moi toi server (server kiem tra dang o trong ham).
                GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(this) && _playerInside) NetworkClient.Instance?.SendMsg(new { t = "dboss_join" }); };
                return;
            }
            if (IsBossFloor && !AdventureSystem.Instance.IsBossAvailable("boss_dungeon")) SpawnChestIfNeeded();
        }

        // Tin tu server: "dboss" (tra loi dboss_join) + su kien dboss_hp / dboss_end.
        public static void OnNet(string type, JsonElement m)
        {
            var f = _netFloor;
            if (f == null || !IsInstanceValid(f)) return;
            switch (type)
            {
                case "dboss":
                    if (m.GetProperty("state").GetString() == "done") { Notify.Show(Loc.T("dungeon.party_done_today")); f.SpawnChestIfNeeded(); return; }
                    f.SpawnNetBoss(m.GetProperty("id").GetString(), m.GetProperty("hp").GetInt32(), m.GetProperty("maxHp").GetInt32(), m.GetProperty("size").GetInt32());
                    break;
                case "dboss_hp":
                    if (f._boss != null && IsInstanceValid(f._boss) && f._netBossId == m.GetProperty("id").GetString())
                        f._boss.SetNetHp(m.GetProperty("hp").GetInt32(), f._boss.MaxHp);
                    break;
                case "dboss_end":
                    if (f._boss != null && IsInstanceValid(f._boss)) f._boss.SetNetHp(0, f._boss.MaxHp);
                    Notify.Show(m.GetProperty("rewarded").GetBoolean()
                        ? string.Format(Loc.T("dungeon.party_reward_fmt"), m.GetProperty("party").GetInt32(), m.GetProperty("gold").GetInt32(), WorldEventsClient.ItemsText(m.GetProperty("items")))
                        : Loc.T("dungeon.party_done_today"));
                    f.SpawnChestIfNeeded();
                    break;
            }
        }

        private void SpawnNetBoss(string id, int hp, int maxHp, int size)
        {
            if (hp <= 0) { SpawnChestIfNeeded(); return; }
            if (_boss != null && IsInstanceValid(_boss) && !_boss.IsDead && _netBossId == id) { _boss.SetNetHp(hp, maxHp); return; }
            _enemyScene ??= GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            _netBossId = id;
            _boss = _enemyScene.Instantiate<Enemy>();
            _boss.EnemyId = "boss_dungeon";
            _boss.StatMultiplier = Enemy.SeasonalMultiplier();
            _boss.NetHit = dmg => NetworkClient.Instance?.SendMsg(new { t = "dboss_hit", id, dmg });
            GetParent().AddChild(_boss);
            _boss.GlobalPosition = GlobalPosition + new Vector3(0, 8f, -RoomHalf * 0.5f);
            _boss.NetHome = _boss.GlobalPosition;
            _boss.NetLeash = RoomHalf * 0.8f;
            _boss.SetNetHp(hp, maxHp);
            _alive.Add(_boss);
            Notify.Show(string.Format(Loc.T("dungeon.party_boss_fmt"), size));
        }

        private void SpawnWave()
        {
            _hadSpawn = true;
            _clearedAt = 0;
            _enemyScene ??= GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            var pool = new List<string>();
            foreach (var (id, min) in Pool) if (FloorIndex >= min) pool.Add(id);
            int count = 3 + FloorIndex;
            float mult = (1f + 0.25f * (FloorIndex - 1)) * Enemy.SeasonalMultiplier();
            for (int i = 0; i < count; i++)
            {
                var e = _enemyScene.Instantiate<Enemy>();
                e.EnemyId = pool[rng.RandiRange(0, pool.Count - 1)];
                e.StatMultiplier = mult;
                GetParent().AddChild(e);
                e.GlobalPosition = GlobalPosition + new Vector3(rng.RandfRange(-RoomHalf * 0.7f, RoomHalf * 0.7f), 8f, rng.RandfRange(-RoomHalf * 0.7f, RoomHalf * 0.2f));
                _alive.Add(e);
            }
            if (IsBossFloor && !Online && AdventureSystem.Instance.IsBossAvailable("boss_dungeon") && (_boss == null || !IsInstanceValid(_boss)))
            {
                _boss = _enemyScene.Instantiate<Enemy>();
                _boss.EnemyId = "boss_dungeon";
                _boss.StatMultiplier = Enemy.SeasonalMultiplier();
                GetParent().AddChild(_boss);
                _boss.GlobalPosition = GlobalPosition + new Vector3(0, 8f, -RoomHalf * 0.5f);
                _alive.Add(_boss);
                Notify.Show(Loc.T("dungeon.boss_awake"));
            }
        }

        private void SpawnChestIfNeeded()
        {
            if (_chest != null && IsInstanceValid(_chest)) return;
            if (AdventureSystem.Instance.IsChestOpened(WeeklyChestId)) return;
            _chest = new TreasureChest { ChestId = WeeklyChestId, Tier = 3 };
            GetParent().AddChild(_chest);
            _chest.GlobalPosition = GlobalPosition + new Vector3(0, 0, -RoomHalf * 0.6f);
        }
    }
}
