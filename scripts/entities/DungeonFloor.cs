using Godot;
using System.Collections.Generic;
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

        private void OnEnter()
        {
            Notify.Show(string.Format(Loc.T("dungeon.floor_fmt"), FloorIndex, FloorCount));
            AdventureSystem.Instance.OnDungeonFloorReached(FloorIndex);
            bool cooledDown = !_hadSpawn || (_clearedAt > 0 && Time.GetTicksMsec() - _clearedAt > RespawnSeconds * 1000);
            if (_alive.Count == 0 && cooledDown) SpawnWave();
            if (IsBossFloor && !AdventureSystem.Instance.IsBossAvailable("boss_dungeon")) SpawnChestIfNeeded();
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
            if (IsBossFloor && AdventureSystem.Instance.IsBossAvailable("boss_dungeon") && (_boss == null || !IsInstanceValid(_boss)))
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
