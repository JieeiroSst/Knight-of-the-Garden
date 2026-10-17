using Godot;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Core
{
	// Phan mo rong cua Main (partial - tach file cho Main.cs khong phinh them): kham pha & phieu
	// luu (cot da dich chuyen, ruong bau, hang trum, Ham Nguc Co, Bao Tang, Bang Le Hoi) va cac
	// cong trinh nong trai bo sung (Bang Nang Cap Nha, Bu Nhin).
	public partial class Main
	{
		// Vi tri nam trong vung cua tung khu (lech khoi diem den xay tay o tam khu). Dung PROPERTY
		// (khong phai static readonly field): thu tu khoi tao field tinh GIUA cac file partial khong
		// duoc C# dam bao - field o day co the doc *RegionCenter (khai bao o Main.cs) khi chua gan.
		private static (string id, string label, Vector3 center, int tier, string boss)[] AdventureRegions => new[]
		{
			("forest", "map.landmark.forest", ForestRegionCenter, 2, "boss_forest"),
			("mountain", "map.landmark.mountain", MountainRegionCenter, 2, "boss_mountain"),
			("fields", "map.landmark.fields", FieldRegionCenter, 1, null),
			("lake", "map.landmark.lake", LakeRegionCenter, 2, null),
			("river", "map.landmark.river", RiverRegionCenter, 1, null),
			("village", "map.landmark.village", VillageRegionCenter, 1, null),
			("city", "map.landmark.city", BigCityRegionCenter, 1, null),
			("ruins", "map.landmark.ruins", RuinsRegionCenter, 3, "boss_ruins"),
			("cemetery", "map.landmark.cemetery", CemeteryRegionCenter, 3, "boss_cemetery"),
			("swamp", "map.landmark.swamp", SwampRegionCenter, 2, "boss_swamp"),
			("cave", "map.landmark.cave", CaveRegionCenter, 3, "boss_cave"),
		};

		private static readonly Vector3 WaystoneOffset = new(-380, 0, 380);
		private static readonly Vector3 BossLairOffset = new(520, 0, -520);
		private static readonly Vector3[] ChestOffsets = { new(720, 0, 260), new(-260, 0, -760) };
		private static Vector3 DungeonEntrancePos => RuinsRegionCenter + new Vector3(0, 0, 560);

		private void BuildAdventure()
		{
			// ==== Online nhieu nguoi choi ====
			ZoneMap.FarmCenter = FarmhousePos;
			ZoneMap.TownCenter = VillageAnchor;
			AddChild(new ChatUI());
			AddChild(new PlayerActionsUI());
			AddChild(new TradeUI());
			AddChild(new MarketUI());
			var marketBoard = new MarketBoard { Position = VillageAnchor + new Vector3(160, 0, -560) };
			_world.AddChild(marketBoard);
			AddBuildingLabelZone(marketBoard.Position, 40f, "label.market_board");

			// ==== Tinh nang them: ren, xa hoi, dau truong, trang tri, thu cung, thoi tiet ====
			AddChild(new ForgeUI());
			AddChild(new SocialUI());
			AddChild(new DuelUI());
			AddChild(new BaseUI());
			AddChild(new CaravanUI());
			BuildMarkets();
			HorseRegistry.SpawnAll(); // ngua da thuan hoa (neu save da nap truoc khi the gioi dung xong)
			AddChild(new DecorUI());
			AddChild(new PetUI());
			var forge = new ForgeStation { Position = ProcessingAnchor + new Vector3(-90, 0, -150) };
			_world.AddChild(forge);
			AddBuildingLabelZone(forge.Position, 35f, "label.forge");
			var townForge = new ForgeStation { Position = VillageAnchor + new Vector3(-170, 0, 260) };
			_world.AddChild(townForge);
			AddBuildingLabelZone(townForge.Position, 35f, "label.forge");
			// Tu Do (phim C) + Ban May: o thi tran (canh Trai Thu Cung) va o khu che bien nong trai.
			AddChild(new WardrobeUI());
			AddChild(new GuideUI()); // [F1] huong dan choi + bang viec cho nguoi moi
			var tailor = new TailorStation { Position = VillageAnchor + new Vector3(-80, 0, -600) };
			_world.AddChild(tailor);
			AddBuildingLabelZone(tailor.Position, 38f, "label.tailor");
			var farmTailor = new TailorStation { Position = ProcessingAnchor + new Vector3(-170, 0, -150) };
			_world.AddChild(farmTailor);
			AddBuildingLabelZone(farmTailor.Position, 38f, "label.tailor");
			var petBoard = new PetBoard { Position = VillageAnchor + new Vector3(-160, 0, -560) };
			_world.AddChild(petBoard);
			AddBuildingLabelZone(petBoard.Position, 40f, "label.pet_board");
			var fx = new WeatherFX();
			fx.Setup(GetNode<WorldEnvironment>("WorldEnvironment"));
			AddChild(fx);
			AddChild(new PetManager { Name = "PetManager" });
			VisitSystem.FarmCenter = FarmhousePos;

			AddChild(new WaystoneUI());
			AddChild(new MuseumUI());
			AddChild(new HomeUI());
			AddChild(new AdventureTracker());

			// Cot da: nong trai (kich hoat san) + thi tran + ham mo + moi khu vuc.
			AddWaystone("ws_farm", "label.ws_farm", FarmhousePos + new Vector3(-200, 0, 260), startActive: true);
			AddWaystone("ws_town", "map.landmark.town", VillageAnchor + new Vector3(-780, 0, -120));
			AddWaystone("ws_mine", "map.landmark.mine", MineEntrancePos + new Vector3(260, 0, 160));

			foreach (var r in AdventureRegions)
			{
				AddWaystone("ws_" + r.id, r.label, r.center + WaystoneOffset);
				for (int i = 0; i < ChestOffsets.Length; i++)
				{
					var pos = r.center + ChestOffsets[i];
					WorldStreamer.ExclusionZones.Add((pos, 60f));
					_world.AddChild(new TreasureChest { ChestId = $"chest_{r.id}_{i}", Tier = r.tier, Position = pos });
				}
				if (r.boss != null)
				{
					var lairPos = r.center + BossLairOffset;
					WorldStreamer.ExclusionZones.Add((lairPos, 230f));
					_world.AddChild(new BossLair { BossId = r.boss, LabelKey = "boss_label." + r.boss, Position = lairPos });
				}
			}

			// Bao Tang (bac Toa Thi Chinh) + Bang Le Hoi (quang truong).
			var museum = new MuseumCounter { Position = VillageAnchor + new Vector3(0, 0, -560) };
			_world.AddChild(museum);
			AddBuildingLabelZone(museum.Position, 90f, "label.museum");
			var fest = new FestivalBoard { Position = VillageAnchor + new Vector3(0, 0, 40) };
			_world.AddChild(fest);
			AddBuildingLabelZone(fest.Position, 40f, "label.festival_board");

			// Nong trai: Bang Nang Cap Nha (doi xung Thung Giao Hang) + bu nhin trang tri co san
			// cung xua qua (xem DrawGround).
			var board = new HouseBoard { Position = FarmhousePos + new Vector3(-175, 0, 20) };
			_world.AddChild(board);
			AddBuildingLabelZone(board.Position, 35f, "label.house_board");
			Scarecrow.ExtraPoints.Add(new Vector3(70, 0, 330));

			BuildDungeon();
		}

		private void AddWaystone(string id, string labelKey, Vector3 pos, bool startActive = false)
		{
			WorldStreamer.ExclusionZones.Add((pos, 60f));
			_world.AddChild(new Waystone { WaystoneId = id, LabelKey = labelKey, StartActive = startActive, Position = pos });
		}

		// ==== Ham Nguc Co: 5 tang duoi Phe Tich, moi tang 1 phong da rieng (giong Ham Mo - xem
		// BuildMine), quai manh dan theo tang, tang 5 co Rong Co Dai + ruong bau tuan. ====
		private const int DungeonFloors = 5;

		private void BuildDungeon()
		{
			WorldStreamer.ExclusionZones.Add((DungeonEntrancePos, 200f));
			var dark = GetCachedMaterial(new Color(0.22f, 0.2f, 0.24f), 1f);
			// Cong vom da: 2 tru + xa ngang + o cua toi.
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(26, 110, 26) }, Position = DungeonEntrancePos + new Vector3(-55, 55, 0), MaterialOverride = dark });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(26, 110, 26) }, Position = DungeonEntrancePos + new Vector3(55, 55, 0), MaterialOverride = dark });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(150, 24, 30) }, Position = DungeonEntrancePos + new Vector3(0, 120, 0), MaterialOverride = dark });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(84, 100, 6) }, Position = DungeonEntrancePos + new Vector3(0, 50, -8), MaterialOverride = GetCachedMaterial(new Color(0.03f, 0.02f, 0.05f), 1f) });
			_world.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 0.3f, 1f), LightEnergy = 3f, OmniRange = 160f, Position = DungeonEntrancePos + new Vector3(0, 60, 30) });
			var body = new StaticBody3D { Position = DungeonEntrancePos };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(26, 110, 26) }, Position = new Vector3(-55, 55, 0) });
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(26, 110, 26) }, Position = new Vector3(55, 55, 0) });
			_world.AddChild(body);
			AddBuildingLabelZone(DungeonEntrancePos, 120f, "label.dungeon");
			AdventureSystem.Instance.RegisterPoi(AdventureSystem.PoiKind.Dungeon, "dungeon", DungeonEntrancePos, "label.dungeon");

			var anchors = new Vector3[DungeonFloors];
			for (int i = 0; i < DungeonFloors; i++)
			{
				anchors[i] = new Vector3(DungeonEntrancePos.X, 500f + _nextInteriorIndex * 900f, DungeonEntrancePos.Z);
				_nextInteriorIndex++;
			}
			AddBuildingDoor(DungeonEntrancePos + new Vector3(0, 0, 20), 70f, isExit: false, anchors[0]);

			var wall = new Color(0.25f, 0.22f, 0.3f);
			var floorCol = new Color(0.16f, 0.15f, 0.19f);
			for (int i = 0; i < DungeonFloors; i++)
			{
				int floorNo = i + 1;
				float size = 400f + i * 30f;
				bool first = i == 0, last = i == DungeonFloors - 1;
				BuildRoom(anchors[i], size, 160f, wall.Lerp(new Color(0.35f, 0.1f, 0.12f), i / 4f), floorCol, null,
					first ? default : anchors[i - 1], backIsExit: first, last ? null : anchors[i + 1],
					a => FurnishDungeonFloor(a, floorNo, size));
			}
		}

		private void FurnishDungeonFloor(Vector3 anchor, int floorNo, float size)
		{
			float half = size / 2f;
			var pillar = GetCachedMaterial(new Color(0.3f, 0.28f, 0.33f), 1f);
			// Cot da + duoc chay (anh sang am) doc 2 ben phong.
			foreach (var x in new[] { -half * 0.6f, half * 0.6f })
			foreach (var z in new[] { -half * 0.5f, 0f, half * 0.5f })
			{
				var p = anchor + new Vector3(x, 0, z);
				_world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 10, BottomRadius = 13, Height = 150 }, Position = p + Vector3.Up * 75f, MaterialOverride = pillar });
				var body = new StaticBody3D { Position = p };
				body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 12, Height = 150 }, Position = Vector3.Up * 75f });
				_world.AddChild(body);
				_world.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 1.6f, OmniRange = 150f, Position = p + new Vector3(0, 90, 0) });
			}
			_world.AddChild(new DungeonFloor { FloorIndex = floorNo, FloorCount = DungeonFloors, RoomHalf = half, Position = anchor });
		}

		// Sau khi nap save: cam lai bu nhin nguoi choi da dat.
		private void RestoreScarecrows()
		{
			foreach (Node n in GetTree().GetNodesInGroup("scarecrows")) n.QueueFree();
			foreach (var p in HomeSystem.Instance.ScarecrowPositions)
				_world.AddChild(new Scarecrow { Position = new Vector3(p.X, 0, p.Y) });
		}
	}
}
