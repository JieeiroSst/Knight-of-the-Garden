using Godot;

namespace HiepSiVeVuon.Core
{
    // Ham dung chung cho kieu di chuyen "quay dau roi moi buoc toi" (non-holonomic steering) ma
    // moi loai vat nuoi/NPC trong game deu dung (Cow/Horse/Dog/FarmDog/FarmCat/Chicken/
    // FarmhandNpc/StablehandNpc/PoultryKeeperNpc): xoay dan huong than THAT (_facing) ve huong
    // muon den, gioi han toc do quay theo dt.
    public static class SteeringUtil
    {
        // Dung NLERP (lerp roi chuan hoa lai) thay vi Godot Vector3.Slerp - Slerp NEM LOI
        // ArgumentException("Argument is not normalized") khi 2 vector NGUOC HUONG HOAN TOAN
        // (180 do - truc xoay noi suy ra la vector khong/degenerate, khong the chuan hoa). Day
        // la tinh huong HOAN TOAN BINH THUONG trong game (vd vat nuoi chon muc tieu wander moi
        // dung nguoc huong dang dung) va da gay crash that (xem FarmCat.cs). Voi buoc xoay NHO
        // moi khung hinh (t << 1, dung cho steering muot moi frame) nlerp va slerp gan nhu khong
        // the phan biet bang mat, nhung nlerp khong bao gio nem loi.
        public static Vector3 SmoothTurn(Vector3 current, Vector3 desired, float t)
        {
            var blended = current.Lerp(desired, Mathf.Clamp(t, 0f, 1f));
            return blended.LengthSquared() > 0.0001f ? blended.Normalized() : desired;
        }

        // Dung chung cho MOI NPC co lich trinh "ve nha ngu" (FarmhandNpc/StablehandNpc/
        // PoultryKeeperNpc/EstateWorkerNpc/FarmWorkerNpc/TownCitizenNpc/ScheduledFarmNpc): khi
        // dang ngu, model duoc XOAY NAM NGANG (thay vi dung nguyen dang dung) de trong giong dang
        // nam tren giuong that su, dong thoi BO QUA logic xoay theo huong di chuyen (neu khong,
        // khoi if (_facing != Vector3.Zero) moi frame se lien tuc ep model dung day lai). Khi
        // thuc/di lam, goi lai voi isAsleep=false de tro ve dung logic xoay-theo-huong-di-chuyen
        // nhu cu.
        public static void ApplyStandingOrLyingPose(Node3D model, bool isAsleep, Vector3 facing, bool flipModelFacing, float turnAmount)
        {
            if (model == null) return;
            if (isAsleep)
            {
                // Nam ngua, xoay -90 do quanh truc X (than nam ngang thay vi dung), giu nguyen
                // huong Y=90 do co dinh de khop voi huong dat giuong trong AddBuildingEntrance
                // (xem bedRotationY trong Main.cs).
                model.RotationDegrees = new Vector3(-90f, 90f, 0f);
                return;
            }
            if (facing == Vector3.Zero) return;
            var lookDir = flipModelFacing ? -facing : facing;
            var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
            model.Basis = model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(turnAmount, 0f, 1f));
        }

        // Phat hien "bi ket" + tu thoat: MOI NPC/vat nuoi trong game chi biet "quay huong ve muc
        // tieu roi buoc thang toi" (khong co pathfinding that su tranh vat can) - the gioi cang
        // ngay cang day cong trinh/hang rao/coi xay gio/thap canh hon qua tung lan bo sung, nen
        // 1 muc tieu (wander target/diem tuan tra/nha/cho lam viec) ngay cang de roi vao "sau"
        // mot vat can, khien NPC dung yen dam mai vao 1 cho ma khong bao gio toi duoc. StuckDetector
        // theo doi quang duong DI THAT su duoc trong 1 khoang thoi gian - neu qua it du dang
        // "muon di", tu dong tra ve 1 huong "lach" tam thoi (xoay lech so voi huong that su muon
        // di) trong vai giay de NPC truot qua vat can, roi tra huong dieu khien ve lai binh
        // thuong. Ap dung truc tiep vao "desiredDir" ngay truoc khi dung de xoay/di chuyen, khong
        // can biet NPC dang o trang thai/muc tieu gi (wander/patrol/goto deu dung chung duoc).
        public class StuckDetector
        {
            private Vector3 _checkpointPos;
            private double _timer;
            private bool _initialized;
            private double _escapeCooldown;
            private Vector3 _escapeDir;

            public Vector3 ApplyEscape(Vector3 desiredDir, Vector3 currentPos, bool wantsToMove, float dt,
                float minMoveDist = 10f, double windowSec = 1.5, double escapeDurationSec = 1.2)
            {
                if (!_initialized) { _checkpointPos = currentPos; _initialized = true; }

                if (!wantsToMove)
                {
                    _checkpointPos = currentPos;
                    _timer = 0;
                }
                else
                {
                    _timer += dt;
                    if (_timer >= windowSec)
                    {
                        float moved = new Vector2(currentPos.X - _checkpointPos.X, currentPos.Z - _checkpointPos.Z).Length();
                        _checkpointPos = currentPos;
                        _timer = 0;
                        if (moved < minMoveDist && desiredDir != Vector3.Zero)
                        {
                            var rng = new RandomNumberGenerator();
                            rng.Randomize();
                            float angle = rng.RandfRange(Mathf.Pi * 0.5f, Mathf.Pi * 0.85f) * (rng.Randf() < 0.5f ? 1f : -1f);
                            _escapeDir = desiredDir.Rotated(Vector3.Up, angle);
                            _escapeCooldown = escapeDurationSec;
                        }
                    }
                }

                if (_escapeCooldown > 0)
                {
                    _escapeCooldown -= dt;
                    return _escapeDir;
                }
                return desiredDir;
            }
        }
    }
}
