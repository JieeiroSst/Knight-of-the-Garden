using Godot;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // Nguoi choi KHAC trong cung kenh (du lieu tu gateway qua NetworkClient): model + mau tu tao, ten
    // tren dau, noi suy muot giua cac goi vi tri de khong bi giat. Lop va cham rieng (8): Player bam
    // [E] vao duoc (giao dich/nhan rieng) nhung than nguoi khong chan nhau.
    //
    // Toi uu khi DONG nguoi (toi 80/kenh):
    //  - Noi suy theo "anh chup" (snapshot): ve nhan vat tre InterpDelay giay so voi goi moi nhat, noi
    //    giua 2 goi that -> chuyen dong deu dan du mang chap chon, khong "truot cao su" nhu Lerp cu.
    //  - LOD theo khoang cach: gan = day du; vua = tat bong do + hoat canh 1/3 khung hinh; xa = an
    //    model, dung hoat canh (chi con cap nhat vi tri, gan nhu khong ton gi).
    //  - Giong noi (VoiceChat) phat tu AudioStreamPlayer3D tren nhan vat -> nghe theo huong/khoang cach.
    public partial class RemotePlayer : StaticBody3D
    {
        public const int NetLayer = 8;

        public int PlayerId;
        public int GuildId;
        public string PartyId = "";
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
        private long _shownBounty;
        private Label3D _bubble;
        private ulong _bubbleUntil;

        // ==== Noi suy snapshot ====
        private const double InterpDelay = 0.3; // = nhip gui 0.2s cua client + nhip gom WS_TICK_MS
        private const double MaxExtrapolate = 0.2;
        private readonly struct Snap
        {
            public readonly double T; public readonly Vector3 P; public readonly float Yaw;
            public Snap(double t, Vector3 p, float yaw) { T = t; P = p; Yaw = yaw; }
        }
        private readonly System.Collections.Generic.List<Snap> _snaps = new(8);
        private static double Now => Time.GetTicksUsec() / 1_000_000.0;

        // ==== LOD ====
        public enum Lod { Near, Mid, Far }
        public Lod CurrentLod { get; private set; } = Lod.Near;
        private float _lodTimer;
        private int _animFrame;
        private double _animAccum;
        private float NearDist => 700f * GraphicsSettings.DrawDistanceScale;
        private float MidDist => 1800f * GraphicsSettings.DrawDistanceScale;

        // ==== Giong noi ====
        private AudioStreamPlayer3D _voice;
        private AudioStreamGeneratorPlayback _voicePlayback;
        private readonly System.Collections.Generic.Queue<float> _voiceQueue = new();
        private bool _voiceStarted;
        private ulong _lastVoiceMs;
        private bool _shownSpeaking;
        private bool _shownFriendly;
        private bool _nameDirty = true;

        // ==== Thanh mau PvP (server phat "hpbar" khi nguoi nay trung don/hoi mau) ====
        private Label3D _hpLabel;
        private ulong _hpUntil;

        // ==== Cuoi ngua (bit 32 cua ma hoat canh) ====
        private const string HorseModel = "res://assets3d/quaternius/animals/horse.glb";
        private Node3D _mount;
        private AnimationPlayer _mountAnim;
        private string _mountAnimCur = "";
        private Vector3 _prevPos;
        private float _speed;
        public bool IsMounted => (_animCode & 32) != 0;
        private int Role => _animCode & 31;

        private void UpdateMount(float dt)
        {
            float inst = dt > 0f ? new Vector2(Position.X - _prevPos.X, Position.Z - _prevPos.Z).Length() / dt : 0f;
            _prevPos = Position;
            _speed = Mathf.Lerp(_speed, Mathf.Min(inst, 600f), Mathf.Clamp(dt * 6f, 0f, 1f));
            bool mounted = IsMounted && !IsHidden && CurrentLod != Lod.Far;
            if (mounted && _mount == null)
            {
                _mount = new Node3D { Name = "Mount" };
                AddChild(_mount);
                _mountAnim = CharacterRig.Attach(_mount, HorseModel, 4.6f);
                _model.Scale = Vector3.One * 0.88f; // ngoi thap tren yen, giong nguoi choi
            }
            else if (!IsMounted && _mount != null)
            {
                _mount.QueueFree();
                _mount = null;
                _mountAnim = null;
                _mountAnimCur = "";
                _model.Scale = Vector3.One;
            }
            if (_mount == null) return;
            _mount.Visible = mounted;
            // Nguoi = goc o yen (xem Horse.SeatOffset) -> ngua nam thap hon + lui ra sau theo huong nhin.
            _mount.Basis = _model.Basis;
            // _model.Basis = Basis(Up, yaw): truc +Z cuc bo = huong mat (giong Basis.LookingAt(-facing) cua model Quaternius lat nguoc).
            var fwd = _model.Basis * Vector3.Back;
            _mount.Position = -Vector3.Up * Horse.SeatOffset.Y - fwd * Horse.SeatOffset.Z;
            string anim = _speed > 170f ? "Armature|Run" : _speed > 35f ? "Armature|Walk" : "Armature|Idle";
            if (_mountAnim != null && anim != _mountAnimCur && _mountAnim.HasAnimation(anim))
            {
                _mountAnim.Play(anim, 0.2);
                _mountAnimCur = anim;
                if (_mountAnim.GetAnimation(anim) is Animation a) a.LoopMode = Animation.LoopModeEnum.Linear;
            }
        }

        // Dang an trong bui co/leu (server bao "vanish") - an model + ten, khong bi tu dong nham danh.
        public bool IsHidden { get; private set; }

        public void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            if (_model != null) _model.Visible = !hidden && CurrentLod != Lod.Far;
            if (_nameLabel != null) _nameLabel.Visible = !hidden && CurrentLod != Lod.Far;
            if (hidden && _hpLabel != null) _hpLabel.Visible = false;
            if (_mount != null) _mount.Visible = !hidden && CurrentLod != Lod.Far;
        }

        public void ShowHp(int hp, int max)
        {
            if (_hpLabel == null) return;
            float r = max > 0 ? Mathf.Clamp((float)hp / max, 0f, 1f) : 0f;
            int filled = Mathf.RoundToInt(r * 12);
            _hpLabel.Text = new string('|', filled) + new string('.', 12 - filled) + $"  {hp}/{max}";
            _hpLabel.Modulate = r > 0.5f ? new Color(0.5f, 1f, 0.45f) : r > 0.25f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.3f, 0.25f);
            _hpLabel.Visible = true;
            _hpUntil = Time.GetTicksMsec() + 6000;
        }
        private const int VoicePrebuffer = HiepSiVeVuon.Systems.VoiceChat.SampleRate / 8; // 125 ms dem chong giat
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
            _hpLabel = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.2f, Position = Vector3.Up * 66f,
                OutlineSize = 6, FontSize = 30, NoDepthTest = true, Visible = false,
            };
            AddChild(_hpLabel);
            _target = Position;

            _voice = new AudioStreamPlayer3D
            {
                Stream = new AudioStreamGenerator { MixRate = HiepSiVeVuon.Systems.VoiceChat.SampleRate, BufferLength = 0.5f },
                Bus = HiepSiVeVuon.Systems.VoiceChat.OutBus,
                Position = Vector3.Up * 45f,
                // Don vi the gioi lon (nguoi cao ~40) -> UnitSize lon de tieng nho dan tu nhien trong ~45 m.
                UnitSize = 140f,
                MaxDistance = HiepSiVeVuon.Systems.VoiceChat.VoiceRange,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            };
            AddChild(_voice);
        }

        public void SetState(Vector3 pos, float yaw, int animCode, bool snap = false)
        {
            _target = pos;
            _targetYaw = yaw;
            _animCode = animCode;
            double now = Now;
            // Dich chuyen/nap lai (lech qua xa) -> nhay thang, bo dem cu.
            if (snap || _snaps.Count == 0 || _snaps[^1].P.DistanceTo(pos) > 400f)
            {
                _snaps.Clear();
                Position = pos;
                _snaps.Add(new Snap(now - InterpDelay, pos, yaw));
            }
            // Client chi gui khi di chuyen: sau 1 luc dung yen, chen lai diem dung yen ngay truoc goi
            // moi - neu khong se noi tu 1 diem rat cu va nhan vat "troi" cham chap.
            else if (now - _snaps[^1].T > 0.5)
                _snaps.Add(new Snap(now - 0.2, _snaps[^1].P, _snaps[^1].Yaw));
            _snaps.Add(new Snap(now, pos, yaw));
            if (_snaps.Count > 8) _snaps.RemoveRange(0, _snaps.Count - 8);
        }

        // Mau giong noi da giai nen (16 kHz) tu VoiceChat.
        public void PushVoice(float[] samples)
        {
            // Qua xa / bi an (LOD Far) van nghe neu trong tam voice - server da loc khoang cach.
            foreach (var s in samples) _voiceQueue.Enqueue(s);
            // Chong tre tich luy: dem qua 0.6s thi bo bot phan cu nhat.
            while (_voiceQueue.Count > HiepSiVeVuon.Systems.VoiceChat.SampleRate * 0.6f) _voiceQueue.Dequeue();
            _lastVoiceMs = Time.GetTicksMsec();
        }

        public bool IsSpeaking => Time.GetTicksMsec() - _lastVoiceMs < 350;

        private void PumpVoice()
        {
            if (_voiceQueue.Count == 0)
            {
                _voiceStarted = false;
                return;
            }
            if (!_voiceStarted)
            {
                if (_voiceQueue.Count < VoicePrebuffer) return;
                _voiceStarted = true;
                if (!_voice.Playing) _voice.Play();
                _voicePlayback = _voice.GetStreamPlayback() as AudioStreamGeneratorPlayback;
            }
            if (_voicePlayback == null) return;
            int n = Mathf.Min(_voicePlayback.GetFramesAvailable(), _voiceQueue.Count);
            if (n <= 0) return;
            var buf = new Vector2[n];
            for (int i = 0; i < n; i++) { float v = _voiceQueue.Dequeue(); buf[i] = new Vector2(v, v); }
            _voicePlayback.PushBuffer(buf);
        }

        private void SampleSnapshots(out Vector3 pos, out float yaw)
        {
            double rt = Now - InterpDelay;
            pos = _target; yaw = _targetYaw;
            if (_snaps.Count == 0) return;
            if (rt <= _snaps[0].T) { pos = _snaps[0].P; yaw = _snaps[0].Yaw; return; }
            for (int i = 0; i < _snaps.Count - 1; i++)
            {
                var a = _snaps[i]; var b = _snaps[i + 1];
                if (rt <= b.T)
                {
                    float t = (float)((rt - a.T) / System.Math.Max(b.T - a.T, 1e-4));
                    pos = a.P.Lerp(b.P, t);
                    yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t);
                    return;
                }
            }
            // Het du lieu (goi den tre): ngoai suy theo van toc 2 goi cuoi, toi da MaxExtrapolate.
            var last = _snaps[^1];
            pos = last.P; yaw = last.Yaw;
            // Chi ngoai suy khi dang di/chay/lan (1/2/6) - luc dung lai client da gui goi Idle kem vi
            // tri cuoi nen khong duoc "truot qua" diem dung.
            bool moving = Role == 1 || Role == 2 || Role == 6 || IsMounted;
            if (moving && _snaps.Count >= 2)
            {
                var prev = _snaps[^2];
                double span = last.T - prev.T;
                double ahead = System.Math.Min(rt - last.T, MaxExtrapolate);
                if (span > 1e-3 && span < 0.6) pos = last.P + (last.P - prev.P) * (float)(ahead / span);
            }
        }

        private void UpdateLod()
        {
            var cam = GetViewport()?.GetCamera3D();
            float d = cam != null ? cam.GlobalPosition.DistanceTo(GlobalPosition) : 0f;
            var lod = d < NearDist ? Lod.Near : d < MidDist ? Lod.Mid : Lod.Far;
            if (lod == CurrentLod) return;
            CurrentLod = lod;
            _model.Visible = lod != Lod.Far && !IsHidden;
            _nameLabel.Visible = lod != Lod.Far && !IsHidden;
            if (_mount != null) _mount.Visible = lod != Lod.Far && !IsHidden;
            SetShadows(_model, lod == Lod.Near);
            if (_anim != null)
            {
                // Vua: tu tinh hoat canh 1/3 khung hinh (Advance thu cong); xa: dung han.
                _anim.CallbackModeProcess = lod == Lod.Near ? AnimationMixer.AnimationCallbackModeProcess.Idle : AnimationMixer.AnimationCallbackModeProcess.Manual;
                _animAccum = 0;
            }
            if (_armPose != null) _armPose.Active = lod == Lod.Near;
        }

        private static void SetShadows(Node n, bool on)
        {
            if (n is GeometryInstance3D g) g.CastShadow = on ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            foreach (Node c in n.GetChildren()) SetShadows(c, on);
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
            PumpVoice();
            if (_hpLabel.Visible && Time.GetTicksMsec() > _hpUntil) _hpLabel.Visible = false;
            // Ten: dang noi = xanh la; dong doi (to doi/bang hoi) = xanh lam; nguoi la (danh duoc) = cam do.
            bool speaking = IsSpeaking;
            bool friendly = HiepSiVeVuon.Systems.PvpClient.Instance?.IsFriendly(this) ?? false;
            long bounty = HiepSiVeVuon.Systems.BountyBoard.AmountOf(PlayerId);
            if (speaking != _shownSpeaking || friendly != _shownFriendly || _nameDirty || bounty != _shownBounty)
            {
                _shownSpeaking = speaking;
                _shownFriendly = friendly;
                _shownBounty = bounty;
                _nameDirty = false;
                _nameLabel.Modulate = speaking ? new Color(0.45f, 1f, 0.5f) : friendly ? new Color(0.55f, 0.85f, 1f) : bounty > 0 ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.62f, 0.5f);
                string name = speaking ? "• " + DisplayName + " •" : DisplayName;
                // Bi truy na: hien tien thuong tren dau - ai ha duoc se lanh.
                _nameLabel.Text = bounty > 0 ? string.Format(HiepSiVeVuon.Systems.Loc.T("bounty.tag_fmt"), bounty) + "\n" + name : name;
            }

            _lodTimer -= (float)delta;
            if (_lodTimer <= 0f) { _lodTimer = 0.25f; UpdateLod(); }

            SampleSnapshots(out var pos, out var yaw);
            Position = pos;
            if (CurrentLod == Lod.Far) return;
            _model.Basis = new Basis(Vector3.Up, yaw);

            if (_anim != null && CurrentLod == Lod.Mid)
            {
                _animAccum += delta;
                if (++_animFrame % 3 == 0) { _anim.Advance(_animAccum); _animAccum = 0; }
            }

            // Cung bang ma voi Player.NetAnimRoles (combo, lan, trung don, nga, the thu kiem...).
            UpdateMount((float)delta);
            int code = Role;
            string role = code < Player.NetAnimRoles.Length ? Player.NetAnimRoles[code] : "Idle";
            // Tren ngua: chan "ngoi yen" (khong di/chay), than tren van vung kiem/ban cung.
            if (IsMounted && (role == "Walk" || role == "Run")) role = "Idle";
            // Dong tac tu dung (cuoc, tuoi, quang can...): dung Idle + lop xoay tay giong nguoi choi.
            if (role.StartsWith("Motion:"))
            {
                if (code != _lastMotionCode && _armPose != null && Motions.Get(role[7..]) is ArmPoseModifier.Motion mo) _armPose.Play(mo);
                role = "Idle";
            }
            _lastMotionCode = role == "Idle" && code >= 14 ? code : -1;
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
