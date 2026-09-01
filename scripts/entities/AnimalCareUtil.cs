using Godot;

namespace HiepSiVeVuon.Entities
{
    // NPC cham chuong (FarmhandNpc/StablehandNpc/ScheduledFarmNpc...) dung chung theo mau
    // BuildPenCaretakerDorm cu KHONG con biet ro minh dang phu trach LOAI vat nuoi nao (1 chuong
    // co the la bo/cuu/heo/ngua/ga/de tuy Main.cs spawn) - quet CA 6 nhom (group) thay vi gia dinh
    // 1 loai cu the, chi tinh con vat trong BAN KINH gan vi tri cho an cua NPC do (tranh 1 NPC
    // "thay" duoc ca con vat o chuong khac o xa).
    public static class AnimalCareUtil
    {
        private static readonly string[] Groups = { "cows", "sheep", "pigs", "horses", "chickens", "goats" };

        public static IHungryAnimal FindHungriestNear(SceneTree tree, Vector3 center, float radius)
        {
            IHungryAnimal worst = null;
            int worstDays = 0;
            float r2 = radius * radius;
            foreach (var g in Groups)
            {
                foreach (Node node in tree.GetNodesInGroup(g))
                {
                    if (!GodotObject.IsInstanceValid(node) || node is not IHungryAnimal a) continue;
                    if (!a.IsHungry) continue;
                    if (a.GlobalPosition.DistanceSquaredTo(center) > r2) continue;
                    if (a.HungerDays > worstDays) { worstDays = a.HungerDays; worst = a; }
                }
            }
            return worst;
        }

        public static int CountHungryNear(SceneTree tree, Vector3 center, float radius)
        {
            int count = 0;
            float r2 = radius * radius;
            foreach (var g in Groups)
                foreach (Node node in tree.GetNodesInGroup(g))
                    if (GodotObject.IsInstanceValid(node) && node is IHungryAnimal a && a.IsHungry
                        && a.GlobalPosition.DistanceSquaredTo(center) <= r2)
                        count++;
            return count;
        }
    }
}
