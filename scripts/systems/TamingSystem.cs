using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // Thuan hoa thu hoang ngoai the gioi mo:
    //  - SOI hoang: cho an THIT SONG bang [E] (moi lan +20-30%), du 100% -> thanh thu cung "Soi Xam" di theo,
    //    lao vao danh quai cung minh (xem PetSystem/Pet.cs).
    //  - Ngua hoang: xem Horse.TryTame (so ngua da thuan luu o HorseRegistry).
    public static class TamingSystem
    {
        private static readonly string[] Meats = { "thit_rung", "thit_bo", "thit_heo", "thit_cuu", "thit_de", "thit_ga" };
        private static readonly Dictionary<ulong, float> _wolfProgress = new();

        public static bool TryTameWolf(WildAnimal wolf)
        {
            string meat = null;
            foreach (var m in Meats) if (Inventory.Instance.CountOf(m) > 0) { meat = m; break; }
            if (meat == null) { Notify.Show(Loc.T("tame.need_food_wolf")); return true; }
            Inventory.Instance.RemoveItem(meat, 1);
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            ulong id = wolf.GetInstanceId();
            float p = (_wolfProgress.TryGetValue(id, out var v) ? v : 0f) + rng.RandfRange(20f, 30f);
            _wolfProgress[id] = p;
            if (p < 100f)
            {
                Notify.Show(string.Format(Loc.T("tame.wolf_progress_fmt"), Mathf.RoundToInt(p)));
                return true;
            }
            _wolfProgress.Remove(id);
            var pets = PetSystem.Instance;
            if (pets != null)
            {
                pets.Owned.Add("pet_soi");
                pets.SetActive("pet_soi");
            }
            WaterEcosystem.Instance?.OnPlayerCatch("wolf", 1f);
            wolf.QueueFree(); // con soi gio di theo nguoi choi duoi dang thu cung
            Notify.Show(Loc.T("tame.wolf_done"));
            return true;
        }
    }

    // Ngua da thuan hoa (luu qua SaveSystem): moi lan vao game dung lai canh chuong ngua nong trai.
    public static class HorseRegistry
    {
        public class TamedHorse
        {
            public string Id { get; set; }
            public float Speed { get; set; }
            public ulong GallopCooldownMs { get; set; }
            public string SpawnKey { get; set; } // cho sinh ngua hoang goc - khong sinh lai con nay nua
        }

        public static bool IsSpawnTamed(string key) => key != null && Horses.Exists(h => h.SpawnKey == key);

        public static readonly List<TamedHorse> Horses = new();
        private static readonly HashSet<string> _spawned = new();
        public static readonly Vector3 LocalHomePos = new(-180, 0, 260);
        public static Vector3 HomePos = LocalHomePos; // doi theo nong trai rieng (Main.RelocateFarm)

        public static void OnTamed(Horse h)
        {
            Horses.Add(new TamedHorse { Id = h.TamedId, Speed = h.RideSpeed, GallopCooldownMs = h.GallopCooldownMsOverride, SpawnKey = h.SpawnKey });
            _spawned.Add(h.TamedId);
        }

        public static void Load(List<TamedHorse> list)
        {
            Horses.Clear();
            if (list != null) Horses.AddRange(list);
            SpawnAll();
        }

        // Goi khi nap save va khi Main dung xong the gioi - con nao da co mat thi bo qua.
        public static void SpawnAll()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var scene = tree?.CurrentScene;
            if (scene == null || scene.GetNodeOrNull("WorldEnvironment") == null) return;
            var horseScene = GD.Load<PackedScene>("res://scenes/Horse.tscn");
            int i = 0;
            foreach (var t in Horses)
            {
                i++;
                if (_spawned.Contains(t.Id)) continue;
                var h = horseScene.Instantiate<Horse>();
                h.TamedId = t.Id;
                h.RideSpeed = t.Speed;
                h.GallopCooldownMsOverride = t.GallopCooldownMs;
                h.Position = HomePos + new Vector3((i % 4) * 45f, 0, (i / 4) * 45f);
                h.HomeCenter = HomePos;
                h.PastureHalfExtent = 160f;
                scene.AddChild(h);
                _spawned.Add(t.Id);
            }
        }

        public static void Reset() { Horses.Clear(); _spawned.Clear(); }
    }
}
