using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Entities
{
	// Quai vat voi may trang thai (state machine): Idle -> Chase -> Attack.
	// Cau hinh tu EnemyDef (data-driven). Roi loot khi chet.
	// Model that + animation (Quaternius Ultimate Monsters Bundle, CC0): Pink Slime cho
	// mud_monster, Green Spiky Blob cho spiky_monster.
	public partial class Enemy : CharacterBody3D
	{
		private enum State { Idle, Patrol, Chase, Attack, Dead }

		[Export] public string EnemyId = "mud_monster";
		[Export] public float Gravity = 980f;
		[Export] public float ModelScale = 16f;
		// He so nhan vao Hp/sat thuong luc spawn (mua Dong/tang ham mo sau - xem SeasonalMultiplier).
		// KHONG sua truc tiep _def.MaxHp/_def.Damage vi _def la EnemyDef DUNG CHUNG cho MOI quai
		// cung EnemyId (singleton tra ve tu ItemDatabase) - sua thang vao do se lam sai ca nhung
		// con da spawn tu truoc/sau do, khong rieng gi con nay.
		[Export] public float StatMultiplier = 1f;
		// He so nhan EXP/vang khi ha (quai o vung xa nong trai manh hon nen thuong nhieu hon - xem
		// WorldBiomes.Danger/WorldStreamer).
		[Export] public float RewardMultiplier = 1f;

		private const float SleepDistance = 1400f;
		private bool _asleep;

		// Model 3D rieng cho tung loai quai nam trong enemies.json (EnemyDef.ModelPath/ModelScale) -
		// truoc day 9 loai quai dung chung 2 model slime/blob chi khac mau. Cac goi model Quaternius
		// dat ten hoat canh KHAC nhau (Bite_Front/HitRecieve, Punch/HitReact/Flying_Idle, Attack/
		// Gallop cua soi...) nen tra cuu theo VAI TRO voi danh sach ten ung vien (xem ResolveAnims).
		private const string DefaultModel = "res://assets3d/quaternius/monsters/slime.glb";
		private static readonly string[] IdleNames = { "Idle", "Flying_Idle" };
		private static readonly string[] WalkNames = { "Walk", "Fast_Flying", "Run", "Gallop" };
		private static readonly string[] AttackNames = { "Bite_Front", "Punch", "Headbutt", "Attack", "Weapon" };
		private static readonly string[] HitNames = { "HitRecieve", "HitReact", "Idle_HitReact_Left" };
		private static readonly string[] DeathNames = { "Death" };
		private string AnimIdle = "", AnimWalk = "", AnimAttack = "", AnimHit = "", AnimDeath = "";

		private EnemyDef _def;
		private int _hp;
		private int _maxHp;
		public EnemyDef Def => _def;
		public int Hp => _hp;
		public int MaxHp => _maxHp;
		public bool IsDead => _state == State.Dead;
		private State _state = State.Idle;
		private Player _player;
		private Node3D _model;
		private AnimationPlayer _animPlayer;
		private string _currentAnim = "";
		private bool _actionPlaying = false;
		private ulong _lastAttack = 0;
		private const int AttackCooldownMs = 900;
		private Vector3 _patrolTarget;
		private ulong _nextPatrolTime = 0;

		[Signal] public delegate void DiedEventHandler(string enemyId);

		// ==== Trum MANG (Trum The Gioi / Rong Co Dai cua to doi - xem WorldEventsClient, DungeonFloor) ====
		// Mau CHUNG do server giu: don danh cua nguoi choi gui len (NetHit), mau that ve qua SetNetHp; chi
		// guc khi server bao het mau, phan thuong do server chia (khong roi vang/do tai cho).
		public System.Action<int> NetHit;
		public bool NetMode => NetHit != null;
		public Vector3? NetHome;       // trum khong bi du ra xa diem server (server chi nhan don quanh day)
		public float NetLeash = 320f;

		public void SetNetHp(int hp, int maxHp)
		{
			if (_state == State.Dead) return;
			_maxHp = Mathf.Max(1, maxHp);
			_hp = Mathf.Clamp(hp, 0, _maxHp);
			_hpBar ??= EnemyVisuals.HpBar.Create(this, 145f + (_def?.HoverHeight ?? 0f), true);
			_hpBar.SetRatio(_hp / (float)_maxHp);
			if (_bossHudShown) UpdateBossHud();
			if (_hp <= 0) Die();
		}

		// Trum het gio / bi ha o noi khac -> bien mat (khong thuong).
		public void NetVanish()
		{
			if (_state == State.Dead) return;
			_state = State.Dead;
			if (_bossHudShown) (GetTree().GetFirstNodeInGroup("hud") as HiepSiVeVuon.UI.HUD)?.HideBossBar();
			var tw = CreateTween();
			if (_model != null) tw.TweenProperty(_model, "scale", Vector3.One * 0.01f, 0.6f);
			tw.TweenCallback(Callable.From(QueueFree));
		}

		// Mua Dong quai manh hon (theo yeu cau) - goi truoc AddChild tai MOI diem spawn quai
		// trong game (xem Main.SpawnEnemy va WorldStreamer.GenerateWildernessDecor - CA HAI duong
		// spawn doc lap deu can goi ham nay, thieu 1 cho se co quai khong theo mua).
		// Mua Dong x1.6; Dem Trang Mau (su kien the gioi) x1.6 nua.
		public static float SeasonalMultiplier() =>
			(GameManager.Instance.CurrentSeason == GameManager.Season.Winter ? 1.6f : 1f)
			* (WorldEventsClient.BloodMoon ? 1.6f : 1f);

		public override void _Ready()
		{
			AddToGroup("enemies");
			_model = GetNodeOrNull<Node3D>("Model");
			_def = ItemDatabase.Instance.GetEnemy(EnemyId);
			if (_def == null)
			{
				GD.PushWarning($"Enemy def khong tim thay: {EnemyId}");
				_hp = 30;
			}
			else
			{
				_hp = Mathf.RoundToInt(_def.MaxHp * StatMultiplier);
			}

			if (_model != null)
			{
				string path = !string.IsNullOrEmpty(_def?.ModelPath) ? _def.ModelPath : DefaultModel;
				float scale = _def != null && _def.ModelScale > 0f ? _def.ModelScale : ModelScale;
				_animPlayer = CharacterRig.Attach(_model, path, scale);
				if (_animPlayer != null)
				{
					ResolveAnims();
					_animPlayer.AnimationFinished += OnAnimationFinished;
					PlayLoop(AnimIdle);
				}
				// Khong ve quai qua xa (suong mu da che) - re hon nhieu voi model co xuong.
				Core.GraphicsSettings.SetVisibilityRange(_model, 2000f);
				// Nhuom mau (xem EnemyDef.TintColor) - game chi co 2 model quai goc, nhieu EnemyId
				// "khac nhau" (theo khu vuc, xem WorldStreamer.RegionProfile) can nhin KHAC MAU du
				// dung chung 1 file .glb.
				if (_def != null && !string.IsNullOrEmpty(_def.TintColor))
					ApplyTint(_model, new Color("#" + _def.TintColor.TrimStart('#')));
				if (_def != null) EnemyVisuals.Decorate(this, _model, _def);
			}
			_maxHp = _hp;
			if (_def != null && _def.IsBoss) SetupBoss();
			_patrolTarget = GlobalPosition;
		}

		// Chon ten hoat canh THAT co trong model cho tung vai tro (bo tien to "Armature|").
		private void ResolveAnims()
		{
			string Find(string[] wanted)
			{
				foreach (var w in wanted)
					foreach (var a in _animPlayer.GetAnimationList())
						if (a == w || a.EndsWith("|" + w)) return a;
				return "";
			}
			AnimIdle = Find(IdleNames);
			AnimWalk = Find(WalkNames);
			if (AnimWalk == "") AnimWalk = AnimIdle;
			AnimAttack = Find(AttackNames);
			AnimHit = Find(HitNames);
			AnimDeath = Find(DeathNames);
			foreach (var loop in new[] { AnimIdle, AnimWalk })
				if (loop != "" && _animPlayer.GetAnimation(loop) is Animation anim) anim.LoopMode = Animation.LoopModeEnum.Linear;
		}

		// Nhan AlbedoColor voi mau nhuom TREN material RIENG (Duplicate() + SetSurfaceOverrideMaterial,
		// KHONG sua thang material goc - material co the dung CHUNG giua nhieu quai cung model, sua
		// truc tiep se nhuom nham CA NHUNG CON KHONG can nhuom). Godot tu nhan AlbedoColor voi
		// AlbedoTexture neu co, nen van giu duoc chi tiet texture goc, chi doi TONG mau.
		// public: dung lai boi WildAnimal.cs de nhuom mau cho dong vat hoang da tai su dung model
		// (vd dog.glb -> "cao" mau cam) - cung 1 ky thuat, khong lien quan gi den logic quai.
		public static void ApplyTint(Node root, Color color)
		{
			if (root is MeshInstance3D mi && mi.Mesh != null)
			{
				int surfCount = mi.Mesh.GetSurfaceCount();
				for (int s = 0; s < surfCount; s++)
				{
					if (mi.GetActiveMaterial(s) is StandardMaterial3D baseMat)
					{
						var dup = (StandardMaterial3D)baseMat.Duplicate();
						dup.AlbedoColor = color;
						mi.SetSurfaceOverrideMaterial(s, dup);
					}
				}
			}
			foreach (var child in root.GetChildren())
				ApplyTint(child, color);
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			if (_state == State.Dead || _def == null)
			{
				if (!IsOnFloor()) Velocity = new Vector3(Velocity.X, Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed), Velocity.Z);
				MoveAndSlide();
				GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "Enemy:" + Name);
				return;
			}
			if (_player == null || !IsInstanceValid(_player))
				_player = GetTree().GetFirstNodeInGroup("player") as Player;

			float distToPlayer = _player != null
				? GlobalPosition.DistanceTo(_player.GlobalPosition)
				: 99999f;

			// Ngu khi o xa nguoi choi (khong ai thay, khong the danh nhau): bo qua AI/vat ly/hoat
			// canh - voi 25 chunk tai quanh nguoi choi, phan lon quai nam o day. Chi ngu khi da cham
			// dat (khong bi treo lo lung giua khong trung).
			bool sleep = distToPlayer > SleepDistance && _state != State.Chase && _state != State.Attack && IsOnFloor() && !_def.IsBoss;
			if (sleep != _asleep)
			{
				_asleep = sleep;
				if (_animPlayer != null) _animPlayer.SpeedScale = sleep ? 0f : 1f;
			}
			if (sleep) return;

			// Chuyen trang thai theo cam nhan (perception)
			Vector3 horizontal = Vector3.Zero;
			switch (_state)
			{
				case State.Idle:
				case State.Patrol:
					if (distToPlayer <= _def.DetectRange) { _state = State.Chase; AnimalSounds.Play(this, "monster_roar", _def.IsBoss ? 0.75f : (float)GD.RandRange(0.95, 1.3), _def.IsBoss ? 4f : -2f); }
					else horizontal = DoPatrol();
					break;
				case State.Chase:
					if (distToPlayer > _def.DetectRange * 1.6f) _state = State.Patrol;
					else if (distToPlayer <= 34f) _state = State.Attack;
					else horizontal = DoChase();
					break;
				case State.Attack:
					if (distToPlayer > 40f) _state = State.Chase;
					else DoAttack();
					break;
			}

			// Trum mang: bi "xich" quanh diem server - ra xa qua thi quay ve.
			if (NetHome is Vector3 home)
			{
				var back = home - GlobalPosition; back.Y = 0f;
				if (back.Length() > NetLeash) horizontal = back.Normalized() * Mathf.Max(90f, horizontal.Length());
			}
			float vy = IsOnFloor() ? 0f : Mathf.Max(Velocity.Y - Gravity * dt, -SteeringUtil.TerminalFallSpeed);
			Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
			MoveAndSlide();
			GlobalPosition = SteeringUtil.GuardAgainstRunaway(GlobalPosition, "Enemy:" + Name);

			if (horizontal != Vector3.Zero)
				FaceDirection(horizontal.Normalized());
			PlayLoop(horizontal != Vector3.Zero ? AnimWalk : AnimIdle);
			UpdateHover();
			if (_def.IsBoss) UpdateBoss(distToPlayer);
		}

		private void UpdateHover()
		{
			if (_model == null || _def == null || _def.HoverHeight <= 0f) return;
			float bob = Mathf.Sin(Time.GetTicksMsec() / 380f + GetInstanceId() % 7) * 3f;
			_model.Position = new Vector3(_model.Position.X, _def.HoverHeight + bob, _model.Position.Z);
		}

		private void FaceDirection(Vector3 dir)
		{
			if (_model == null) return;
			// Model Quaternius huong mat theo +Z, LookingAt cua Godot huong -Z -> lat nguoc huong
			// (giong Player/WildAnimal FlipModelFacing), neu khong quai di "lui".
			var targetBasis = Basis.LookingAt(-dir, Vector3.Up);
			_model.Basis = _model.Basis.Orthonormalized().Slerp(targetBasis, 0.15f);
		}

		private void PlayLoop(string anim)
		{
			if (_actionPlaying) return; // dang choi hoat canh tan cong/trung don, khong ghi de
			if (_animPlayer != null && _currentAnim != anim && _animPlayer.HasAnimation(anim))
			{
				_animPlayer.Play(anim);
				_currentAnim = anim;
			}
		}

		private void PlayAction(string anim)
		{
			if (_animPlayer == null || !_animPlayer.HasAnimation(anim)) return;
			_animPlayer.Play(anim);
			_currentAnim = anim;
			_actionPlaying = true;
		}

		private void OnAnimationFinished(StringName animName)
		{
			_actionPlaying = false;
		}

		private Vector3 DoPatrol()
		{
			ulong now = Time.GetTicksMsec();
			if (now >= _nextPatrolTime)
			{
				var rng = new RandomNumberGenerator();
				rng.Randomize();
				_patrolTarget = GlobalPosition + new Vector3(
					rng.RandfRange(-60, 60), 0f, rng.RandfRange(-60, 60));
				_nextPatrolTime = now + (ulong)rng.RandiRange(1500, 3000);
			}
			Vector3 dir = _patrolTarget - GlobalPosition;
			dir.Y = 0f;
			return dir.Length() > 6 ? dir.Normalized() * _def.Speed * 0.4f : Vector3.Zero;
		}

		private Vector3 DoChase()
		{
			if (_player == null) return Vector3.Zero;
			Vector3 dir = _player.GlobalPosition - GlobalPosition;
			dir.Y = 0f;
			return dir.Normalized() * _def.Speed;
		}

		private void DoAttack()
		{
			if (_player != null)
			{
				var toPlayer = _player.GlobalPosition - GlobalPosition;
				toPlayer.Y = 0f;
				FaceDirection(toPlayer.Normalized());
			}

			ulong now = Time.GetTicksMsec();
			if (now - _lastAttack < (ulong)AttackCooldownMs) return;
			_lastAttack = now;

			int dmg = Mathf.Max(1, Mathf.RoundToInt(_def.Damage * StatMultiplier) - Inventory.Instance.GetArmorDefense());
			GameManager.Instance.TakeDamage(dmg);
			PlayAction(AnimAttack);
		}

		public void TakeDamage(int dmg)
		{
			if (_state == State.Dead) return;
			if (NetMode)
			{
				// Du doan tai cho (khong guc) - server tra mau that ngay sau.
				NetHit(dmg);
				_hp = Mathf.Max(1, _hp - dmg);
				ShowDamageNumber(dmg);
				EnemyVisuals.HitFlash(_model);
				_hpBar ??= EnemyVisuals.HpBar.Create(this, 145f + (_def?.HoverHeight ?? 0f), true);
				_hpBar.SetRatio(_hp / (float)Mathf.Max(1, _maxHp));
				if (_def != null && _def.IsBoss) UpdateBossHud();
				PlayAction(AnimHit);
				_state = State.Chase;
				return;
			}
			_hp -= dmg;
			ShowDamageNumber(dmg);
			EnemyVisuals.HitFlash(_model);
			// Quai thuong cao ~1.5-2x slime (~40-55 don vi) - dat thanh mau phia tren dau.
			_hpBar ??= EnemyVisuals.HpBar.Create(this, 62f + (_def?.HoverHeight ?? 0f), false);
			_hpBar.SetRatio(Mathf.Max(0, _hp) / (float)Mathf.Max(1, _maxHp));
			if (_def != null && _def.IsBoss) UpdateBossHud();
			// Bat lui nhe ve phia sau (khong ap cho trum - trum dung vung).
			if (_player != null && _def != null && !_def.IsBoss)
			{
				var away = GlobalPosition - _player.GlobalPosition; away.Y = 0f;
				// MoveAndCollide (khong gan thang GlobalPosition) - khong bi day xuyen tuong ham nguc.
				if (away.LengthSquared() > 0.01f) MoveAndCollide(away.Normalized() * 6f);
			}
			PlayAction(AnimHit);
			_state = State.Chase; // bi danh thi duoi theo
			if (_hp <= 0) Die();
		}

		// ==== Thanh mau tren dau (hien sau lan trung don dau tien; trum luon hien) ====
		private EnemyVisuals.HpBar _hpBar;

		// ==== Trum ====
		private OmniLight3D _bossAura;
		private ulong _nextSlam = 0;
		private bool _bossHudShown = false;
		private const float SlamRadius = 75f;

		private void SetupBoss()
		{
			// Va cham to hon theo kich thuoc trum - nhan ban shape (dung chung giua moi Enemy.tscn).
			var col = GetNodeOrNull<CollisionShape3D>("Collision");
			if (col?.Shape is CapsuleShape3D cap)
			{
				var big = (CapsuleShape3D)cap.Duplicate();
				big.Radius = cap.Radius * 1.8f;
				big.Height = cap.Height * 1.8f;
				col.Shape = big;
				col.Position = new Vector3(0, big.Height / 2f, 0);
			}
			var glow = string.IsNullOrEmpty(_def.GlowColor) ? new Color(1f, 0.4f, 0.3f) : new Color("#" + _def.GlowColor);
			_bossAura = new OmniLight3D { LightColor = glow, LightEnergy = 2.5f, OmniRange = 140f, Position = Vector3.Up * 40f };
			AddChild(_bossAura);
			var name = new Label3D
			{
				Text = BossTitle, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.35f,
				Position = Vector3.Up * (160f + _def.HoverHeight), Modulate = glow.Lightened(0.3f), OutlineSize = 10, FontSize = 48, NoDepthTest = true,
			};
			AddChild(name);
			_hpBar = EnemyVisuals.HpBar.Create(this, 145f + _def.HoverHeight, true);
		}

		public string BossTitle => _def == null ? "" :
			Loc.Current == Loc.Lang.EN && !string.IsNullOrEmpty(_def.BossTitleEn) ? _def.BossTitleEn : (_def.BossTitle ?? _def.Name);

		private void UpdateBossHud()
		{
			_bossHudShown = true;
			(GetTree().GetFirstNodeInGroup("hud") as HiepSiVeVuon.UI.HUD)?.ShowBossBar(BossTitle, Mathf.Max(0, _hp), _maxHp);
		}

		private void UpdateBoss(float distToPlayer)
		{
			bool engaged = _state == State.Chase || _state == State.Attack;
			if (engaged && distToPlayer < 600f) UpdateBossHud();
			else if (_bossHudShown && distToPlayer > 800f)
			{
				_bossHudShown = false;
				(GetTree().GetFirstNodeInGroup("hud") as HiepSiVeVuon.UI.HUD)?.HideBossBar();
			}
			if (_bossAura != null) _bossAura.LightEnergy = 2.2f + Mathf.Sin(Time.GetTicksMsec() / 300f) * 0.6f;

			// Don dap dat: canh bao vong do duoi chan nguoi choi 1.1s roi gay sat thuong x1.5 neu
			// van dung trong vong - nguoi choi chu y + ne la tranh duoc.
			ulong now = Time.GetTicksMsec();
			if (!engaged || distToPlayer > 260f || now < _nextSlam || _player == null) return;
			_nextSlam = now + 5200;
			var center = _player.GlobalPosition;
			EnemyVisuals.SlamTelegraph(GetTree().CurrentScene, center, SlamRadius, 1.1f, () =>
			{
				if (_state == State.Dead || _player == null || !IsInstanceValid(_player)) return;
				var d = _player.GlobalPosition - center; d.Y = 0f;
				if (d.Length() <= SlamRadius)
				{
					int dmg = Mathf.Max(1, Mathf.RoundToInt(_def.Damage * StatMultiplier * 1.5f) - Inventory.Instance.GetArmorDefense());
					GameManager.Instance.TakeDamage(dmg);
				}
			});
			PlayAction(AnimAttack);
		}

		private void ShowDamageNumber(int dmg)
		{
			var label = new Label3D();
			label.Text = dmg.ToString();
			label.Modulate = Colors.Yellow;
			label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
			label.PixelSize = 0.3f;
			label.Position = Vector3.Up * 30f;
			label.NoDepthTest = true;
			AddChild(label);
			var tw = CreateTween();
			tw.TweenProperty(label, "position:y", 50f, 0.5f);
			tw.Parallel().TweenProperty(label, "modulate:a", 0f, 0.5f);
			tw.TweenCallback(Callable.From(label.QueueFree));
		}

		private void Die()
		{
			_state = State.Dead;
			SetCollisionLayerValue(2, false);
			SetCollisionMaskValue(1, false);

			// Trao thuong (trum mang: server chia thuong theo dong gop - khong trao tai cho)
			if (!NetMode)
			{
				GameManager.Instance.AddExp(Mathf.RoundToInt(_def.ExpReward * RewardMultiplier));
				GameManager.Instance.AddGold(Mathf.RoundToInt(_def.GoldReward * RewardMultiplier), "kill");
				QuestSystem.Instance.OnEnemyKilled(EnemyId);

				// Roi loot
				var rng = new RandomNumberGenerator();
				rng.Randomize();
				foreach (var loot in _def.Loot)
				{
					if (rng.Randf() <= loot.Chance)
					{
						int amount = rng.RandiRange(loot.Min, loot.Max);
						DroppedItem.Spawn(GetTree().CurrentScene, GlobalPosition, loot.ItemId, amount, "loot");
					}
				}
			}
			else GameManager.Instance.AddExp(Mathf.RoundToInt(_def.ExpReward * 2f));
			EmitSignal(SignalName.Died, EnemyId);
			AchievementSystem.Instance?.Add("monsters_killed");
			if (_def.IsBoss)
			{
				if (!NetMode) AdventureSystem.Instance?.OnBossDefeated(EnemyId);
				(GetTree().GetFirstNodeInGroup("hud") as HiepSiVeVuon.UI.HUD)?.HideBossBar();
				_bossAura?.QueueFree();
			}
			_hpBar?.QueueFree();
			_hpBar = null;

			// Chet: choi hoat canh chet, roi no bui hat (mau theo quai) + thu nho bien mat.
			float deathDelay = _animPlayer != null && AnimDeath != "" ? 1.1f : 0.05f;
			if (deathDelay > 0.1f) { _animPlayer.Play(AnimDeath); _actionPlaying = true; }
			var tw = CreateTween();
			tw.TweenInterval(deathDelay);
			tw.TweenCallback(Callable.From(() => EnemyVisuals.DeathBurst(this, _def)));
			if (_model != null) tw.TweenProperty(_model, "scale", Vector3.One * 0.01f, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			tw.TweenInterval(0.6f);
			tw.TweenCallback(Callable.From(QueueFree));
		}
	}
}
