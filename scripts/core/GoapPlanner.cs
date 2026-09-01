using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HiepSiVeVuon.Core
{
    // 1 buoc hanh dong GOAP: co dieu kien can (Preconditions) va hieu ung sau khi lam xong
    // (Effects) tren 1 "the gioi thu nho" rieng cua TUNG UtilityAction (xem UtilityAi.cs) - vi du
    // "hasFeed"/"animalFed" chi co y nghia trong pham vi hanh dong "cho an", khong dung chung 1
    // bang trang thai toan cuc voi hanh dong "sua hang rao" (tranh dung do ten khoa giua cac vai
    // tro NPC rat khac nhau). "target" (Node) la doi tuong cu the NPC dang huong toi (o dat/chuong/
    // hang rao...) - duoc UtilityBrain chon LUC cham diem, truyen lai cho tung buoc.
    public class GoapAction
    {
        public string Id;
        public Dictionary<string, bool> Preconditions = new();
        public Dictionary<string, bool> Effects = new();
        public float Cost = 1f;
        // (context, target) -> vi tri THE GIOI can di toi de thuc hien buoc nay. Null = khong can
        // di dau (NPC dung o cho, vd bao cao/doi thoai).
        public System.Func<NpcNeedContext, Node, Vector3?> TargetPos;
        // Dung lai bao lau SAU KHI den noi truoc khi tinh la xong buoc (vd sua hang rao 6s).
        public float DurationSec = 0f;
        // Hieu ung THAT thuc thi khi den noi (+ het DurationSec neu co) - vd goi FarmPlot.UseOn(),
        // FarmStorage.Add, fence.Repair()...
        public System.Action<NpcNeedContext, Node> Execute;
    }

    // Lap ke hoach GOAP THAT: tim 1 CHUOI GoapAction (khong phai 1 hanh dong don) dua "the gioi
    // thu nho" tu trang thai ban dau (initialState) toi trang thai thoa man muc tieu (goal), dua
    // tren Preconditions/Effects - THU TU do THUAT TOAN tim ra, khong phai hard-code san (vd "het
    // thuc an -> di mua -> ve cho an" tu dong xuat hien khi hasFeed=false, con neu hasFeed=true
    // san thi bo qua buoc mua). Dung tim kiem tien (uniform-cost/Dijkstra don gian) - tap hanh
    // dong moi UtilityAction rat nho (2-4 buoc) nen KHONG can A* phuc tap/heuristic.
    public static class GoapPlanner
    {
        private class PlanNode
        {
            public Dictionary<string, bool> State;
            public List<GoapAction> Plan;
            public float Cost;
        }

        public static Queue<GoapAction> Plan(Dictionary<string, bool> initialState, Dictionary<string, bool> goal,
            List<GoapAction> availableActions, int maxDepth = 6, int maxIterations = 500)
        {
            if (IsSatisfied(initialState, goal)) return new Queue<GoapAction>();
            if (availableActions == null || availableActions.Count == 0) return null;

            var frontier = new List<PlanNode> { new PlanNode { State = initialState, Plan = new List<GoapAction>(), Cost = 0f } };
            var visited = new HashSet<string>();

            for (int iter = 0; iter < maxIterations && frontier.Count > 0; iter++)
            {
                frontier.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                var node = frontier[0];
                frontier.RemoveAt(0);

                if (IsSatisfied(node.State, goal))
                    return new Queue<GoapAction>(node.Plan);

                if (node.Plan.Count >= maxDepth) continue;

                string key = StateKey(node.State) + "|" + node.Plan.Count;
                if (!visited.Add(key)) continue;

                foreach (var action in availableActions)
                {
                    if (!PreconditionsMet(action, node.State)) continue;

                    var nextState = new Dictionary<string, bool>(node.State);
                    foreach (var kv in action.Effects) nextState[kv.Key] = kv.Value;

                    var nextPlan = new List<GoapAction>(node.Plan) { action };
                    frontier.Add(new PlanNode { State = nextState, Plan = nextPlan, Cost = node.Cost + action.Cost });
                }
            }
            return null; // khong tim duoc ke hoach trong gioi han - UtilityBrain se bo qua hanh dong nay
        }

        private static bool PreconditionsMet(GoapAction action, Dictionary<string, bool> state)
        {
            foreach (var kv in action.Preconditions)
            {
                bool cur = state.TryGetValue(kv.Key, out var v) && v;
                if (cur != kv.Value) return false;
            }
            return true;
        }

        private static bool IsSatisfied(Dictionary<string, bool> state, Dictionary<string, bool> goal)
        {
            foreach (var kv in goal)
            {
                bool cur = state.TryGetValue(kv.Key, out var v) && v;
                if (cur != kv.Value) return false;
            }
            return true;
        }

        private static string StateKey(Dictionary<string, bool> state) =>
            string.Join(",", state.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
