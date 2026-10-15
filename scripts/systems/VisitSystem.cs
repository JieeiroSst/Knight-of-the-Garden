using Godot;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Tham nong trai BAN BE: tai ruong cua ban (GET /api/visits/<id>/farm), HIEN ruong do thay cho ruong
    // minh (chi xem + tuoi ho), vao zone gateway farm_<id> de gap chu nha. Tuoi ho gui len server
    // (POST /water) - chu nong trai nhan o lan dong bo sau (TakeHelps). Ve nha -> khoi phuc ruong minh.
    public static class VisitSystem
    {
        public class Plot
        {
            public string Key;
            public string CropId;
            public int GrowStage, GrowDays;
            public bool Watered, IsGiant, Pest;
            public bool Freeform;
            public Vector3 Pos;
        }

        public static bool Active { get; private set; }
        public static int HostId { get; private set; }
        public static string HostName { get; private set; }
        public static readonly Dictionary<string, Plot> Plots = new();
        private static readonly List<string> _pendingWater = new();
        private static readonly List<Node> _tempPlots = new();
        public static Vector3 FarmCenter;

        public static string KeyOf(FarmPlot p) => p.GridX >= 0 ? $"g{p.GridX}_{p.GridY}" : $"f{Mathf.RoundToInt(p.FreeformPos.X)}_{Mathf.RoundToInt(p.FreeformPos.Z)}";

        public static void Start(int hostId, string hostName, SceneTree tree)
        {
            BackendClient.Instance.GetJson($"/api/visits/{hostId}/farm", (ok, status, text) =>
            {
                if (!ok || status != 200) { Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error"))); return; }
                if (Active) End(tree);
                Plots.Clear();
                using var doc = JsonDocument.Parse(text);
                foreach (var t in doc.RootElement.GetProperty("farm").EnumerateArray())
                {
                    bool ff = t.GetProperty("freeform").GetBoolean();
                    var p = new Plot
                    {
                        Freeform = ff,
                        CropId = t.GetProperty("cropId").ValueKind == JsonValueKind.String ? t.GetProperty("cropId").GetString() : null,
                        GrowStage = t.GetProperty("growStage").GetInt32(),
                        GrowDays = t.TryGetProperty("growDays", out var gd) && gd.ValueKind == JsonValueKind.Number ? gd.GetInt32() : 3,
                        Watered = t.GetProperty("watered").GetBoolean(),
                        IsGiant = t.GetProperty("isGiant").GetBoolean(),
                        Pest = t.GetProperty("pest").GetBoolean(),
                    };
                    if (ff)
                    {
                        float x = t.GetProperty("posX").GetSingle(), z = t.GetProperty("posZ").GetSingle();
                        p.Pos = new Vector3(x, 0, z);
                        p.Key = $"f{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(z)}";
                    }
                    else p.Key = $"g{t.GetProperty("x").GetInt32()}_{t.GetProperty("y").GetInt32()}";
                    Plots[p.Key] = p;
                }
                HostId = hostId;
                HostName = hostName;
                Active = true;
                // O tu do: an o cua minh, tao o tam cho o cua chu nha.
                foreach (Node n in tree.GetNodesInGroup("farm_plots"))
                    if (n is FarmPlot fp && fp.GridX < 0) fp.Visible = false;
                foreach (var p in Plots.Values)
                    if (p.Freeform)
                    {
                        var plot = FarmPlot.CreateVisitPlot(p.Pos, tree.CurrentScene);
                        _tempPlots.Add(plot);
                    }
                RefreshPlots(tree);
                (tree.GetFirstNodeInGroup("player") as Player)?.FastTravel(FarmCenter + new Vector3(0, 0, 140));
                Notify.Show(string.Format(Loc.T("visit.start_fmt"), hostName));
            });
        }

        public static void End(SceneTree tree)
        {
            if (!Active) return;
            FlushWater();
            Active = false;
            foreach (var n in _tempPlots) if (GodotObject.IsInstanceValid(n)) n.QueueFree();
            _tempPlots.Clear();
            foreach (Node n in tree.GetNodesInGroup("farm_plots"))
                if (n is FarmPlot fp) { if (fp.GridX < 0) fp.Visible = true; fp.RefreshVisual(); }
            Plots.Clear();
            Notify.Show(Loc.T("visit.end"));
        }

        public static void RefreshPlots(SceneTree tree)
        {
            foreach (Node n in tree.GetNodesInGroup("farm_plots"))
                if (n is FarmPlot fp) fp.RefreshVisual();
        }

        public static Plot Get(FarmPlot p) => Plots.TryGetValue(KeyOf(p), out var v) ? v : null;

        // Tuoi ho 1 o (chi o co cay, chua tuoi) - gom lai gui len server.
        public static void Water(FarmPlot p)
        {
            var v = Get(p);
            if (v == null || v.CropId == null) { Notify.Show(Loc.T("visit.nothing")); return; }
            if (v.Watered) { Notify.Show(Loc.T("visit.already_watered")); return; }
            v.Watered = true;
            _pendingWater.Add(v.Key);
            p.RefreshVisual();
            if (_pendingWater.Count >= 5) FlushWater();
            else p.GetTree().CreateTimer(3.0).Timeout += FlushWater;
        }

        private static void FlushWater()
        {
            if (_pendingWater.Count == 0 || HostId == 0) return;
            var keys = _pendingWater.ToArray();
            _pendingWater.Clear();
            BackendClient.Instance.PostJson($"/api/visits/{HostId}/water", JsonSerializer.Serialize(new { plots = keys }), (ok, status, text) =>
            {
                if (!ok || status != 200) { Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error"))); return; }
                using var doc = JsonDocument.Parse(text);
                int n = doc.RootElement.GetProperty("watered").GetInt32();
                if (n > 0)
                {
                    Notify.Show(string.Format(Loc.T("visit.watered_fmt"), n, HostName));
                    FriendshipBonus();
                }
            });
        }

        private static void FriendshipBonus() => SkillSystem.Instance.AddXp(Data.SkillType.Farming, 2);

        // Chu nong trai: lay cac o duoc ban tuoi ho hom nay va ap dung.
        public static void TakeHelps(SceneTree tree)
        {
            BackendClient.Instance.PostJson("/api/visits/helps/take", null, (ok, status, text) =>
            {
                if (!ok || status != 200) return;
                using var doc = JsonDocument.Parse(text);
                var byKey = new Dictionary<string, FarmPlot>();
                foreach (Node n in tree.GetNodesInGroup("farm_plots")) if (n is FarmPlot fp) byKey[KeyOf(fp)] = fp;
                int applied = 0;
                foreach (var h in doc.RootElement.EnumerateArray())
                    if (byKey.TryGetValue(h.GetProperty("plotKey").GetString(), out var plot)) { plot.AutoWater(); applied++; }
                if (applied > 0) Notify.Show(string.Format(Loc.T("visit.helped_fmt"), applied));
            });
        }
    }
}
