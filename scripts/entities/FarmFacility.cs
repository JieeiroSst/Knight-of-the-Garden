using Godot;

namespace HiepSiVeVuon.Entities
{
    // Lop co so cho cac tram tuong tac nho (Thung Giao Hang/Hop Thu/Thung U Phan/Ban Che Tao/
    // Giuong) - dung hoan toan bang code (khong can .tscn rieng): khoi hop don gian lam hinh +
    // va cham de Player.InteractArea bat duoc, bam [E] goi Interact().
    // KHONG de abstract - Godot dang ky moi lop C# ke thua Node lam script, lop abstract co the gay
    // loi khi engine thu tao instance.
    public partial class FarmFacility : StaticBody3D
    {
        public virtual void Interact() { }

        // Va cham (hop) tam dat o day khoi - can cho InteractArea cua Player (GetOverlappingBodies).
        protected void AddCollisionBox(Vector3 size)
        {
            AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = size },
                Position = new Vector3(0, size.Y / 2f, 0),
            });
        }

        protected MeshInstance3D AddBox(Vector3 size, Vector3 center, Color color)
        {
            var mi = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                Position = center,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
            };
            AddChild(mi);
            return mi;
        }
    }
}
