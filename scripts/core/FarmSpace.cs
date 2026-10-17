using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HiepSiVeVuon.Core
{
    // NONG TRAI RIENG TAI TOA DO CO DINH: server cap cho moi nguoi choi 1 o luoi (cach nhau 5 km - xem
    // backend farmService). Main dung nong trai NHU CU quanh goc (0,0) ("toa do nong trai"), roi DOI moi
    // node thuoc nong trai toi Origin (toa do the gioi - xem Main.RelocateFarm). Nho vay:
    //  - Save cu (vi tri o ruong tu do, do trang tri... theo toa do nong trai cu) duoc doi ra the gioi
    //    luc nap (Display); do dat moi luu thang toa do the gioi.
    //  - Thi tran / 11 khu vuc / cho / the gioi vo han van o toa do the gioi chung nhu moi nguoi khac.
    public static class FarmSpace
    {
        // Goc nong trai cua MINH trong toa do the gioi (0 = choi offline / server chua cap).
        public static Vector3 Origin = Vector3.Zero;

        // Tam + nua canh vung nong trai (tuong da 10 hecta vuong +-3162, xem Main.BuildFarmStoneWall,
        // them le ngoai tuong) - toa do nong trai. Mo + cao nguyen + canh dong huong duong nam trong tuong.
        public static readonly Vector3 LocalCenter = new(202, 0, 390);
        public const float HalfSize = 3500f;
        // Tuong nong trai THU GON (Main.FarmWallCenter/FarmWallHalfSize) - ~165m x 165m.
        public static readonly Vector3 LocalWallCenter = new(0, 0, 250);
        public const float WallHalfSize = 1650f;
        public static bool InLocalWall(Vector3 v, float margin = 0f) =>
            Mathf.Abs(v.X - LocalWallCenter.X) <= WallHalfSize + margin && Mathf.Abs(v.Z - LocalWallCenter.Z) <= WallHalfSize + margin;
        // Nha chinh (Main.FarmhousePos) - toa do nong trai.
        public static readonly Vector3 LocalFarmhouse = new(-300, 0, -60);
        // Cho dung khi vao game / hoi sinh "ve nong trai" (canh nha chinh) - toa do nong trai.
        public static readonly Vector3 LocalSpawn = new(-300, 0, 80);

        public static Vector3 ToWorld(Vector3 local) => local + Origin;
        public static Vector3 ToLocal(Vector3 world) => world - Origin;
        public static Vector3 WorldCenter => ToWorld(LocalCenter);
        public static Vector3 SpawnPoint => ToWorld(LocalSpawn);

        public static bool InMyFarm(Vector3 world) => InLocalFarmArea(ToLocal(world));

        // ==== Doi cac truong vi tri TUYET DOI (toa do nong trai cu) cua moi script trong nong trai ====
        // Thuc the nong trai (vat nuoi, NPC, cua, ruong...) luu cac diem dich bang toa do tuyet doi (HomeCenter,
        // TroughPosition, WorkPos, PatrolPoints, _wanderTarget...) va so voi GlobalPosition -> sau khi doi
        // FarmRoot phai cong Origin vao chung. Bo qua truong la HUONG/DO LECH (theo ten) va vecto rat ngan.
        private static readonly Regex NotAPosition = new(
            "facing|dir|velocity|vel\\b|vel[A-Z_]|offset|axis|normal|scale|size|extent|half|rot|forward|right|up\\b|lean|tilt|delta|impulse|knock|bob|sway|spin|local|seat|grip|tip|bend|swing|aim|color",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static int ShiftAbsoluteFields(object target, Vector3 by, Func<Vector3, bool> isFarmPos) =>
            ShiftAbsoluteFields(target, by, isFarmPos, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);

        // Di sau vao object thuong (khong phai node Godot) cua du an - vd lich trinh NPC chua cac diem dich.
        private static bool IsOurPlainObject(object o) =>
            o != null && o is not GodotObject && o.GetType().Assembly == typeof(FarmSpace).Assembly && !o.GetType().IsValueType;

        private static int ShiftAbsoluteFields(object target, Vector3 by, Func<Vector3, bool> isFarmPos, HashSet<object> seen, int depth)
        {
            int n = 0;
            if (target == null || depth > 3 || !seen.Add(target)) return 0;
            for (var t = target.GetType(); t != null && t.Assembly == typeof(FarmSpace).Assembly; t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsInitOnly || f.IsLiteral) continue;
                    string name = f.Name.Replace("k__BackingField", "").Trim('<', '>');
                    if (NotAPosition.IsMatch(name)) continue;
                    var ft = f.FieldType;
                    if (ft == typeof(Vector3))
                    {
                        var v = (Vector3)f.GetValue(target);
                        if (isFarmPos(v)) { f.SetValue(target, v + by); n++; }
                    }
                    else if (ft == typeof(Vector3?))
                    {
                        var v = (Vector3?)f.GetValue(target);
                        if (v is Vector3 vv && isFarmPos(vv)) { f.SetValue(target, (Vector3?)(vv + by)); n++; }
                    }
                    else if (ft == typeof(Vector3[]))
                    {
                        if (f.GetValue(target) is Vector3[] arr)
                            for (int i = 0; i < arr.Length; i++) if (isFarmPos(arr[i])) { arr[i] += by; n++; }
                    }
                    else if (ft == typeof(List<Vector3>))
                    {
                        if (f.GetValue(target) is List<Vector3> list)
                            for (int i = 0; i < list.Count; i++) if (isFarmPos(list[i])) { list[i] += by; n++; }
                    }
                    else if (!ft.IsValueType && ft != typeof(string) && !typeof(GodotObject).IsAssignableFrom(ft))
                    {
                        var val = f.GetValue(target);
                        if (IsOurPlainObject(val)) n += ShiftAbsoluteFields(val, by, isFarmPos, seen, depth + 1);
                        else if (val is IList lst && val is not Godot.Collections.Array)
                            foreach (var item in lst) if (IsOurPlainObject(item)) n += ShiftAbsoluteFields(item, by, isFarmPos, seen, depth + 1);
                    }
                }
            }
            return n;
        }

        public static bool InLocalFarmArea(Vector3 v, float margin = 0f) =>
            Mathf.Abs(v.X - LocalCenter.X) <= HalfSize + margin && Mathf.Abs(v.Z - LocalCenter.Z) <= HalfSize + margin;

        // Hien thi (ban do...): toa do co dinh thuoc vung nong trai cu -> toa do the gioi that.
        public static Vector3 Display(Vector3 v) => Origin != Vector3.Zero && InLocalFarmArea(v) ? ToWorld(v) : v;

        // Vecto co phai la 1 VI TRI trong vung nong trai cu (quanh goc) khong.
        public static bool IsLocalFarmPos(Vector3 v) =>
            v.Length() >= 20f && InLocalFarmArea(v, 400f);
    }
}
