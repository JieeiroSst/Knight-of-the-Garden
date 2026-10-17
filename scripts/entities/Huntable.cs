using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Thanh phan "giet thit / san ban" gan vao vat nuoi trong trai (bo, heo, cuu, de, ga) va thu rung
    // (huou, tho, cao, soi, vit): co mau, trung don thi giat minh + so sat thuong, guc thi nga xuong va
    // ROI thit / da dong vat / long thu (nhat bang [E] hoac di qua - nguon "hunt", server gioi han
    // so luong moi gio). Ngua KHONG gan (la phuong tien cuoi).
    //
    // Vat nuoi da giet duoc ghi vao AnimalLedger (luu qua save) -> vao game lan sau KHONG hien lai (con
    // non moi sinh bu dan vao) - khong co thit vo han bang cach thoat/vao lai game.
    public partial class Huntable : Node
    {
        public string Species = "";
        public int MaxHp = 30;
        public bool IsFarmAnimal;
        public bool IsDead { get; private set; }
        private int _hp;
        private Node3D _animal;

        private static readonly Dictionary<string, (string item, int min, int max)[]> Drops = new()
        {
            ["cow"] = new[] { ("thit_bo", 3, 4), ("da_thu", 2, 2) },
            ["pig"] = new[] { ("thit_heo", 3, 4), ("da_thu", 1, 1) },
            ["sheep"] = new[] { ("thit_cuu", 2, 3), ("da_thu", 1, 1), ("wool", 1, 2) },
            ["goat"] = new[] { ("thit_de", 2, 3), ("da_thu", 1, 1), ("long_thu", 1, 1) },
            ["chicken"] = new[] { ("thit_ga", 1, 2), ("long_vu", 2, 4) },
            ["deer"] = new[] { ("thit_rung", 3, 3), ("da_thu", 2, 2) },
            ["rabbit"] = new[] { ("thit_rung", 1, 1), ("long_thu", 1, 1) },
            ["fox"] = new[] { ("long_thu", 2, 2), ("da_thu", 1, 1) },
            ["wolf"] = new[] { ("thit_rung", 1, 2), ("long_thu", 2, 2), ("da_thu", 1, 1) },
            ["duck"] = new[] { ("thit_ga", 1, 1), ("long_vu", 2, 3) },
            ["horse"] = new[] { ("thit_rung", 3, 4), ("da_thu", 3, 3) }, // chi ngua HOANG
        };

        public static bool CanHunt(string species) => Drops.ContainsKey(species);

        public static Huntable Of(Node n) => n?.GetNodeOrNull<Huntable>("Huntable");

        // Goi trong _Ready cua con vat. Tra ve false neu con nay da bi giet thit o phien truoc (tu xoa).
        public static bool Attach(Node3D animal, string species, int hp, bool farm)
        {
            if (!CanHunt(species)) return true;
            if (farm && AnimalLedger.ConsumePendingRemoval(species))
            {
                animal.QueueFree();
                return false;
            }
            var h = new Huntable { Name = "Huntable", Species = species, MaxHp = hp, IsFarmAnimal = farm };
            animal.AddChild(h);
            animal.AddToGroup("huntables");
            return true;
        }

        public override void _Ready()
        {
            _animal = GetParent<Node3D>();
            _hp = MaxHp;
        }

        // Tu keu thinh thoang khi nguoi choi o gan (loai chua co am thanh rieng: bo/ga da tu keu).
        private double _callIn = GD.RandRange(6.0, 25.0);
        public override void _Process(double delta)
        {
            if (IsDead || _animal == null || Species is "cow" or "chicken") return;
            _callIn -= delta;
            if (_callIn > 0) return;
            _callIn = GD.RandRange(14.0, 40.0);
            var p = _animal.GetTree().GetFirstNodeInGroup("player") as Node3D;
            if (p != null && p.GlobalPosition.DistanceTo(_animal.GlobalPosition) < 600f) AnimalSounds.Call(_animal, Species);
        }

        public void TakeHit(int dmg)
        {
            if (IsDead || _animal == null) return;
            _hp -= dmg;
            AnimalSounds.Hurt(_animal, Species);
            if (_animal is WildAnimal wild) wild.Provoke();
            var pop = new Label3D
            {
                Text = dmg.ToString(), Modulate = new Color(1f, 0.85f, 0.3f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.3f, NoDepthTest = true, OutlineSize = 6, FontSize = 36, Position = Vector3.Up * 30f,
            };
            _animal.AddChild(pop);
            var tw = pop.CreateTween().SetParallel();
            tw.TweenProperty(pop, "position:y", 48f, 0.5f);
            tw.TweenProperty(pop, "modulate:a", 0f, 0.5f);
            tw.Chain().TweenCallback(Callable.From(pop.QueueFree));
            if (_animal.GetNodeOrNull<Node3D>("Model") is Node3D model)
            {
                var jolt = model.CreateTween();
                jolt.TweenProperty(model, "position:y", model.Position.Y + 3f, 0.06f);
                jolt.TweenProperty(model, "position:y", model.Position.Y, 0.1f);
            }
            if (_hp <= 0) Die();
        }

        private void Die()
        {
            IsDead = true;
            _animal.RemoveFromGroup("huntables");
            // Dung AI/di chuyen, nga nghieng sang 1 ben roi chim xuong dat.
            _animal.SetPhysicsProcess(false);
            _animal.SetProcess(false);
            if (_animal is CollisionObject3D body) { body.CollisionLayer = 0; body.CollisionMask = 0; }
            var model = _animal.GetNodeOrNull<Node3D>("Model");
            foreach (var ap in FindAnimationPlayers(_animal)) ap.Pause();

            var rng = new RandomNumberGenerator();
            rng.Randomize();
            var at = _animal.GlobalPosition;
            foreach (var (item, min, max) in Drops[Species])
            {
                var jitter = new Vector3(rng.RandfRange(-14f, 14f), 0, rng.RandfRange(-14f, 14f));
                DroppedItem.Spawn(_animal.GetTree().CurrentScene, at + jitter, item, rng.RandiRange(min, max), "hunt");
            }
            if (IsFarmAnimal) AnimalLedger.OnSlaughter(Species);
            else WaterEcosystem.Instance?.OnPlayerCatch(Species, 1f);
            AchievementSystem.Instance?.Add("animals_hunted");

            var tw = _animal.CreateTween();
            if (model != null) tw.TweenProperty(model, "rotation:z", Mathf.Pi / 2f, 0.45f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tw.TweenInterval(2.5);
            tw.TweenProperty(_animal, "position:y", _animal.Position.Y - 20f, 1.2f);
            tw.TweenCallback(Callable.From(_animal.QueueFree));
        }

        private static IEnumerable<AnimationPlayer> FindAnimationPlayers(Node root)
        {
            if (root is AnimationPlayer ap) yield return ap;
            foreach (Node c in root.GetChildren())
                foreach (var x in FindAnimationPlayers(c)) yield return x;
        }
    }

    // So vat nuoi da giet thit (theo loai) - LUU qua SaveSystem. Moi phien choi: an dung chung ay con
    // truong thanh luc sinh ra; con non sinh tu giao phoi bu lai dan (OnBirth).
    public static class AnimalLedger
    {
        public static readonly Dictionary<string, int> Slaughtered = new();
        private static readonly Dictionary<string, int> _pending = new();

        public static void OnSlaughter(string species) =>
            Slaughtered[species] = (Slaughtered.TryGetValue(species, out var n) ? n : 0) + 1;

        public static void OnBirth(string species)
        {
            if (Slaughtered.TryGetValue(species, out var n) && n > 0) Slaughtered[species] = n - 1;
        }

        public static bool ConsumePendingRemoval(string species)
        {
            if (!_pending.TryGetValue(species, out var n) || n <= 0) return false;
            _pending[species] = n - 1;
            return true;
        }

        // Nap save: dat lai so cho xoa + xoa ngay nhung con da co mat trong the gioi.
        public static void Load(Dictionary<string, int> data, SceneTree tree)
        {
            Slaughtered.Clear();
            _pending.Clear();
            if (data != null)
                // Nong trai thu gon chi con 2 con/loai: an toi da 1 con (con lai sinh bu - Main.TryBreed*).
                foreach (var (k, v) in data) if (v > 0) { Slaughtered[k] = 1; _pending[k] = 1; }
            if (tree == null) return;
            foreach (Node n in tree.GetNodesInGroup("huntables"))
            {
                if (Huntable.Of(n) is not Huntable h || !h.IsFarmAnimal || h.IsDead) continue;
                if (ConsumePendingRemoval(h.Species)) n.QueueFree();
            }
        }
    }
}
