using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Thu cung dang di theo nguoi choi: theo sau, lao vao danh quai gan chu, tu nhat do roi gan do.
    // Khong co va cham vat ly (khong vuong chan nguoi choi) - di chuyen bang noi suy vi tri.
    public partial class Pet : Node3D
    {
        public string PetId;
        private PetCatalog.PetDef _def;
        private Node3D _model;
        private AnimationPlayer _anim;
        private Dictionary<string, string> _anims = new();
        private Enemy _target;
        private double _attackCd;
        private double _scanCd;
        private string _current = "";

        private static readonly string[] IdleNames = { "Idle", "Flying_Idle", "Idle_2" };
        private static readonly string[] MoveNames = { "Walk", "Gallop", "Fast_Flying", "Run" };
        private static readonly string[] AttackNames = { "Attack", "Bite_Front", "Punch", "Headbutt" };

        public override void _Ready()
        {
            AddToGroup("pets");
            _def = PetCatalog.Pets[PetId];
            _model = ModelUtil.Instance(_def.Model);
            if (_model == null) return;
            ModelUtil.FitToHeight(_model, _def.Height);
            var holder = new Node3D { Name = "Body" };
            holder.AddChild(_model);
            AddChild(holder);
            _model = holder;
            _anim = FindAnim(holder);
            if (_anim != null)
            {
                foreach (var a in _anim.GetAnimationList())
                {
                    string shortName = a.Contains('|') ? a[(a.LastIndexOf('|') + 1)..] : a;
                    _anims.TryAdd(shortName, a);
                }
                foreach (var n in IdleNames) if (_anims.TryGetValue(n, out var loop) && _anim.GetAnimation(loop) is Animation an) an.LoopMode = Animation.LoopModeEnum.Linear;
                foreach (var n in MoveNames) if (_anims.TryGetValue(n, out var loop) && _anim.GetAnimation(loop) is Animation an) an.LoopMode = Animation.LoopModeEnum.Linear;
            }
            // Ten + cap tren dau.
            AddChild(new Label3D
            {
                Text = $"{PetSystem.DisplayName(PetId)} Lv{PetSystem.Instance.Level(PetId)}", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.2f, FontSize = 32, OutlineSize = 6, NoDepthTest = true, Position = Vector3.Up * (_def.Height + 12f + (_def.Flying ? 14f : 0f)),
                Modulate = new Color(1f, 0.9f, 0.6f),
            });
        }

        private static AnimationPlayer FindAnim(Node n)
        {
            if (n is AnimationPlayer ap) return ap;
            foreach (Node c in n.GetChildren()) if (FindAnim(c) is AnimationPlayer f) return f;
            return null;
        }

        private void Play(string[] names)
        {
            if (_anim == null) return;
            foreach (var n in names)
                if (_anims.TryGetValue(n, out var a))
                {
                    if (a != _current) { _anim.Play(a, 0.15); _current = a; }
                    return;
                }
        }

        public override void _Process(double delta)
        {
            if (GetTree().GetFirstNodeInGroup("player") is not Player player) return;
            float dt = (float)delta;
            _attackCd -= delta;
            _scanCd -= delta;
            if (_scanCd <= 0) { _scanCd = 0.4; Scan(player); }

            Vector3 goal;
            if (_target != null && IsInstanceValid(_target) && !_target.IsDead)
                goal = _target.GlobalPosition + (GlobalPosition - _target.GlobalPosition).Normalized() * 22f;
            else
            {
                _target = null;
                goal = player.GlobalPosition - player.Facing * 35f + player.Facing.Cross(Vector3.Up) * 22f;
            }
            float hover = _def.Flying ? 22f + Mathf.Sin((float)Time.GetTicksMsec() / 300f) * 3f : 0f;
            goal.Y = player.GlobalPosition.Y + hover;
            var to = goal - GlobalPosition;
            float dist = new Vector2(to.X, to.Z).Length();
            if (dist > 900f) { GlobalPosition = goal; return; } // bi bo xa (dich chuyen) -> nhay theo
            float speed = dist > 120f ? 220f : 120f;
            bool moving = dist > 8f;
            if (moving) GlobalPosition += to.Normalized() * Mathf.Min(dist, speed * dt);
            else GlobalPosition = GlobalPosition with { Y = Mathf.Lerp(GlobalPosition.Y, goal.Y, 0.2f) };
            var face = _target != null ? _target.GlobalPosition - GlobalPosition : to;
            face.Y = 0;
            if (face.LengthSquared() > 1f) _model.Basis = _model.Basis.Orthonormalized().Slerp(new Basis(Vector3.Up, Mathf.Atan2(face.X, face.Z)), Mathf.Clamp(dt * 8f, 0f, 1f));

            if (_target != null && dist < 30f && _attackCd <= 0)
            {
                _attackCd = 1.0;
                int dmg = Mathf.RoundToInt(_def.BaseDamage * (1f + 0.15f * (PetSystem.Instance.Level(PetId) - 1)));
                _target.TakeDamage(dmg);
                Play(AttackNames);
                if (_target.IsDead) PetSystem.Instance.AddXp(PetId, 10);
            }
            else if (_current == "" || !_anim.IsPlaying() || !AttackNamesContains(_current)) Play(moving ? MoveNames : IdleNames);
        }

        private bool AttackNamesContains(string a)
        {
            foreach (var n in AttackNames) if (a.EndsWith(n)) return true;
            return false;
        }

        // Tim quai gan CHU (<=160) de tan cong + nhat do roi gan thu cung (<=45).
        private void Scan(Player player)
        {
            if (_target == null)
            {
                float best = 160f;
                foreach (Node n in GetTree().GetNodesInGroup("enemies"))
                    if (n is Enemy e && !e.IsDead && e.GlobalPosition.DistanceTo(player.GlobalPosition) < best)
                    {
                        best = e.GlobalPosition.DistanceTo(player.GlobalPosition);
                        _target = e;
                    }
            }
            foreach (Node n in GetTree().GetNodesInGroup("dropped_items"))
                if (n is DroppedItem d && IsInstanceValid(d) && d.GlobalPosition.DistanceTo(GlobalPosition) < 45f && !d.IsQueuedForDeletion())
                    d.PickUp();
        }
    }
}
