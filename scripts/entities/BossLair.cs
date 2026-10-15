using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Hang o cua trum khu vuc: vong da + cot dau lau. Trum xuat hien khi chua bi ha trong 7 ngay
    // gan nhat (xem AdventureSystem.IsBossAvailable) - kiem tra dinh ky nen tu hoi sinh dung han
    // ma khong can tai lai game.
    public partial class BossLair : Node3D
    {
        public string BossId = "";
        public string LabelKey = "";

        private static PackedScene _enemyScene;
        private Enemy _boss;
        private double _checkIn = 1.0;

        public override void _Ready()
        {
            AddToGroup("boss_lairs");
            var stone = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.3f, 0.3f) };
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.Tau * i / 10f;
                AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(14, 26 + (i % 3) * 8, 10) },
                    Position = new Vector3(Mathf.Cos(a) * 150f, 13, Mathf.Sin(a) * 150f),
                    RotationDegrees = new Vector3(0, -Mathf.RadToDeg(a), (i % 2 == 0 ? 6 : -5)),
                    MaterialOverride = stone,
                });
            }
            // Cot dau lau phat sang do - dau hieu nhan ra hang trum tu xa.
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 4, BottomRadius = 6, Height = 60 }, Position = new Vector3(0, 30, -175), MaterialOverride = stone });
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 9, Height = 16 }, Position = new Vector3(0, 66, -175),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), EmissionEnabled = true, Emission = new Color(1f, 0.2f, 0.1f), EmissionEnergyMultiplier = 0.6f },
            });
            AddChild(new OmniLight3D { LightColor = new Color(1f, 0.3f, 0.2f), LightEnergy = 2f, OmniRange = 120f, Position = new Vector3(0, 70, -175) });
            AdventureSystem.Instance.RegisterPoi(AdventureSystem.PoiKind.Boss, BossId, GlobalPosition, LabelKey);
        }

        public override void _Process(double delta)
        {
            _checkIn -= delta;
            if (_checkIn > 0) return;
            _checkIn = 2.0;
            if (_boss != null && IsInstanceValid(_boss) && !_boss.IsDead) return;
            _boss = null;
            if (!AdventureSystem.Instance.IsBossAvailable(BossId)) return;
            _enemyScene ??= GD.Load<PackedScene>("res://scenes/Enemy.tscn");
            var e = _enemyScene.Instantiate<Enemy>();
            e.EnemyId = BossId;
            e.StatMultiplier = Enemy.SeasonalMultiplier();
            e.Position = Position; // cung cha voi hang
            GetParent().AddChild(e);
            _boss = e;
        }
    }
}
