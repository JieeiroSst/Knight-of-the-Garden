using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Bay cua dat canh mat nuoc (model hop go CC0): bam [E] bo 1 Moi Cau; sang ngay sau co cua/ca nho/
    // rong; bam [E] lay roi bo moi tiep (neu con moi trong tui).
    public partial class CrabPot : FarmFacility
    {
        public PlacementSystem.Entry Entry;   // Stage: 1 = da co moi
        private Node3D _crab;

        public override void _Ready()
        {
            AddCollisionBox(new Vector3(22, 16, 22));
            var box = ModelUtil.Instance("res://assets3d/features/crate_k.glb");
            if (box != null) { ModelUtil.FitToHeight(box, 14f); AddChild(box); }
            _crab = ModelUtil.Instance("res://assets3d/features/crab.glb");
            if (_crab != null) { ModelUtil.FitToHeight(_crab, 7f); _crab.Position += new Vector3(0, 14f, 0); AddChild(_crab); }
        }

        private bool HasCatch => Entry.Stage == 1 && PlacementSystem.DaysSince(Entry) >= 1;

        public override void _Process(double delta)
        {
            if (_crab != null) _crab.Visible = HasCatch;
        }

        public override void Interact()
        {
            if (HasCatch)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                float r = rng.Randf();
                string item = r < 0.55f ? "cua" : r < 0.8f ? "ca" : "rong";
                Inventory.Instance.AddItem(item, 1, "trap");
                SkillSystem.Instance.AddXp(Data.SkillType.Fishing, 5);
                Notify.Show(string.Format(Loc.T("pickup.got_fmt"), ItemDatabase.Instance.GetDisplayName(item), 1));
                Entry.Stage = 0;
            }
            else if (Entry.Stage == 1) { Notify.Show(Loc.T("place.crab_wait")); return; }
            if (Inventory.Instance.RemoveItem("moi_cau", 1))
            {
                Entry.Stage = 1;
                Entry.Day = GameManager.Instance.Day;
                Notify.Show(Loc.T("place.crab_baited"));
            }
            else Notify.Show(Loc.T("place.crab_need_bait"));
        }
    }
}
