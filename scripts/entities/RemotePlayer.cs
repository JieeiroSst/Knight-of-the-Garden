using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Nguoi choi KHAC trong cung kenh (du lieu tu gateway qua NetworkClient): model + mau tu tao, ten
    // tren dau, noi suy muot giua cac goi vi tri (5 lan/giay) de khong bi giat. Lop va cham rieng (8):
    // Player bam [E] vao duoc (giao dich/nhan rieng) nhung than nguoi khong chan nhau.
    public partial class RemotePlayer : StaticBody3D
    {
        public const int NetLayer = 8;

        public int PlayerId;
        public string DisplayName = "";
        public Appearance Look = new();

        private Node3D _model;
        private AnimationPlayer _anim;
        private System.Collections.Generic.Dictionary<string, string> _anims = new();
        private Vector3 _target;
        private float _targetYaw;
        private int _animCode;
        private int _lastMotionCode = -1;
        private ArmPoseModifier _armPose;
        private string _current = "";
        private Label3D _nameLabel;
        private Label3D _bubble;
        private ulong _bubbleUntil;

        public override void _Ready()
        {
            AddToGroup("remote_players");
            CollisionLayer = 0;
            CollisionMask = 0;
            SetCollisionLayerValue(NetLayer, true);
            AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 12f, Height = 40f }, Position = Vector3.Up * 20f });

            _model = new Node3D { Name = "Model" };
            AddChild(_model);
            var def = CharacterCatalog.Get(Look.ModelId);
            _anim = CharacterCatalog.AttachModel(_model, def);
            CharacterCatalog.ApplyColors(_model, Look);
            if (!string.IsNullOrEmpty(Look.Outfit)) new OutfitRig(_model, Look.Gender).Apply(Look, Look.Outfit);
            _anims = CharacterCatalog.ResolveAnims(_anim);
            if (CharacterRig.FindSkeleton(_model) is Skeleton3D sk)
            {
                _armPose = new ArmPoseModifier();
                sk.AddChild(_armPose);
                _armPose.Setup(sk, _model);
            }

            _nameLabel = new Label3D
            {
                Text = DisplayName, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.25f,
                Position = Vector3.Up * 58f, OutlineSize = 8, FontSize = 40, NoDepthTest = true,
                Modulate = new Color(0.75f, 0.95f, 1f),
            };
            AddChild(_nameLabel);
            _bubble = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.22f, Position = Vector3.Up * 72f,
                OutlineSize = 8, FontSize = 36, NoDepthTest = true, Visible = false, Width = 600, AutowrapMode = TextServer.AutowrapMode.Word,
            };
            AddChild(_bubble);
            _target = Position;
        }

        public void SetState(Vector3 pos, float yaw, int animCode, bool snap = false)
        {
            _target = pos;
            _targetYaw = yaw;
            _animCode = animCode;
            if (snap) Position = pos;
        }

        // Bong bong chat tren dau 5 giay.
        public void Say(string text)
        {
            _bubble.Text = text.Length > 60 ? text[..60] + "..." : text;
            _bubble.Visible = true;
            _bubbleUntil = Time.GetTicksMsec() + 5000;
        }

        public override void _Process(double delta)
        {
            float t = Mathf.Clamp((float)delta * 10f, 0f, 1f);
            // Lech qua xa (vua dich chuyen/nap lai) thi nhay thang thay vi truot qua ca ban do.
            Position = Position.DistanceTo(_target) > 400f ? _target : Position.Lerp(_target, t);
            var targetBasis = new Basis(Vector3.Up, _targetYaw);
            _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, t);

            // Cung bang ma voi Player.NetAnimRoles (combo, lan, trung don, nga, the thu kiem...).
            string role = _animCode >= 0 && _animCode < Player.NetAnimRoles.Length ? Player.NetAnimRoles[_animCode] : "Idle";
            // Dong tac tu dung (cuoc, tuoi, quang can...): dung Idle + lop xoay tay giong nguoi choi.
            if (role.StartsWith("Motion:"))
            {
                if (_animCode != _lastMotionCode && _armPose != null && Motions.Get(role[7..]) is ArmPoseModifier.Motion mo) _armPose.Play(mo);
                role = "Idle";
            }
            _lastMotionCode = role == "Idle" && _animCode >= 14 ? _animCode : -1;
            string anim = _anims.TryGetValue(role, out var a) ? a : (_anims.TryGetValue("Idle", out var i) ? i : "");
            if (_anim != null && anim != "" && anim != _current)
            {
                _anim.Play(anim, 0.15);
                _current = anim;
            }
            if (_bubble.Visible && Time.GetTicksMsec() > _bubbleUntil) _bubble.Visible = false;
        }
    }
}
