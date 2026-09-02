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
        // public: BuildMenuUI.cs can dung de dat cong trinh MOI truoc mat nguoi choi.
        public Vector3 Facing => _facing;
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
        [Export] public float SwimSpeedMult = 0.55f;
        private Horse _mountedHorse;
        // Thuyen (xem Boat.cs) - dung CHUNG phim [R] voi cuoi ngua (mount_horse), coi nhu "len
        // phuong tien gan nhat" thay vi rieng cho ngua.
        private Boat _mountedBoat;

        public override void _Ready()
        {
            _model = GetNodeOrNull<Node3D>("Model");
            _interactArea = GetNodeOrNull<Area3D>("InteractArea");
            _camera = GetNodeOrNull<Camera3D>("Camera3D");
            if (_model != null)
            {
                _animPlayer = CharacterRig.Attach(_model, ModelPath, ModelScale);
                if (_animPlayer != null)
                {
                    _animPlayer.AnimationFinished += _ => _actionPlaying = false;
                    // Cac hoat canh DI CHUYEN (dung khi Idle/Walk/Run) PHAI lap lien tuc - file
                    // GLTF khong luon ghi ro "loop" (Godot import mac dinh CO THE ra LoopMode.None
                    // neu khong tu phat hien duoc chu ky khop dau/cuoi), khien hoat canh chi choi
                    // MOT LAN roi dung yen o khung hinh cuoi (nhin nhu "treo 1 tu the") du nhan
                    // vat van dang di chuyen - ep LoopMode = Linear TRUC TIEP bang code, khong phu
                    // thuoc vao cai dat import (co the sai/khong nhat quan giua cac model), de dam
                    // bao buoc chan luon lap muot ma tu nhien nhu that.
                    foreach (var loopAnim in new[] { "Idle", "Walk", "Run" })
                    {
                        if (!_animPlayer.HasAnimation(loopAnim)) continue;
                        var anim = _animPlayer.GetAnimation(loopAnim);
                        if (anim != null) anim.LoopMode = Animation.LoopModeEnum.Linear;
                    }
                }

                // Vu khi/cong cu dang trang bi hien THAT SU tren tay (xem HeldItemVisual.cs) -
                // gan vao xuong co tay phai cua model vua tai.
                var skeleton = CharacterRig.FindSkeleton(_model);
                if (skeleton != null)
                {
                    _heldItem = new HeldItemVisual();
                    AddChild(_heldItem);
                    _heldItem.Setup(skeleton, ModelScale);
                    RefreshHeldItem();
                }
            }
            Inventory.Instance.InventoryChanged += RefreshHeldItem;
            GameManager.Instance.PlayerDied += OnDied;
            SetupFade();
        }

        public override void _ExitTree()
        {
            Inventory.Instance.InventoryChanged -= RefreshHeldItem;
        }

        // Uu tien hien vat pham THUOC LOAI HANH DONG vua thuc hien gan day nhat (tan cong ->
        // kiem, dung cong cu -> cuoc/can cau...) - nguoi choi co the trang bi CA vu khi lan cong
        // cu CUNG LUC (2 o rieng biet), nen can 1 quy uoc de biet hien cai nao. Mac dinh (chua
        // hanh dong nao) uu tien cong cu vi day la game nong trai, cong cu dung thuong xuyen hon.
        private HeldItemVisual _heldItem;
        private bool _lastActionWasAttack = false;

        private void RefreshHeldItem()
        {
            if (_heldItem == null) return;
            string tool = Inventory.Instance.EquippedTool;
            string weapon = Inventory.Instance.EquippedWeapon;
            string toShow = _lastActionWasAttack
                ? (weapon ?? tool)
                : (tool ?? weapon);
            _heldItem.ShowItem(toShow);
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

            if (_mountedBoat != null && IsInstanceValid(_mountedBoat))
            {
                // Y het logic "ngoi" tren ngua o tren (xoay SeatOffset theo huong thuyen dang
                // quay mat) - Boat.cs tu doc input va di chuyen (xem Boat._Process).
                _facing = _mountedBoat.Facing;
                Vector3 boatRight = _facing.Cross(Vector3.Up).Normalized();
                Vector3 seatWorldOffset = boatRight * Boat.SeatOffset.X
                    + Vector3.Up * Boat.SeatOffset.Y
                    + _facing * Boat.SeatOffset.Z;
                GlobalPosition = _mountedBoat.GlobalPosition + seatWorldOffset;
                UpdateVisuals(dt, 0f);
                return;
            }

            var input2 = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var dir = new Vector3(input2.X, 0f, input2.Y);

            // Boi qua ho: cham hon di tren dat (khong co animation/trang thai rieng - chi giam
            // toc do di chuyen khi dang o trong pham vi ho, xem WaterEcosystem.IsNearLake).
            float moveSpeed = WaterEcosystem.Instance.IsNearLake(GlobalPosition) ? Speed * SwimSpeedMult : Speed;
            var targetVelocity = dir * moveSpeed;
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
            if (_mountedBoat != null)
            {
                DismountBoat();
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
                if (body is Boat boat)
                {
                    MountBoat(boat);
                    return;
                }
            }
        }

        private void MountBoat(Boat boat)
        {
            _mountedBoat = boat;
            boat.SetRidden(true);
            CollisionLayer = 0;
            CollisionMask = 0;
            if (_camera != null) _camera.Position = MountedCameraOffset;
        }

        private void DismountBoat()
        {
            if (_mountedBoat == null) return;
            _mountedBoat.SetRidden(false);
            _mountedBoat = null;
            CollisionLayer = 1;
            CollisionMask = 1;
            if (_camera != null) _camera.Position = _indoors ? IndoorCameraOffset : OutdoorCameraOffset;
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

            _lastActionWasAttack = true;
            RefreshHeldItem();

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
            // Dao quang trong ham mo neu co (xem OreNode.cs) - dung suc Cuoc (Pickaxe) rieng,
            // KHONG phai sat thuong vu khi (mo phong dung mau chat cay o tren).
            int pickPower = Inventory.Instance.GetToolPower();
            var oreNodes = GetTree().GetNodesInGroup("ore_nodes");
            foreach (var node in oreNodes)
            {
                if (node is OreNode ore && IsInstanceValid(ore))
                {
                    float dist = GlobalPosition.DistanceTo(ore.GlobalPosition);
                    Vector3 toOre = (ore.GlobalPosition - GlobalPosition).Normalized();
                    if (dist <= AttackRange && _facing.Dot(toOre) > 0.3f)
                        ore.Mine(pickPower);
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
                if (body is WaterTower tower) { tower.Interact(); return; }
                if (body is WildAnimal wild && wild.SpeciesId == "duck") { TryFeedDuck(); return; }
                if (body is ProcessingMachine machine) { machine.Interact(); return; }
                if (body is CookingStation cooking) { cooking.Interact(); return; }
                if (body is GreenhouseGate gate) { gate.Interact(); return; }
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

        // Cuoc nang cap (xem items.json "cuoc_bac"/"cuoc_vang") tac dong CA VUNG quanh nguoi choi
        // thay vi tung o - 0 = hanh vi co ban (chi 1 o gan nhat, khong doi hanh vi cu).
        private static float ToolAreaRadius(string toolId) => toolId switch
        {
            "cuoc_bac" => 100f,  // ~3x3 o (FreeformTileSize=84 o FarmPlot.cs)
            "cuoc_vang" => 180f, // ~5x5 o
            _ => 0f,
        };

        private void TryUseTool()
        {
            _lastActionWasAttack = false;
            RefreshHeldItem();

            var plots = GetTree().GetNodesInGroup("farm_plots");
            float areaRadius = ToolAreaRadius(Inventory.Instance.EquippedTool);

            if (areaRadius > 0f)
            {
                // Cuoc nang cap: dung TAT CA o dat trong ban kinh (khong chi 1 o gan nhat).
                bool didAny = false;
                foreach (var n in plots)
                    if (n is FarmPlot ap && GlobalPosition.DistanceTo(ap.GlobalPosition) <= areaRadius)
                    {
                        ap.UseOn();
                        didAny = true;
                    }
                if (didAny) return;
            }

            // Tim o dat gan nhat de cay/trong/tuoi/bon phan/diet sau/thu hoach
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
            if (nearest != null) { nearest.UseOn(); return; }

            // Khong co o dat nao gan - thu tim CAY LAU NAM gan nhat (vuon cay/vuon nho NHO, xem
            // FruitTree.cs) de hai neu da chin. Nhanh RIENG, khong anh huong toi logic FarmPlot
            // o tren.
            var trees = GetTree().GetNodesInGroup("fruit_trees");
            FruitTree nearestTree = null;
            float bestTree = 48f;
            foreach (var n in trees)
            {
                if (n is FruitTree t)
                {
                    float d = GlobalPosition.DistanceTo(t.GlobalPosition);
                    if (d < bestTree) { bestTree = d; nearestTree = t; }
                }
            }
            if (nearestTree != null) { nearestTree.UseOn(); return; }

            // Khong co gi gan de dung - neu dang trang bi Cuoc (bat ky cap nao), thu cuoc dat MOI
            // ngay truoc mat (xem FarmPlot.TryTillFreeform) de mo rong nong trai tu do. Cuoc nang
            // cap (bac/vang) cuoc CA CUM O (3x3/5x5) cung 1 lan thay vi tung o.
            string tool = Inventory.Instance.EquippedTool;
            if (tool == "hoe" || tool == "cuoc_bac" || tool == "cuoc_vang")
            {
                int tilesPerSide = tool switch { "cuoc_bac" => 3, "cuoc_vang" => 5, _ => 1 };
                int half = tilesPerSide / 2;
                Vector3 center = GlobalPosition + _facing * 42f;
                Vector3 right = _facing.Cross(Vector3.Up).Normalized();
                int tilled = 0;
                for (int r = -half; r <= half; r++)
                {
                    for (int c = -half; c <= half; c++)
                    {
                        var spot = center + right * (c * 84f) + _facing * (r * 84f);
                        if (FarmPlot.TryTillFreeform(spot, GetTree().CurrentScene) != null) tilled++;
                    }
                }
                GD.Print(tilled > 0 ? $"Đã cuốc {tilled} ô đất mới." : "Không thể cuốc đất ở đây.");
            }
            else if (tool == "can_cau")
            {
                TryFish();
            }
            else if (tool == "may_tuoi_tu_dong")
            {
                TryPlaceSprinkler();
            }
        }

        [Export] public float FishCooldownSec = 1.5f;
        private ulong _lastFishTime = 0;

        private void TryFish()
        {
            if (!WaterEcosystem.Instance.IsNearLake(GlobalPosition, 220f))
            {
                GD.Print("Cần ở gần hồ/sông để câu cá.");
                return;
            }
            ulong now = Time.GetTicksMsec();
            if (now - _lastFishTime < (ulong)(FishCooldownSec * 1000)) return;
            _lastFishTime = now;

            if (WaterEcosystem.Instance.Get("fish") < 5f)
            {
                GD.Print("Khu vực này không còn cá để câu, chờ quần thể phục hồi.");
                return;
            }
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            if (rng.Randf() < 0.6f)
            {
                Inventory.Instance.AddItem("ca", 1);
                FarmStorage.Instance.Add("ca", 1);
                WaterEcosystem.Instance.OnPlayerCatch("fish", 3f);
                GD.Print("Câu được 1 con cá!");
            }
            else
            {
                GD.Print("Cá chưa cắn, thử lại sau.");
            }
        }

        private static PackedScene _sprinklerScene;

        private void TryPlaceSprinkler()
        {
            if (!Inventory.Instance.RemoveItem("may_tuoi_tu_dong", 1))
            {
                GD.Print("Không có Máy Tưới Tự Động trong túi đồ.");
                return;
            }
            _sprinklerScene ??= GD.Load<PackedScene>("res://scenes/AutoSprinkler.tscn");
            var sprinkler = _sprinklerScene.Instantiate<AutoSprinkler>();
            sprinkler.Position = GlobalPosition + _facing * 50f;
            GetTree().CurrentScene.AddChild(sprinkler);
            GD.Print("Đã đặt Máy Tưới Tự Động - sẽ tự tưới ruộng quanh đây mỗi ngày.");
        }

        private void TryFeedDuck()
        {
            if (Inventory.Instance.RemoveItem("thucan_giasuc", 1))
            {
                WaterEcosystem.Instance.OnFeedDucks();
                GD.Print("Đã cho vịt ăn.");
            }
            else
            {
                GD.Print("Cần thức ăn gia súc để cho vịt ăn.");
            }
        }

        private void OnDied()
        {
            GD.Print("Người chơi đã gục ngã! Hồi sinh tại nhà.");
            GameManager.Instance.Hp = GameManager.Instance.MaxHp / 2;
            GlobalPosition = Vector3.Zero;
            GameManager.Instance.EmitSignal(GameManager.SignalName.StatsChanged);
        }
    }
}
