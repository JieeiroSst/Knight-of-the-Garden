using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace HiepSiVeVuon.Systems
{
    // Bang truy na (server: backend bountyService) - lam moi moi 45s, dung de hien "TRUY NA" tren dau nguoi
    // choi khac (RemotePlayer) va tab Truy Na trong SocialUI.
    public partial class BountyBoard : Node
    {
        public static BountyBoard Instance { get; private set; }
        public readonly Dictionary<int, (string name, long amount, int kills)> Wanted = new();
        public long MyAmount { get; private set; }
        private double _in = 3.0;

        public override void _EnterTree() => Instance = this;

        public static long AmountOf(int playerId) =>
            Instance != null && Instance.Wanted.TryGetValue(playerId, out var w) ? w.amount : 0;

        public override void _Process(double delta)
        {
            _in -= delta;
            if (_in > 0) return;
            _in = 45.0;
            Refresh();
        }

        public void Refresh(System.Action done = null)
        {
            if (NetworkClient.Instance == null || !NetworkClient.Instance.Connected) return;
            BackendClient.Instance.GetJson("/api/bounties", (ok, status, text) =>
            {
                if (!ok || status != 200) return;
                using var doc = JsonDocument.Parse(text);
                Wanted.Clear();
                foreach (var b in doc.RootElement.GetProperty("top").EnumerateArray())
                    Wanted[b.GetProperty("playerId").GetInt32()] = (b.GetProperty("name").GetString(), b.GetProperty("amount").GetInt64(), b.GetProperty("kills").GetInt32());
                MyAmount = doc.RootElement.GetProperty("me").GetProperty("amount").GetInt64();
                done?.Invoke();
            });
        }
    }
}
