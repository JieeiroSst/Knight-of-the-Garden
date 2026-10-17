using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Cong trinh can cu nguoi choi xay o the gioi mo (server luu - xem backend pvpService): tuong go
    // (chan duong that su - co va cham), leu (diem hoi sinh cua chu), ruong (cat do), lua trai (hoi mau
    // dong doi). Nguoi khac danh (chuot trai) de pha; chu/dong doi bam [E] de sua/mo ruong/go bo.
    public partial class BaseStructure : FarmFacility
    {
        public PvpClient.StructureInfo Info;

        private Label3D _label;
        private OmniLight3D _fireLight;
        private ulong _showLabelUntil;
        private int _lastHp;

        public override void _Ready()
        {
            AddToGroup("base_structures");
            switch (Info.Kind)
            {
                case "wall": BuildWall(); break;
                case "tent": BuildTent(); break;
                case "chest": BuildChest(); break;
                case "bed": BuildBed(); break;
                case "workbench": case "carpentry": case "forge": case "workshop": BuildStation(Info.Kind); break;
                default: BuildCampfire(); break;
            }
            _label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.2f, FontSize = 32, OutlineSize = 8,
                NoDepthTest = true, Position = Vector3.Up * (Info.Kind == "wall" ? 78f : 62f),
            };
            AddChild(_label);
            _lastHp = Info.Hp;
            RefreshLabel();
            GraphicsSettings.SetVisibilityRange(this, 2600f);
        }

        private void BuildWall()
        {
            // Doan tuong go dai 90, cao 60 - tran ngang theo huong nguoi dat (truc X cuc bo).
            AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(90, 60, 10) }, Position = Vector3.Up * 30f });
            var model = ModelUtil.Instance("res://assets3d/kenney/town/Models/GLB format/wall-wood.glb");
            if (model != null)
            {
                ModelUtil.FitToHeight(model, 60f);
                var aabb = ModelUtil.LocalAabb(model);
                float sx = aabb.Size.X > 0.01f ? 90f / aabb.Size.X : 1f;
                model.Scale = new Vector3(model.Scale.X * sx, model.Scale.Y, model.Scale.Z);
                AddChild(model);
            }
            else AddBox(new Vector3(90, 60, 10), Vector3.Up * 30f, new Color(0.5f, 0.34f, 0.2f));
            // Coc go 2 dau cho chac chan (nhin ro la tuong phong thu chu khong phai vach nha).
            for (int i = -1; i <= 1; i += 2)
                AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 4f, BottomRadius = 5f, Height = 70f },
                    Position = new Vector3(i * 46f, 35f, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.28f, 0.16f), Roughness = 1f },
                });
        }

        private void BuildTent()
        {
            AddCollisionBox(new Vector3(60, 40, 70));
            var canvas = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.62f, 0.42f), Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(64, 42, 72) }, Position = Vector3.Up * 21f, MaterialOverride = canvas });
            // Cua leu toi mau + co nho tren dinh (de nhan ra tu xa).
            AddBox(new Vector3(18, 26, 1), new Vector3(0, 13, 36.2f), new Color(0.25f, 0.18f, 0.12f));
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.8f, Height = 26f }, Position = new Vector3(0, 54f, 0) });
            AddBox(new Vector3(14, 8, 0.6f), new Vector3(7.5f, 62f, 0), new Color(0.8f, 0.2f, 0.18f));
        }

        private void BuildChest()
        {
            AddCollisionBox(new Vector3(34, 26, 24));
            var wood = new Color(0.36f, 0.24f, 0.14f);
            var iron = new Color(0.32f, 0.33f, 0.36f);
            AddBox(new Vector3(34, 18, 22), new Vector3(0, 9, 0), wood);
            AddBox(new Vector3(34, 7, 22), new Vector3(0, 21.5f, 0), wood.Lightened(0.08f));
            AddBox(new Vector3(36, 2, 24), new Vector3(0, 1, 0), iron);
            AddBox(new Vector3(36, 1.6f, 24), new Vector3(0, 18.5f, 0), iron);
            AddBox(new Vector3(5, 6, 1.2f), new Vector3(0, 16, 11.5f), new Color(0.75f, 0.65f, 0.3f));
        }

        // Giuong hoi sinh: khung go + nem + goi + chan (diem hoi sinh cua chu khi bi ha).
        private void BuildBed()
        {
            AddCollisionBox(new Vector3(30, 14, 52));
            var wood = new Color(0.45f, 0.3f, 0.17f);
            AddBox(new Vector3(30, 6, 52), new Vector3(0, 5, 0), wood);
            AddBox(new Vector3(30, 16, 3), new Vector3(0, 8, -25), wood.Darkened(0.1f)); // dau giuong
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    AddBox(new Vector3(3, 4, 3), new Vector3(i * 13.5f, 1.5f, j * 24f), wood.Darkened(0.2f));
            AddBox(new Vector3(27, 4, 48), new Vector3(0, 10, 1), new Color(0.88f, 0.86f, 0.8f)); // nem
            AddBox(new Vector3(18, 3, 9), new Vector3(0, 13, -18), new Color(0.95f, 0.95f, 0.92f)); // goi
            AddBox(new Vector3(28, 1.5f, 30), new Vector3(0, 12.6f, 9), new Color(0.6f, 0.18f, 0.16f)); // chan
        }

        // Tram che tao: ban go (+ dung cu) cap 1-2, lo ren da co lua + de (cap 3), xuong mai che (cap 4).
        private void BuildStation(string kind)
        {
            var wood = new Color(0.47f, 0.32f, 0.18f);
            var iron = new Color(0.35f, 0.36f, 0.4f);
            var stone = new Color(0.5f, 0.48f, 0.45f);
            if (kind == "forge")
            {
                AddCollisionBox(new Vector3(50, 40, 40));
                AddBox(new Vector3(40, 34, 34), new Vector3(-6, 17, 0), stone);
                AddBox(new Vector3(18, 12, 2), new Vector3(-6, 14, 17.5f), new Color(1f, 0.45f, 0.1f)); // mieng lo
                AddBox(new Vector3(10, 30, 10), new Vector3(-12, 49, -6), stone.Darkened(0.2f)); // ong khoi
                AddBox(new Vector3(14, 8, 8), new Vector3(24, 14, 6), iron); // de
                AddBox(new Vector3(6, 10, 6), new Vector3(24, 5, 6), wood);
                _fireLight = new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 1.8f, OmniRange = 150f, Position = new Vector3(-6, 16, 24),
                    DistanceFadeEnabled = true, DistanceFadeBegin = 700f, DistanceFadeLength = 200f };
                AddChild(_fireLight);
                return;
            }
            bool big = kind == "workshop";
            float w = big ? 70 : 48, d = big ? 34 : 26;
            AddCollisionBox(new Vector3(w, 20, d));
            AddBox(new Vector3(w, 4, d), new Vector3(0, 18, 0), wood); // mat ban
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    AddBox(new Vector3(3, 18, 3), new Vector3(i * (w / 2 - 3), 9, j * (d / 2 - 3)), wood.Darkened(0.2f));
            if (kind == "carpentry" || big)
            {
                AddBox(new Vector3(16, 1, 6), new Vector3(-8, 21, 0), iron); // cua
                for (int k = 0; k < 3; k++) AddBox(new Vector3(30, 4, 4), new Vector3(w / 2 + 12, 2 + k * 4.2f, -6 + k * 3), wood.Lightened(0.1f)); // go xep
            }
            else
            {
                AddBox(new Vector3(6, 6, 6), new Vector3(12, 23, 4), iron); // bua/dung cu
                AddBox(new Vector3(14, 1, 10), new Vector3(-10, 20.5f, 0), new Color(0.85f, 0.8f, 0.65f)); // ban ve
            }
            if (big)
            {
                // Mai che + cot (xuong).
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        AddBox(new Vector3(4, 56, 4), new Vector3(i * 46, 28, j * 30), wood.Darkened(0.25f));
                AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(104, 18, 70) }, Position = new Vector3(0, 65, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.3f, 0.2f), CullMode = BaseMaterial3D.CullModeEnum.Disabled } });
                AddBox(new Vector3(12, 14, 12), new Vector3(-22, 27, 0), iron); // may tien
            }
        }

        private void BuildCampfire()
        {
            AddCollisionBox(new Vector3(30, 10, 30));
            var log = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.22f, 0.12f), Roughness = 1f };
            for (int i = 0; i < 4; i++)
                AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 2.2f, Height = 24f },
                    Position = Vector3.Up * 3f,
                    RotationDegrees = new Vector3(78f, i * 45f, 0),
                    MaterialOverride = log,
                });
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Tau * i / 8f;
                AddBox(new Vector3(5, 4, 5), new Vector3(Mathf.Cos(a) * 15f, 2f, Mathf.Sin(a) * 15f), new Color(0.45f, 0.44f, 0.42f));
            }
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 5f, Height = 12f }, Position = Vector3.Up * 8f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.5f, 0.12f), EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.1f),
                    EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            });
            _fireLight = new OmniLight3D
            {
                LightColor = new Color(1f, 0.6f, 0.3f), LightEnergy = 2f, OmniRange = 170f, Position = Vector3.Up * 22f,
                DistanceFadeEnabled = true, DistanceFadeBegin = 700f, DistanceFadeLength = 200f,
            };
            AddChild(_fireLight);
        }

        public void UpdateInfo(PvpClient.StructureInfo info)
        {
            if (info.Hp < _lastHp)
            {
                _showLabelUntil = Time.GetTicksMsec() + 6000;
                // Rung nhe khi trung don.
                var tw = CreateTween();
                tw.TweenProperty(this, "position:x", Position.X + 2.5f, 0.04f);
                tw.TweenProperty(this, "position:x", info.X, 0.06f);
            }
            _lastHp = info.Hp;
            Info = info;
            RefreshLabel();
        }

        private bool Mine => Info.OwnerId == (NetworkClient.Instance?.MyId ?? -1);

        private void RefreshLabel()
        {
            if (_label == null) return;
            string kind = Loc.T("base.kind." + Info.Kind);
            string owner = Mine ? Loc.T("base.yours") : Info.OwnerName;
            _label.Text = $"{kind} - {owner}\n{Info.Hp}/{Info.MaxHp}";
            float r = Info.MaxHp > 0 ? (float)Info.Hp / Info.MaxHp : 1f;
            _label.Modulate = r > 0.6f ? new Color(0.85f, 1f, 0.8f) : r > 0.3f ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.35f);
        }

        public override void _Process(double delta)
        {
            if (_fireLight != null) _fireLight.LightEnergy = 1.8f + Mathf.Sin(Time.GetTicksMsec() / 90f) * 0.25f + Mathf.Sin(Time.GetTicksMsec() / 37f) * 0.15f;
            // Nhan chi hien khi lai gan / vua bi danh (tranh roi man hinh o can cu lon).
            var cam = GetViewport()?.GetCamera3D();
            bool show = Time.GetTicksMsec() < _showLabelUntil || (cam != null && cam.GlobalPosition.DistanceTo(GlobalPosition) < 320f);
            if (_label.Visible != show) _label.Visible = show;
        }

        public override void Interact() => BaseUI.Instance?.OpenStructure(this);

        // Bi pha: vo vun + bien mat.
        public void Shatter()
        {
            RemoveFromGroup("base_structures");
            foreach (Node c in GetChildren()) if (c is CollisionShape3D cs) cs.Disabled = true;
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(this, "scale", new Vector3(1.2f, 0.05f, 1.2f), 0.35f).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(QueueFree));
        }
    }
}
