using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ky nang chien dau (phim 1/2/3), mo khoa theo cap nhan vat, ton the luc + hoi chieu:
    //  1. Luot  (cap 1): lao nhanh ve phia truoc, mien sat thuong 0.4s.
    //  2. Chem Xoay (cap 3): gay 2x sat thuong vu khi cho moi quai trong ban kinh 80.
    //  3. Hoi Phuc (cap 5): hoi 30% mau toi da.
    public partial class PlayerAbilities : Node
    {
        public record Ability(string Key, string Action, int Level, float Cooldown, float Stamina);

        public static readonly Ability[] All =
        {
            new("dash", "ability_1", 1, 3f, 6f),
            new("whirl", "ability_2", 3, 6f, 10f),
            new("heal", "ability_3", 5, 30f, 15f),
        };

        private Player _player;
        private readonly double[] _cdLeft = new double[3];

        public float CooldownLeft(int i) => (float)_cdLeft[i];

        public override void _Ready()
        {
            _player = GetParent<Player>();
            AddToGroup("player_abilities");
        }

        public override void _Process(double delta)
        {
            for (int i = 0; i < 3; i++) if (_cdLeft[i] > 0) _cdLeft[i] -= delta;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (ChatUI.IsTyping || _player.IsDowned) return;
            for (int i = 0; i < All.Length; i++)
                if (e.IsActionPressed(All[i].Action)) { Use(i); GetViewport().SetInputAsHandled(); return; }
        }

        private void Use(int i)
        {
            var a = All[i];
            var gm = GameManager.Instance;
            if (gm.Level < a.Level) { Notify.Show(string.Format(Loc.T("ability.locked_fmt"), Loc.T("ability." + a.Key), a.Level)); return; }
            if (_cdLeft[i] > 0) return;
            if (!gm.UseStamina(a.Stamina)) { Notify.Show(Loc.T("stamina.too_tired")); return; }
            _cdLeft[i] = a.Cooldown;
            switch (a.Key)
            {
                case "dash": Dash(); break;
                case "whirl": Whirl(); break;
                case "heal": Heal(); break;
            }
        }

        private void Dash()
        {
            var dir = _player.PlayRoll();
            gmInvuln(0.45);
            var tw = _player.CreateTween();
            var target = _player.GlobalPosition + dir * 160f;
            // Luot bang MoveAndCollide tung buoc nho de khong xuyen tuong.
            tw.TweenMethod(Callable.From<float>(_ => _player.MoveAndCollide(dir * 16f)), 0f, 1f, 0.15f);
            Burst(_player.GlobalPosition + Vector3.Up * 10f, new Color(0.7f, 0.9f, 1f), 18, 40f);
        }

        private static void gmInvuln(double seconds) => GameManager.Instance.SetInvulnerable(seconds);

        private void Whirl()
        {
            _player.PlaySpin();
            int dmg = Inventory.Instance.GetWeaponDamage() * 2;
            int hits = 0;
            foreach (Node n in GetTree().GetNodesInGroup("enemies"))
                if (n is Enemy e && IsInstanceValid(e) && !e.IsDead && e.GlobalPosition.DistanceTo(_player.GlobalPosition) <= 80f)
                {
                    e.TakeDamage(dmg);
                    hits++;
                }
            Burst(_player.GlobalPosition + Vector3.Up * 18f, new Color(1f, 0.85f, 0.4f), 60, 90f);
            if (hits > 0) Notify.Show(string.Format(Loc.T("ability.whirl_hit_fmt"), hits));
        }

        private void Heal()
        {
            var gm = GameManager.Instance;
            _player.PlayEmote("Interact");
            gm.Heal(Mathf.RoundToInt(gm.MaxHp * 0.3f));
            Burst(_player.GlobalPosition + Vector3.Up * 20f, new Color(0.4f, 1f, 0.5f), 40, 50f);
        }

        private void Burst(Vector3 pos, Color color, int amount, float speed)
        {
            var p = new CpuParticles3D
            {
                OneShot = true, Emitting = true, Amount = amount, Lifetime = 0.6f, Explosiveness = 0.95f,
                Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = speed * 0.5f, InitialVelocityMax = speed, Gravity = Vector3.Zero,
                Color = color,
                Mesh = new SphereMesh { Radius = 1.5f, Height = 3f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, EmissionEnabled = true, Emission = color } },
            };
            GetTree().CurrentScene.AddChild(p);
            p.GlobalPosition = pos;
            GetTree().CreateTimer(1.5).Timeout += () => { if (IsInstanceValid(p)) p.QueueFree(); };
        }
    }
}
