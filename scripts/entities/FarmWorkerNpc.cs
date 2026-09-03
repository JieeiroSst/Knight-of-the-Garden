using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // NPC "nguoi lam ruong thue" - QUY HOACH LAI sang Utility AI + GOAP (xem FarmhandNpc.cs/
    // RepairmanNpc.cs de biet chi tiet kien truc chung). Truoc day di TUAN TU qua tung o dat theo
    // thu tu co dinh; gio moi lan quyet dinh, CHAM DIEM tung o dat (xem FarmPlot.Urgency01) va di
    // toi o CAN CHAM SOC NHAT (sau benh/sap chet vi thieu nuoc/da chin > can tuoi/bon > khong can
    // gi) - dung y "ruong khat +90" trong vi du nguoi dung neu. Nong san thu hoach duoc van tu
    // dong cho vao tui do nguoi choi (logic nay nam trong FarmPlot.Harvest(), khong doi).
    public partial class FarmWorkerNpc : NPC
    {
        [Export] public float Speed = 50f;
        [Export] public float Acceleration = 190f;
        [Export] public float Friction = 230f;
        [Export] public float TurnSpeed = 7f;
        [Export] public bool FlipModelFacing = true;
        [Export] public float Gravity = 980f;
        [Export] public float ArriveDist = 16f;
        [Export] public double WorkPauseSec = 1.2; // dung "lam viec" tai moi o bao lau

        // Main.cs gan cac gia tri nay ngay sau khi tao (truoc AddChild).
        public Vector3 HomePos;
        public Vector3 InteriorHomePos;
        public Vector3 WorkPos; // tam vung ruong phu trach

        // true = CHI cham soc o dat Nha Kinh (IsGreenhouse), false (mac dinh) = CHI cham soc o
        // dat NGOAI Nha Kinh (luoi ruong chinh 12x6 + o cuoc tu do) - tach biet 2 nhom NPC theo
        // dung yeu cau "1 NPC rieng quan ly Nha Kinh, cac NPC con lai chi lo ruong chinh". Neu
        // khong tach, NPC ruong chinh se co the tim duong vao Nha Kinh (dang bi khoa boi
        // GreenhouseGate truoc khi mo khoa) va bi ket ngay truoc cong.
        [Export] public bool GreenhouseOnly = false;

        private Vector3 _facing = Vector3.Back;
        private readonly SteeringUtil.StuckDetector _stuckDetector = new();
        private readonly UtilityBrain _brain = new();

        public override void _Ready()
        {
            base._Ready();

            _brain.Actions.Add(MakeTendPlotAction());
            _brain.Actions.Add(UtilityPresets.MakeSleep(() => InteriorHomePos));
            _brain.Actions.Add(UtilityPresets.MakeWander(() => WorkPos, 140f));
        }

        // Vai tro chia se "kinh nghiem" hoc duoc (xem NpcExperience.cs) - NPC ruong chinh dung
        // chung "field_work" voi ScheduledFarmNpc (cung lam dong ruong, quy mo lon ~72 o) de gop
        // du lieu hoc nhanh/on dinh hon; NPC Nha Kinh dung rieng "greenhouse_work" (quy mo nho hon
        // han, ~16 o, khoang cach/trong so phu hop rat khac ruong chinh nen KHONG the dung chung).
        private string ExperienceRole => GreenhouseOnly ? "greenhouse_work" : "field_work";

        // Cham diem TAT CA o dat (nhom "farm_plots") theo Urgency01 TRU DI phat khoang cach (trong
        // so hoc duoc qua thoi gian - xem NpcExperience.cs) - truoc day CHI dung Urgency01 thuan
        // tuy, khien NPC co the boi qua 1 o dat GAN (hoi khan) de chay xa hon toi 1 o khan hon chut
        // it, ton thoi gian di duong trong luc cac o gan tiep tuc xuong cap. Diem so dung TRUC TIEP
        // lam Utility score (nhan 100 de cung thang do voi cac hanh dong khac trong game).
        private UtilityAction MakeTendPlotAction()
        {
            return new UtilityAction
            {
                Id = "TendPlot",
                Evaluate = ctx =>
                {
                    float distWeight = NpcExperience.DistanceWeight(ExperienceRole);
                    FarmPlot best = null;
                    float bestScore = float.NegativeInfinity;
                    float bestUrgency = 0f;
                    foreach (var node in GetTree().GetNodesInGroup("farm_plots"))
                    {
                        if (node is not FarmPlot plot || !IsInstanceValid(plot)) continue;
                        if (plot.IsGreenhouse != GreenhouseOnly) continue; // tach rieng khu Nha Kinh / ruong chinh
                        if (plot.IsEmpty) continue; // NPC lam thue khong tu gieo hat moi, chi cham soc
                        float u = plot.Urgency01;
                        if (u <= 0f) continue;
                        if (NpcTaskBoard.IsClaimedByOther(plot, this)) continue;
                        float dist = ctx.SelfPos.DistanceTo(plot.GlobalPosition);
                        float score = u * 100f - dist * distWeight;
                        if (score > bestScore) { bestScore = score; bestUrgency = u; best = plot; }
                    }
                    if (best == null) return new UtilityResult(float.NegativeInfinity);
                    return new UtilityResult(bestUrgency * 100f, best);
                },
                InitialState = (ctx, t) => new Dictionary<string, bool> { { "tended", false } },
                Goal = (ctx, t) => new Dictionary<string, bool> { { "tended", true } },
                Steps = new List<GoapAction>
                {
                    new GoapAction
                    {
                        Id = "UseOnPlot", Effects = { { "tended", true } }, DurationSec = (float)WorkPauseSec,
                        TargetPos = (ctx, t) => (t as FarmPlot)?.GlobalPosition ?? ctx.SelfPos,
                        Execute = (ctx, t) =>
                        {
                            (t as FarmPlot)?.UseOn();
                            NpcExperience.ReportOutcome(ExperienceRole, AverageFarmPlotUrgency());
                        },
                    },
                },
            };
        }

        // Do khan cap TRUNG BINH con lai cua ca nhom o dat dang trong (CUNG NHOM Nha Kinh/ruong
        // chinh voi NPC nay) - dung lam "phan hoi" cho NpcExperience sau moi lan hoan thanh 1
        // buoc viec (xem NpcExperience.ReportOutcome).
        private float AverageFarmPlotUrgency()
        {
            float sum = 0f; int count = 0;
            foreach (var node in GetTree().GetNodesInGroup("farm_plots"))
            {
                if (node is not FarmPlot plot || !IsInstanceValid(plot) || plot.IsEmpty) continue;
                if (plot.IsGreenhouse != GreenhouseOnly) continue;
                sum += plot.Urgency01;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            var (desiredDir, targetSpeed) = _brain.Tick(dt, this, ArriveDist, Speed, null, null);

            bool wantsToMove = desiredDir != Vector3.Zero;
            desiredDir = _stuckDetector.ApplyEscape(desiredDir, GlobalPosition, wantsToMove, dt);
            wantsToMove = desiredDir != Vector3.Zero;
            if (wantsToMove)
                _facing = SteeringUtil.SmoothTurn(_facing, desiredDir, TurnSpeed * dt);

            SteeringUtil.ApplyStandingOrLyingPose(_model, _brain.IsSleeping && !wantsToMove, _facing, FlipModelFacing, TurnSpeed * dt);

            Vector3 targetVel = wantsToMove ? _facing * targetSpeed : Vector3.Zero;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z)
                .MoveToward(targetVel, (wantsToMove ? Acceleration : Friction) * dt);

            float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "FarmWorkerNpc:" + Name);

            if (_animPlayer != null)
            {
                string anim = horizontal.Length() > 3f ? "Walk" : "Idle";
                if (_animPlayer.HasAnimation(anim) && _animPlayer.CurrentAnimation != anim)
                    _animPlayer.Play(anim);
            }
        }
    }
}
