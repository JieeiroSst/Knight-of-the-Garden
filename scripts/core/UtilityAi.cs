using System.Collections.Generic;
using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
    // "Bang chup" du lieu 1 NPC dung de cham diem/lap ke hoach MOI LAN quyet dinh - CO Y giu that
    // nho (chi vi tri/gio/thoi tiet/do met CHUNG cho moi vai tro) vi tung UtilityAction se tu doc
    // them du lieu RIENG cua minh can (vd FarmPlot gan nhat, vat nuoi trong chuong minh phu
    // trach...) qua closure luc dang ky trong _Ready() cua tung NPC - tranh phai nhoi TAT CA du
    // lieu co the can cua MOI vai tro vao 1 struct chung khong lien quan.
    public class NpcNeedContext
    {
        public Node3D Self;
        public Vector3 SelfPos;
        public float Fatigue;   // 0..1, do UtilityBrain tu quan ly (tang khi thuc, giam khi ngu)
        public int Hour;
        public bool IsRaining;
        public bool IsNight;
    }

    // Ket qua cham diem 1 UtilityAction: diem SO (cao nhat thang) + doi tuong CU THE (neu co, vd 1
    // FarmPlot/FenceMarker cu the) de UtilityBrain claim (tranh 2 NPC cung lam 1 viec) va truyen
    // lai cho GOAP ben duoi.
    public readonly struct UtilityResult
    {
        public readonly float Score;
        public readonly Node Target;
        public UtilityResult(float score, Node target = null) { Score = score; Target = target; }
    }

    // 1 "nhu cau" NPC co the chon lam (vd "Tuoi ruong"/"Cho an"/"Ngu") - Evaluate cham diem NGAY
    // BAY GIO (co the tra ve Target khac nhau moi lan, vd o dat KHAT NHAT hien tai), Goal/
    // InitialState/Steps mo ta RIENG "the gioi thu nho" + tap hanh dong GOAP de dat duoc nhu cau
    // do (xem GoapPlanner) - moi UtilityAction TU CHU hoan toan, khong chia se khoa trang thai voi
    // action khac.
    public class UtilityAction
    {
        public string Id;
        public System.Func<NpcNeedContext, UtilityResult> Evaluate;
        public System.Func<NpcNeedContext, Node, Dictionary<string, bool>> InitialState;
        public System.Func<NpcNeedContext, Node, Dictionary<string, bool>> Goal;
        public List<GoapAction> Steps;
    }

    // Bo nao Utility AI + GOAP dung CHUNG cho moi loai NPC - la 1 class C# thuan (KHONG phai
    // Node), moi NPC giu 1 instance rieng qua field va goi Tick() moi _PhysicsProcess. Thay THE
    // HOAN TOAN lich gio co dinh (WorkStartHour/WorkEndHour) cu: NPC luon dang "lam viec gi do"
    // duoc UtilityAction diem cao nhat quyet dinh, ke ca ngu (hanh dong "Sleep" tu thang diem theo
    // do met + gio dem, khong con hard-code khung gio).
    public class UtilityBrain
    {
        public List<UtilityAction> Actions = new();
        public float DecisionIntervalSec = 20f;
        public float FatigueGrowPerSec = 1f / 2400f;   // ~40 phut THAT thuc lien tuc -> met toi da
        public float FatigueRecoverPerSec = 1f / 300f;  // ~5 phut THAT ngu -> het met hoan toan

        private readonly RandomNumberGenerator _rng = new();
        private float _timer;
        private float _fatigue;

        private UtilityAction _currentAction;
        private Node _target;
        private Node _claimedTarget;
        private Queue<GoapAction> _plan;
        private GoapAction _currentStep;
        private Vector3 _currentStepTarget;
        private float _stepWaitLeft;
        private bool _waitingAtStep;

        public bool IsSleeping => _currentAction?.Id == "Sleep";
        public string CurrentActionId => _currentAction?.Id;

        public UtilityBrain()
        {
            _rng.Randomize();
            _timer = _rng.RandfRange(0f, 6f); // lech pha lan quyet dinh dau tien, tranh dong loat
        }

        // Goi MOI _PhysicsProcess. Tra ve huong di + toc do mong muon (giong y het cac ham
        // "DoWork/DoWander" cu tra ve, de NPC script tai su dung nguyen phan gia toc/trong luc/
        // MoveAndSlide/hoat hinh da co - CHI thay nguon "muon di dau lam gi", KHONG viet lai di
        // chuyen).
        public (Vector3 dir, float speed) Tick(float dt, Node3D self, float arriveDist, float speed,
            SteeringUtil.NavSteering nav, NavigationAgent3D navAgent)
        {
            var ctx = new NpcNeedContext
            {
                Self = self,
                SelfPos = self.GlobalPosition,
                Fatigue = _fatigue,
                Hour = GameManager.Instance.Hour,
                IsRaining = GameManager.Instance.IsRaining,
                IsNight = GameManager.Instance.IsNight,
            };

            if (IsSleeping) _fatigue = Mathf.Max(0f, _fatigue - FatigueRecoverPerSec * dt);
            else _fatigue = Mathf.Min(1f, _fatigue + FatigueGrowPerSec * dt);

            bool planActive = _currentStep != null;
            _timer -= dt;
            if (!planActive && _timer <= 0f)
            {
                _timer = DecisionIntervalSec * _rng.RandfRange(0.8f, 1.2f);
                Decide(ctx);
            }

            if (_currentStep == null)
                return (Vector3.Zero, 0f);

            if (_waitingAtStep)
            {
                _stepWaitLeft -= dt;
                if (_stepWaitLeft <= 0f) AdvanceStep(ctx);
                return (Vector3.Zero, 0f);
            }

            Vector3 straightDir = _currentStepTarget - ctx.SelfPos;
            straightDir.Y = 0f;
            if (straightDir.Length() <= arriveDist)
            {
                if (_currentStep.DurationSec > 0f) { _waitingAtStep = true; _stepWaitLeft = _currentStep.DurationSec; }
                else AdvanceStep(ctx);
                return (Vector3.Zero, 0f);
            }

            var dir = nav != null ? nav.GetDirection(navAgent, ctx.SelfPos, _currentStepTarget) : Vector3.Zero;
            if (dir == Vector3.Zero) dir = straightDir.Normalized();
            return (dir, speed);
        }

        private void Decide(NpcNeedContext ctx)
        {
            UtilityAction best = null;
            UtilityResult bestResult = default;
            float bestScore = float.NegativeInfinity;

            foreach (var action in Actions)
            {
                UtilityResult result;
                try { result = action.Evaluate(ctx); }
                catch (System.Exception e) { GD.PushWarning($"UtilityBrain: loi cham diem '{action.Id}': {e.Message}"); continue; }

                float score = result.Score;
                // "-40 neu NPC khac dang lam" - dung y nguoi dung neu khi mo ta yeu cau.
                if (result.Target != null && NpcTaskBoard.IsClaimedByOther(result.Target, ctx.Self))
                    score -= 40f;

                if (score > bestScore) { bestScore = score; best = action; bestResult = result; }
            }

            if (best == null) { ClearPlan(); return; }

            if (_claimedTarget != null && _claimedTarget != bestResult.Target)
            {
                NpcTaskBoard.Release(_claimedTarget);
                _claimedTarget = null;
            }

            _currentAction = best;
            _target = bestResult.Target;
            if (_target != null)
            {
                NpcTaskBoard.TryClaim(_target, ctx.Self);
                _claimedTarget = _target;
            }

            var goal = best.Goal?.Invoke(ctx, _target) ?? new Dictionary<string, bool>();
            var initial = best.InitialState?.Invoke(ctx, _target) ?? new Dictionary<string, bool>();
            var planned = GoapPlanner.Plan(initial, goal, best.Steps);

            if (planned == null || planned.Count == 0) { ClearPlan(); return; }

            _plan = planned;
            _currentStep = _plan.Dequeue();
            _currentStepTarget = _currentStep.TargetPos?.Invoke(ctx, _target) ?? ctx.SelfPos;
            _waitingAtStep = false;
        }

        private void AdvanceStep(NpcNeedContext ctx)
        {
            _currentStep.Execute?.Invoke(ctx, _target);
            _waitingAtStep = false;

            if (_plan != null && _plan.Count > 0)
            {
                _currentStep = _plan.Dequeue();
                _currentStepTarget = _currentStep.TargetPos?.Invoke(ctx, _target) ?? ctx.SelfPos;
            }
            else
            {
                ClearPlan();
            }
        }

        private void ClearPlan()
        {
            _currentStep = null;
            _plan = null;
            _waitingAtStep = false;
            if (_claimedTarget != null) { NpcTaskBoard.Release(_claimedTarget); _claimedTarget = null; }
        }
    }

    // 2 hanh dong DUNG CHUNG cho hau het NPC (Sleep/Wander) - tranh moi script phai tu viet lai
    // cung 1 khuon GOAP 1-buoc don gian. Moi vai tro chi dang ky THEM cac UtilityAction rieng cua
    // minh (TendPlot/RepairFence/...) canh 2 cai nay.
    public static class UtilityPresets
    {
        // Diem tang theo do MET (thay lich gio co dinh "ve nha luc Xh cu") + thuong lon vao ban
        // dem - NPC se tu "buon ngu" dan ve khuya thay vi bat ngo tat may luc dung 1 gio co dinh.
        public static UtilityAction MakeSleep(System.Func<Vector3> interiorHomePos, float nightBonus = 55f, float fatigueWeight = 70f)
        {
            return new UtilityAction
            {
                Id = "Sleep",
                Evaluate = ctx => new UtilityResult(ctx.Fatigue * fatigueWeight + (ctx.IsNight ? nightBonus : 0f)),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "atHome", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "atHome", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction { Id = "GoSleep", Effects = { { "atHome", true } }, TargetPos = (ctx, t) => interiorHomePos() },
                },
            };
        }

        // Diem NEN thap, luon co san - dam bao NPC KHONG BAO GIO "dung hinh" (0 hanh dong nao
        // duoc chon) neu moi nhu cau khac deu = 0 diem luc do.
        public static UtilityAction MakeWander(System.Func<Vector3> center, float radius, float baseScore = 4f, float pauseSec = 4f)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            return new UtilityAction
            {
                Id = "Wander",
                Evaluate = ctx => new UtilityResult(baseScore),
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "there", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "there", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "WanderStep",
                        Effects = { { "there", true } },
                        DurationSec = pauseSec,
                        TargetPos = (ctx, t) =>
                        {
                            float angle = rng.RandfRange(0f, Mathf.Tau);
                            float r = rng.RandfRange(0f, radius);
                            return center() + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                        },
                    },
                },
            };
        }
    }
}
