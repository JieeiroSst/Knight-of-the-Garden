using Godot;
using System.Linq;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.Core
{
	// Phan mo rong cua Main: 3 HAM MO MOI (ngoai Ham Mo goc o BuildMine) - moi mo 3 tang quang quy dan
	// theo do sau, can cuoc chim tot hon (OreNode.RequiredPower), quai manh dan, mo lo thien quanh cua
	// mo, cot da dich chuyen ngay canh. Khai bao bang du lieu (MineDefs) - them mo moi chi can them 1 muc.
	public partial class Main
	{
		private record OreSpot(string Ore, int Hp, int Required, Color Color, int Count, bool Shiny = false);
		private record MineFloorDef(OreSpot[] Ores, string[] Enemies, float EnemyMult, int EnemyCount);
		private record MineDef(string Id, string LabelKey, Vector3 Entrance, Color RockColor, MineFloorDef[] Floors, OreSpot[] Surface);

		// Mau quang (dung cho khoi da + tinh the lap lanh).
		private static readonly Color Coal = new(0.16f, 0.15f, 0.15f);
		private static readonly Color Iron = new(0.5f, 0.32f, 0.25f);
		private static readonly Color Silver = new(0.78f, 0.8f, 0.85f);
		private static readonly Color Gold = new(0.95f, 0.75f, 0.2f);
		private static readonly Color Stone = new(0.45f, 0.44f, 0.42f);
		private static readonly Color Quartz = new(0.88f, 0.9f, 0.98f);
		private static readonly Color Crystal = new(0.72f, 0.55f, 1f);
		private static readonly Color Diamond = new(0.65f, 0.92f, 1f);
		private static readonly Color Emerald = new(0.2f, 0.8f, 0.45f);
		private static readonly Color Ruby = new(0.9f, 0.15f, 0.25f);
		private static readonly Color Obsidian = new(0.22f, 0.12f, 0.32f);
		private static readonly Color Copper = new(0.75f, 0.45f, 0.25f);

		// PROPERTY (khong phai field): doc *RegionCenter khai bao o Main.cs - thu tu khoi tao field tinh
		// giua cac file partial khong duoc dam bao (xem MainAdventure.AdventureRegions).
		private static MineDef[] MineDefs => new[]
		{
			new MineDef("coal", "label.mine_coal", MountainRegionCenter + new Vector3(-760, 0, -160), new Color(0.26f, 0.24f, 0.23f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("than_da", 25, 0, Coal, 8), new OreSpot("da", 15, 0, Stone, 3) }, new[] { "mud_monster", "mountain_troll" }, 1.3f, 3),
					new MineFloorDef(new[] { new OreSpot("sat_tho", 40, 0, Iron, 6), new OreSpot("than_da", 25, 0, Coal, 4) }, new[] { "mountain_troll" }, 1.6f, 4),
					new MineFloorDef(new[] { new OreSpot("bac_tho", 55, 12, Silver, 7, true), new OreSpot("quang_nui", 35, 0, Stone, 3) }, new[] { "mountain_troll", "spiky_monster" }, 2.0f, 5),
				},
				new[] { new OreSpot("than_da", 25, 0, Coal, 5), new OreSpot("da", 15, 0, Stone, 4) }),
			new MineDef("crystal", "label.mine_crystal", CaveRegionCenter + new Vector3(-720, 0, 160), new Color(0.3f, 0.26f, 0.38f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("thach_anh", 35, 12, Quartz, 7, true), new OreSpot("da", 15, 0, Stone, 3) }, new[] { "cave_bat" }, 1.5f, 4),
					new MineFloorDef(new[] { new OreSpot("pha_le", 55, 24, Crystal, 6, true), new OreSpot("thach_anh", 35, 12, Quartz, 3, true) }, new[] { "cave_bat", "dungeon_alien" }, 1.9f, 4),
					new MineFloorDef(new[] { new OreSpot("kim_cuong", 90, 42, Diamond, 4, true), new OreSpot("pha_le", 55, 24, Crystal, 4, true), new OreSpot("ngoc_luc_bao", 70, 24, Emerald, 2, true) }, new[] { "dungeon_alien", "spiky_monster" }, 2.4f, 5),
				},
				new[] { new OreSpot("thach_anh", 35, 12, Quartz, 4, true), new OreSpot("da", 15, 0, Stone, 4) }),
			new MineDef("gold", "label.mine_gold", RuinsRegionCenter + new Vector3(-760, 0, -220), new Color(0.42f, 0.36f, 0.26f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("vang_tho", 45, 24, Gold, 6, true), new OreSpot("dong_tho", 20, 0, Copper, 4) }, new[] { "ruins_wraith" }, 1.7f, 4),
					new MineFloorDef(new[] { new OreSpot("vang_tho", 45, 24, Gold, 5, true), new OreSpot("hong_ngoc", 65, 24, Ruby, 4, true) }, new[] { "ruins_wraith", "dungeon_ninja" }, 2.1f, 5),
					new MineFloorDef(new[] { new OreSpot("da_hac_dien", 100, 42, Obsidian, 5, true), new OreSpot("vang_tho", 45, 24, Gold, 3, true), new OreSpot("co_vat", 60, 24, new Color(0.6f, 0.5f, 0.35f), 2) }, new[] { "dungeon_ninja", "ruins_wraith" }, 2.6f, 6),
				},
				new[] { new OreSpot("dong_tho", 20, 0, Copper, 4), new OreSpot("da", 15, 0, Stone, 4) }),

			// ==== 6 mo nua - moi khu vuc the gioi mo (tru khu dan cu) deu co ham mo rieng ====
			new MineDef("forest", "label.mine_forest", ForestRegionCenter + new Vector3(-760, 0, -160), new Color(0.3f, 0.32f, 0.26f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("dong_tho", 20, 0, Copper, 7), new OreSpot("da", 15, 0, Stone, 3) }, new[] { "forest_wolf" }, 1.2f, 3),
					new MineFloorDef(new[] { new OreSpot("sat_tho", 40, 0, Iron, 6), new OreSpot("dong_tho", 20, 0, Copper, 3) }, new[] { "forest_wolf", "spiky_monster" }, 1.5f, 4),
					new MineFloorDef(new[] { new OreSpot("ngoc_luc_bao", 70, 24, Emerald, 4, true), new OreSpot("sat_tho", 40, 0, Iron, 4) }, new[] { "forest_wolf", "spiky_monster" }, 1.9f, 5),
				},
				new[] { new OreSpot("dong_tho", 20, 0, Copper, 4), new OreSpot("da", 15, 0, Stone, 3) }),
			new MineDef("swamp", "label.mine_swamp", SwampRegionCenter + new Vector3(-760, 0, -160), new Color(0.24f, 0.28f, 0.22f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("than_da", 25, 0, Coal, 8), new OreSpot("da", 15, 0, Stone, 2) }, new[] { "swamp_lurker" }, 1.4f, 4),
					new MineFloorDef(new[] { new OreSpot("than_da", 25, 0, Coal, 4), new OreSpot("sat_tho", 40, 0, Iron, 5) }, new[] { "swamp_lurker" }, 1.7f, 4),
					new MineFloorDef(new[] { new OreSpot("bac_tho", 55, 12, Silver, 5, true), new OreSpot("ngoc_luc_bao", 70, 24, Emerald, 3, true) }, new[] { "swamp_lurker", "mud_monster" }, 2.1f, 5),
				},
				new[] { new OreSpot("than_da", 25, 0, Coal, 5) }),
			new MineDef("river", "label.mine_river", RiverRegionCenter + new Vector3(-760, 0, -160), new Color(0.36f, 0.38f, 0.4f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("dong_tho", 20, 0, Copper, 6), new OreSpot("quang_nui", 35, 0, Stone, 4) }, new[] { "mud_monster" }, 1.3f, 3),
					new MineFloorDef(new[] { new OreSpot("bac_tho", 55, 12, Silver, 6, true), new OreSpot("thach_anh", 35, 12, Quartz, 3, true) }, new[] { "lake_serpent", "mud_monster" }, 1.7f, 4),
					new MineFloorDef(new[] { new OreSpot("bac_tho", 55, 12, Silver, 4, true), new OreSpot("vang_tho", 45, 24, Gold, 5, true) }, new[] { "lake_serpent" }, 2.1f, 5),
				},
				new[] { new OreSpot("dong_tho", 20, 0, Copper, 4), new OreSpot("quang_nui", 35, 0, Stone, 3) }),
			new MineDef("lake", "label.mine_lake", LakeRegionCenter + new Vector3(-760, 0, -160), new Color(0.28f, 0.34f, 0.42f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("thach_anh", 35, 12, Quartz, 7, true), new OreSpot("da", 15, 0, Stone, 3) }, new[] { "lake_serpent" }, 1.5f, 4),
					new MineFloorDef(new[] { new OreSpot("ngoc_luc_bao", 70, 24, Emerald, 5, true), new OreSpot("thach_anh", 35, 12, Quartz, 3, true) }, new[] { "lake_serpent" }, 1.9f, 4),
					new MineFloorDef(new[] { new OreSpot("kim_cuong", 90, 42, Diamond, 3, true), new OreSpot("ngoc_luc_bao", 70, 24, Emerald, 4, true) }, new[] { "lake_serpent", "dungeon_alien" }, 2.4f, 5),
				},
				new[] { new OreSpot("thach_anh", 35, 12, Quartz, 4, true) }),
			new MineDef("cemetery", "label.mine_cemetery", CemeteryRegionCenter + new Vector3(-760, 0, -160), new Color(0.32f, 0.3f, 0.34f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("sat_tho", 40, 0, Iron, 6), new OreSpot("da", 15, 0, Stone, 3) }, new[] { "cemetery_ghost" }, 1.6f, 4),
					new MineFloorDef(new[] { new OreSpot("bac_tho", 55, 12, Silver, 5, true), new OreSpot("co_vat", 60, 24, new Color(0.6f, 0.5f, 0.35f), 3) }, new[] { "cemetery_ghost", "ruins_wraith" }, 2.0f, 5),
					new MineFloorDef(new[] { new OreSpot("da_hac_dien", 100, 42, Obsidian, 4, true), new OreSpot("hong_ngoc", 65, 24, Ruby, 4, true) }, new[] { "ruins_wraith", "cemetery_ghost" }, 2.5f, 6),
				},
				new[] { new OreSpot("sat_tho", 40, 0, Iron, 4), new OreSpot("da", 15, 0, Stone, 3) }),
			// Mo da cho nguoi moi (gan, de) - chi 2 tang.
			new MineDef("field", "label.mine_field", FieldRegionCenter + new Vector3(-760, 0, -160), new Color(0.42f, 0.4f, 0.36f),
				new[]
				{
					new MineFloorDef(new[] { new OreSpot("da", 15, 0, Stone, 6), new OreSpot("dong_tho", 20, 0, Copper, 4) }, new[] { "mud_monster" }, 1.0f, 2),
					new MineFloorDef(new[] { new OreSpot("sat_tho", 40, 0, Iron, 5), new OreSpot("than_da", 25, 0, Coal, 5) }, new[] { "mud_monster", "spiky_monster" }, 1.2f, 3),
				},
				new[] { new OreSpot("da", 15, 0, Stone, 5), new OreSpot("dong_tho", 20, 0, Copper, 3) }),
		};

		// Vi tri cua tung mo (cho ban do) - doc tu chinh MineDefs nen luon khop.
		public static System.Collections.Generic.IEnumerable<(string labelKey, Vector3 pos)> MineLocations =>
			MineDefs.Select(d => (d.LabelKey, d.Entrance)).Append(("label.mine_shaft", MineEntrancePos));

		private void BuildMoreMines()
		{
			foreach (var def in MineDefs) BuildMineFromDef(def);
		}

		private void BuildMineFromDef(MineDef def)
		{
			var pos = def.Entrance;
			WorldStreamer.ExclusionZones.Add((pos, 300f));

			// Mat tien bang MODEL 3D (CC0): khoi da lon + cong vom mo + xe goong/cuoc chim + da tang
			// quanh cua (xem BuildMineEntranceModels).
			BuildMineEntranceModels(pos, def.RockColor, def.Id);
			AddBuildingLabelZone(pos + new Vector3(0, 0, 60f), 120f, def.LabelKey);

			// Mo lo thien quanh cua mo (khong can xuong ham).
			var rng = new RandomNumberGenerator { Seed = (ulong)def.Id.GetHashCode() };
			int k = 0;
			foreach (var spot in def.Surface)
				for (int i = 0; i < spot.Count; i++, k++)
				{
					float ang = Mathf.Pi * 0.15f + k * 0.45f;
					float r = rng.RandfRange(150f, 260f);
					AddOreNodeEx(pos + new Vector3(Mathf.Cos(ang) * r, 0, 70f + Mathf.Abs(Mathf.Sin(ang)) * r), spot, regrowDays: 2, rng, persistKey: $"surf_{def.Id}_{k}");
				}

			// Cot da dich chuyen canh mo.
			AddWaystone("ws_mine_" + def.Id, def.LabelKey, pos + new Vector3(-300f, 0, -60f));

			// Cac tang (phong noi that rieng, chong len cao nhu Ham Mo goc - xem BuildMine).
			int n = def.Floors.Length;
			var anchors = new Vector3[n];
			for (int i = 0; i < n; i++)
			{
				anchors[i] = new Vector3(pos.X, 500f + _nextInteriorIndex * 900f, pos.Z);
				_nextInteriorIndex++;
			}
			AddBuildingDoor(pos + new Vector3(0, 0, 45f), 70f, isExit: false, anchors[0]);
			var wall = def.RockColor.Darkened(0.1f);
			var floorCol = def.RockColor.Darkened(0.3f);
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				float size = 380f + i * 30f;
				bool first = i == 0, last = i == n - 1;
				BuildRoom(anchors[i], size, 150f, wall, floorCol, null,
					first ? default : anchors[i - 1], backIsExit: first, last ? null : anchors[i + 1],
					a => FurnishMineFloorEx(a, def, idx, size));
			}
		}

		private void FurnishMineFloorEx(Vector3 anchor, MineDef def, int floorIdx, float size)
		{
			var floor = def.Floors[floorIdx];
			var rng = new RandomNumberGenerator { Seed = (ulong)(def.Id.GetHashCode() * 31 + floorIdx) };
			float half = size / 2f;
			int total = floor.Ores.Sum(o => o.Count);
			int k = 0;
			foreach (var spot in floor.Ores)
				for (int i = 0; i < spot.Count; i++, k++)
				{
					// Rai deu tren 2 vong tron, chua lai loi di giua phong (cau thang/cua o 2 dau).
					float ang = Mathf.Tau * k / total + rng.RandfRange(-0.15f, 0.15f);
					float r = rng.RandfRange(half * 0.35f, half * 0.75f);
					AddOreNodeEx(anchor + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r), spot, regrowDays: 2 + floorIdx, rng, persistKey: $"mine_{def.Id}_{floorIdx}_{k}");
				}
			// Den mo (anh sang am) - phong toi nhung van nhin thay duong.
			for (int i = 0; i < 4; i++)
			{
				float ang = Mathf.Tau * i / 4f + 0.4f;
				_world.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.68f, 0.35f), LightEnergy = 1.4f, OmniRange = 170f, Position = anchor + new Vector3(Mathf.Cos(ang) * half * 0.6f, 90f, Mathf.Sin(ang) * half * 0.6f) });
			}
			for (int i = 0; i < floor.EnemyCount; i++)
			{
				var e = _enemyScene.Instantiate<Enemy>();
				e.EnemyId = floor.Enemies[rng.RandiRange(0, floor.Enemies.Length - 1)];
				e.StatMultiplier = floor.EnemyMult * Enemy.SeasonalMultiplier();
				e.Position = anchor + new Vector3(rng.RandfRange(-half * 0.5f, half * 0.5f), 0, rng.RandfRange(-half * 0.5f, half * 0.5f));
				_world.AddChild(e);
			}
			string summary = string.Join(", ", floor.Ores.Select(o => ItemDatabase.Instance.GetDisplayName(o.Ore)));
			int maxReq = floor.Ores.Max(o => o.Required);
			if (maxReq > 0) summary += " - " + string.Format(Loc.T("mine.need_pick_fmt"), OreNode.PickaxeNameFor(maxReq));
			_world.AddChild(new MineFloorMarker
			{
				MineId = def.Id, MineLabelKey = def.LabelKey, Floor = floorIdx + 1, FloorCount = def.Floors.Length,
				OreSummary = summary, RoomHalf = half, Position = anchor,
			});
		}

		// Khoi quang: tang da xoay ngau nhien + (quang quy) vai manh tinh the phat sang nhe de nhan ra
		// tu xa. Toan bo hinh nam trong 1 Node3D de OreNode an/hien khi vo/moc lai.
		// Khoi quang = MODEL 3D theo loai quang (OreVisuals - quang vang/da/tinh the/da quy that), cao
		// ~26 don vi (nhan vat ~40). Toan bo hinh nam trong 1 Node3D de OreNode an/hien khi vo/moc lai.
		private OreNode AddOreNodeEx(Vector3 pos, OreSpot spot, int regrowDays, RandomNumberGenerator rng, Node3D parent = null, string persistKey = null)
		{
			var ore = new OreNode
			{
				Position = pos, MaxHp = spot.Hp, OreItemId = spot.Ore, DropAmount = 2, RegrowDays = regrowDays,
				RequiredPower = spot.Required, PersistKey = persistKey,
			};
			float h = rng.RandfRange(22f, 30f);
			var visual = OreVisuals.Build(spot.Ore, h, rng);
			ore.AddChild(visual);
			ore.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(h * 0.9f, h, h * 0.9f) }, Position = Vector3.Up * h / 2f });
			(parent ?? _world).AddChild(ore);
			ore.Init(visual);
			return ore;
		}

		// Cua ham mo dung tu model: Rock Formation (Kenney) lam vach nui sau lung, Doorway (Quaternius)
		// lam cong vom, xe goong + xe hong + cuoc chim vang de canh, da tang lon 2 ben, (mo pha le) cum
		// tinh the phat sang. Collision la hop vo hinh bao quanh khoi nui.
		private void BuildMineEntranceModels(Vector3 pos, Color rockTint, string mineId)
		{
			const string M = "res://assets3d/mining/";
			var rng = new RandomNumberGenerator { Seed = (ulong)(mineId.GetHashCode() ^ 0x51DE) };
			void Place(string model, Vector3 offset, float height, float rotY, Color? tint = null, float glow = 0f)
			{
				var m = ModelUtil.Instance(M + model);
				if (m == null) return;
				ModelUtil.FitToHeight(m, height);
				// Doi han mau (model low-poly mau phang): khoi nui Kenney goc mau sa mac (vat lieu "wood"/"sand"),
				// da tang Quaternius goc nau - to lai theo mau da cua tung vung.
				if (tint is Color t)
				{
					ModelUtil.Recolor(m, t, null, glow);
					ModelUtil.Recolor(m, t.Darkened(0.25f), "wood", glow);
				}
				else if (glow > 0f) ModelUtil.Glow(m, glow);
				var holder = new Node3D { Position = pos + offset, RotationDegrees = new Vector3(0, rotY, 0) };
				holder.AddChild(m);
				_world.AddChild(holder);
			}
			// Mau da vung + sang len cho de nhin (mau vung khai bao kha toi vi truoc day dung cho tuong ham).
			var rockCol = rockTint.Lightened(0.45f);
			Place("rock_formation.glb", new Vector3(0, 0, -45f), 130f, 0f, rockCol);
			Place("rock_large_a.glb", new Vector3(-115f, 0, -10f), 75f, rng.RandfRange(0, 360), rockCol);
			Place("rock_large_b.glb", new Vector3(118f, 0, -15f), 70f, rng.RandfRange(0, 360), rockCol);
			Place("doorway_q.glb", new Vector3(0, 0, 18f), 82f, 0f); // cong go/da goc cua model - giu nguyen mau
			Place("minecart.glb", new Vector3(-62f, 0, 70f), 18f, 25f);
			Place("cart_broken.glb", new Vector3(75f, 0, 80f), 22f, -30f);
			Place("pickaxe_gold.glb", new Vector3(46f, 0, 48f), 16f, 60f);
			Place("rocks_cluster.glb", new Vector3(-100f, 0, 110f), 20f, rng.RandfRange(0, 360), rockCol);
			if (mineId == "crystal")
			{
				Place("crystal_big.glb", new Vector3(-48f, 0, 30f), 34f, 20f, new Color(0.69f, 0.25f, 1f), 0.6f);
				Place("crystal_q.glb", new Vector3(52f, 0, 28f), 24f, -40f, new Color(0.9f, 0.94f, 1f), 0.4f);
			}
			// Cua mo toi + den treo am - nhin ro loi vao tu xa ca ban dem.
			_world.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.7f, 0.35f), LightEnergy = 2.4f, OmniRange = 140f, Position = pos + new Vector3(0, 55f, 55f) });
			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(200f, 120f, 70f) }, Position = new Vector3(0, 60f, -45f) });
			_world.AddChild(body);
		}
	}
}
