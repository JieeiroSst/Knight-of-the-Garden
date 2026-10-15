using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Moi ngay moi (va 1 lan luc vao game) xoa do hai luom cu, rai ngau nhien do hai luom MOI theo
    // mua (data/forage.json) trong cac vung Main.cs dang ky (rung, canh dong, quanh nong trai).
    // Do hai luom la DroppedItem (IsForage=true) - cham vao/bam [E] de nhat, cong EXP Hai Luom.
    public partial class ForageSpawner : Node3D
    {
        public readonly List<(Vector3 center, float radius, int count)> Areas = new();

        private bool _pendingSpawn = true;

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += _ => _pendingSpawn = true;
        }

        // Goi lai sau khi nap save (Day/mua co the da doi so voi luc vao game).
        public void RequestRespawn() => _pendingSpawn = true;

        // Raycast tim mat dat can chay trong buoc vat ly (DirectSpaceState chi an toan o day).
        public override void _PhysicsProcess(double delta)
        {
            if (!_pendingSpawn) return;
            _pendingSpawn = false;
            Respawn();
        }

        private void Respawn()
        {
            foreach (Node n in GetTree().GetNodesInGroup("forage_items")) n.QueueFree();

            string season = GameManager.Instance.CurrentSeason.ToString();
            var pool = ItemDatabase.Instance.Forage
                .Where(f => f.Seasons == null || f.Seasons.Length == 0 || f.Seasons.Contains(season))
                .ToList();
            if (pool.Count == 0) return;
            int totalWeight = pool.Sum(f => f.Weight);

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            var space = GetWorld3D().DirectSpaceState;
            var parent = GetTree().CurrentScene;

            foreach (var (center, radius, count) in Areas)
            {
                int placed = 0;
                for (int attempt = 0; attempt < count * 4 && placed < count; attempt++)
                {
                    float ang = rng.Randf() * Mathf.Tau;
                    float r = Mathf.Sqrt(rng.Randf()) * radius;
                    var pos = center + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);

                    // Bo vi tri nam TREN vat the khac (mai nha/tang da/cay) - chi rai tren mat dat.
                    var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 600f, pos + Vector3.Down * 50f));
                    if (hit.Count > 0)
                    {
                        float y = ((Vector3)hit["position"]).Y;
                        if (y > 30f) continue;
                        pos.Y = y;
                    }

                    int pick = rng.RandiRange(0, totalWeight - 1);
                    var def = pool.First();
                    foreach (var f in pool) { if (pick < f.Weight) { def = f; break; } pick -= f.Weight; }

                    var drop = new DroppedItem { ItemId = def.ItemId, Amount = 1, IsForage = true, Source = "forage" };
                    drop.AddToGroup("forage_items");
                    parent.AddChild(drop);
                    drop.GlobalPosition = pos;
                    placed++;
                }
            }
        }
    }
}
