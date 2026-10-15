using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.UI;

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
        private System.Collections.Generic.Dictionary<string, string> _anims = new();
        // Ten hoat canh that theo vai tro (rig nam/nu dat ten khac nhau - xem CharacterCatalog).
        private string A(string role) => _anims.TryGetValue(role, out var n) ? n : role;

        // Cho NetworkClient gui len gateway: 0 dung, 1 di, 2 chay, 3 chem, 4 dung cong cu, 5 tuong tac.
        public int NetAnimCode { get; private set; }
        private ulong _netActionUntil;
        public float Yaw => Mathf.Atan2(_facing.X, _facing.Z);
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
        [Export] public float ExhaustedSpeedMult = 0.7f;
        private Horse _mountedHorse;
        // Thuyen (xem Boat.cs) - dung CHUNG phim [R] voi cuoi ngua (mount_horse), coi nhu "len
        // phuong tien gan nhat" thay vi rieng cho ngua.
        private Boat _mountedBoat;

        public override void _Ready()
        {
            _model = GetNodeOrNull<Node3D>("Model");
            _interactArea = GetNodeOrNull<Area3D>("InteractArea");
            // Nguoi choi khac (RemotePlayer, lop va cham 8) - bam [E] de giao dich/nhan rieng, nhung
            // than nguoi KHONG chan nhau (mask cua than Player khong co lop 8).
            _interactArea?.SetCollisionMaskValue(RemotePlayer.NetLayer, true);
            // Nhan vat tu tao (CharacterCreatorUI) thay cho model Farmer mac dinh.
            var look = CharacterProfile.Current?.Look;
            var modelDef = CharacterCatalog.Get(look?.ModelId ?? "Farmer");
            ModelPath = modelDef.Path;
            ModelScale = modelDef.Scale;
            _camera = GetNodeOrNull<Camera3D>("Camera3D");
            if (_model != null)
            {
                _animPlayer = CharacterCatalog.AttachModel(_model, modelDef);
                if (look != null) CharacterCatalog.ApplyColors(_model, look);
                _anims = CharacterCatalog.ResolveAnims(_animPlayer);
                _outfitRig = new OutfitRig(_model, look?.Gender ?? "male");
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
                    foreach (var loopAnim in new[] { A("Idle"), A("Walk"), A("Run"), A("Idle_Sword") })
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
                    // Lop dong tac vung tay (cuoc, tuoi, quet liem, quang can...) chay sau AnimationPlayer.
                    _armPose = new ArmPoseModifier { Name = "ArmPose" };
                    skeleton.AddChild(_armPose);
                    _armPose.Setup(skeleton, _model);
                }
            }
            Inventory.Instance.InventoryChanged += OnInventoryChanged;
            RefreshOutfit();
            GameManager.Instance.PlayerDied += OnDied;
            AddChild(new PlayerAbilities { Name = "Abilities" });
            SetupFade();
            SetupActionFeel();
        }

        public override void _ExitTree()
        {
            Inventory.Instance.InventoryChanged -= OnInventoryChanged;
            CleanupActionFeel();
        }

        // Uu tien hien vat pham THUOC LOAI HANH DONG vua thuc hien gan day nhat (tan cong ->
        // kiem, dung cong cu -> cuoc/can cau...) - nguoi choi co the trang bi CA vu khi lan cong
        // cu CUNG LUC (2 o rieng biet), nen can 1 quy uoc de biet hien cai nao. Mac dinh (chua
        // hanh dong nao) uu tien cong cu vi day la game nong trai, cong cu dung thuong xuyen hon.
        private HeldItemVisual _heldItem;
        private bool _lastActionWasAttack = false;

        // Rieng cho su kien Equip TU BALO/TUI DO (khong phai hanh dong tan cong/dung cong cu that
        // su) - can "day" _lastActionWasAttack theo dung LOAI vua duoc trang bi de tren tay THAY
        // DOI NGAY LAP TUC, khong bi che boi hanh dong choi gan nhat (vd nguoi choi vua tan cong
        // quai xong, sau do vao Balo bam chon Cao Co - neu khong co buoc nay, uu tien cu se van
        // chon vu khi vi _lastActionWasAttack con la true, khien tren tay KHONG DOI du vua bam
        // chon cong cu khac trong Balo).
        private void OnInventoryChanged()
        {
            var justEquipped = Inventory.Instance.LastEquippedType;
            if (justEquipped == ItemType.Weapon) _lastActionWasAttack = true;
            else if (justEquipped == ItemType.Tool) _lastActionWasAttack = false;
            RefreshHeldItem();
            RefreshOutfit();
        }

        // Trang phuc/bo giap tren nguoi (xem OutfitCatalog) - chi dung lai khi thuc su doi.
        private OutfitRig _outfitRig;
        private void RefreshOutfit()
        {
            if (_outfitRig == null) return;
            string want = Inventory.Instance.VisualOutfit;
            if (want == _outfitRig.Current) return;
            _outfitRig.Apply(CharacterProfile.Current?.Look, want);
            NetworkClient.Instance?.SendOutfit(want);
        }

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
                GlobalPosition = SteeringUtil.GuardAgainstRunaway(_mountedHorse.GlobalPosition + seatWorldOffset, "Player-mounted-horse");
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
                GlobalPosition = SteeringUtil.GuardAgainstRunaway(_mountedBoat.GlobalPosition + seatWorldOffset, "Player-mounted-boat");
                UpdateVisuals(dt, 0f);
                return;
            }

            if (_dying)
            {
                // Dang guc: chi chiu trong luc, khong dieu khien duoc.
                Velocity = new Vector3(0f, IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed), 0f);
                MoveAndSlide();
                NetAnimCode = 8;
                UpdateVisuals(dt, 0f);
                UpdateDust(0f);
                return;
            }

            // Dang go chat -> phim WASD la chu, khong phai di chuyen.
            var input2 = ChatUI.IsTyping ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");
            var dir = new Vector3(input2.X, 0f, input2.Y);

            // Boi qua ho: cham hon di tren dat (khong co animation/trang thai rieng - chi giam
            // toc do di chuyen khi dang o trong pham vi ho, xem WaterEcosystem.IsNearLake).
            float moveSpeed = WaterEcosystem.Instance.IsNearLake(GlobalPosition) ? Speed * SwimSpeedMult : Speed;
            if (GameManager.Instance.IsExhausted) moveSpeed *= ExhaustedSpeedMult; // kiet suc - di cham lai
            moveSpeed *= Inventory.Instance.OutfitSpeedMult(); // vd Do Tham Hiem: chay nhanh hon
            // Dang vung don/dung cong cu/lan: buoc chan cham lai (nhan vat "dung tan" khi ra don).
            bool locked = ActionLocked;
            if (locked) moveSpeed *= _lockMoveMult;
            var targetVelocity = dir * moveSpeed;
            float rate = dir != Vector3.Zero ? Acceleration : Friction;
            var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z).MoveToward(targetVelocity, rate * dt);

            float vy = Velocity.Y;
            if (!IsOnFloor()) vy = Mathf.Max(vy - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
            else vy = 0f;

            Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
            // Huong mat luon khop dung huong dang DI CHUYEN THAT (van toc da lam muot qua gia
            // toc), khong phai huong phim bam tuc thi - neu khong nhan vat co the "truot" theo
            // huong khac voi huong dang chay (giong truot bang) khi doi huong gap.
            // Trong luc ra don: giu nguyen huong (khong bi xoay theo phim di chuyen giua nhat chem).
            if (horizontal.Length() > 5f && !locked) _facing = horizontal.Normalized();
            MoveAndSlide();
            GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "Player");

            float speedRatio = new Vector2(horizontal.X, horizontal.Z).Length() / Speed;
            if (Time.GetTicksMsec() > _netActionUntil)
                NetAnimCode = speedRatio < 0.08f ? (InStance ? 12 : 0) : speedRatio >= 0.65f ? 2 : 1;
            PollAttack();
            if (_armPose != null)
            {
                _armPose.Carry = _heldItem != null && _heldItem.HasItem && !_dying && !_actionPlaying;
                _armPose.Grip = _heldItem != null && _heldItem.HasItem && !_dying;
                _armPose.MoveRatio = speedRatio;
            }
            UpdateVisuals(dt, speedRatio);
            UpdateDust(speedRatio);
        }

        private void UpdateVisuals(float dt, float speedRatio)
        {
            UpdateShake(dt);
            if (_model != null && _facing != Vector3.Zero)
            {
                var lookDir = FlipModelFacing ? -_facing : _facing;
                var targetBasis = Basis.LookingAt(lookDir, Vector3.Up);
                float spin = SpinAngle(dt);
                if (spin > 0f) _model.Basis = new Basis(Vector3.Up, spin) * targetBasis;
                else _model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            }

            if (_dying) return; // giu tu the nga, khong quay lai Idle
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
            // Vua danh xong (co vu khi) -> dung the thu kiem vai giay roi moi tha long.
            if (anim == "Idle" && InStance && HasAnim("Idle_Sword")) anim = "Idle_Sword";
            if (!_animPlayer.HasAnimation(A(anim))) anim = "Idle";

            if (_currentAnim != anim)
            {
                // Chuyen muot thay vi cat cung tuc thi - neu khong chan se "nhay" sang tu the
                // khac ngay giua chung buoc. Tu the thu -> dung thuong tha long cham hon.
                _animPlayer.Play(A(anim), _currentAnim == "Idle_Sword" ? 0.4 : 0.15);
                _currentAnim = anim;
            }
            _animPlayer.SpeedScale = anim is "Idle" or "Idle_Sword" ? 1f : Mathf.Lerp(0.8f, 1.3f, speedRatio);
        }

        private bool InStance => Time.GetTicksMsec() < _stanceUntil && Inventory.Instance.EquippedWeapon != null;

        private void PlayAction(string anim, float speed = 1f, float blend = 0.1f)
        {
            // Hoat canh moi (lan, trung don, nga...) cat ngang dong tac nong cu dang vung.
            if (_armPose != null && _armPose.Busy && _armPose.CurrentMotion != "TwistSlash") _armPose.Stop();
            int code = System.Array.IndexOf(NetAnimRoles, anim);
            NetAnimCode = code > 2 ? code : 5;
            float len = AnimLength(anim, 0.6f) / Mathf.Max(0.1f, speed);
            _netActionUntil = Time.GetTicksMsec() + (ulong)(Mathf.Clamp(len, 0.3f, 1.5f) * 1000f);
            if (_animPlayer == null || !_animPlayer.HasAnimation(A(anim))) return;
            _actionSpeed = speed;
            _animPlayer.SpeedScale = speed;
            // Phat lai tu dau ca khi trung hoat canh dang chay (2 nhat chem lien tiep).
            _animPlayer.Play(A(anim), blend);
            _animPlayer.Seek(0, true);
            _currentAnim = anim;
            _actionPlaying = true;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (_dying) return;
            if (e.IsActionPressed("attack")) PressAttack();
            else if (e.IsActionPressed("interact")) TryInteract();
            else if (e.IsActionPressed("use_tool")) { _useHeld = true; TryUseTool(); }
            else if (e.IsActionPressed("mount_horse")) TryToggleRide();
            else if (e.IsActionPressed("give_gift")) TryGiveGift();
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

        private void TryInteract()
        {
            if (_interactArea == null) return;
            foreach (var body in _interactArea.GetOverlappingBodies())
            {
                if (body is NPC npc) { FaceToward(npc); npc.Interact(); PlayEmote("Interact"); return; }
                if (body is DroppedItem drop) { FaceToward(drop); drop.PickUp(); PlayEmote("Interact"); return; }
                if (body is WaterTower tower) { tower.Interact(); return; }
                if (body is WildAnimal wild && wild.SpeciesId == "duck") { TryFeedDuck(); return; }
                if (body is ProcessingMachine machine) { machine.Interact(); return; }
                if (body is CookingStation cooking) { cooking.Interact(); return; }
                if (body is GreenhouseGate gate) { gate.Interact(); return; }
                if (body is FarmFacility facility) { FaceToward(facility); facility.Interact(); PlayEmote("Interact"); return; }
                if (body is IHungryAnimal && body is Node3D animal) { PetAnimal(animal); return; }
                if (body is RemotePlayer other) { (GetTree().GetFirstNodeInGroup("player_actions_ui") as PlayerActionsUI)?.Open(other); return; }
            }
            foreach (var area in _interactArea.GetOverlappingAreas())
            {
                if (area is DroppedItem d2) { FaceToward(d2); d2.PickUp(); PlayEmote("Interact"); return; }
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

        // Truoc day TryUseTool() KHONG choi hoat canh nao ca (chi co hieu ung logic tuc thi - o
        // dat lon len/tuoi nuoc... nhung nhan vat dung yen, khong thay dong tac gi) - moi cong cu
        // NHAM LAN voi nhau vi tat ca deu "vo hinh". Model Farmer.gltf la goi hoat canh CHIEN DAU/
        // hanh dong CHUNG cua Quaternius (Punch/Kick/Interact/Sword_Slash...), KHONG co san hoat
        // canh rieng cho tung nong cu (khong co "vung cuoc"/"tuoi nuoc"/"cao co" that su) - danh
        // gia lai cac hoat canh CO SAN, chon hoat canh GAN DUNG nhat ve tinh chat chuyen dong that
        // (cuoc/xeng: dong tac tay MANH bam xuong dat -> Punch_Left/Right, phan biet 2 tay de nhin
        // KHONG GIONG het nhau; cao co: dong tac QUET NHE hon -> Interact; binh tuoi: dong tac
        // GIO/NGHIENG tay nhe -> Wave, gan voi tu the do nuoc nhat). Neu ban co goi hoat canh nong
        // trai chuyen dung (vd Quaternius "Universal Animation Library 2" co san hoat canh Farming/
        // Fishing that su), gui cho toi de gan vao thay the cho chinh xac hon.
        // Dong tac vung tay cho tung nong cu (xem ArmPoseModifier/Motions) - thay cho viec muon tam
        // hoat canh dam/vay tay truoc day (goi Quaternius khong co hoat canh nong trai rieng).
        private static ArmPoseModifier.Motion ToolMotion(string toolId) => toolId switch
        {
            "hoe" or "cuoc_bac" or "cuoc_vang" => Motions.Overhead,
            "pickaxe" or "pickaxe_bac" or "pickaxe_vang" or "pickaxe_kim_cuong" => Motions.Overhead,
            "xeng" => Motions.Dig,
            "cao_co" => Motions.Sweep,
            "binh_tuoi" => Motions.Pour,
            "can_cau" => Motions.Cast,
            _ => Motions.Bend,
        };

        // Bam [Space]: vung nong cu - ket qua (cuoc dat, tuoi, gieo, cau ca...) xay ra DUNG luc luoi
        // cuoc cham dat / nuoc bat dau chay, khong phai ngay luc bam phim.
        private void TryUseTool()
        {
            if (_dying) return;
            if (_armPose != null && _armPose.Busy) return; // dang vung do - khong spam
            _lastActionWasAttack = false;
            RefreshHeldItem();
            string tool = Inventory.Instance.EquippedTool;
            if (tool == "can_cau" && (GetTree().GetFirstNodeInGroup("fishing_ui") as FishingUI)?.IsActive == true) return;
            if (_armPose == null || _mountedHorse != null || _mountedBoat != null) { DoUseTool(); return; }
            var motion = ToolMotion(tool);
            AimAtFarmTarget();
            float speed = motion == Motions.Pour ? 1.25f : motion == Motions.Bend ? 1.15f : 1f;
            StartMotion(motion, speed, () => { DoUseTool(); ToolImpactFx(tool, motion); });
            if (motion != Motions.Pour && motion != Motions.Bend && motion != Motions.Cast)
                _heldItem?.StartTrail(motion.Duration * motion.Impact + 0.05f, new Color(1f, 0.95f, 0.8f));
        }

        private void DoUseTool()
        {
            var plots = GetTree().GetNodesInGroup("farm_plots");
            float areaRadius = ToolAreaRadius(Inventory.Instance.EquippedTool);

            if (areaRadius > 0f)
            {
                // Cuoc nang cap: dung TAT CA o dat trong ban kinh (khong chi 1 o gan nhat). The luc
                // tinh 1 LAN cho ca vung (khong nhan theo so o) - kiem tra du TRUOC, tru SAU neu
                // that su co o nao ton suc.
                // Het the luc: van trong/thu hoach duoc, chi bo qua cac o can lam viec ton suc.
                bool canWork = GameManager.Instance.Stamina >= SkillSystem.Instance.StaminaCost(SkillType.Farming, AreaToolStamina);
                bool didAny = false, effort = false, skippedTired = false;
                foreach (var n in plots)
                    if (n is FarmPlot ap && GlobalPosition.DistanceTo(ap.GlobalPosition) <= areaRadius)
                    {
                        if (!canWork && ap.WouldTakeEffort()) { skippedTired = true; continue; }
                        effort |= ap.UseOn(true);
                        didAny = true;
                    }
                if (effort) SpendStamina(SkillType.Farming, AreaToolStamina);
                if (skippedTired) NotifyTooTired();
                if (skippedTired && !didAny) return;
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
            if (nearest != null)
            {
                if (nearest.WouldTakeEffort() && !HasStamina(SkillType.Farming, PlotToolStamina)) return;
                if (nearest.UseOn(true)) SpendStamina(SkillType.Farming, PlotToolStamina);
                return;
            }

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

            // Canh dong lua mi RIENG (xem WheatPlant.cs/Main.BuildWheatField) - cung la "cay lau
            // nam" tu moc lai sau khi hai giong FruitTree o tren, nhung KHONG PHAI FruitTree (dung
            // 3 model rieng cho 3 giai doan thay vi phong to 1 model), nen can quet nhom rieng.
            var wheatPlants = GetTree().GetNodesInGroup("wheat_plants");
            WheatPlant nearestWheat = null;
            float bestWheat = 48f;
            foreach (var n in wheatPlants)
            {
                if (n is WheatPlant w)
                {
                    float d = GlobalPosition.DistanceTo(w.GlobalPosition);
                    if (d < bestWheat) { bestWheat = d; nearestWheat = w; }
                }
            }
            if (nearestWheat != null) { nearestWheat.UseOn(); return; }

            // Cay an qua nguoi choi tu trong (PlacedFruitTree) - hai bang Space nhu cay trong.
            foreach (Node n in GetTree().GetNodesInGroup("fruit_trees_placed"))
                if (n is PlacedFruitTree pt && GlobalPosition.DistanceTo(pt.GlobalPosition) < 60f) { pt.Interact(); return; }

            // Khong co gi gan de dung - neu dang trang bi Cuoc (bat ky cap nao), thu cuoc dat MOI
            // ngay truoc mat (xem FarmPlot.TryTillFreeform) de mo rong nong trai tu do. Cuoc nang
            // cap (bac/vang) cuoc CA CUM O (3x3/5x5) cung 1 lan thay vi tung o.
            string tool = Inventory.Instance.EquippedTool;
            if (tool == "hoe" || tool == "xeng" || tool == "cuoc_bac" || tool == "cuoc_vang")
            {
                // Xeng: cong cu cuoc dat CHUYEN DUNG, cuoc duoc 2x2 O MOT LAN - o giua Cuoc Dat
                // thuong (1 o) va Cuoc Bac (3x3), rieng cho viec khai hoang dat tu do NHANH hon
                // ma khong can nang cap len Cuoc Bac (van khong co AOE tuoi/thu hoach - xem
                // ToolAreaRadius, giu Cuoc Bac/Vang la lua chon toan dien hon).
                int tilesPerSide = tool switch { "xeng" => 2, "cuoc_bac" => 3, "cuoc_vang" => 5, _ => 1 };
                // Cuoc dat ton the luc: 3 + 1/2 moi o (cuoc vang 5x5 = ~15, re hon cuoc tung o).
                float tillCost = 3f + tilesPerSide * tilesPerSide / 2f;
                if (!HasStamina(SkillType.Farming, tillCost)) return;
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
                if (tilled > 0)
                {
                    SpendStamina(SkillType.Farming, tillCost);
                    SkillSystem.Instance.AddXp(SkillType.Farming, tilled);
                }
                Notify.Show(tilled > 0 ? string.Format(Loc.T("farm.tilled_fmt"), tilled) : Loc.T("farm.cannot_till"));
            }
            else if (tool == "can_cau")
            {
                TryFish();
            }
            else if (tool == "may_tuoi_tu_dong")
            {
                TryPlaceSprinkler();
            }
            else if (tool == "bu_nhin")
            {
                TryPlaceScarecrow();
            }
            else if (tool != null && PlacementSystem.KindOf(tool) is string kind && kind != "decor")
            {
                // To ong / cay giong / may lam mut / may ep / bay cua / ho ca canh.
                var pos = GlobalPosition + _facing * 60f;
                pos.Y = GlobalPosition.Y;
                PlacementSystem.Instance.Place(tool, pos, Mathf.RadToDeg(Yaw), GetTree().CurrentScene);
                if (Inventory.Instance.CountOf(tool) == 0 && Inventory.Instance.EquippedTool == tool) Inventory.Instance.EquippedTool = null;
            }
        }


        private void TryFish()
        {
            var ui = GetTree().GetFirstNodeInGroup("fishing_ui") as FishingUI;
            if (ui == null || ui.IsActive) return;
            if (!WaterEcosystem.Instance.IsNearLake(GlobalPosition, 220f))
            {
                Notify.Show(Loc.T("fish.need_water"));
                return;
            }
            if (WaterEcosystem.Instance.Get("fish") < 5f)
            {
                Notify.Show(Loc.T("fish.depleted"));
                return;
            }
            if (!SpendStamina(SkillType.Fishing, FishStamina)) return;
            ui.Start(this);
        }

        // ==== The luc ====
        private const float PlotToolStamina = 2f;
        private const float AreaToolStamina = 5f;
        private const float FishStamina = 4f;

        private bool HasStamina(SkillType skill, float baseCost)
        {
            if (GameManager.Instance.Stamina >= SkillSystem.Instance.StaminaCost(skill, baseCost)) return true;
            NotifyTooTired();
            return false;
        }

        private bool SpendStamina(SkillType skill, float baseCost)
        {
            if (GameManager.Instance.UseStamina(SkillSystem.Instance.StaminaCost(skill, baseCost))) return true;
            NotifyTooTired();
            return false;
        }

        // Tranh spam thong bao khi giu phim lien tuc.
        private ulong _lastTiredMsg = 0;
        private void NotifyTooTired()
        {
            ulong now = Time.GetTicksMsec();
            if (now - _lastTiredMsg < 2000) return;
            _lastTiredMsg = now;
            Notify.Show(Loc.T("stamina.too_tired"));
        }

        // ==== Tang qua / vuot ve vat nuoi ====
        private void TryGiveGift()
        {
            if (_interactArea == null) return;
            foreach (var body in _interactArea.GetOverlappingBodies())
            {
                if (body is NPC npc)
                {
                    FaceToward(npc);
                    PlayEmote("Wave");
                    (GetTree().GetFirstNodeInGroup("gift_ui") as GiftUI)?.Open(npc);
                    return;
                }
            }
            Notify.Show(Loc.T("gift.no_npc"));
        }

        // Moi con vat 1 lan/ngay: +EXP Chan Nuoi (khong luu - reset khi tai lai game la chap nhan duoc).
        private readonly System.Collections.Generic.Dictionary<ulong, int> _pettedDay = new();
        private void PetAnimal(Node3D animal)
        {
            PlayAction("Interact");
            int day = GameManager.Instance.Day;
            ulong id = animal.GetInstanceId();
            if (_pettedDay.TryGetValue(id, out var d) && d == day)
            {
                Notify.Show(Loc.T("animal.already_petted"));
                return;
            }
            _pettedDay[id] = day;
            SkillSystem.Instance.AddXp(SkillType.Animal, 5);
            HomeSystem.Instance.OnAnimalPetted();
            Notify.Show(Loc.T("animal.petted"));
            // Chu "Vui!" bay len tren dau con vat.
            var pop = new Label3D
            {
                Text = Loc.T("animal.happy_pop"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.3f,
                Modulate = new Color(1f, 0.45f, 0.6f), OutlineSize = 8, NoDepthTest = true, Position = Vector3.Up * 40f,
            };
            animal.AddChild(pop);
            var tw = pop.CreateTween();
            tw.TweenProperty(pop, "position:y", 65f, 0.9f);
            tw.Parallel().TweenProperty(pop, "modulate:a", 0f, 0.9f);
            tw.TweenCallback(Callable.From(pop.QueueFree));
        }

        // Cam Bu Nhin truoc mat (xem Scarecrow.cs) - vi tri luu qua HomeSystem.ScarecrowPositions.
        private void TryPlaceScarecrow()
        {
            if (!Inventory.Instance.RemoveItem("bu_nhin", 1)) return;
            var pos = GlobalPosition + _facing * 50f;
            pos.Y = 0f;
            GetTree().CurrentScene.AddChild(new Scarecrow { Position = pos });
            HomeSystem.Instance.ScarecrowPositions.Add(new Vector2(pos.X, pos.Z));
            if (Inventory.Instance.CountOf("bu_nhin") == 0 && Inventory.Instance.EquippedTool == "bu_nhin") Inventory.Instance.EquippedTool = null;
            Notify.Show(Loc.T("farm.scarecrow_placed"));
        }

        // Dich chuyen nhanh qua Cot Da (xem WaystoneUI) - xuong ngua/thuyen, ra khoi nha neu dang
        // o trong, man hinh toi dan nhu qua cua.
        public void FastTravel(Vector3 dest)
        {
            if (_mountedHorse != null) DismountHorse();
            if (_mountedBoat != null) DismountBoat();
            _indoors = false;
            if (_camera != null) _camera.Position = OutdoorCameraOffset;
            TeleportWithFade(dest + Vector3.Up * 8f);
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
    }
}
