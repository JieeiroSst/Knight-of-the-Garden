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

		private static readonly Dictionary<string, string> ModelPathByEnemyId = new()
		{
			{ "mud_monster", "res://assets3d/quaternius/monsters/slime.glb" },
			{ "spiky_monster", "res://assets3d/quaternius/monsters/spiky_blob.glb" },
		};

		private const string AnimIdle = "CharacterArmature|Idle";
		private const string AnimWalk = "CharacterArmature|Walk";
		private const string AnimAttack = "CharacterArmature|Bite_Front";
		private const string AnimHit = "CharacterArmature|HitRecieve";
		private const string AnimDeath = "CharacterArmature|Death";

		private EnemyDef _def;
		private int _hp;
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

		// Mua Dong quai manh hon (theo yeu cau) - goi truoc AddChild tai MOI diem spawn quai
		// trong game (xem Main.SpawnEnemy va WorldStreamer.GenerateWildernessDecor - CA HAI duong
		// spawn doc lap deu can goi ham nay, thieu 1 cho se co quai khong theo mua).
		public static float SeasonalMultiplier() =>
			GameManager.Instance.CurrentSeason == GameManager.Season.Winter ? 1.6f : 1f;

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
				string path = ModelPathByEnemyId.TryGetValue(EnemyId, out var p)
					? p : "res://assets3d/quaternius/monsters/slime.glb";
				float scale = EnemyId == "spiky_monster" ? ModelScale * 1.15f : ModelScale;
				_animPlayer = CharacterRig.Attach(_model, path, scale);
				if (_animPlayer != null)
				{
					_animPlayer.AnimationFinished += OnAnimationFinished;
					PlayLoop(AnimIdle);
				}
			}
			_patrolTarget = GlobalPosition;
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			if (_state == State.Dead || _def == null)
			{
				if (!IsOnFloor()) Velocity += Vector3.Down * Gravity * dt;
				MoveAndSlide();
				return;
			}
			if (_player == null || !IsInstanceValid(_player))
				_player = GetTree().GetFirstNodeInGroup("player") as Player;

			float distToPlayer = _player != null
				? GlobalPosition.DistanceTo(_player.GlobalPosition)
				: 99999f;

			// Chuyen trang thai theo cam nhan (perception)
			Vector3 horizontal = Vector3.Zero;
			switch (_state)
			{
				case State.Idle:
				case State.Patrol:
					if (distToPlayer <= _def.DetectRange) _state = State.Chase;
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

			float vy = IsOnFloor() ? 0f : Velocity.Y - Gravity * dt;
			Velocity = new Vector3(horizontal.X, vy, horizontal.Z);
			MoveAndSlide();

			if (horizontal != Vector3.Zero)
				FaceDirection(horizontal.Normalized());
			PlayLoop(horizontal != Vector3.Zero ? AnimWalk : AnimIdle);
		}

		private void FaceDirection(Vector3 dir)
		{
			if (_model == null) return;
			var targetBasis = Basis.LookingAt(dir, Vector3.Up);
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
			_hp -= dmg;
			ShowDamageNumber(dmg);
			PlayAction(AnimHit);
			_state = State.Chase; // bi danh thi duoi theo
			if (_hp <= 0) Die();
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

			// Trao thuong
			GameManager.Instance.AddExp(_def.ExpReward);
			GameManager.Instance.AddGold(_def.GoldReward);
			QuestSystem.Instance.OnEnemyKilled(EnemyId);

			// Roi loot
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var loot in _def.Loot)
			{
				if (rng.Randf() <= loot.Chance)
				{
					int amount = rng.RandiRange(loot.Min, loot.Max);
					DroppedItem.Spawn(GetTree().CurrentScene, GlobalPosition, loot.ItemId, amount);
				}
			}
			EmitSignal(SignalName.Died, EnemyId);

			if (_animPlayer != null && _animPlayer.HasAnimation(AnimDeath))
			{
				_animPlayer.Play(AnimDeath);
				GetTree().CreateTimer(1.2).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
			}
			else
			{
				QueueFree();
			}
		}
	}
}
