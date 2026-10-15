using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
    // Hieu ung thoi tiet quanh nguoi choi: hat mua / tuyet (CPUParticles bam theo nguoi choi), suong
    // mu (fog cua WorldEnvironment), troi toi khi mua/bao, chop set ngau nhien khi bao, nang nong (sang
    // gat). Chi la hinh anh - anh huong cay/vat nuoi nam o FarmPlot/HomeSystem.
    public partial class WeatherFX : Node3D
    {
        private WorldEnvironment _env;
        private CpuParticles3D _rain, _snow;
        private OmniLight3D _flash;
        private float _baseFog, _baseBright;
        private double _nextBolt;

        public void Setup(WorldEnvironment env) => _env = env;

        public override void _Ready()
        {
            _rain = MakeParticles(new Color(0.75f, 0.82f, 0.95f, 0.6f), new Vector3(0.4f, 9f, 0.4f), 900, 0.9f, 900f);
            _snow = MakeParticles(new Color(1f, 1f, 1f, 0.9f), new Vector3(2f, 2f, 2f), 600, 5f, 60f);
            _flash = new OmniLight3D { LightColor = new Color(0.85f, 0.9f, 1f), OmniRange = 3000f, LightEnergy = 0f, Position = Vector3.Up * 400f };
            AddChild(_flash);
            if (_env?.Environment != null)
            {
                _baseFog = _env.Environment.FogDensity;
                _baseBright = _env.Environment.AdjustmentBrightness;
            }
            WeatherSystem.Instance.WeatherChanged += _ => Apply();
            Apply();
        }

        private CpuParticles3D MakeParticles(Color color, Vector3 size, int amount, float lifetime, float fallSpeed)
        {
            var p = new CpuParticles3D
            {
                Amount = amount, Lifetime = lifetime, Emitting = false, LocalCoords = false,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(450f, 10f, 450f),
                Direction = Vector3.Down, Spread = 4f, InitialVelocityMin = fallSpeed * 0.8f, InitialVelocityMax = fallSpeed, Gravity = Vector3.Zero,
                Mesh = new BoxMesh
                {
                    Size = size,
                    Material = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
                },
                Position = Vector3.Up * 380f,
            };
            AddChild(p);
            return p;
        }

        private void Apply()
        {
            var k = WeatherSystem.Instance.Today;
            _rain.Emitting = k is WeatherSystem.Kind.Rain or WeatherSystem.Kind.Storm;
            _rain.Amount = k == WeatherSystem.Kind.Storm ? 1400 : 800;
            _snow.Emitting = k == WeatherSystem.Kind.Snow;
            if (_env?.Environment == null) return;
            var env = _env.Environment;
            env.FogDensity = k switch
            {
                WeatherSystem.Kind.Fog => _baseFog * 12f + 0.002f,
                WeatherSystem.Kind.Rain or WeatherSystem.Kind.Storm or WeatherSystem.Kind.Snow => _baseFog * 4f + 0.0006f,
                _ => _baseFog,
            };
            env.AdjustmentBrightness = _baseBright * k switch
            {
                WeatherSystem.Kind.Storm => 0.72f,
                WeatherSystem.Kind.Rain or WeatherSystem.Kind.Cloudy or WeatherSystem.Kind.Fog => 0.86f,
                WeatherSystem.Kind.Heatwave => 1.08f,
                _ => 1f,
            };
        }

        public override void _Process(double delta)
        {
            if (GetTree().GetFirstNodeInGroup("player") is Node3D player)
                GlobalPosition = new Vector3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z);
            if (WeatherSystem.Instance.Today != WeatherSystem.Kind.Storm) { _flash.LightEnergy = 0f; return; }
            _nextBolt -= delta;
            if (_nextBolt <= 0)
            {
                _nextBolt = GD.RandRange(4.0, 11.0);
                var tw = CreateTween();
                tw.TweenProperty(_flash, "light_energy", 6f, 0.05f);
                tw.TweenProperty(_flash, "light_energy", 0.5f, 0.08f);
                tw.TweenProperty(_flash, "light_energy", 4f, 0.05f);
                tw.TweenProperty(_flash, "light_energy", 0f, 0.4f);
            }
        }
    }
}
