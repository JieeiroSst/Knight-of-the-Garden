using Godot;
using System;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Entities
{
    // Hieu ung hinh anh cho quai (tach khoi Enemy.cs cho gon): ma trong suot/phat sang, bong do
    // duoi quai bay, chop trang khi trung don, thanh mau tren dau, no bui hat khi chet, vong canh
    // bao don dap dat cua trum. Tat ca dung primitive/vat lieu tao bang code, khong can asset moi.
    public static class EnemyVisuals
    {
        public static void Decorate(Node3D owner, Node3D model, EnemyDef def)
        {
            Color? glow = string.IsNullOrEmpty(def.GlowColor) ? null : new Color("#" + def.GlowColor.TrimStart('#'));
            if (def.Ghostly || glow.HasValue)
                ForEachMaterial(model, mat =>
                {
                    if (def.Ghostly)
                    {
                        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                        mat.AlbedoColor = new Color(mat.AlbedoColor, 0.55f);
                        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                    }
                    if (glow.HasValue)
                    {
                        mat.EmissionEnabled = true;
                        mat.Emission = glow.Value;
                        mat.EmissionEnergyMultiplier = def.Ghostly ? 0.6f : 0.25f;
                    }
                    // Vien sang mem (rim) - quai noi bat hon tren nen co/rung toi.
                    mat.RimEnabled = true;
                    mat.Rim = 0.6f;
                    mat.RimTint = 0.4f;
                });

            if (def.Ghostly && glow.HasValue)
                owner.AddChild(new OmniLight3D { LightColor = glow.Value, LightEnergy = 0.9f, OmniRange = 55f, Position = Vector3.Up * (def.HoverHeight + 12f), DistanceFadeEnabled = true, DistanceFadeBegin = 700f, DistanceFadeLength = 200f });

            // Bong do tron duoi quai bay - giu cam giac "dang lo lung tren mat dat".
            if (def.HoverHeight > 0f)
            {
                owner.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 11f, BottomRadius = 11f, Height = 0.2f },
                    Position = Vector3.Up * 0.6f,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0, 0, 0, 0.35f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    },
                });
            }
        }

        // Nhan ban vat lieu RIENG cho con quai nay (vat lieu goc dung chung giua moi instance).
        private static void ForEachMaterial(Node root, Action<StandardMaterial3D> edit)
        {
            if (root is MeshInstance3D mi && mi.Mesh != null)
            {
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is StandardMaterial3D baseMat)
                    {
                        var dup = (StandardMaterial3D)baseMat.Duplicate();
                        edit(dup);
                        mi.SetSurfaceOverrideMaterial(s, dup);
                    }
                }
            }
            foreach (Node child in root.GetChildren()) ForEachMaterial(child, edit);
        }

        // Chop trang 0.15s tren toan than (MaterialOverlay phu len tren vat lieu goc).
        public static void HitFlash(Node3D model)
        {
            if (model == null || !GodotObject.IsInstanceValid(model)) return;
            var flash = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, 0.75f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            SetOverlay(model, flash);
            var tw = model.CreateTween();
            tw.TweenProperty(flash, "albedo_color:a", 0f, 0.15f);
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(model)) SetOverlay(model, null); }));
        }

        private static void SetOverlay(Node root, Material mat)
        {
            if (root is GeometryInstance3D gi) gi.MaterialOverlay = mat;
            foreach (Node child in root.GetChildren()) SetOverlay(child, mat);
        }

        public static void DeathBurst(Node3D at, EnemyDef def)
        {
            if (!GodotObject.IsInstanceValid(at) || !at.IsInsideTree()) return;
            var color = def != null && !string.IsNullOrEmpty(def.GlowColor) ? new Color("#" + def.GlowColor.TrimStart('#')) : new Color(0.75f, 0.6f, 1f);
            bool boss = def != null && def.IsBoss;
            var p = new CpuParticles3D
            {
                OneShot = true, Emitting = true, Amount = boss ? 120 : 36, Lifetime = boss ? 1.4f : 0.8f, Explosiveness = 0.95f,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = boss ? 30f : 10f,
                Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 30f, InitialVelocityMax = boss ? 140f : 80f,
                Gravity = new Vector3(0, -60f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f, Color = color,
                Mesh = new SphereMesh
                {
                    Radius = 2f, Height = 4f, RadialSegments = 6, Rings = 3,
                    Material = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                        EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 2f,
                    },
                },
            };
            var scene = at.GetTree().CurrentScene;
            scene.AddChild(p);
            p.GlobalPosition = at.GlobalPosition + Vector3.Up * (12f + (def?.HoverHeight ?? 0f));
            scene.GetTree().CreateTimer(2.0).Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.QueueFree(); };
        }

        // Vong do tren mat dat lon dan trong `delay` giay, roi goi onHit va chop sang bien mat.
        public static void SlamTelegraph(Node parent, Vector3 center, float radius, float delay, Action onHit)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.15f, 0.1f, 0.35f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            var ring = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.5f, RadialSegments = 32 },
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Scale = new Vector3(0.2f, 1f, 0.2f),
            };
            parent.AddChild(ring);
            ring.GlobalPosition = new Vector3(center.X, center.Y + 1f, center.Z);
            var tw = ring.CreateTween();
            tw.TweenProperty(ring, "scale", Vector3.One, delay).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(mat, "albedo_color:a", 0.6f, delay);
            tw.TweenCallback(Callable.From(() => onHit?.Invoke()));
            tw.TweenProperty(mat, "albedo_color", new Color(1f, 0.9f, 0.6f, 0f), 0.25f);
            tw.TweenCallback(Callable.From(ring.QueueFree));
        }

        // Thanh mau billboard ve bang Image (cap nhat chi khi trung don - re).
        public class HpBar
        {
            private const int W = 48, H = 6;
            private readonly Sprite3D _sprite;
            private readonly Image _img;
            private readonly ImageTexture _tex;
            private readonly bool _boss;

            private HpBar(Sprite3D sprite, bool boss)
            {
                _sprite = sprite;
                _boss = boss;
                _img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
                _tex = ImageTexture.CreateFromImage(_img);
                _sprite.Texture = _tex;
                SetRatio(1f);
            }

            public static HpBar Create(Node3D owner, float height, bool boss)
            {
                var sprite = new Sprite3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    PixelSize = boss ? 1.4f : 0.7f,
                    Position = Vector3.Up * height,
                    NoDepthTest = true,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                    Shaded = false,
                };
                owner.AddChild(sprite);
                return new HpBar(sprite, boss);
            }

            public void SetRatio(float ratio)
            {
                if (!GodotObject.IsInstanceValid(_sprite)) return;
                ratio = Mathf.Clamp(ratio, 0f, 1f);
                var fill = _boss ? new Color(0.85f, 0.15f, 0.2f)
                    : new Color(0.9f, 0.2f, 0.15f).Lerp(new Color(0.3f, 0.85f, 0.3f), ratio);
                _img.Fill(new Color(0.05f, 0.05f, 0.05f, 0.85f));
                int filled = Mathf.RoundToInt((W - 2) * ratio);
                if (filled > 0) _img.FillRect(new Rect2I(1, 1, filled, H - 2), fill);
                _tex.Update(_img);
            }

            public void QueueFree()
            {
                if (GodotObject.IsInstanceValid(_sprite)) _sprite.QueueFree();
            }
        }
    }
}
