using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Vat pham roi tren mat dat. Nhat bang phim tuong tac (E) hoac cham vao.
    public partial class DroppedItem : Area3D
    {
        public string ItemId;
        public int Amount = 1;
        private Sprite3D _sprite;

        public override void _Ready()
        {
            AddToGroup("dropped_items");
            _sprite = GetNodeOrNull<Sprite3D>("Sprite");
            if (_sprite == null)
            {
                _sprite = new Sprite3D();
                AddChild(_sprite);
            }
            _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _sprite.PixelSize = 0.35f;
            _sprite.Position = Vector3.Up * 10f;
            var tex = ItemDatabase.Instance.GetItemIcon(ItemId);
            if (tex != null) { _sprite.Texture = tex; _sprite.Scale = Vector3.One * 0.9f; }

            // Va cham de nhat khi cham
            var col = GetNodeOrNull<CollisionShape3D>("Collision");
            if (col == null)
            {
                col = new CollisionShape3D();
                var shape = new SphereShape3D { Radius = 14f };
                col.Shape = shape;
                AddChild(col);
            }
            // CHI nguoi choi moi tu dong nhat khi cham - truoc day loc theo BAT KY body nao, nen
            // vat nuoi/NPC (cung layer mac dinh=1) di ngang qua se VO TINH nhat mat vat pham vao
            // thang tui do NGUOI CHOI (vd ga tu "nhat" luon qua trung no vua de). Can loc rieng
            // de NPC thu hoach (xem PoultryKeeperNpc.HarvestNearbyEggs) hoat dong dung.
            BodyEntered += body => { if (body is Player) PickUp(); };

            // Hieu ung nhun nhe
            var tw = CreateTween().SetLoops();
            tw.TweenProperty(_sprite, "position:y", 14f, 0.6f).SetTrans(Tween.TransitionType.Sine);
            tw.TweenProperty(_sprite, "position:y", 10f, 0.6f).SetTrans(Tween.TransitionType.Sine);
        }

        public void PickUp()
        {
            if (Inventory.Instance.AddItem(ItemId, Amount))
            {
                QuestSystem.Instance.OnItemCollected(ItemId);
                var def = ItemDatabase.Instance.GetItem(ItemId);
                GD.Print($"Nhặt được: {def?.Name} x{Amount}");
                QueueFree();
            }
        }

        // Helper de spawn tu code (vd tu Enemy khi chet)
        public static void Spawn(Node parent, Vector3 pos, string itemId, int amount)
        {
            var drop = new DroppedItem { ItemId = itemId, Amount = amount };
            // PHAI AddChild TRUOC roi moi gan GlobalPosition - GlobalPosition can node dang o
            // TRONG scene tree de tinh (doc transform cha), gan luc con "mo coi" (chua co cha)
            // nem loi "!is_inside_tree()" va vi tri KHONG duoc ap dung (con o goc toa do (0,0,0)).
            parent.AddChild(drop);
            drop.GlobalPosition = pos + new Vector3(
                (float)GD.RandRange(-16, 16), 0f, (float)GD.RandRange(-16, 16));
        }
    }
}
