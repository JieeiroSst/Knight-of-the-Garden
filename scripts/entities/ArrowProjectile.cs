using Godot;

namespace HiepSiVeVuon.Entities
{
    // Mui ten bay (chi hinh anh + hen gio trung): bay thang tu `from` toi `to` voi toc do co dinh, cham
    // dich thi goi onHit (sat thuong quai do client tinh; sat thuong nguoi choi do SERVER tinh qua
    // pvp_hit). Cam lai cho trung 2 giay roi bien mat. Dung chung cho mui ten cua minh va cua nguoi khac
    // (tin "shot" tu server).
    public partial class ArrowProjectile : Node3D
    {
        public const float Speed = 1500f;

        private static StandardMaterial3D _wood, _metal, _feather;

        public static ArrowProjectile Spawn(Node parent, Vector3 from, Vector3 to, System.Action onHit)
        {
            var a = new ArrowProjectile();
            parent.AddChild(a);
            a.Launch(from, to, onHit);
            return a;
        }

        public override void _Ready()
        {
            _wood ??= new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.38f, 0.2f), Roughness = 0.8f };
            _metal ??= new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.72f, 0.75f), Metallic = 0.7f, Roughness = 0.35f };
            _feather ??= new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            // Truc mui ten = -Z (LookAt huong -Z ve dich).
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.35f, Height = 22f }, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = _wood, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.9f, Height = 3f }, Position = new Vector3(0, 0, -12f), RotationDegrees = new Vector3(-90, 0, 0), MaterialOverride = _metal, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            for (int i = 0; i < 2; i++)
                AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.6f, 4f) }, Position = new Vector3(0, 0, 9.5f), RotationDegrees = new Vector3(90, i * 90f, 0), MaterialOverride = _feather, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        private void Launch(Vector3 from, Vector3 to, System.Action onHit)
        {
            GlobalPosition = from;
            if (from.DistanceSquaredTo(to) > 1f) LookAt(to, Vector3.Up);
            float t = Mathf.Max(0.05f, from.DistanceTo(to) / Speed);
            var tw = CreateTween();
            tw.TweenProperty(this, "global_position", to, t);
            tw.TweenCallback(Callable.From(() => onHit?.Invoke()));
            tw.TweenInterval(2.0);
            tw.TweenCallback(Callable.From(QueueFree));
        }
    }
}
