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
        private static readonly Vector3 MountedCameraOffset = new(0, 160, 125);

        // Cuoi ngua: [R] gan ngua de len, [R] lan nua de xuong. Khi cuoi, CON NGUA moi la thuc
        // the tu doc input va di chuyen that su (xem Horse.DoRiddenMovement) - nguoi choi chi
        // "ngoi" dung vi tri tren lung ngua (Horse.SeatOffset) va giu nguyen model hien thi
        // (khong an di), khac voi ban dau lam nguoc: an nguoi choi va bien ngua thanh nhan vat
        // dieu khien, khien nhin nhu nguoi choi "nhap" vao than ngua thay vi ngoi tren.
        [Export] public float MountRange = 55f;
        private Horse _mountedHorse;

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

            if (_mountedHorse != null && IsInstanceValid(_mountedHorse))
            {
                // Dang cuoi: KHONG con tu di chuyen/roi tu do rieng nua - con ngua moi la thuc
                // the tu doc input va di chuyen that su (xem Horse.DoRiddenMovement). Nguoi choi
                // chi "ngoi" dung vi tri tren lung ngua (SeatOffset) va quay mat theo huong ngua
                // dang di, giu nguyen HINH ANH (khong an di) - dung "nhap" vao than ngua.
                // QUAN TRONG: SeatOffset la toa do CUC BO theo than ngua (trai/phai, len/xuong,
                // truoc/sau) - phai XOAY theo dung huong ngua dang quay mat (_facing) truoc khi
                // cong vao vi tri ngua, neu khong (cong thang theo truc THE GIOI) thi cho ngoi se
                // "troi" sang mot ben ngay khi ngua re huong khac voi luc vua len ngua.
                _facing = _mountedHorse.Facing;
                Vector3 horseRight = _facing.Cross(Vector3.Up).Normalized();
                Vector3 seatWorldOffset = horseRight * Horse.SeatOffset.X
                    + Vector3.Up * Horse.SeatOffset.Y
                    + _facing * Horse.SeatOffset.Z;
                GlobalPosition = _mountedHorse.GlobalPosition + seatWorldOffset;
                // Bo hoan toan viec tu nghieng Basis (thu truoc do lam nhan vat bi xoay gan 90 do,
                // nam bet xuong dat thay vi ngoi) - giu than nguoi THANG DUNG, chi dua vi tri len
                // dung tam LUNG NGUA that (gan vai/withers, khong cao hon dinh dau hay thap qua
                // xuong duoi bung) - day la vi tri con nguoi that su ngoi khi cuoi ngua.
                UpdateVisuals(dt, 0f); // dang "ngoi" - dung tu the Idle, khong chay hoat canh Di/Chay
                return;
            }

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
            // Dung NGUONG KEP (hysteresis) quanh moi moc chuyen doi - vi du dang Idle phai vuot
            // 0.12 moi sang Walk, nhung dang Walk phai tut duoi 0.08 moi ve lai Idle - neu chi
            // dung 1 moc duy nhat, toc do dao dong nhe quanh moc do (vd di cheo, va nhe vao
            // tuong) se lam animation nhay qua lai lien tuc, nhin rat gia/giat.
            string anim = _currentAnim switch
            {
                "Run" => speedRatio < 0.55f ? (speedRatio < 0.08f ? "Idle" : "Walk") : "Run",
                "Walk" => speedRatio < 0.08f ? "Idle" : speedRatio >= 0.65f ? "Run" : "Walk",
                _ => speedRatio < 0.12f ? "Idle" : speedRatio >= 0.65f ? "Run" : "Walk",
            };
            if (!_animPlayer.HasAnimation(anim)) anim = "Idle";

            if (_currentAnim != anim)
            {
                // Chuyen muot (blend 0.15s) thay vi cat cung tuc thi - neu khong chan se "nhay"
                // sang tu the khac ngay giua chung buoc, nhin may moc/khong tu nhien.
                _animPlayer.Play(anim, 0.15);
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
            else if (e.IsActionPressed("mount_horse")) TryToggleRide();
        }

        private void TryToggleRide()
        {
            if (_mountedHorse != null)
            {
                DismountHorse();
                return;
            }
            if (_interactArea == null) return;
            foreach (var body in _interactArea.GetOverlappingBodies())
            {
                if (body is Horse horse)
                {
                    MountHorse(horse);
                    return;
                }
            }
        }

        private void MountHorse(Horse horse)
        {
            _mountedHorse = horse;
            horse.SetRidden(true);
            // Tat va cham rieng cua nguoi choi trong luc cuoi - vi tri nguoi choi gio chi "an
            // theo" vi tri ngua (khong tu MoveAndSlide nua), neu de nguyen va cham thi than
            // nguoi choi dung yen chong len ngua se can tro chinh MoveAndSlide cua con ngua.
            CollisionLayer = 0;
            CollisionMask = 0;
            if (_camera != null) _camera.Position = MountedCameraOffset;
            // Thu nho nguoi choi mot chut khi cuoi - phu hop voi dang "ngoi thap" (SeatOffset da
            // ha xuong) hon la dung sung sung cao tren lung ngua.
            if (_model != null) _model.Scale = Vector3.One * 0.88f;
        }

        private void DismountHorse()
        {
            if (_mountedHorse == null) return;
            _mountedHorse.SetRidden(false);
            _mountedHorse = null;
            CollisionLayer = 1;
            CollisionMask = 1;
            if (_camera != null) _camera.Position = _indoors ? IndoorCameraOffset : OutdoorCameraOffset;
            if (_model != null) _model.Scale = Vector3.One;
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
