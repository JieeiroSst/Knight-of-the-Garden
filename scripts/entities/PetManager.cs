using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Tao/doi node Pet theo PetSystem.Active (thu cung dang dan theo).
    public partial class PetManager : Node
    {
        private Pet _pet;

        public override void _Ready()
        {
            PetSystem.Instance.Changed += Sync;
            Sync();
        }

        public override void _ExitTree() => PetSystem.Instance.Changed -= Sync;

        public void Sync()
        {
            if (_pet != null && IsInstanceValid(_pet) && _pet.PetId == PetSystem.Instance.Active) return;
            _pet?.QueueFree();
            _pet = null;
            if (PetSystem.Instance.Active == null) return;
            if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;
            _pet = new Pet { PetId = PetSystem.Instance.Active };
            GetTree().CurrentScene.AddChild(_pet);
            _pet.GlobalPosition = player.GlobalPosition + new Vector3(30, 0, 30);
        }
    }
}
