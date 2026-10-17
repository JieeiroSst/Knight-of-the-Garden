using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ruong tiep te: roi cham tu do cao xuong (du + khoi do bao hieu tu xa), cham dat thi [E] mo duoc.
    public partial class SupplyCrate : FarmFacility
    {
        public WorldEventsClient.Drop Data;
        private Node3D _chute;
        private Label3D _label;
        private CpuParticles3D _smoke;
        private OmniLight3D _beacon;
        private const float DropHeight = 1600f;

        public override void _Ready()
        {
            AddCollisionBox(new Vector3(36, 30, 36));
            var wood = new Color(0.45f, 0.3f, 0.16f);
            AddBox(new Vector3(36, 28, 36), Vector3.Up * 14f, wood);
            AddBox(new Vector3(38, 3, 38), Vector3.Up * 28f, new Color(0.75f, 0.15f, 0.12f));
            AddBox(new Vector3(37, 4, 6), Vector3.Up * 14f, new Color(0.3f, 0.3f, 0.32f));
            _chute = new Node3D();
            AddChild(_chute);
            _chute.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 60f, Height = 50f, IsHemisphere = true }, Position = Vector3.Up * 110f,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), CullMode = BaseMaterial3D.CullModeEnum.Disabled },
            });
            _smoke = new CpuParticles3D
            {
                Amount = 40, Lifetime = 6f, Direction = Vector3.Up, Spread = 8f, InitialVelocityMin = 60f, InitialVelocityMax = 90f,
                Gravity = Vector3.Zero, ScaleAmountMin = 6f, ScaleAmountMax = 12f, Color = new Color(0.9f, 0.2f, 0.15f, 0.55f),
                Mesh = new SphereMesh { Radius = 2f, Height = 4f, Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha } },
                Position = Vector3.Up * 30f, Emitting = false,
            };
            AddChild(_smoke);
            _beacon = new OmniLight3D { LightColor = new Color(1f, 0.3f, 0.2f), LightEnergy = 2f, OmniRange = 160f, Position = Vector3.Up * 40f };
            AddChild(_beacon);
            _label = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.3f, FontSize = 40, OutlineSize = 10, NoDepthTest = true, Position = Vector3.Up * 80f, Modulate = new Color(1f, 0.8f, 0.4f) };
            AddChild(_label);
            Position = new Vector3(Data.X, DropHeight, Data.Z);
        }

        public override void _Process(double delta)
        {
            long now = WorldEventsClient.ServerNow;
            long left = Data.LandsAt - now;
            float fall = Mathf.Clamp(left / 120000f, 0f, 1f);
            Position = new Vector3(Data.X, DropHeight * fall, Data.Z);
            _chute.Visible = left > 0;
            _smoke.Emitting = left <= 0 && !Data.Looted;
            _beacon.Visible = !Data.Looted;
            _label.Text = Data.Looted ? Loc.T("world.drop_empty")
                : left > 0 ? string.Format(Loc.T("world.drop_falling_fmt"), left / 1000)
                : Loc.T("world.drop_ready");
        }

        public override void Interact()
        {
            if (Data.Looted) { Notify.Show(Loc.T("world.drop_empty")); return; }
            if (Data.LandsAt > WorldEventsClient.ServerNow) { Notify.Show(Loc.T("world.drop_not_landed")); return; }
            WorldEventsClient.Instance?.OpenDrop(Data);
        }
    }

    // Xe doan thuong nhan luu dong: [E] mo bang hang (CaravanUI).
    public partial class CaravanSite : FarmFacility
    {
        public WorldEventsClient.Caravan Data;
        private Label3D _label;

        public override void _Ready()
        {
            AddCollisionBox(new Vector3(80, 40, 40));
            var wood = new Color(0.5f, 0.33f, 0.18f);
            AddBox(new Vector3(80, 18, 40), Vector3.Up * 22f, wood);
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 22f, BottomRadius = 22f, Height = 78f, RadialSegments = 16 }, Position = Vector3.Up * 36f,
                RotationDegrees = new Vector3(0, 0, 90),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.78f, 0.6f), CullMode = BaseMaterial3D.CullModeEnum.Disabled },
            });
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 11f, BottomRadius = 11f, Height = 3f }, Position = new Vector3(i * 28f, 11f, j * 21f),
                        RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.2f, 0.12f) },
                    });
            AddBox(new Vector3(4, 70, 4), new Vector3(-42, 35, 0), wood.Darkened(0.2f));
            AddBox(new Vector3(22, 14, 0.6f), new Vector3(-30, 62, 0), new Color(0.85f, 0.65f, 0.15f)); // co hieu
            AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.5f), LightEnergy = 1.4f, OmniRange = 160f, Position = Vector3.Up * 60f,
                DistanceFadeEnabled = true, DistanceFadeBegin = 900f, DistanceFadeLength = 200f });
            _label = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.28f, FontSize = 38, OutlineSize = 10, NoDepthTest = true, Position = Vector3.Up * 95f, Modulate = new Color(1f, 0.9f, 0.5f) };
            AddChild(_label);
            Position = new Vector3(Data.X, 0, Data.Z);
        }

        public override void _Process(double delta)
        {
            long left = Mathf.Max(0, (int)((Data.Until - WorldEventsClient.ServerNow) / 1000));
            _label.Text = string.Format(Loc.T("world.caravan_label_fmt"), left / 60, left % 60);
        }

        public override void Interact() => CaravanUI.Instance?.Open();
    }
}
