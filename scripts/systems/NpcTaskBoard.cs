using System.Collections.Generic;
using Godot;

namespace HiepSiVeVuon.Systems
{
    // Bang "ai dang lam gi" DUNG CHUNG cho tat ca NPC - truoc day KHONG TON TAI co che nao de 1
    // NPC biet "muc tieu nay (1 o dat/1 hang rao/1 chuong) da co NPC khac dang lam roi", nen 2 NPC
    // co the cung lao vao 1 viec. UtilityBrain (xem UtilityAi.cs) tra bang nay MOI LAN cham diem
    // de tru diem manh cho muc tieu da bi giu (dung y "-40 neu NPC khac dang lam" trong yeu cau),
    // va tu CLAIM/RELEASE muc tieu khi bat dau/ket thuc 1 ke hoach. Static don gian (khong phai
    // singleton Node) - toan game chi co 1 "bang" duy nhat, khong can vao scene tree.
    public static class NpcTaskBoard
    {
        private static readonly Dictionary<Node, Node> _claims = new();

        public static bool TryClaim(Node target, Node claimant)
        {
            if (target == null) return true;
            if (_claims.TryGetValue(target, out var holder) && GodotObject.IsInstanceValid(holder) && holder != claimant)
                return false;
            _claims[target] = claimant;
            return true;
        }

        public static void Release(Node target)
        {
            if (target != null) _claims.Remove(target);
        }

        public static bool IsClaimedByOther(Node target, Node self)
        {
            if (target == null) return false;
            return _claims.TryGetValue(target, out var holder) && GodotObject.IsInstanceValid(holder) && holder != self;
        }
    }
}
