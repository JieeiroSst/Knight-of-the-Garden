using Godot;

namespace HiepSiVeVuon.Core
{
    // Cho an nap (bui co cao) rai khap the gioi theo HAM TAT DINH - CHEP Y HET backend
    // src/game/pvpRules.js (hash3/bushInCell/inBush). Server dung cung ham de KIEM TRA loi bao "dang
    // an" cua client -> khong the tu nhan la dang an o giua bai trong. Doi 1 ben thi PHAI doi ca ben kia
    // (test: pvpRules.hash3(-5, 7, 77) == 3310952574).
    public static class HideSpots
    {
        public const float Cell = 300f;
        private const uint Chance = 300;
        private const uint Seed = 77;

        public readonly struct Bush
        {
            public readonly float X, Z, R;
            public Bush(float x, float z, float r) { X = x; Z = z; R = r; }
        }

        public static uint Hash3(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = ((uint)x * 0x8da6b343u) ^ ((uint)y * 0xd8163841u) ^ (seed * 0xcb1ab31fu);
                h ^= h >> 13;
                h *= 0x85ebca6bu;
                h ^= h >> 16;
                return h;
            }
        }

        public static bool TryGetBush(int cx, int cz, out Bush bush)
        {
            uint h = Hash3(cx, cz, Seed);
            bush = default;
            if ((h & 1023u) >= Chance) return false;
            const float margin = 50f;
            const float span = Cell - 2f * margin;
            float ox = margin + ((h >> 10) & 255u) / 255f * span;
            float oz = margin + ((h >> 18) & 255u) / 255f * span;
            bush = new Bush(cx * Cell + ox, cz * Cell + oz, 30f + ((h >> 26) & 15u));
            return true;
        }

        public static bool InBush(Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.X / Cell), cz = Mathf.FloorToInt(p.Z / Cell);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (TryGetBush(cx + dx, cz + dz, out var b) && new Vector2(p.X - b.X, p.Z - b.Z).Length() <= b.R) return true;
            return false;
        }
    }
}
