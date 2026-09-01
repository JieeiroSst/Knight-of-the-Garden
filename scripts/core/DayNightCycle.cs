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

        // Bau troi that (ProceduralSkyMaterial, xem Main.tscn Environment_1.background_mode=Sky)
        // thay the mau phang truoc day - can 2 mau (dinh troi/chan troi) rieng cho ca ngay LAN
        // dem, khong chi 1 mau nhu ban goc (bau troi that co gradient, "1 mau" se khong tu nhien).
        private static readonly Color NightSkyTop = new(0.02f, 0.03f, 0.08f);
        private static readonly Color NightSkyHorizon = new(0.06f, 0.07f, 0.14f);
        private static readonly Color NightGroundHorizon = new(0.05f, 0.05f, 0.06f);
        private static readonly Color DaySkyTop = new(0.32f, 0.55f, 0.92f);
        private static readonly Color DaySkyHorizon = new(0.75f, 0.82f, 0.88f);
        private static readonly Color DayGroundHorizon = new(0.75f, 0.82f, 0.88f);

        private ProceduralSkyMaterial _skyMaterial;

        public void Setup(DirectionalLight3D light, WorldEnvironment worldEnv)
        {
            _light = light;
            _worldEnv = worldEnv;
            _skyMaterial = _worldEnv?.Environment?.Sky?.SkyMaterial as ProceduralSkyMaterial;
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
                _worldEnv.Environment.AmbientLightEnergy = Mathf.Lerp(0.25f, 0.6f, dayFactor);

            if (_skyMaterial != null)
            {
                _skyMaterial.SkyTopColor = NightSkyTop.Lerp(DaySkyTop, dayFactor);
                _skyMaterial.SkyHorizonColor = NightSkyHorizon.Lerp(DaySkyHorizon, dayFactor);
                _skyMaterial.GroundHorizonColor = NightGroundHorizon.Lerp(DayGroundHorizon, dayFactor);
            }
        }
    }
}
