using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Bang go 3D dat trong nha kho, hien SO LUONG san pham (trung ga...) da duoc cac NPC cham
    // nuoi thu hoach va cat vao kho nong san chung (FarmStorage) - cap nhat truc tiep moi khi
    // FarmStorage.StorageChanged phat tin, khong can Refresh() thu cong tu ben ngoai.
    public partial class FarmStorageBoard : Node3D
    {
        private Label3D _label;

        public override void _Ready()
        {
            var backing = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(90f, 46f, 4f) },
                Position = Vector3.Zero,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.17f, 0.08f), Roughness = 0.9f }
            };
            AddChild(backing);

            _label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                FontSize = 30,
                OutlineSize = 6,
                PixelSize = 0.09f,
                Modulate = new Color(1f, 0.95f, 0.8f),
                Position = Vector3.Forward * 2.5f,
            };
            AddChild(_label);

            FarmStorage.Instance.StorageChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            string eggName = WarehouseDatabase.Instance.GetProduct("egg")?.Name ?? "Trung Ga";
            _label.Text = $"KHO NONG SAN\n{eggName}: {FarmStorage.Instance.GetCount("egg")}";
        }
    }
}
