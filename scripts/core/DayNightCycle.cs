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

            // Huong theo LA BAN THAT cua the gioi (khop voi MapUI.DrawCompassRose): the gioi +X =
            // Dong, -X = Tay, +Z = Nam, -Z = Bac (goc dia ly az=0 la Bac, az=90 la Dong, tang theo
            // chieu kim dong ho). Mat troi moc DUNG huong Dong (az=90 luc angle=0, tuc 6h sang),
            // quet qua Nam luc dinh ngo (az=180 luc trua - giong huong mat troi ban trua o Bac Ban
            // Cau that), roi lan DUNG huong Tay (az=270 luc angle=Pi, tuc 6h toi) - truoc day dung
            // truc tiep Cos/Sin(angle) lam X/Z, KHONG khop voi quy uoc la ban tren (huong moc/lan
            // bi lech, khong dung Dong/Tay that su).
            float azimuthDeg = 90f + (angle / Mathf.Tau) * 360f;
            float azimuthRad = Mathf.DegToRad(azimuthDeg);
            float elevation = Mathf.Sin(angle); // 0 luc moc/lan, dinh diem luc trua, am ban dem (duoi chan troi)
            var sunDir = new Vector3(Mathf.Sin(azimuthRad), elevation, -Mathf.Cos(azimuthRad)).Normalized();
            var moonDir = -sunDir;

            var basePos = _player != null ? _player.GlobalPosition : Vector3.Zero;
            _sun.GlobalPosition = basePos + sunDir * OrbitRadius;
            _moon.GlobalPosition = basePos + moonDir * OrbitRadius;
            _sun.Visible = sunDir.Y > -0.15f;
            _moon.Visible = moonDir.Y > -0.15f;

            // Ngoai doi that, troi bat dau sang dan (hoang hon/binh minh) TU KHI mat troi con o
            // duoi chan troi khoang 15-20 do (twilight thien van/hang hai), khong phai cho DEN LUC
            // mat troi thuc su moc moi bat dau sang - truoc day dung thang Clamp(sunDir.Y,0,1) nen
            // moi luc mat troi con duoi chan troi (vd 5h sang, con ~1h nua moi den 6h moc) deu bi
            // tinh dayFactor=0 GIONG HET nua dem, khien troi van den kit ngay ca luc gan sang. Mo
            // rong vung noi suy bat dau tu do cao -0.4 (mat troi ~-23.6 do, gan muc twilight thien
            // van that) thay vi 0, de troi sang dan TRUOC khi mat troi thuc su nhoi len.
            const float TwilightStart = -0.4f;
            float twilightT = Mathf.Clamp((sunDir.Y - TwilightStart) / -TwilightStart, 0f, 1f);
            float dayFactor = twilightT * twilightT * (3f - 2f * twilightT); // smoothstep - chuyen muot, khong bi "bat/tat" dot ngot

            if (_light != null)
            {
                _light.Basis = Basis.LookingAt(-sunDir, Vector3.Forward);
                _light.LightColor = NightLightColor.Lerp(DayLightColor, dayFactor);
                // Tang do sang toi da (1.15 -> 2.0 ban ngay, 0.18 -> 0.35 ban dem) - voi tonemap
                // ACES (xem Main.tscn), can nang luong dau vao CAO hon de anh sang thuc su "sang"
                // sau khi nen tonemap, khong chi "du sang" theo con so tuyet doi.
                _light.LightEnergy = Mathf.Lerp(0.35f, 2.0f, dayFactor);
            }

            if (_worldEnv?.Environment != null)
                // Anh sang moi truong (chieu vao vung khuat nang truc tiep) cung tang tuong ung -
                // 0.6 cu qua toi cho cac mang khuat/duoi tan cay, de lai cam giac "toi" du mat troi
                // da sang.
                _worldEnv.Environment.AmbientLightEnergy = Mathf.Lerp(0.4f, 0.9f, dayFactor);

            if (_skyMaterial != null)
            {
                _skyMaterial.SkyTopColor = NightSkyTop.Lerp(DaySkyTop, dayFactor);
                _skyMaterial.SkyHorizonColor = NightSkyHorizon.Lerp(DaySkyHorizon, dayFactor);
                _skyMaterial.GroundHorizonColor = NightGroundHorizon.Lerp(DayGroundHorizon, dayFactor);
            }
        }
    }
}
