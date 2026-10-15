using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Cot Da Dich Chuyen - lan dau bam [E] de kich hoat, sau do bam [E] o BAT KY cot nao da kich
    // hoat de mo danh sach va dich chuyen toi cot khac (xem WaystoneUI).
    public partial class Waystone : FarmFacility
    {
        public string WaystoneId = "";
        public string LabelKey = "";
        public bool StartActive = false;

        private MeshInstance3D _crystal;
        private OmniLight3D _light;
        private StandardMaterial3D _crystalMat;

        public Vector3 ArrivalPoint => GlobalPosition + new Vector3(0, 6, 45);

        public override void _Ready()
        {
            AddToGroup("waystones");
            AddCollisionBox(new Vector3(26, 60, 26));
            var stone = new Color(0.5f, 0.5f, 0.55f);
            AddBox(new Vector3(36, 6, 36), new Vector3(0, 3, 0), stone.Darkened(0.2f));
            AddBox(new Vector3(18, 44, 18), new Vector3(0, 28, 0), stone);
            _crystalMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.8f, 1f), EmissionEnabled = true, Emission = new Color(0.3f, 0.7f, 1f) };
            _crystal = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(12, 16, 12) }, Position = new Vector3(0, 60, 0), MaterialOverride = _crystalMat };
            AddChild(_crystal);
            _light = new OmniLight3D { LightColor = new Color(0.4f, 0.8f, 1f), OmniRange = 90f, Position = Vector3.Up * 60f };
            AddChild(_light);
            AdventureSystem.Instance.RegisterPoi(AdventureSystem.PoiKind.Waystone, WaystoneId, GlobalPosition, LabelKey);
            if (StartActive) AdventureSystem.Instance.Waystones.Add(WaystoneId);
        }

        public bool Active => AdventureSystem.Instance.Waystones.Contains(WaystoneId);

        public override void _Process(double delta)
        {
            bool on = Active;
            _crystal.RotateY((float)delta * (on ? 1.5f : 0.3f));
            _crystalMat.EmissionEnergyMultiplier = on ? 1.6f + Mathf.Sin(Time.GetTicksMsec() / 300f) * 0.4f : 0.05f;
            _light.LightEnergy = on ? 1.5f : 0f;
        }

        public override void Interact()
        {
            if (!Active) { AdventureSystem.Instance.ActivateWaystone(WaystoneId); return; }
            (GetTree().GetFirstNodeInGroup("waystone_ui") as WaystoneUI)?.Open(this);
        }
    }
}
