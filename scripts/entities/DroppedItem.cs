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
            BodyEntered += _ => PickUp();

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
                GD.Print($"Nhat duoc: {def?.Name} x{Amount}");
                QueueFree();
            }
        }

        // Helper de spawn tu code (vd tu Enemy khi chet)
        public static void Spawn(Node parent, Vector3 pos, string itemId, int amount)
        {
            var drop = new DroppedItem { ItemId = itemId, Amount = amount };
            drop.GlobalPosition = pos + new Vector3(
                (float)GD.RandRange(-16, 16), 0f, (float)GD.RandRange(-16, 16));
            parent.AddChild(drop);
        }
    }
}
