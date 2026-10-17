using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Tui do roi ra khi 1 nguoi choi bi ha (hoac ruong can cu bi pha) - bam [E] de nhat. Nguoi ha duoc
    // uu tien nhat trong 1 phut, sau do ai cung nhat duoc; het 10 phut tui bien mat (server quyet dinh).
    public partial class LootBag : FarmFacility
    {
        public long BagId;
        public string OwnerName = "";
        public int KillerId;
        public long PriorityUntilMs, ExpiresAtMs; // gio server (ms epoch)

        private Label3D _label;
        private OmniLight3D _glow;
        private MeshInstance3D _sack;
        private double _labelTimer;

        public override void _Ready()
        {
            AddToGroup("loot_bags");
            AddCollisionBox(new Vector3(24, 20, 24));
            var cloth = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.4f, 0.24f), Roughness = 1f };
            _sack = new MeshInstance3D { Mesh = new SphereMesh { Radius = 11f, Height = 18f }, Position = Vector3.Up * 9f, MaterialOverride = cloth };
            AddChild(_sack);
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 3f, BottomRadius = 5f, Height = 6f }, Position = Vector3.Up * 19f, MaterialOverride = cloth });
            AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 3.2f, OuterRadius = 4.4f }, Position = Vector3.Up * 17f,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.7f, 0.3f), Metallic = 0.6f },
            });
            _glow = new OmniLight3D
            {
                LightColor = new Color(1f, 0.8f, 0.35f), LightEnergy = 1.4f, OmniRange = 80f, Position = Vector3.Up * 26f,
                DistanceFadeEnabled = true, DistanceFadeBegin = 700f, DistanceFadeLength = 200f,
            };
            AddChild(_glow);
            _label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.22f, FontSize = 34, OutlineSize = 8,
                NoDepthTest = true, Position = Vector3.Up * 40f, Modulate = new Color(1f, 0.88f, 0.5f),
            };
            AddChild(_label);
            UpdateLabel();
        }

        private static long ServerNowMs => (long)GameManager.Instance.ServerNowMs;

        private void UpdateLabel()
        {
            long now = ServerNowMs;
            string text = string.Format(Loc.T("bag.label_fmt"), string.IsNullOrEmpty(OwnerName) ? Loc.T("bag.unknown") : OwnerName);
            int myId = NetworkClient.Instance?.MyId ?? 0;
            if (KillerId != 0 && PriorityUntilMs > now)
                text += "\n" + string.Format(Loc.T(KillerId == myId ? "bag.yours_fmt" : "bag.priority_fmt"), (PriorityUntilMs - now) / 1000);
            _label.Text = text;
        }

        public override void _Process(double delta)
        {
            if (_glow != null) _glow.LightEnergy = 1.1f + Mathf.Sin(Time.GetTicksMsec() / 220f) * 0.4f;
            if (_sack != null) _sack.Position = Vector3.Up * (9f + Mathf.Sin(Time.GetTicksMsec() / 400f) * 1.2f);
            _labelTimer -= delta;
            if (_labelTimer <= 0) { _labelTimer = 0.5; UpdateLabel(); }
        }

        public override void Interact() => PvpClient.Instance?.Loot(this);
    }
}
