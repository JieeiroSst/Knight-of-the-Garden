using Godot;

namespace HiepSiVeVuon.Core
{
    // Muc chat luong do hoa (Thap/Trung binh/Cao/Rat cao) + gioi han FPS - luu o user://settings.cfg.
    // Rat cao = dung nhu cau hinh goc cua Main.tscn (SSAO/SSIL Ultra, SSR, suong the tich, MSAA 4x,
    // bong do 2500) - rat nang voi may yeu. Mac dinh "Cao": giu gan nhu nguyen hinh anh nhung bo cac
    // muc Ultra ton GPU nhat.
    //
    // DrawDistanceScale: he so tam nhin cho cay/da/co/quai cua WorldStreamer (VisibilityRange) va
    // nguoi choi khac (RemotePlayer LOD) - may yeu nhin gan hon, ve it vat hon.
    public static class GraphicsSettings
    {
        public enum Quality { Low, Medium, High, Ultra }

        private const string Path = "user://settings.cfg";

        public static Quality Current { get; private set; } = Quality.High;
        public static int MaxFps { get; private set; } = 0; // 0 = khong gioi han (theo VSync)
        public static bool VSync { get; private set; } = true;

        public static float DrawDistanceScale => Current switch
        {
            Quality.Low => 0.55f,
            Quality.Medium => 0.75f,
            Quality.High => 1f,
            _ => 1.25f,
        };

        // Mat do tham co 3D cua moi chunk (WorldStreamer.BuildGrassField).
        public static float GrassDensityScale => Current switch
        {
            Quality.Low => 0.3f,
            Quality.Medium => 0.6f,
            _ => 1f,
        };

        public static event System.Action Changed;

        public static void Load()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(Path) != Error.Ok) return;
            Current = (Quality)Mathf.Clamp((int)cfg.GetValue("graphics", "quality", (int)Quality.High), 0, 3);
            MaxFps = (int)cfg.GetValue("graphics", "max_fps", 0);
            VSync = (bool)cfg.GetValue("graphics", "vsync", true);
        }

        private static void Save()
        {
            var cfg = new ConfigFile();
            cfg.Load(Path); // giu cac muc khac (vd voice) neu da co
            cfg.SetValue("graphics", "quality", (int)Current);
            cfg.SetValue("graphics", "max_fps", MaxFps);
            cfg.SetValue("graphics", "vsync", VSync);
            cfg.Save(Path);
        }

        public static void Set(Quality q, SceneTree tree)
        {
            Current = q;
            Save();
            Apply(tree);
            Changed?.Invoke();
        }

        public static void SetFrameLimit(int maxFps, bool vsync, SceneTree tree)
        {
            MaxFps = maxFps;
            VSync = vsync;
            Save();
            Apply(tree);
        }

        // Ap dung cho scene hien tai (WorldEnvironment/Sun cua Main.tscn neu co) + viewport goc.
        public static void Apply(SceneTree tree)
        {
            Engine.MaxFps = MaxFps;
            DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

            var vp = tree.Root;
            var q = Current;
            vp.Msaa3D = q switch
            {
                Quality.Low => Viewport.Msaa.Disabled,
                Quality.Medium => Viewport.Msaa.Msaa2X,
                _ => Viewport.Msaa.Msaa4X,
            };
            // May yeu: FXAA re thay MSAA + ve 3D o 75% do phan giai roi phong len bang FSR.
            vp.ScreenSpaceAA = q == Quality.Low ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
            vp.Scaling3DMode = q == Quality.Low ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
            vp.Scaling3DScale = q == Quality.Low ? 0.75f : 1f;

            RenderingServer.EnvironmentSetSsaoQuality(q switch
            {
                Quality.Low => RenderingServer.EnvironmentSsaoQuality.VeryLow,
                Quality.Medium => RenderingServer.EnvironmentSsaoQuality.Low,
                Quality.High => RenderingServer.EnvironmentSsaoQuality.Medium,
                _ => RenderingServer.EnvironmentSsaoQuality.Ultra,
            }, q != Quality.Ultra, 0.5f, 2, 50f, 300f);
            RenderingServer.EnvironmentSetSsilQuality(q == Quality.Ultra ? RenderingServer.EnvironmentSsilQuality.Ultra : RenderingServer.EnvironmentSsilQuality.Low,
                q != Quality.Ultra, 0.5f, 4, 50f, 300f);
            RenderingServer.EnvironmentSetSsrRoughnessQuality(q == Quality.Ultra ? RenderingServer.EnvironmentSsrRoughnessQuality.High : RenderingServer.EnvironmentSsrRoughnessQuality.Low);
            RenderingServer.DirectionalSoftShadowFilterSetQuality(q switch
            {
                Quality.Low => RenderingServer.ShadowQuality.Hard,
                Quality.Medium => RenderingServer.ShadowQuality.SoftVeryLow,
                Quality.High => RenderingServer.ShadowQuality.SoftLow,
                _ => RenderingServer.ShadowQuality.SoftHigh,
            });
            RenderingServer.PositionalSoftShadowFilterSetQuality(q <= Quality.Medium ? RenderingServer.ShadowQuality.Hard : RenderingServer.ShadowQuality.SoftLow);
            RenderingServer.DirectionalShadowAtlasSetSize(q switch { Quality.Low => 2048, Quality.Medium => 2048, _ => 4096 }, true);

            var scene = tree.CurrentScene;
            if (scene == null) return;
            if (scene.GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment is Environment env)
            {
                env.SsaoEnabled = q >= Quality.Medium;
                env.SsilEnabled = q >= Quality.High;
                env.SsrEnabled = q >= Quality.High;
                env.VolumetricFogEnabled = q >= Quality.High;
                env.GlowEnabled = q >= Quality.Medium;
            }
            if (scene.GetNodeOrNull<DirectionalLight3D>("Sun") is DirectionalLight3D sun)
            {
                sun.DirectionalShadowMaxDistance = q switch { Quality.Low => 700f, Quality.Medium => 1400f, Quality.High => 2000f, _ => 2500f };
                sun.DirectionalShadowMode = q == Quality.Low ? DirectionalLight3D.ShadowMode.Orthogonal : DirectionalLight3D.ShadowMode.Parallel4Splits;
            }
        }

        // Gan tam nhin (distance culling) cho moi mesh ben duoi root: xa hon endDistance thi GPU bo
        // qua han (khong ve, khong do bong). castShadow=false cho vat nho (co/hoa) - bong cua chung
        // gan nhu khong thay nhung rat ton.
        public static void SetVisibilityRange(Node root, float endDistance, bool castShadow = true)
        {
            if (root is GeometryInstance3D g)
            {
                g.VisibilityRangeEnd = endDistance * DrawDistanceScale;
                g.VisibilityRangeEndMargin = 60f;
                g.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled;
                if (!castShadow) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            foreach (Node child in root.GetChildren()) SetVisibilityRange(child, endDistance, castShadow);
        }
    }
}
