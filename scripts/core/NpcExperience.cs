using System.Collections.Generic;
using Godot;

namespace HiepSiVeVuon.Core
{
    // He thong "hoc" NHE cho NPC lam viec dong ruong (khong dung thu vien ML ngoai - "leo doi
    // ngau nhien" / stochastic hill-climbing don gian, khong phai mang no-ron). Van de goc: cac
    // UtilityAction chon muc tieu (vd MakeTendPlotAction trong FarmWorkerNpc.cs/ScheduledFarmNpc.cs)
    // truoc day LUON chon o dat KHAN CAP NHAT tren toan nong trai, bat ke o do cach NPC bao xa -
    // khien NPC co the boi qua 1 o gan (hoi khan) de chay toi 1 o xa hon (khan hon mot chut), ton
    // thoi gian di duong trong luc cac o GAN LAI tiep tuc xuong cap vi khong ai cham.
    //
    // "Hoc" o day nghia la: NPC dan dan TU DIEU CHINH 1 trong so "phat khoang cach" khi cham diem
    // lua chon (score = do_khan_cap*100 - khoang_cach*DistanceWeight), dua tren KET QUA THUC TE
    // quan sat duoc qua nhieu lan lam viec - do khan cap TRUNG BINH con lai cua ca nhom muc tieu
    // NGAY SAU moi lan hoan thanh 1 viec dung lam "phan hoi" (reward, cang THAP cang tot). Neu
    // buoc dieu chinh gan day giup do khan cap trung binh GIAM dan, tiep tuc di theo huong do; neu
    // lam no TANG (te hon), dao chieu huong thu. Dung CHUNG 1 "kinh nghiem" cho ca 1 VAI TRO (vd
    // toan bo FarmWorkerNpc + ScheduledFarmNpc lam dong ruong dung chung khoa "field_work") thay
    // vi tung NPC rieng le - nhieu NPC gop du lieu giup hoc nhanh/on dinh hon.
    public static class NpcExperience
    {
        private class RoleState
        {
            public float DistanceWeight = 0.05f;
            public float LastAvgUrgency = -1f;
            public float StepDir = 1f;
            public float StepSize = 0.012f;
            public int SamplesSinceAdjust = 0;
        }

        private static readonly Dictionary<string, RoleState> _roles = new();

        private static RoleState Get(string role) =>
            _roles.TryGetValue(role, out var s) ? s : (_roles[role] = new RoleState());

        // Trong so phat khoang cach HIEN TAI cua 1 vai tro (diem tru moi don vi khoang cach khi
        // cham diem 1 muc tieu ung vien) - bat dau nho (uu tien do khan cap gan nhu tuyet doi luc
        // dau, giong hanh vi cu), tu dieu chinh dan qua ReportOutcome khi co du kinh nghiem.
        public static float DistanceWeight(string role) => Get(role).DistanceWeight;

        // Goi sau MOI lan 1 NPC thuoc vai tro nay hoan thanh 1 buoc viec - avgUrgencyAfter la do
        // khan cap TRUNG BINH con lai cua ca nhom muc tieu (vd Urgency01 trung binh cua tat ca
        // FarmPlot dang trong) NGAY SAU do.
        public static void ReportOutcome(string role, float avgUrgencyAfter)
        {
            var s = Get(role);
            s.SamplesSinceAdjust++;
            if (s.LastAvgUrgency < 0f) { s.LastAvgUrgency = avgUrgencyAfter; return; }
            // Chi dieu chinh sau du mau (~12 lan hoan thanh viec, gop tu MOI NPC cung vai tro) -
            // tranh nhieu do 1-2 lan viec ngau nhien lam sai lech huong hoc.
            if (s.SamplesSinceAdjust < 12) return;

            if (avgUrgencyAfter > s.LastAvgUrgency + 0.01f)
                s.StepDir = -s.StepDir; // buoc dieu chinh truoc lam TE HON -> dao chieu huong thu
            else if (avgUrgencyAfter >= s.LastAvgUrgency - 0.01f)
                s.StepSize = Mathf.Max(0.002f, s.StepSize * 0.85f); // gan nhu khong doi -> thu buoc nho dan (hoi tu dan)

            float old = s.DistanceWeight;
            s.DistanceWeight = Mathf.Clamp(s.DistanceWeight + s.StepDir * s.StepSize, 0f, 0.4f);
            s.LastAvgUrgency = avgUrgencyAfter;
            s.SamplesSinceAdjust = 0;

            if (!Mathf.IsEqualApprox(old, s.DistanceWeight))
                GD.Print($"NpcExperience[{role}]: dieu chinh trong so khoang cach {old:F3} -> {s.DistanceWeight:F3} (do khan cap TB: {avgUrgencyAfter:F3}).");
        }
    }
}
