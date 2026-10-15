using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Gui chi so cho bang xep hang (POST /api/stats) - server chi nhan TANG va chan toi da/ngay.
    // Vang + diem dau truong server tu tinh, khong gui.
    public static class StatsReporter
    {
        private static readonly string[] Keys = { "fish_caught", "crops_harvested", "bosses_defeated", "monsters_killed", "ores_mined" };

        public static void Report(Action onDone = null)
        {
            if (BackendClient.Instance == null || !BackendClient.Instance.IsLoggedIn) { onDone?.Invoke(); return; }
            var stats = new Dictionary<string, long> { ["level"] = GameManager.Instance.Level };
            foreach (var k in Keys) stats[k] = AchievementSystem.Instance.GetStat(k);
            BackendClient.Instance.PostJson("/api/stats", JsonSerializer.Serialize(new { stats }), (_, _, _) => onDone?.Invoke());
        }
    }
}
