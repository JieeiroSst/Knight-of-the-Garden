using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace HiepSiVeVuon.Systems
{
    // Cap cuong hoa trang bi (+0..+10) - SERVER giu va quyet dinh (GET/POST /api/forge). Client chi
    // giu ban sao de tinh sat thuong/phong thu (Inventory) va hien thi.
    public static class EnhanceClient
    {
        private static readonly Dictionary<string, int> _levels = new();

        public static int Level(string itemId) => itemId != null && _levels.TryGetValue(itemId, out var l) ? l : 0;
        public static float Mult(string itemId) => 1f + 0.1f * Level(itemId);

        public static void Refresh(System.Action onDone = null)
        {
            BackendClient.Instance.GetJson("/api/forge", (ok, status, text) =>
            {
                if (ok && status == 200)
                {
                    _levels.Clear();
                    using var doc = JsonDocument.Parse(text);
                    foreach (var p in doc.RootElement.EnumerateObject()) _levels[p.Name] = p.Value.GetInt32();
                }
                onDone?.Invoke();
            });
        }

        public static void Set(string itemId, int level) => _levels[itemId] = level;

        // Bang chi phi - PHAI khop backend src/game/combatRules.js ENHANCE.
        public static readonly (float chance, int gold, (string id, int n)[] mats)[] Steps =
        {
            (1.0f, 100, new[] { ("thoi_sat", 1) }), (0.95f, 200, new[] { ("thoi_sat", 2) }), (0.9f, 350, new[] { ("thoi_sat", 3) }),
            (0.8f, 500, new[] { ("thoi_bac", 1) }), (0.7f, 800, new[] { ("thoi_bac", 2) }), (0.6f, 1200, new[] { ("thoi_bac", 3) }),
            (0.5f, 1800, new[] { ("thoi_vang", 1) }), (0.4f, 2600, new[] { ("thoi_vang", 2) }),
            (0.3f, 3600, new[] { ("thoi_vang", 3), ("kim_cuong", 1) }), (0.2f, 5000, new[] { ("thoi_vang", 4), ("kim_cuong", 2) }),
        };
    }
}
