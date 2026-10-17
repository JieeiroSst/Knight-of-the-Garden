using Godot;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Tham nong trai NGUOI KHAC: moi nguoi co nong trai o toa do rieng (FarmSpace/FarmReplicas). Tai ruong
    // cong khai cua chu nha (GET /api/farms/<id>/view) -> dung o dat TAM dung vi tri tren nong trai cua ho
    // (chi xem + tuoi ho - server chi nhan tuoi ho cua ban be), vao zone gateway farm_<id> de gap chu nha.
    // Tuoi ho gui len server (POST /water) - chu nong trai nhan o lan dong bo sau (TakeHelps).
    public static class VisitSystem
    {
        public class Plot
        {
            public string Key;
            public string CropId;
            public int GrowStage, GrowDays;
            public bool Watered, IsGiant, Pest;
            public bool Freeform;
            public int GridX = -1, GridY = -1;
            public Vector3 Pos;
        }

        public static bool Active { get; private set; }
        public static int HostId { get; private set; }
        public static string HostName { get; private set; }
        public static readonly Dictionary<string, Plot> Plots = new();
        private static readonly List<string> _pendingWater = new();
        private static readonly List<Node> _tempPlots = new();
        public static Vector3 FarmCenter;
        // Goc nong trai cua chu nha dang tham (toa do the gioi).
        public static Vector3 HostOrigin { get; private set; }

        public static string KeyOf(FarmPlot p) => p.GridX >= 0 ? $"g{p.GridX}_{p.GridY}" : $"f{Mathf.RoundToInt(p.FreeformPos.X)}_{Mathf.RoundToInt(p.FreeformPos.Z)}";

        // travel = dich chuyen toi nong trai cua chu nha (tu menu ban be); false = da dung san trong do
        // (FarmReplicas tu tham khi buoc vao tuong nong trai nguoi khac).
        public static void Start(int hostId, string hostName, SceneTree tree, bool travel = true, System.Action onDone = null)
        {
            BackendClient.Instance.GetJson($"/api/farms/{hostId}/view", (ok, status, text) =>
            {
                onDone?.Invoke();
                if (!ok || status != 200) { Notify.Show(BackendClient.ErrorOf(text, Loc.T("net.server_error"))); return; }
                if (Active) End(tree, quiet: true);
                Plots.Clear();
                using var doc = JsonDocument.Parse(text);
                var hostOrigin = new Vector3((float)doc.RootElement.GetProperty("x").GetDouble(), 0, (float)doc.RootElement.GetProperty("z").GetDouble());
                if (hostOrigin == Vector3.Zero || hostOrigin == FarmSpace.Origin) { Notify.Show(Loc.T("visit.no_farm")); return; }
                // Vi tri o luoi co dinh = cung vi tri tren nong trai cua minh (moi nong trai dung giong nhau).
                var gridLocal = new Dictionary<string, Vector3>();
                foreach (Node n in tree.GetNodesInGroup("farm_plots"))
                    if (n is FarmPlot mine && mine.GridX >= 0 && !mine.IsVisitTemp) gridLocal[KeyOf(mine)] = mine.GlobalPosition - FarmSpace.Origin;
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
                        var saved = new Vector3(x, 0, z);
                        // Save cu luu toa do nong trai cu (quanh goc) -> doi ra nong trai cua chu nha.
                        p.Pos = FarmSpace.InLocalFarmArea(saved) ? hostOrigin + saved : saved;
                        p.Key = $"f{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(z)}";
                    }
                    else
                    {
                        int gx = t.GetProperty("x").GetInt32(), gy = t.GetProperty("y").GetInt32();
                        p.Key = $"g{gx}_{gy}";
                        p.GridX = gx; p.GridY = gy;
                        if (!gridLocal.TryGetValue(p.Key, out var local)) continue;
                        p.Pos = hostOrigin + local;
                    }
                    Plots[p.Key] = p;
                }
                HostId = hostId;
                HostName = hostName;
                HostOrigin = hostOrigin;
                Active = true;
                // O dat TAM dung vi tri tren nong trai cua chu nha (ruong cua minh o xa, khong dung toi).
                foreach (var p in Plots.Values)
                {
                    var plot = FarmPlot.CreateVisitPlot(p.Pos, tree.CurrentScene);
                    if (!p.Freeform) { plot.GridX = p.GridX; plot.GridY = p.GridY; }
                    else plot.FreeformPos = KeyPos(p.Key);
                    _tempPlots.Add(plot);
                }
                RefreshPlots(tree);
                if (travel) (tree.GetFirstNodeInGroup("player") as Player)?.FastTravel(hostOrigin + FarmSpace.LocalSpawn);
                Notify.Show(string.Format(Loc.T("visit.start_fmt"), hostName));
            });
        }

        // Khoa "f<x>_<z>" -> FreeformPos (de KeyOf cua o tam khop khoa cua chu nha).
        private static Vector3 KeyPos(string key)
        {
            var parts = key[1..].Split('_');
            return new Vector3(int.Parse(parts[0]), 0, int.Parse(parts[1]));
        }

        public static void End(SceneTree tree, bool quiet = false)
        {
            if (!Active) return;
            FlushWater();
            Active = false;
            foreach (var n in _tempPlots) if (GodotObject.IsInstanceValid(n)) n.QueueFree();
            _tempPlots.Clear();
            foreach (Node n in tree.GetNodesInGroup("farm_plots"))
                if (n is FarmPlot fp && !fp.IsVisitTemp) fp.RefreshVisual();
            Plots.Clear();
            if (!quiet) Notify.Show(Loc.T("visit.end"));
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
