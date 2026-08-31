using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Entities
{
    // Nguoi choi: di chuyen 8 huong (mat phang XZ), tan cong quai, tuong tac NPC/vat pham,
    // dung cong cu tren o dat (farming). Su dung CharacterBody3D.
    public partial class Player : CharacterBody3D
    {
        [Export] public float Speed = 120f;
        [Export] public float Acceleration = 900f;
        [Export] public float Friction = 1000f;
        [Export] public float Gravity = 980f;
        [Export] public float AttackRange = 40f;
        [Export] public int AttackCooldownMs = 400;
        [Export] public float ModelScale = 22f;
        [Export] public float TurnSpeed = 14f;
        [Export] public bool FlipModelFacing = true;
        [Export] public string ModelPath = "res://assets3d/quaternius/characters/Farmer.gltf";

        private ulong _lastAttackTime = 0;
        private Vector3 _facing = Vector3.Back;
        private Area3D _interactArea;
        private Node3D _model;
        private AnimationPlayer _animPlayer;
        private string _currentAnim = "";
        private bool _actionPlaying = false;

        // Vao/ra cong trinh: moi cong trinh co 1 phong noi that rieng (xem BuildingDoor.InteriorAnchor),
        // nho vi tri ngoai troi de quay lai dung cho khi ra.
        private bool _indoors = false;
        private Vector3 _returnPos;
        private ColorRect _fadeRect;
        private Camera3D _camera;

        // Camera ngoai troi dat lui ve sau nhan vat 115 don vi theo truc Z THE GIOI (khong xoay
        // theo huong nhan vat) - phu hop khong gian mo. Nhung trong phong kin, di lai gan bat ky
        // buc tuong nao ve phia +Z se day camera vuot QUA tuong do (vi offset cong don vao vi tri
        // nguoi choi), lam man hinh thay "khong gian bi vo" (nhin xuyen ra ngoai tu phia sau
        // tuong). Vi vay khi vao nha phai thu nho offset nay lai that nhieu.
        private static readonly Vector3 OutdoorCameraOffset = new(0, 140, 115);
        private static readonly Vector3 IndoorCameraOffset = new(0, 60, 15);

        public override void _Ready()
        {
            _model = GetNodeOrNull<Node3D>("Model");
            _interactArea = GetNodeOrNull<Area3D>("InteractArea");
            _camera = GetNodeOrNull<Camera3D>("Camera3D");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, ModelPath, ModelScale);
                if (_animPlayer != null)
                    _animPlayer.AnimationFinished += _ => _actionPlaying = false;
            }
            GameManager.Instance.PlayerDied += OnDied;
            SetupFade();
        }

        // Man hinh toi den nhanh khi qua cua (vao/ra nha) - che di buoc "day tuc thoi" phia sau,
        // cho cam giac giong dang thuc su buoc qua cua chu khong phai bi teleport dot ngot.
        private void SetupFade()
        {
            var layer = new CanvasLayer { Layer = 100 };
            AddChild(layer);
            _fadeRect = new ColorRect
            {
                Color = new Color(0, 0, 0, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(_fadeRect);
        }

        private void TeleportWithFade(Vector3 dest)
        {
            var tw = CreateTween();
            tw.TweenProperty(_fadeRect, "color:a", 1f, 0.18f);
            tw.TweenCallback(Callable.From(() =>
            {
                // Reset van toc de khong mang theo da roi/chay tu truoc khi qua cua (vd dang
                // roi tu do cao ngoai troi se lam nguoi choi "xuyen san" ngay khi vao phong).
                Velocity = Vector3.Zero;
                GlobalPosition = dest;
            }));
            tw.TweenProperty(_fadeRect, "color:a", 0f, 0.18f);
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            var input2 = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var dir = new Vector3(input2.X, 0f, input2.Y);

            var targetVelocity = dir * Speed;
            float rate = dir != Vector3.Zero ? Acceleration : Friction;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z).MoveToward(targetVelocity, rate * dt);

            float vy = Velocity.Y;
            if (!IsOnFloor()) vy -= Gravity * dt;
            else vy = 0f;

            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            // Huong mat luon khop dung huong dang DI CHUYEN THAT (van toc da lam muot qua gia
            // toc), khong phai huong phim bam tuc thi - neu khong nhan vat co the "truot" theo
            // huong khac voi huong dang chay (giong truot bang) khi doi huong gap.
            if (horizontal.Length() > 5f) _facing = horizontal.Normalized();
            MoveAndSlide();

            float speedRatio = new Vector2(horizontal.X, horizontal.Z).Length() / Speed;
            UpdateVisuals(dt, speedRatio);
        }

        private void UpdateVisuals(float dt, float speedRatio)
        {
            if (_model != null && _facing != Vector3.Zero)
            {
                var lookDir = FlipModelFacing ? -_facing : _facing;
                var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
                _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            }

            if (_actionPlaying) return; // dang choi hoat canh tan cong/tuong tac, khong ghi de
            if (_animPlayer == null) return;

            // Chuyen Idle -> Walk -> Run theo toc do thuc te (nhu con nguoi that: chi chay khi
            // di nhanh), va dieu chinh nhip chan (SpeedScale) khop voi toc do di chuyen de tranh
            // hieu ung "truot bang" (chan dong nhung nguoi di nhanh/cham hon animation).
            string anim = speedRatio < 0.1f ? "Idle" : speedRatio < 0.6f ? "Walk" : "Run";
            if (!_animPlayer.HasAnimation(anim)) anim = "Idle";

            if (_currentAnim != anim)
            {
                _animPlayer.Play(anim);
                _currentAnim = anim;
            }
            _animPlayer.SpeedScale = anim == "Idle" ? 1f : Mathf.Lerp(0.8f, 1.3f, speedRatio);
        }

        private void PlayAction(string anim)
        {
            if (_animPlayer == null || !_animPlayer.HasAnimation(anim)) return;
            _animPlayer.SpeedScale = 1f;
            _animPlayer.Play(anim);
            _currentAnim = anim;
            _actionPlaying = true;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e.IsActionPressed("attack")) TryAttack();
            else if (e.IsActionPressed("interact")) TryInteract();
            else if (e.IsActionPressed("use_tool")) TryUseTool();
        }

        private void TryAttack()
        {
            ulong now = Time.GetTicksMsec();
            if (now - _lastAttackTime < (ulong)AttackCooldownMs) return;
            _lastAttackTime = now;

            int dmg = Inventory.Instance.GetWeaponDamage();
            // Tim quai trong ban kinh tan cong theo huong dang nhin
            var enemies = GetTree().GetNodesInGroup("enemies");
            foreach (var node in enemies)
            {
                if (node is Enemy enemy && IsInstanceValid(enemy))
                {
                    float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                    Vector3 toEnemy = (enemy.GlobalPosition - GlobalPosition).Normalized();
                    if (dist <= AttackRange && _facing.Dot(toEnemy) > 0.3f)
                        enemy.TakeDamage(dmg);
                }
            }
            // Chat cay trong tam neu co (lay go)
            var trees = GetTree().GetNodesInGroup("choppable_trees");
            foreach (var node in trees)
            {
                if (node is Tree tree && IsInstanceValid(tree))
                {
                    float dist = GlobalPosition.DistanceTo(tree.GlobalPosition);
                    Vector3 toTree = (tree.GlobalPosition - GlobalPosition).Normalized();
                    if (dist <= AttackRange && _facing.Dot(toTree) > 0.3f)
                        tree.Chop(dmg);
                }
            }
            SpawnSlash();
            PlayAction("Sword_Slash");
        }

        private void SpawnSlash()
        {
            var slash = new Sprite3D();
            AddChild(slash);
            slash.Position = _facing * 24f + Vector3.Up * 16f;
            slash.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            slash.PixelSize = 0.5f;
            slash.Modulate = new Color(1, 1, 1, 0.7f);
            var tex = GD.Load<Texture2D>("res://assets/items/sword.png");
            if (tex != null) slash.Texture = tex;
            var tw = CreateTween();
            tw.TweenProperty(slash, "modulate:a", 0f, 0.2f);
            tw.TweenCallback(Callable.From(slash.QueueFree));
        }

        private void TryInteract()
        {
            if (_interactArea == null) return;
            foreach (var body in _interactArea.GetOverlappingBodies())
            {
                if (body is NPC npc) { npc.Interact(); PlayAction("Interact"); return; }
                if (body is DroppedItem drop) { drop.PickUp(); return; }
            }
            foreach (var area in _interactArea.GetOverlappingAreas())
            {
                if (area is DroppedItem d2) { d2.PickUp(); return; }
                if (area is BuildingDoor door)
                {
                    if (door.IsExit) ExitBuilding();
                    else if (door.IsFloorChange) ChangeFloor(door.InteriorAnchor);
                    else EnterBuilding(door.InteriorAnchor);
                    return;
                }
            }
        }

        private void EnterBuilding(Vector3 interiorAnchor)
        {
            if (_indoors) return;
            _returnPos = GlobalPosition;
            _indoors = true;
            if (_camera != null) _camera.Position = IndoorCameraOffset;
            // Dat cao hon san mot chut (khong dat dung khit len san) de trong luc tu nhien keo
            // xuong va IsOnFloor() nhan dung san ngay, thay vi mot diem cham khit co the bi
            // xu ly sai thanh "chua cham san" roi roi xuyen qua san mong xuong vuc.
            TeleportWithFade(interiorAnchor + Vector3.Up * 8f);
        }

        private void ExitBuilding()
        {
            if (!_indoors) return;
            _indoors = false;
            if (_camera != null) _camera.Position = OutdoorCameraOffset;
            TeleportWithFade(_returnPos);
        }

        // Doi tang trong CUNG mot cong trinh (cau thang) - khac EnterBuilding/ExitBuilding: KHONG
        // dong cham den _indoors hay _returnPos (van dang o trong cung cong trinh, van dung
        // camera thu nho nhu luc o tang tret), chi doi vi tri sang phong tang khac.
        private bool _changingFloor = false;
        private void ChangeFloor(Vector3 targetAnchor)
        {
            if (_changingFloor) return;
            _changingFloor = true;
            TeleportWithFade(targetAnchor + Vector3.Up * 8f);
            GetTree().CreateTimer(0.5).Timeout += () => _changingFloor = false;
        }

        // Goi tu BuildingDoor khi nguoi choi CHAM VAO cau thang (tu dong len tang, khong can [E]
        // - xem BuildingDoor.IsAutoTrigger) - giu hanh dong "buoc len cau thang" tu nhien nhu
        // con nguoi that, khong phai mot thao tac tuong tac rieng biet nhu mo cua.
        public void TriggerFloorChange(Vector3 targetAnchor) => ChangeFloor(targetAnchor);

        private void TryUseTool()
        {
            // Tim o dat gan nhat de cay/trong/tuoi/thu hoach
            var plots = GetTree().GetNodesInGroup("farm_plots");
            FarmPlot nearest = null;
            float best = 48f;
            foreach (var n in plots)
            {
                if (n is FarmPlot p)
                {
                    float d = GlobalPosition.DistanceTo(p.GlobalPosition);
                    if (d < best) { best = d; nearest = p; }
                }
            }
            nearest?.UseOn();
        }

        private void OnDied()
        {
            GD.Print("Nguoi choi da guc nga! Hoi sinh tai nha.");
            GameManager.Instance.Hp = GameManager.Instance.MaxHp / 2;
            GlobalPosition = Vector3.Zero;
            GameManager.Instance.EmitSignal(GameManager.SignalName.StatsChanged);
        }
    }
}
