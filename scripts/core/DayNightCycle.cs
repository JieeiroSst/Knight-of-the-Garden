using Godot;

namespace HiepSiVeVuon.Core
{
    // Dieu khien chu ky ngay-dem 24h (GameManager.DayProgress): xoay/doi mau anh sang mat troi,
    // doi mau bau troi, va hien mat troi/mat trang di chuyen tren "vom troi" quanh nguoi choi.
    // Khong tim duoc model 3D mat troi/mat trang giay phep CC0 phu hop (chi co ban CC-BY yeu
    // cau ghi cong) -> tu dung khoi cau phat sang (emission), phu hop vi luon o rat xa/nho.
    public partial class DayNightCycle : Node3D
    {
        [Export] public float OrbitRadius = 1800f;

        private DirectionalLight3D _light;
        private WorldEnvironment _worldEnv;
        private MeshInstance3D _sun;
        private MeshInstance3D _moon;
        private Node3D _player;

        private static readonly Color NightLightColor = new(0.25f, 0.32f, 0.55f);
        private static readonly Color DayLightColor = new(1f, 0.95f, 0.85f);
        private static readonly Color NightSky = new(0.04f, 0.05f, 0.12f);
        private static readonly Color DaySky = new(0.55f, 0.75f, 0.95f);

        public void Setup(DirectionalLight3D light, WorldEnvironment worldEnv)
        {
            _light = light;
            _worldEnv = worldEnv;
        }

        public override void _Ready()
        {
            _sun = MakeOrb(140f, new Color(1f, 0.95f, 0.6f), 3.5f);
            _moon = MakeOrb(90f, new Color(0.85f, 0.9f, 1f), 1.2f);
            AddChild(_sun);
            AddChild(_moon);
        }

        private static MeshInstance3D MakeOrb(float radius, Color color, float emissionEnergy)
        {
            var mesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2f } };
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = emissionEnergy,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            return mesh;
        }

        public override void _Process(double delta)
        {
            if (_player == null || !IsInstanceValid(_player))
                _player = GetTree().GetFirstNodeInGroup("player") as Node3D;

            float progress = GameManager.Instance.DayProgress;
            // 0 = 6h sang (chan troi), Pi/2 = trua (dinh dau), Pi = 6h toi (chan troi)
            float angle = (progress - 0.25f) * Mathf.Tau;
            var sunDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.35f).Normalized();
            var moonDir = -sunDir;

            var basePos = _player != null ? _player.GlobalPosition : Vector3.Zero;
            _sun.GlobalPosition = basePos + sunDir * OrbitRadius;
            _moon.GlobalPosition = basePos + moonDir * OrbitRadius;
            _sun.Visible = sunDir.Y > -0.15f;
            _moon.Visible = moonDir.Y > -0.15f;

            float dayFactor = Mathf.Clamp(sunDir.Y, 0f, 1f);

            if (_light != null)
            {
                _light.Basis = Basis.LookingAt(-sunDir, Vector3.Forward);
                _light.LightColor = NightLightColor.Lerp(DayLightColor, dayFactor);
                _light.LightEnergy = Mathf.Lerp(0.18f, 1.15f, dayFactor);
            }

            if (_worldEnv?.Environment != null)
            {
                _worldEnv.Environment.BackgroundColor = NightSky.Lerp(DaySky, dayFactor);
                _worldEnv.Environment.AmbientLightEnergy = Mathf.Lerp(0.25f, 0.6f, dayFactor);
            }
        }
    }
}
