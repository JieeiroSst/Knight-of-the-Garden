using Godot;

namespace HiepSiVeVuon.Entities
{
    // Cay chat duoc trong vung hoang da (WorldStreamer). Bi tan cong du so nhat -> nga, roi go.
    public partial class Tree : StaticBody3D
    {
        [Export] public int Hp = 3;
        [Export] public string WoodItemId = "wood";
        [Export] public int WoodMin = 2;
        [Export] public int WoodMax = 4;

        private Node3D _model;

        public override void _Ready()
        {
            AddToGroup("choppable_trees");
            _model = GetNodeOrNull<Node3D>("Model");
        }

        public void Chop(int dmg)
        {
            if (Hp <= 0) return;
            Hp -= dmg;

            // Rung nhe khi bi chat
            if (_model != null)
            {
                var tw = CreateTween();
                tw.TweenProperty(_model, "rotation:z", 0.12f, 0.07f);
                tw.TweenProperty(_model, "rotation:z", 0f, 0.09f);
            }

            if (Hp <= 0) Fall();
        }

        private void Fall()
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            int amount = rng.RandiRange(WoodMin, WoodMax);
            DroppedItem.Spawn(GetTree().CurrentScene, GlobalPosition, WoodItemId, amount);
            QueueFree();
        }
    }
}
