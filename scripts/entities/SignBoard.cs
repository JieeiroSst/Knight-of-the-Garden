using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Cac bang/quay tuong tac don gian (moi lop 1 cong dung, deu ke thua FarmFacility):
    // ve hinh "bang go co mai" dung chung qua BuildSign().
    public partial class SignBoard : FarmFacility
    {
        protected void BuildSign(Color board)
        {
            AddCollisionBox(new Vector3(40, 50, 14));
            var wood = new Color(0.45f, 0.3f, 0.18f);
            AddBox(new Vector3(4, 46, 4), new Vector3(-16, 23, 0), wood);
            AddBox(new Vector3(4, 46, 4), new Vector3(16, 23, 0), wood);
            AddBox(new Vector3(40, 26, 3), new Vector3(0, 34, 0), board);
            AddBox(new Vector3(46, 3, 10), new Vector3(0, 49, 0), wood.Darkened(0.3f));
        }
    }
}
