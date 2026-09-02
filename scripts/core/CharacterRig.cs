using Godot;

namespace HiepSiVeVuon.Core
{
    // Gan model nhan vat (Quaternius, tu chua san skeleton + animation) vao mot Node3D cha.
    public static class CharacterRig
    {
        public static AnimationPlayer Attach(Node3D parent, string modelPath, float scale)
        {
            var modelScene = GD.Load<PackedScene>(modelPath);
            if (modelScene == null) return null;
            var model = modelScene.Instantiate<Node3D>();
            model.Name = "Body";
            model.Scale = Vector3.One * scale;
            parent.AddChild(model);
            return FindAnimationPlayer(model);
        }

        private static AnimationPlayer FindAnimationPlayer(Node root)
        {
            if (root is AnimationPlayer ap) return ap;
            foreach (Node child in root.GetChildren())
            {
                var found = FindAnimationPlayer(child);
                if (found != null) return found;
            }
            return null;
        }

        // Tim Skeleton3D trong model vua gan (xem Attach) - dung de gan BoneAttachment3D (vd vat
        // pham cam tay, xem HeldItemVisual.cs) vao dung xuong co tay (Wrist.R trong rig Quaternius).
        public static Skeleton3D FindSkeleton(Node root)
        {
            if (root is Skeleton3D sk) return sk;
            foreach (Node child in root.GetChildren())
            {
                var found = FindSkeleton(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
