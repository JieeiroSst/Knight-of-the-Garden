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

        // Boc NavigationAgent3D (navmesh THAT SU cua Godot, xem Main.BuildFarmNavigation) thanh
        // 1 ham dung don gian nhu cac ham steering khac trong file nay: dua vao "muc tieu" (target
        // toa do THE GIOI), tra ve HUONG DI toi DIEM KE TIEP tren duong di THAT (vong qua hang
        // rao/nha/vat can that su), khong phai huong thang toi muc tieu nhu truoc. Chi cap nhat
        // lai TargetPosition cua agent khi muc tieu doi > 10 don vi (tranh yeu cau tinh lai duong
        // di moi frame mot cach khong can thiet - ton kem va khong can thiet vi muc tieu it khi
        // doi tung frame).
        public class NavSteering
        {
            private Vector3 _lastTarget = new(float.NaN, 0f, float.NaN);

            public Vector3 GetDirection(NavigationAgent3D agent, Vector3 currentPos, Vector3 target)
            {
                if (agent == null) return FallbackDirection(currentPos, target);

                // NavigationServer moi DONG BO map lan dau vao dau physics frame KE TIEP sau khi
                // agent duoc them vao cay canh (khong cung frame) - truy van agent (IsNavigationFinished/
                // GetNextPathPosition) TRUOC thoi diem do se in loi "map query... before first map
                // synchronization" ra console (khong crash, nhung on ao + tra ve ket qua sai). Bo
                // qua truy van agent trong truong hop nay, dung huong thang lam du phong tam thoi -
                // se tu chuyen sang duong di THAT ngay khi map dong bo xong (thuong chi 1 frame sau).
                if (NavigationServer3D.MapGetIterationId(agent.GetNavigationMap()) == 0)
                    return FallbackDirection(currentPos, target);

                if (float.IsNaN(_lastTarget.X) || target.DistanceSquaredTo(_lastTarget) > 100f)
                {
                    agent.TargetPosition = target;
                    _lastTarget = target;
                }

                // Navmesh chua san sang (vd frame dau tien ngay sau khi bake) hoac da toi noi -
                // dung huong thang toi muc tieu lam du phong, khong dung yen mai.
                if (!agent.IsNavigationFinished())
                {
                    Vector3 nextPoint = agent.GetNextPathPosition();
                    Vector3 dir = nextPoint - currentPos;
                    dir.Y = 0f;
                    if (dir.LengthSquared() > 1f) return dir.Normalized();
                }
                return FallbackDirection(currentPos, target);
            }

            private static Vector3 FallbackDirection(Vector3 currentPos, Vector3 target)
            {
                Vector3 dir = target - currentPos;
                dir.Y = 0f;
                return dir.LengthSquared() > 1f ? dir.Normalized() : Vector3.Zero;
            }
        }

        // Luoi an toan CUOI CUNG chong "no toa do": mot lan tung ghi nhan Godot bao loi engine
        // "_set_transform: Object went too far away" (vi tri mot PhysicsBody3D vuot qua nguong an
        // toan cua physics server, ~3.16e18 don vi) - da sua 1 nguyen nhan (loi doi don vi x10 o
        // vung que Phap trong Main.cs) nhung van con xay ra rai rac. DA XAC MINH nguyen nhan CHINH
        // qua GuardAgainstRunaway nay (gan tam log ten class de dieu tra): 7 loai NPC (FarmWorkerNpc/
        // StablehandNpc/FarmhandNpc/EstateWorkerNpc/PoultryKeeperNpc/RepairmanNpc/ScheduledFarmNpc)
        // THIEU dong "GlobalPosition = HomePos" trong _Ready() (xem ghi chu trong tung file) - moi
        // NPC nay sinh ra tai vi tri MAC DINH cua scene (~goc toa do) thay vi vi tri that, khien
        // HANG CHUC NPC (nhieu vai tro) cung dung chong khit hoan toan len nhau + len ca cong trinh
        // co san TAI GOC TOA DO ngay khi world-gen xong. Rieng cac o ngu trong doanh trai tap trung
        // (Cam Ve/nhan vien/nguoi cham chuong - xem BuildPalaceGuardBarracks/BuildWorkerDormsAndStaff/
        // BuildPenCaretakerDorm) CUNG tung dat khoang cach 2 o ngu (20-22 don vi) HEP HON duong kinh
        // capsule va cham that su cua NPC (CapsuleShape3D radius=12 -> duong kinh 24, xem NPC.tscn) -
        // gop phan lam nang them van de tuong tu vao moi dem. Godot's CharacterBody3D.MoveAndSlide()
        // giai quyet va cham/truot giua QUA NHIEU capsule chong lan hoan toan cung luc thinh thoang
        // tra ve 1 vi tri bi "van" ra rat xa (hang chuc-hang tram nghin don vi) chi trong DUNG 1
        // frame - da rieng ra toan bo code C# di chuyen (DoWander/DoChase/GOAP TargetPos...) va xac
        // nhan KHONG co cho nao co the tu tao bien thien lon nhu vay, ket luan day la 1 canh hiem cua
        // chinh engine truoc qua nhieu capsule trung/gan trung khop nhau cung luc (da sua ca 2 nguyen
        // nhan goc: them GlobalPosition=HomePos + noi rong khoang cach o ngu). Ham nay la LUOI AN
        // TOAN CUOI CUNG cho MOI truong hop tuong tu con sot lai/chua luong truoc duoc: goi NGAY SAU
        // MoveAndSlide() moi noi, neu vi tri vua ra la NaN/Infinity hoac vuot xa bien the gioi hop ly
        // (rong hon nhieu vung xa nhat hien co, "vung que Phap" ~26000 don vi tu goc) thi KEO VE GOC
        // TOA DO (khong giu huong/keo ve rin bien nhu ban dau tung lam - vi bien 200000 van la
        // "khoang khong" khong co san, khien vat the tiep tuc roi vo han + spam canh bao MOI FRAME
        // thay vi thuc su hoi phuc; goc toa do luon nam trong khu nong trai co san, co san tro lai
        // duoc ngay).
        private const float MaxSaneDistance = 200_000f;

        public static Vector3 GuardAgainstRunaway(Vector3 pos, string debugName = null)
        {
            bool finite = float.IsFinite(pos.X) && float.IsFinite(pos.Y) && float.IsFinite(pos.Z);
            if (finite && pos.LengthSquared() <= MaxSaneDistance * MaxSaneDistance) return pos;

            GD.PushWarning($"SteeringUtil.GuardAgainstRunaway: vi tri bat thuong {pos}" +
                (debugName != null ? $" ({debugName})" : "") + " - keo ve goc toa do.");
            return Vector3.Up * 20f; // nhinh nhe tren mat dat, tranh ket cung vao san ngay khi roi vao
        }

        // Toc do roi toi da (don vi/s) - MOI script vat nuoi/NPC/quai deu tang Velocity.Y THEO
        // Gravity moi frame khi khong IsOnFloor() ma KHONG HE gioi han (khong co "toc do roi toi
        // da" giong vat ly that) - neu 1 con vo tinh roi khoi mat dat lau (vd rot qua khe ho dia
        // hinh, hoac chinh la he qua cua cu "van" toa do o tren khien no bay ra ngoai moi mat san),
        // Velocity.Y se tang VO HAN theo thoi gian, khien vi tri Y lao xuong cang luc cang xa - gop
        // phan lam nghiem trong hon (khong phai nguyen nhan goc) hien tuong "no toa do". Gioi han o
        // day dam bao du roi bao lau, moi frame vi tri chi doi toi da tung nay don vi - khong con
        // duong nao khien 1 gia tri huu han "roi" thanh vo cung trong huu han thoi gian nua.
        public const float TerminalFallSpeed = 2000f;
    }
}
