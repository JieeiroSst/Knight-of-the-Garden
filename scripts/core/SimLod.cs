using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Core
{
    // "Ngu" mo phong cho thu/NPC o XA nguoi choi: bo qua ca _PhysicsProcess (AI + move_and_slide - phan
    // ton CPU nhat khi co hang tram con vat/NPC) va dung hoat canh. Lai gan thi tinh day, tiep tuc dung cho
    // cu. Chi ngu khi dang cham dat (khong bi treo lo lung). Giong co che ngu cua Enemy.
    public static class SimLod
    {
        public const float SleepDistance = 1200f; // ~60 m (nguoi cao ~40 don vi ~ 1.8 m)
        private const float WakeDistance = 1050f; // tre (hysteresis) - khong bat/tat lien tuc o ranh gioi

        private static ulong _cachedFrame = ulong.MaxValue;
        private static Vector3 _playerPos;
        private static bool _hasPlayer;

        private static void Refresh(Node anyNode)
        {
            ulong f = Engine.GetPhysicsFrames();
            if (f == _cachedFrame) return;
            _cachedFrame = f;
            var p = anyNode.GetTree().GetFirstNodeInGroup("player") as Node3D;
            _hasPlayer = p != null;
            if (_hasPlayer) _playerPos = p.GlobalPosition;
        }

        // Tra ve true neu khung vat ly nay nen BO QUA cho `body`. asleep: bien trang thai rieng cua con do.
        public static bool Sleep(CharacterBody3D body, ref bool asleep)
        {
            Refresh(body);
            if (!_hasPlayer) return false;
            float d2 = body.GlobalPosition.DistanceSquaredTo(_playerPos);
            bool want = asleep ? d2 > WakeDistance * WakeDistance : d2 > SleepDistance * SleepDistance && body.IsOnFloor();
            if (want != asleep)
            {
                asleep = want;
                foreach (var ap in AnimPlayers(body)) ap.SpeedScale = want ? 0f : 1f;
                if (want) body.Velocity = Vector3.Zero;
            }
            return asleep;
        }

        private static IEnumerable<AnimationPlayer> AnimPlayers(Node root)
        {
            if (root is AnimationPlayer ap) yield return ap;
            foreach (Node c in root.GetChildren())
                foreach (var x in AnimPlayers(c)) yield return x;
        }
    }
}
