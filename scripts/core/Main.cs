using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Core
{
	// Dieu phoi man choi: dung the gioi 3D kieu Stardew Valley (nha nong dan + ruong quanh nha,
	// hang rao/duong dat noi sang khu lang co NPC), vong ngay-dem don gian, xu ly luu/nap.
	public partial class Main : Node3D
	{
		// Cache vat lieu mau don gian (khong texture, chi mau+do nham) - dung SAN chung nhau
		// thay vi tao Resource moi moi lan goi (AddInteriorWall/BuildRoom goi rat nhieu lan: moi
		// 1 trong 13 cong trinh x 2 tang x [4 tuong + san + tran] ~ 150+ lan neu khong cache).
		// Giam manh so luong RefCounted object sinh ra dong loat luc dung san - vua nhanh hon,
		// vua giam tai cho GC/he thong tham chieu native cua Godot Mono.
		private readonly Dictionary<(Color, float), StandardMaterial3D> _materialCache = new();

		private StandardMaterial3D GetCachedMaterial(Color color, float roughness = 0.85f)
		{
			var key = (color, roughness);
			if (!_materialCache.TryGetValue(key, out var mat))
			{
				mat = new StandardMaterial3D
				{
					AlbedoColor = color,
					Roughness = roughness,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled
				};
				_materialCache[key] = mat;
			}
			return mat;
		}

		// Chay 1 buoc dung san the gioi CACH LY khoi cac buoc khac - neu step nay loi, chi rieng
		// no bi anh huong (in ro loi ra log), MOI BUOC KHAC (ca truoc lan sau trong danh sach o
		// _Ready) VAN CHAY BINH THUONG. Xem ghi chu tai noi goi trong _Ready().
		private void SafeBuildStep(System.Action step, string label)
		{
			try { step(); }
			catch (System.Exception e)
			{
				GD.PushError($"Loi khi dung san buoc '{label}' (CAC BUOC KHAC van tiep tuc chay binh thuong): {e}");
			}
		}

		private PackedScene _farmScene = GD.Load<PackedScene>("res://scenes/FarmPlot.tscn");
		private PackedScene _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
		private PackedScene _npcScene = GD.Load<PackedScene>("res://scenes/NPC.tscn");
		private PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");
		private PackedScene _cowScene = GD.Load<PackedScene>("res://scenes/Cow.tscn");
		private PackedScene _farmhandScene = GD.Load<PackedScene>("res://scenes/FarmhandNpc.tscn");
		// 4 NPC nhan vien quan trong cua trang trai (Jean/Marcel/Antoine/Henri) - xem
		// BuildFarmStaff. Fence marker: "cam bien" vo hinh de Marcel biet cho nao can sua.
		private PackedScene _farmStewardScene = GD.Load<PackedScene>("res://scenes/FarmStewardNpc.tscn");
		private PackedScene _repairmanScene = GD.Load<PackedScene>("res://scenes/RepairmanNpc.tscn");
		private PackedScene _warehouseManagerScene = GD.Load<PackedScene>("res://scenes/WarehouseManagerNpc.tscn");
		private PackedScene _guardNpcScene = GD.Load<PackedScene>("res://scenes/GuardNpc.tscn");
		private PackedScene _fenceMarkerScene = GD.Load<PackedScene>("res://scenes/FenceMarker.tscn");
		// Vung thong bao ten cong trinh khi nguoi choi lai gan - xem AddBuildingLabelZone.
		private PackedScene _buildingLabelZoneScene = GD.Load<PackedScene>("res://scenes/BuildingLabelZone.tscn");
		// Doi Cam Ve bao ve nong trai (100 NPC) - xem BuildPalaceGuardBarracks.
		private PackedScene _palaceGuardScene = GD.Load<PackedScene>("res://scenes/PalaceGuardNpc.tscn");
		private PackedScene _horseScene = GD.Load<PackedScene>("res://scenes/Horse.tscn");
		private PackedScene _stablehandScene = GD.Load<PackedScene>("res://scenes/StablehandNpc.tscn");
		private PackedScene _dogScene = GD.Load<PackedScene>("res://scenes/Dog.tscn");
		private PackedScene _farmDogScene = GD.Load<PackedScene>("res://scenes/FarmDog.tscn");
		private PackedScene _farmCatScene = GD.Load<PackedScene>("res://scenes/FarmCat.tscn");
		private PackedScene _chickenScene = GD.Load<PackedScene>("res://scenes/Chicken.tscn");
		private PackedScene _poultryKeeperScene = GD.Load<PackedScene>("res://scenes/PoultryKeeperNpc.tscn");
		private PackedScene _citizenScene = GD.Load<PackedScene>("res://scenes/TownCitizenNpc.tscn");

		// He sinh thai ho nuoc (xem BuildLakeRegion/WaterEcosystem.cs) - dong vat hoang da tren
		// can dung 1 scene chung (WildAnimal.tscn), duoi nuoc dung scene rieng va cham nho hon.
		private PackedScene _wildAnimalScene = GD.Load<PackedScene>("res://scenes/WildAnimal.tscn");
		private PackedScene _wildAquaticScene = GD.Load<PackedScene>("res://scenes/WildAquatic.tscn");
		private PackedScene _boatScene = GD.Load<PackedScene>("res://scenes/Boat.tscn");

		// Nong dan toan dien hon (xem BuildGreenhouse/BuildProcessingArea/BuildCookingStation/
		// BuildBeehives) - nha kinh, may che bien, bep nau an.
		private PackedScene _greenhouseGateScene = GD.Load<PackedScene>("res://scenes/GreenhouseGate.tscn");
		private PackedScene _processingMachineScene = GD.Load<PackedScene>("res://scenes/ProcessingMachine.tscn");
		private PackedScene _cookingStationScene = GD.Load<PackedScene>("res://scenes/CookingStation.tscn");
		private PackedScene _autoSprinklerScene = GD.Load<PackedScene>("res://scenes/AutoSprinkler.tscn");

		// Danh sach vi tri cac can nha trong khu do thi (xem BuildCityDistrict) - dung de gan
		// "nha rieng" cho tung nguoi dan (SpawnTownCitizens) sau khi khu do thi da dung xong.
		private readonly List<Vector3> _cityHousePositions = new();
		private readonly List<Vector3> _cityHouseInteriors = new();

		// "Vung que nuoc Phap" - model nha 3 kich thuoc khac nhau + cau da (tat ca CC0 tu
		// poly.pizza, xac minh giay phep truoc khi tai) - xem BuildFrenchCountryside.
		private PackedScene _cottageScene = GD.Load<PackedScene>("res://assets3d/quaternius/french_countryside/cottage.glb");
		private PackedScene _villageHouseScene = GD.Load<PackedScene>("res://assets3d/quaternius/french_countryside/village_house.glb");
		private PackedScene _stoneBridgeScene = GD.Load<PackedScene>("res://assets3d/quaternius/french_countryside/stone_bridge.glb");
		private readonly List<Vector3> _frenchHousePositions = new();
		// Khoi da tuong rao 10 hecta quanh nong trai (Quaternius "Stone Wall", CC0, poly.pizza/
		// m/tdeAOh3LQV) - xem BuildFarmStoneWall.
		private PackedScene _stoneWallScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/stone_wall.glb");
		// Coi xay gio 3D (Quaternius "Tower Windmill", CC0, poly.pizza/m/52yaPyaAAG) - model co
		// 2 node RIENG BIET (than thap + canh quat), cho phep xoay canh quat that su luc runtime
		// (xem AddWindmill/WindmillBlades.cs) thay vi chi la trang tri tinh.
		private PackedScene _windmillScene = GD.Load<PackedScene>("res://assets3d/polypizza/windmill/tower_windmill.glb");
		// Thap canh 3D (Quaternius "Watch Tower", CC0, poly.pizza/m/f2J0aSLVi4) - dat o 4 goc
		// tuong da, moi thap co 1 dong lua canh gac tren dinh (xem BuildWatchTowers/AddBeaconFire).
		private PackedScene _watchTowerScene = GD.Load<PackedScene>("res://assets3d/quaternius/watchtower/watch_tower.glb");
		private PackedScene _farmWorkerScene = GD.Load<PackedScene>("res://scenes/FarmWorkerNpc.tscn");
		private PackedScene _sheepScene = GD.Load<PackedScene>("res://scenes/Sheep.tscn");
		// De 3D (Poly by Google, CC-BY 3.0, poly.pizza/m/bSetPnvQB5G) - loai vat nuoi DUY NHAT
		// KHONG phai CC0 trong toan bo game (xem CREDITS.md o goc du an - da tim rat ky nhung
		// khong ton tai ban CC0 nao cho De tren poly.pizza tai thoi diem tim kiem).
		private PackedScene _goatScene = GD.Load<PackedScene>("res://scenes/Goat.tscn");
		private PackedScene _pigScene = GD.Load<PackedScene>("res://scenes/Pig.tscn");
		private PackedScene _estateWorkerScene = GD.Load<PackedScene>("res://scenes/EstateWorkerNpc.tscn");
		private PackedScene _wellScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/well.glb");
		private PackedScene _grapesScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/grapes.glb");
		private PackedScene _beeScene = GD.Load<PackedScene>("res://assets3d/quaternius/animals/bee.glb");
		// Chuong ga (Quaternius, CC0, poly.pizza/m/DM0F8siLam) - dat lam cong trinh chinh trong
		// khu chuong ga, giong cach coi cua bo/ngua la hang rao + mang an.
		private PackedScene _chickenCoopScene = GD.Load<PackedScene>("res://assets3d/quaternius/misc/chicken_coop.glb");
		// Cot den duong (Post Lantern by Kay Lousberg, CC0, poly.pizza/m/ZSQ65S4lEu) - phong
		// cach go/lang que hop voi nong trai hon den duong hien dai.
		private PackedScene _lampPostScene = GD.Load<PackedScene>("res://assets3d/misc/lamp_post.glb");

		// Model that (Poly by Google, giay phep CC-BY - can ghi cong, khac cac asset con lai
		// deu CC0 - chon vi khong tim duoc ban CC0 phu hop sau 2 lan tim, va bu nhin nguyen thuy
		// ghep tu khoi hop khong dat yeu cau hinh anh). Ghi cong: "Scarecrow" by Poly by Google,
		// CC-BY (poly.pizza/m/7qFs_DjjuVp).
		private PackedScene _scarecrowScene = GD.Load<PackedScene>("res://assets3d/polybygoogle/scarecrow.glb");
		private PackedScene _barnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/Barn.fbx");
		// Nha o that (model hoan chinh, khong ghep manh) - "House_2" trong Quaternius Medieval
		// Village Pack, CC0, nhieu chi tiet hon (9 manh vat lieu) va cao/to hon ban truoc.
		private PackedScene _farmhouseScene = GD.Load<PackedScene>("res://assets3d/quaternius/buildings/house_v2.glb");
		private PackedScene _bigBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/BigBarn.fbx");
		private PackedScene _smallBarnScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/SmallBarn.fbx");
		private PackedScene _treeScene = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_maple_1.glb");
		private PackedScene _treeScene2 = GD.Load<PackedScene>("res://assets3d/quaternius/nature/tree_birch_1.glb");
		private PackedScene _fenceScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/fence.glb");
		private PackedScene _bridgeScene = GD.Load<PackedScene>("res://assets3d/quaternius/farm/bridge.glb");

		// Chan de (X,Z) o don vi goc cua tung loai model cong trinh - dung de tao va cham dac
		// (xem AddDecor). Barn/BigBarn dung chung 1 khung, SmallBarn nho hon, Farmhouse rieng.
		private static readonly Vector2 BarnFootprint = new(7.727f, 8.222f);
		private static readonly Vector2 SmallBarnFootprint = new(6.079f, 6.274f);
		private static readonly Vector2 FarmhouseFootprint = new(2.22f, 3.42f);
		private PackedScene _roadTileScene = GD.Load<PackedScene>("res://assets3d/quaternius/road/path_straight.glb");

		// Cua vao 3D that + do noi that (Quaternius Medieval Village Pack, CC0) cho tinh nang
		// vao/ra cong trinh - xem BuildInterior/AddBuildingEntrance.
		private PackedScene _doorScene = GD.Load<PackedScene>("res://assets3d/quaternius/buildings/door_round.glb");
		private PackedScene _benchScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bench.glb");
		private PackedScene _crateScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/crate.glb");
		private PackedScene _barrelScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/barrel.glb");
		private PackedScene _bagScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bag.glb");
		// Them do noi that rieng biet cho tung loai phong (Quaternius Ultimate House Interior
		// Pack + do vat nong trai rieng, deu CC0) - nha o co giuong/ban/ghe/lo suoi, nha kho co
		// rom/bao thoc rieng biet khong giong nha dan hay Toa Thi Chinh.
		private PackedScene _tableScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/table_round.glb");
		private PackedScene _chairScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/chair.glb");
		private PackedScene _bedScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bed_single.glb");
		private PackedScene _rugScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/rug.glb");
		private PackedScene _chandelierScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/chandelier.glb");
		private PackedScene _fireplaceScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/fireplace.glb");
		private PackedScene _hayScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/hay.glb");
		private PackedScene _sackScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/sack_trench.glb");
		private PackedScene _stairsScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/stairs.glb");
		// Vat dung nong nghiep that (dung cu + nong san) rieng cho nha kho, CC0.
		private PackedScene _pitchforkScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/pitchfork.glb");
		private PackedScene _hoeScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/hoe.glb");
		private PackedScene _wateringCanScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/watering_can.glb");
		private PackedScene _pumpkinScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/pumpkin.glb");
		private PackedScene _wheatScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/wheat.glb");
		private PackedScene _cartScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/cart.glb");
		private PackedScene _woodLogScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/wood_log.glb");
		// Bo sung noi that nha chinh theo yeu cau (tu/gia sach/den dau/ban lam viec/ruong do) -
		// tat ca CC0 tu poly.pizza, xac minh giay phep truoc khi tai.
		private PackedScene _wardrobeScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/wardrobe.glb");
		private PackedScene _bookshelfScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/bookshelf.glb");
		private PackedScene _oilLampScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/oil_lamp.glb");
		private PackedScene _workbenchScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/workbench.glb");
		private PackedScene _chestScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/chest.glb");
		private PackedScene _axeScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/axe.glb");
		private PackedScene _shovelScene = GD.Load<PackedScene>("res://assets3d/quaternius/furniture/shovel.glb");
		private PackedScene _flowerScene = GD.Load<PackedScene>("res://assets3d/kenney/nature/flower_yellowA.glb");
		private PackedScene _herbBushScene = GD.Load<PackedScene>("res://assets3d/kenney/nature/plant_bush.glb");
		private PackedScene _grassClumpScene = GD.Load<PackedScene>("res://assets3d/kenney/nature/grass_large.glb");
		private PackedScene _scheduledFarmNpcScene = GD.Load<PackedScene>("res://scenes/ScheduledFarmNpc.tscn");

		private Node3D _world;

		// Nha nong dan la tam neo cho toan bo bo cuc (ruong quanh nha, kieu Stardew Valley)
		private static readonly Vector3 FarmhousePos = new(-300, 0, -60);
		private static readonly Vector3 FarmOrigin = new(-260, 0, 180); // goc luoi ruong (gx=0, gz=0) - lui xa nha kho them 2m
		private const float FarmSpacing = 84f; // to hon 40% so voi truoc (60 -> 84), khop voi o dat da phong to
		private const int FarmGridW = 12; // rong them ~9m nua (tong ~18m so voi ban dau)
		private const int FarmGridH = 6;

		// Cong nam cua hang rao ruong (khop cong thuc trong BuildFarmFence) & tam khu lang
		private static readonly Vector3 FarmGatePos = new(
			FarmOrigin.X + (FarmGridW - 1) * FarmSpacing * 0.5f,
			0,
			FarmOrigin.Z + (FarmGridH - 1) * FarmSpacing + 42f);
		// Tam quang truong thi tran - cach nha nong dan ~500m (quy doi 20 don vi/met, nhan vat
		// cao ~40 don vi ~ 2m). Nam trong 1 "dao" dat rieng (xem DrawTownGround), khong dinh lien
		// voi khu nong trai - giua 2 khu la vung hoang da vo han that su cua WorldStreamer, noi
		// voi nhau bang 1 con duong dai (xem AddRoad).
		private static readonly Vector3 VillageAnchor = new(9250, 0, 3750);
		private const float TownGroundSize = 3500f; // phai khop voi vung reserved (15..21, 4..10) trong WorldStreamer.cs

		public override void _Ready()
		{
			_world = new Node3D { Name = "World" };
			AddChild(_world);

			DrawGround();
			DrawTownGround();
			// Duong bat dau cach cong ruong 50m (1000 don vi, quy doi 20 don vi/met) chu khong
			// sat ngay ruong - doan tu cong ra diem nay chi la co, chua trai duong.
			var roadStart = FarmGatePos + (VillageAnchor - FarmGatePos).Normalized() * 1000f;
			AddRoad(roadStart, VillageAnchor);
			SpawnPlayer();

			// Chu ky ngay-dem 24h (anh sang mat troi + mau bau troi) + vung hoang da vo han:
			// dat NGAY SAU KHI CO PLAYER, TRUOC toan bo cong trinh/vat nuoi con lai ben duoi -
			// dam bao anh sang/bau troi LUON duoc thiet lap dung cho du 1 buoc dung san nao do
			// ben duoi loi (vd 1 asset chua duoc Godot import). Truoc day doan nay nam O CUOI
			// _Ready(), nen 1 loi nho (vd .tscn/.glb loi) o giua chuoi dung san se lam ca doan
			// setup anh sang khong bao gio chay toi -> man hinh xam/mat het do hoa hoan toan.
			var dayNight = new DayNightCycle();
			dayNight.Setup(GetNode<DirectionalLight3D>("Sun"), GetNode<WorldEnvironment>("WorldEnvironment"));
			_world.AddChild(dayNight);
			_world.AddChild(new WorldStreamer());

			// Moi buoc dung san chay trong try/catch RIENG (xem SafeBuildStep) - TRUOC DAY ca 30
			// buoc nay dung CHUNG 1 try/catch duy nhat: neu 1 buoc BAT KY loi (vd 1 PackedScene
			// load hong, 1 index sai), TOAN BO cac buoc SAU no trong danh sach se KHONG BAO GIO
			// chay - day la nguyen nhan that su rat co the gay ra tinh trang "cac chuong moi bi
			// trong khong co con vat nao" da duoc bao cao nhieu lan, du ban than tung ham do khong
			// he co loi gi. Cach ly tung buoc dam bao 1 buoc loi chi mat DUNG phan cua buoc do,
			// moi buoc khac van chay day du binh thuong.
			SafeBuildStep(BuildFarm, nameof(BuildFarm));
			SafeBuildStep(BuildFarmFence, nameof(BuildFarmFence));
			SafeBuildStep(SpawnNpcs, nameof(SpawnNpcs));
			SafeBuildStep(SpawnEnemies, nameof(SpawnEnemies));
			SafeBuildStep(GiveStartingItems, nameof(GiveStartingItems));
			// BuildCowPasture/BuildCowherd/BuildHorseStable/BuildStablehand/BuildChickenCoop/
			// BuildPoultryKeeper/BuildSheepPigPasture: QUY HOACH LAI - 4 chuong "cu" (Bo/Ngua/Ga/
			// Cuu-Heo) gio duoc goi TU BEN TRONG BuildAnimalPenDistrict (gop chung vao Khu Chan
			// Nuoi, xem ham do) thay vi la buoc rieng o day - tranh xay 2 lan. BuildCowherd/
			// BuildStablehand/BuildPoultryKeeper (nha + NPC ten rieng) van la buoc doc lap, nhung
			// PHAI chay SAU BuildAnimalPenDistrict (doc CowPastureCenter/HorseStableCenter/
			// ChickenCoopCenter lam WorkPos/TroughPos - xem ghi chu o vi tri BuildAnimalPenDistrict
			// ben duoi) - da doi xuong.
			SafeBuildStep(BuildPlateaus, nameof(BuildPlateaus));
			SafeBuildStep(BuildMine, nameof(BuildMine));
			// 10 khu vuc "the gioi mo" moi (xem yeu cau nguoi dung, Mo o tren da tinh la khu thu
			// 11) - dang ky RegionProfile vao WorldStreamer.Regions TRUOC khi WorldStreamer kip
			// sinh chunk dau tien (giong cach BuildMine/BuildPlateaus dang ky ExclusionZones).
			SafeBuildStep(BuildMountainRegion, nameof(BuildMountainRegion));
			SafeBuildStep(BuildForestRegion, nameof(BuildForestRegion));
			SafeBuildStep(BuildFieldRegion, nameof(BuildFieldRegion));
			SafeBuildStep(BuildLakeRegion, nameof(BuildLakeRegion));
			SafeBuildStep(BuildRiverRegion, nameof(BuildRiverRegion));
			SafeBuildStep(BuildVillageRegion, nameof(BuildVillageRegion));
			SafeBuildStep(BuildBigCityRegion, nameof(BuildBigCityRegion));
			SafeBuildStep(BuildRuinsRegion, nameof(BuildRuinsRegion));
			SafeBuildStep(BuildCemeteryRegion, nameof(BuildCemeteryRegion));
			SafeBuildStep(BuildSwampRegion, nameof(BuildSwampRegion));
			SafeBuildStep(BuildCaveRegion, nameof(BuildCaveRegion));
			SafeBuildStep(BuildSunflowerField, nameof(BuildSunflowerField));
			SafeBuildStep(BuildCityDistrict, nameof(BuildCityDistrict));
			SafeBuildStep(SpawnTownCitizens, nameof(SpawnTownCitizens));
			SafeBuildStep(BuildFrenchCountryside, nameof(BuildFrenchCountryside));
			SafeBuildStep(SpawnFrenchVillagers, nameof(SpawnFrenchVillagers));
			SafeBuildStep(BuildFarmStoneWall, nameof(BuildFarmStoneWall));
			SafeBuildStep(BuildWatchTowers, nameof(BuildWatchTowers));
			SafeBuildStep(BuildFarmWorker, nameof(BuildFarmWorker));
			// BuildSheepPigPasture: gop vao BuildAnimalPenDistrict (xem ghi chu tren) - khong con
			// la buoc rieng o day.
			SafeBuildStep(BuildOrchard, nameof(BuildOrchard));
			SafeBuildStep(BuildVineyard, nameof(BuildVineyard));
			SafeBuildStep(BuildBeehive, nameof(BuildBeehive));
			SafeBuildStep(BuildEstateWorker, nameof(BuildEstateWorker));
			SafeBuildStep(BuildWaterFeatures, nameof(BuildWaterFeatures));
			SafeBuildStep(BuildToolAndWoodpileArea, nameof(BuildToolAndWoodpileArea));
			SafeBuildStep(BuildHerbGarden, nameof(BuildHerbGarden));
			// BuildAnimalPenDistrict PHAI chay SOM (truoc BuildWorkerDormsAndStaff/BuildFarmStaff)
			// vi 2 ham do DOC CowPastureCenter/HorseStableCenter/ChickenCoopCenter/
			// SheepPigPastureCenter lam WorkPos/FenceMarker/PatrolPoints - cac field nay CHI duoc
			// BuildAnimalPenDistrict GAN GIA TRI luc runtime (khong con la hang so co dinh, xem
			// quy hoach lai 5 khu vuc), neu chay SAU se doc phai Vector3.Zero (mac dinh).
			SafeBuildStep(BuildAnimalPenDistrict, nameof(BuildAnimalPenDistrict));
			SafeBuildStep(BuildGoatPen, nameof(BuildGoatPen));
			SafeBuildStep(BuildGreenhouse, nameof(BuildGreenhouse));
			SafeBuildStep(BuildProcessingArea, nameof(BuildProcessingArea));
			SafeBuildStep(BuildCookingStation, nameof(BuildCookingStation));
			SafeBuildStep(BuildBeehives, nameof(BuildBeehives));
			// Nha + NPC ten rieng cua 3 chuong "cu" (Bo/Ngua/Ga) - PHAI chay SAU
			// BuildAnimalPenDistrict (doc CowPastureCenter/HorseStableCenter/ChickenCoopCenter).
			SafeBuildStep(BuildCowherd, nameof(BuildCowherd));
			SafeBuildStep(BuildStablehand, nameof(BuildStablehand));
			SafeBuildStep(BuildPoultryKeeper, nameof(BuildPoultryKeeper));
			SafeBuildStep(BuildWorkerDormsAndStaff, nameof(BuildWorkerDormsAndStaff));
			SafeBuildStep(BuildFarmStaff, nameof(BuildFarmStaff));
			SafeBuildStep(BuildPalaceGuardBarracks, nameof(BuildPalaceGuardBarracks));
			SafeBuildStep(BuildBigVineyard, nameof(BuildBigVineyard));
			SafeBuildStep(BuildEstateLandscaping, nameof(BuildEstateLandscaping));
			SafeBuildStep(BuildFarmOutbuildings, nameof(BuildFarmOutbuildings));
			SafeBuildStep(BuildWindmills, nameof(BuildWindmills));
			SafeBuildStep(BuildOuterWindmills, nameof(BuildOuterWindmills));
			// PHAI chay SAU BuildAnimalPenDistrict (can vi tri THAT SU cua khu chuong/doanh trai).
			SafeBuildStep(BuildFarmPaths, nameof(BuildFarmPaths));
			// PHAI la buoc CUOI CUNG - navmesh duoc "nuong" (bake) dua tren TOAN BO va cham tinh
			// (StaticBody3D) da co trong canh luc bake, nen can moi thu (hang rao/nha/thap canh/
			// coi xay gio/chuong trai/duong mon) da dat xong het truoc do.
			SafeBuildStep(BuildFarmNavigation, nameof(BuildFarmNavigation));

			// Sang ngay thuc moi (GameManager tu phat hien qua dong ho may tinh) -> sinh them quai
			GameManager.Instance.DayChanged += _ => RespawnSomeEnemies();
			GameManager.Instance.DayChanged += _ => RespawnWildlife();
			// Den duong tu bat/tat theo dung gio (18h - 6h sang). Ap dung trang thai ban dau
			// MOT LAN cho TAT CA cot den (ca 4 cot rieng va 2*13 cot o tung cong trinh) - phai
			// dat SAU khi toan bo cong trinh da duoc xay xong (BuildPoultryKeeper la cong trinh cuoi
			// cung o tren), neu khong cac cot tao sau se giu trang thai mac dinh sai.
			GameManager.Instance.HourChanged += OnStreetLampHourChanged;
			SetStreetLampsOn(IsStreetLampHour(GameManager.Instance.Hour));
			// ...va thu cho bo giao phoi sinh be con (xem TryBreedCows).
			GameManager.Instance.DayChanged += _ => TryBreedCows();
			GameManager.Instance.DayChanged += _ => TryBreedSheep();
			GameManager.Instance.DayChanged += _ => TryBreedPigs();
			GameManager.Instance.DayChanged += _ => TryBreedHorses();
			GameManager.Instance.DayChanged += _ => TryBreedGoats();

			// The gioi da dung xong voi trang thai mac dinh - gio moi hoi backend xem nguoi choi
			// dang nhap co ban luu khong (goi MANG bat dong bo, khong the cho dong bo o day nhu
			// doc file truoc day). Neu co, du lieu that se AP LEN TREN sau khi phan hoi ve.
			SaveSystem.Instance.FetchAndApplySave(RestoreFreeformFarmPlots);
		}

		// O dat CUOC TU DO (xem FarmPlot.TryTillFreeform) KHONG nam trong luoi 12x6 co dinh
		// (BuildFarm) nen KHONG duoc tu tao lai moi lan choi nhu cac o luoi - phai tu SPAWN lai
		// node cho tung o da luu, sau khi save ve toi (goi 1 LAN, sau FetchAndApplySave).
		private void RestoreFreeformFarmPlots()
		{
			foreach (var t in SaveSystem.Instance.FarmState)
			{
				if (!t.Freeform) continue;
				var pos = new Vector3(t.PosX, 0, t.PosZ);
				var plot = _farmScene.Instantiate<FarmPlot>();
				plot.GridX = -1;
				plot.GridY = -1;
				plot.FreeformPos = pos;
				plot.Position = pos;
				AddChild(plot);
				plot.ApplyState(t);
			}

			if (_autoSprinklerScene != null)
			{
				foreach (var xz in SaveSystem.Instance.SprinklerPositions)
				{
					var sprinkler = _autoSprinklerScene.Instantiate<AutoSprinkler>();
					sprinkler.Position = new Vector3(xz.X, 0, xz.Y);
					AddChild(sprinkler);
				}
			}

			foreach (var (id, pos) in SaveSystem.Instance.PlacedBuildings)
			{
				var def = BuildingCatalog.Get(id);
				if (def != null) PlacedBuilding.Spawn(def, new Vector3(pos.X, 0, pos.Y), this);
			}
		}

		public override void _Process(double delta)
		{
			if (Input.IsActionJustPressed("save_game"))
				SaveSystem.Instance.SaveGame();
		}

		private void DrawGround()
		{
			// Phai khop CHINH XAC voi vung WorldStreamer bo qua (6 chunk x 500 = 3000, xem
			// ReservedMinCx/MaxCx/Cz trong WorldStreamer.cs) - neu nho hon se ho ra mot vanh
			// dai khong co dat ("vuc") giua San chinh va cac chunk vung hoang da.
			const float width = 3000f;
			const float depth = 3000f;

			int groundSubdiv = GroundMaterial.SubdivisionsFor(width);
			var groundMesh = new MeshInstance3D
			{
				Name = "Ground",
				Mesh = new PlaneMesh { Size = new Vector2(width, depth), SubdivideWidth = groundSubdiv, SubdivideDepth = groundSubdiv }
			};
			// Texture co that (ambientCG, CC0) + go nhe hinh hoc that (xem ground.gdshader)
			groundMesh.MaterialOverride = GroundMaterial.CreateGrass(width, depth);
			_world.AddChild(groundMesh);

			// Khoi dat dac ngay duoi lop co - de nhin tu goc nao (kem ca ngang tam mat)
			// cung thay dat that co chieu sau, khong phai mot mat phang mong "lo lung tren troi".
			_world.AddChild(GroundMaterial.CreateEarthMass(width, depth));

			// San vat ly cho nhan vat dung/roi trung (khop voi khoi dat dac o tren).
			var floorBody = new StaticBody3D { Name = "GroundCollision" };
			var floorShape = new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(width, GroundMaterial.EarthDepth, depth) },
				Position = new Vector3(0, -GroundMaterial.EarthDepth / 2f, 0)
			};
			floorBody.AddChild(floorShape);
			_world.AddChild(floorBody);

			// Nha nong dan (nha nguoi choi) - model nha that chi tiet hon, to them 20%, xoay 180 do
			AddDecor(_farmhouseScene, FarmhousePos, 66f, 180f, FarmhouseFootprint);
			AddBuildingEntrance(FarmhousePos, 180f, 150f, 120f, RoomKind.Farmhouse);
			AddBuildingLabelZone(FarmhousePos, 170f, "label.farmhouse");

			// Nha kho (barn) - dat canh ruong, cach hang rao ruong dung 5m (100 don vi) ve phia tay
			var barnPos = new Vector3(-482, 0, 250);
			AddDecor(_barnScene, barnPos, 24f, 0f, BarnFootprint);
			AddBuildingEntrance(barnPos, 0f, 150f, 110f, RoomKind.Barn);
			AddBuildingLabelZone(barnPos, 170f, "label.storage");

			// Bu nhin dung giua ruong (khe ho giua cac o dat) & cay that quanh nha
			AddDecor(_scarecrowScene, new Vector3(70, 0, 330), 13f);
			AddDecor(_treeScene, new Vector3(-470, 0, -90), 34f);
			AddDecor(_treeScene2, new Vector3(-160, 0, -260), 38f);

			// 4 cot den duong: 2 cai hai ben loi di truoc nha nong dan, 2 cai hai ben cong ruong.
			// (Moi cong trinh khac tu them 2 cot rieng ngay tai cua - xem AddBuildingEntrance.
			// Trang thai bat/tat that su duoc ap dung 1 lan cho TAT CA cot sau khi da dat xong,
			// xem cuoi _Ready().)
			AddStreetLamp(FarmhousePos + new Vector3(-45, 0, 80), 0f);
			AddStreetLamp(FarmhousePos + new Vector3(45, 0, 80), 180f);
			AddStreetLamp(new Vector3(FarmGatePos.X - 35, 0, FarmGatePos.Z), 90f);
			AddStreetLamp(new Vector3(FarmGatePos.X + 35, 0, FarmGatePos.Z), -90f);
		}

		// Thi tran: mot "dao" dat rieng, cach xa khu nong trai (~500m), noi voi nhau qua AddRoad.
		// Quang truong trung tam, Toa Thi Chinh (BigBarn) o phia bac, 3 vong nha dan quanh -
		// tat ca deu cach nhau >=180 de khong chong lan du nha da phong to.
		private void DrawTownGround()
		{
			int townSubdiv = GroundMaterial.SubdivisionsFor(TownGroundSize);
			var groundMesh = new MeshInstance3D
			{
				Name = "TownGround",
				Mesh = new PlaneMesh { Size = new Vector2(TownGroundSize, TownGroundSize), SubdivideWidth = townSubdiv, SubdivideDepth = townSubdiv },
				Position = VillageAnchor
			};
			groundMesh.MaterialOverride = GroundMaterial.CreateGrass(TownGroundSize, TownGroundSize);
			_world.AddChild(groundMesh);

			var earthMass = GroundMaterial.CreateEarthMass(TownGroundSize, TownGroundSize);
			earthMass.Position += VillageAnchor;
			_world.AddChild(earthMass);

			var floorBody = new StaticBody3D { Name = "TownGroundCollision" };
			floorBody.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(TownGroundSize, GroundMaterial.EarthDepth, TownGroundSize) },
				Position = VillageAnchor + new Vector3(0, -GroundMaterial.EarthDepth / 2f, 0)
			});
			_world.AddChild(floorBody);

			var townHallPos = VillageAnchor + new Vector3(0, 0, -180);
			AddDecor(_bigBarnScene, townHallPos, 18f, 0f, BarnFootprint); // Toa Thi Chinh
			AddBuildingEntrance(townHallPos, 0f, 120f, 90f, RoomKind.TownHall);
			AddBuildingLabelZone(townHallPos, 150f, "label.town_hall");

			// Vong trong
			AddSmallHouse(VillageAnchor + new Vector3(-170, 0, -60)); // gia lang
			AddSmallHouse(VillageAnchor + new Vector3(170, 0, -60));  // thuong nhan
			AddSmallHouse(VillageAnchor + new Vector3(-170, 0, 140)); // tho ren
			AddSmallHouse(VillageAnchor + new Vector3(170, 0, 140));  // ba lang
			AddSmallHouse(VillageAnchor + new Vector3(0, 0, 220));    // nguoi gac rung

			// Vong giua
			AddSmallHouse(VillageAnchor + new Vector3(-350, 0, 20));  // chu quan tro
			AddSmallHouse(VillageAnchor + new Vector3(350, 0, 20));   // tho moc
			AddSmallHouse(VillageAnchor + new Vector3(0, 0, 400));    // hoc gia

			// Vong ngoai (nha + NPC moi them)
			AddSmallHouse(VillageAnchor + new Vector3(-500, 0, 250)); // tho may
			AddSmallHouse(VillageAnchor + new Vector3(500, 0, 250));  // nguoi chan cuu

			AddDecor(_treeScene2, VillageAnchor + new Vector3(-300, 0, -140), 38f);
			AddDecor(_treeScene, VillageAnchor + new Vector3(300, 0, -140), 34f);
			AddDecor(_treeScene, VillageAnchor + new Vector3(-460, 0, 120), 34f);
			AddDecor(_treeScene2, VillageAnchor + new Vector3(460, 0, 120), 38f);
			AddDecor(_treeScene, VillageAnchor + new Vector3(-160, 0, 480), 34f);
			AddDecor(_treeScene2, VillageAnchor + new Vector3(160, 0, 480), 38f);
			AddDecor(_treeScene2, VillageAnchor + new Vector3(-650, 0, 350), 38f);
			AddDecor(_treeScene, VillageAnchor + new Vector3(650, 0, 350), 34f);
		}

		// collisionFootprint: kich thuoc chan de (X,Z) o don vi GOC cua model (truoc khi nhan
		// "scale") - neu khac Vector2.Zero se them va cham dac, khien nguoi choi khong the di
		// xuyen qua cong trinh (chi di vong quanh, dung nhu nha/lau dai that ngoai doi).
		private void AddDecor(PackedScene scene, Vector3 pos, float scale, float rotationYDegrees = 0f, Vector2 collisionFootprint = default)
		{
			if (scene == null) return;
			var instance = scene.Instantiate<Node3D>();
			instance.Position = pos;
			instance.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
			instance.Scale = Vector3.One * scale;
			_world.AddChild(instance);

			if (collisionFootprint != default)
			{
				var body = new StaticBody3D();
				body.Position = pos;
				body.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
				const float collisionHeight = 300f; // du cao de chan nguoi choi bat ke chieu cao that cua cong trinh
				body.AddChild(new CollisionShape3D
				{
					Shape = new BoxShape3D { Size = new Vector3(collisionFootprint.X * scale, collisionHeight, collisionFootprint.Y * scale) },
					Position = Vector3.Up * (collisionHeight / 2f)
				});
				_world.AddChild(body);
			}
		}

		private enum RoomKind { Farmhouse, Barn, TownHall, Village }

		// Danh so thu tu cong trinh -> vi tri phong duoi long dat rieng cho tung cong trinh
		// (cach nhau 500 don vi tren truc X de khong bao gio chong len nhau).
		private int _nextInteriorIndex = 0;

		private void AddSmallHouse(Vector3 pos)
		{
			AddDecor(_smallBarnScene, pos, 12f, 0f, SmallBarnFootprint);
			AddBuildingEntrance(pos, 0f, 80f, 50f, RoomKind.Village);
		}

		// Cong trinh + noi that RIENG cho tung cong trinh (khong dung chung nua - moi nha co 1
		// phong khac nhau, xem BuildRoomForKind): them 1 vung tuong tac (E) bao quanh ca cong
		// trinh (du de kich hoat tu bat ky huong tiep can nao, vi khong the xac dinh chinh xac
		// mat tien that su cua tung model neu khong xem truc quan - bai hoc tu lan gan cua rieng
		// bi lech voi tuong nha truoc day) + 1 cua 3D that (Quaternius Door Round, CC0).
		// Tra ve VI TRI GIUONG that su o tang 2 (khong phai tang tret gan cua) - de nguoi goi (vd
		// BuildCowherd) dua NPC vao DUNG cho co giuong khi ngu ban dem, thay vi dung ngay truoc
		// cua tang tret (loi cu: NPC "ngu" bang cach dung yen ngay diem interiorAnchor = tang
		// tret gan cua ra vao, nhin nhu dang dung truoc cua thay vi nam tren giuong tren phong
		// ngu tang 2 - xem BedLocalOffsetForKind ben duoi khop voi vi tri _bedScene that su trong
		// BuildRoomForKind).
		private Vector3 AddBuildingEntrance(Vector3 buildingPos, float rotationYDegrees, float triggerRadius, float doorDistance, RoomKind kind)
		{
			// Phong noi that dat NGAY PHIA TREN vi tri that cua chinh ngoi nha nay (cung X,Z) -
			// gan lien voi ngoi nha thay vi giau o mot khu vuc tach biet rat xa. Khong the nhin
			// xuyen tuong vao trong (model vo ngoai dac, khong co lo hong that), nhung it nhat
			// toa do gan dung vi tri that. Do cac nha trong lang co the dung gan nhau hon kich
			// thuoc phong (vd 2 nha dan cach nhau 200 don vi nhung phong rong toi 380), moi cong
			// trinh dung 1 do cao (Y) RIENG - each 900 don vi - de phong cua nha nay khong bao
			// gio de vao khong gian cua nha ke ben du toa do X,Z co gan nhau.
			var interiorAnchor = new Vector3(buildingPos.X, 500f + _nextInteriorIndex * 900f, buildingPos.Z);
			_nextInteriorIndex++;

			AddBuildingDoor(buildingPos, triggerRadius, isExit: false, interiorAnchor);
			// Tang 2: cung 1 vi tri X,Z, nam cao han 400 don vi (du xa moi wallHeight toi da 200 +
			// du choi de khong cham tran tang 1).
			var floor2Anchor = interiorAnchor + Vector3.Up * 400f;
			BuildRoomForKind(interiorAnchor, floor2Anchor, kind);

			var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));

			if (_doorScene != null)
			{
				var door = _doorScene.Instantiate<Node3D>();
				door.Position = buildingPos + basis * new Vector3(0, 0, doorDistance);
				door.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
				door.Scale = Vector3.One * 55f;
				_world.AddChild(door);
			}

			// Theo yeu cau: moi cong trinh co them 2 cot den, dat hai ben loi vao (dung basis da
			// tinh cho cua) - khoang cach ty le voi kich thuoc cong trinh (triggerRadius) de
			// khong dam vao tuong nha nho hay qua gan nha to.
			float lampOffset = Mathf.Max(35f, triggerRadius * 0.5f);
			AddStreetLamp(buildingPos + basis * new Vector3(-lampOffset, 0, doorDistance * 0.75f), rotationYDegrees);
			AddStreetLamp(buildingPos + basis * new Vector3(lampOffset, 0, doorDistance * 0.75f), rotationYDegrees);

			// Vi tri giuong THAT SU (dung offset da dung khi dat _bedScene trong BuildRoomForKind
			// ben tren) - day la diem NPC se teleport toi khi ngu, khong phai interiorAnchor.
			Vector3 bedLocalOffset = kind switch
			{
				RoomKind.Farmhouse => new Vector3(-100, 0, -90),
				RoomKind.TownHall => new Vector3(-140, 0, 100),
				RoomKind.Barn => Vector3.Zero, // khong ai ngu trong nha kho
				_ => new Vector3(-70, 0, -75), // Village (mac dinh - da so NPC dung loai nay)
			};
			return floor2Anchor + bedLocalOffset;
		}

		private void AddBuildingDoor(Vector3 pos, float triggerRadius, bool isExit, Vector3 interiorAnchor = default, bool isFloorChange = false, bool isAutoTrigger = false)
		{
			var door = new BuildingDoor { IsExit = isExit, InteriorAnchor = interiorAnchor, IsFloorChange = isFloorChange, IsAutoTrigger = isAutoTrigger };
			door.Position = pos;
			door.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = triggerRadius } });
			_world.AddChild(door);
		}

		// Chon kich thuoc/mau sac/do dac theo LOAI cong trinh, de moi loai nha co khong gian
		// rieng thay vi 1 phong dung chung y het nhau cho tat ca (nha nong dan am cung, nha kho
		// chat day thung/bao nhu kho that, Toa Thi Chinh rong va trang trong, nha dan don gian
		// hon va co vai bien the mau/do dac khac nhau giua cac nha). Moi cong trinh co 2 tang:
		// tang tret (groundAnchor, co cua ra ngoai + cau thang len) va tang 2 (floor2Anchor, co
		// cau thang xuong) - noi that tang 2 la 1 bo rieng, khac tang tret.
		private void BuildRoomForKind(Vector3 groundAnchor, Vector3 floor2Anchor, RoomKind kind)
		{
			switch (kind)
			{
				case RoomKind.Farmhouse:
					// roomSize PHAI du lon: camera cua Player nam co dinh cach nhan vat 115 don
					// vi VE PHIA SAU (dung offset ngoai troi, xem Player.tscn Camera3D) - neu
					// roomSize/2 < 115 thi camera se nam NGOAI phong (xuyen qua tuong sau lung),
					// nhin thay mat trong cua tuong o cu ly cuc gan -> day chinh la nguyen nhan
					// "khong gian nha bi vo" da xay ra o moi phong truoc day.
					// Tang tret: phong khach/an - ban ghe + lo suoi. Tang 2: phong ngu rieng.
					BuildRoom(groundAnchor, 380f, 150f,
						wallColor: new Color(0.83f, 0.74f, 0.58f),
						floorColor: new Color(0.5f, 0.34f, 0.19f),
						rugColor: new Color(0.55f, 0.15f, 0.15f),
						backTarget: default, backIsExit: true, upTarget: floor2Anchor,
						furnish: a =>
						{
							AddDecor(_fireplaceScene, a + new Vector3(0, 0, -145), 9f);
							AddDecor(_tableScene, a + new Vector3(110, 0, -90), 12f);
							AddDecor(_chairScene, a + new Vector3(110, 0, -55), 10f, 180f);
							AddDecor(_chairScene, a + new Vector3(75, 0, -90), 10f, 90f);
							AddDecor(_rugScene, a + new Vector3(0, 0, 30), 11f);
							AddDecor(_bagScene, a + new Vector3(-135, 0, 110), 95f);
							AddDecor(_barrelScene, a + new Vector3(130, 0, 120), 95f);
							// Ban lam viec + ruong do (theo yeu cau "ban lam viec/ruong do")
							AddDecor(_workbenchScene, a + new Vector3(-110, 0, -90), 26f, 90f);
							AddDecor(_chestScene, a + new Vector3(-150, 0, -20), 24f, 20f);
						});
					BuildRoom(floor2Anchor, 300f, 130f,
						wallColor: new Color(0.86f, 0.78f, 0.64f),
						floorColor: new Color(0.55f, 0.4f, 0.24f),
						rugColor: new Color(0.35f, 0.3f, 0.55f),
						backTarget: groundAnchor, backIsExit: false, upTarget: null,
						furnish: a =>
						{
							AddDecor(_bedScene, a + new Vector3(-100, 0, -90), 10f, 90f);
							AddDecor(_bagScene, a + new Vector3(90, 0, 100), 90f);
							// Tu, gia sach, den dau (theo yeu cau) - phong ngu
							AddDecor(_wardrobeScene, a + new Vector3(110, 0, -60), 30f, -90f);
							AddDecor(_bookshelfScene, a + new Vector3(-110, 0, 60), 28f, 90f);
							AddDecor(_oilLampScene, a + new Vector3(-40, 0, -108), 22f);
						});
					break;

				case RoomKind.Barn:
					// Dac biet theo yeu cau: kho chua VAT DUNG NONG NGHIEP (cao/cuoc/binh tuoi)
					// + SAN PHAM NONG NGHIEP (bi ngo, lua mi) that, cong voi rom/bao thoc/thung/
					// thung go/xe day/cui go - lap day ca 4 buc tuong va khoang giua phong, khong
					// giong bat ky nha nao khac hay Toa Thi Chinh. Phong nay to HAN HAN cac phong
					// khac (900 thay vi ~400) vi can du dien tich xep hang ke that su (xem
					// BuildWarehouseGrid ben duoi) - phong noi that la khong gian rieng, khong
					// gan voi kich thuoc vo ngoai that su cua nha kho nen phong to bao nhieu cung
					// duoc. Tranh 2 vung: goc Tay Bac (cau thang) va loi vao truoc cua o giua
					// tuong Nam.
					BuildRoom(groundAnchor, 900f, 220f,
						wallColor: new Color(0.5f, 0.28f, 0.2f),
						floorColor: new Color(0.42f, 0.3f, 0.18f),
						rugColor: null,
						backTarget: default, backIsExit: true, upTarget: floor2Anchor,
						furnish: a =>
						{
							// Tuong Bac
							AddDecor(_hayScene, a + new Vector3(-40, 0, -185), 118f);
							AddDecor(_sackScene, a + new Vector3(10, 0, -185), 5f, 20f);
							AddDecor(_hayScene, a + new Vector3(80, 0, -180), 120f, -15f);
							AddDecor(_crateScene, a + new Vector3(150, 0, -185), 100f);
							// Tuong Tay (phia duoi goc cau thang)
							AddDecor(_barrelScene, a + new Vector3(-185, 0, -30), 100f);
							AddDecor(_sackScene, a + new Vector3(-185, 0, 30), 5f, -20f);
							AddDecor(_crateScene, a + new Vector3(-185, 0, 90), 100f);
							AddDecor(_woodLogScene, a + new Vector3(-180, 0, 150), 5.7f, 40f);
							// Tuong Dong
							AddDecor(_crateScene, a + new Vector3(185, 0, -150), 100f, 10f);
							AddDecor(_barrelScene, a + new Vector3(185, 0, -85), 100f);
							AddDecor(_hayScene, a + new Vector3(180, 0, -20), 115f, 60f);
							AddDecor(_barrelScene, a + new Vector3(185, 0, 60), 100f, 15f);
							AddDecor(_sackScene, a + new Vector3(185, 0, 130), 5f);
							// Tuong Nam (2 ben cua ra vao o giua)
							AddDecor(_wheatScene, a + new Vector3(-150, 0, 180), 11f);
							AddDecor(_wheatScene, a + new Vector3(-125, 0, 185), 11f, 30f);
							AddDecor(_pumpkinScene, a + new Vector3(-95, 0, 180), 5.3f);
							AddDecor(_wheatScene, a + new Vector3(100, 0, 180), 11f, -20f);
							AddDecor(_pumpkinScene, a + new Vector3(130, 0, 182), 5.3f, 20f);
							AddDecor(_pumpkinScene, a + new Vector3(150, 0, 175), 5.3f, -30f);
							// Nong san thu hoach chat dong giua khu vuc Tay - dung loai cay trong
							// da co san trong game (pumpkin_seed)
							AddDecor(_pumpkinScene, a + new Vector3(-155, 0, -140), 5.3f);
							AddDecor(_pumpkinScene, a + new Vector3(-135, 0, -155), 5.3f, 40f);
							// Xe day cho hang giua phong - diem nhan trung tam
							AddDecor(_cartScene, a + new Vector3(20, 0, 20), 39f, 25f);
							AddDecor(_woodLogScene, a + new Vector3(-40, 0, 60), 5.7f, -20f);
							// Dung cu lao dong that dung gan cua ra vao, nhu vua duoc dem ve
							AddDecor(_pitchforkScene, a + new Vector3(60, 0, 155), 10f, 15f);
							AddDecor(_hoeScene, a + new Vector3(75, 0, 160), 19f, -10f);
							AddDecor(_wateringCanScene, a + new Vector3(50, 0, 165), 23f);
							// Chat them kien hang nua (rai ngau nhien co seed co dinh, tranh goc
							// cau thang va loi vao) cho kho day ap hang hoa nhu 1 nha kho that su
							// dang hoat dong, khong con trong trai. Loai hang + so luong doc TU
							// DATABASE (WarehouseDatabase/warehouse_products.json) thay vi hard-
							// code truc tiep - muon them/doi hang chi can sua JSON.
							ScatterBarnStock(a, 210f, 4001,
								new Vector3(-145, 0, -145), 95f, new Vector3(0, 0, 185), 70f,
								WarehouseDatabase.Instance.GetScatterRecipe());

							// Khu vuc ke hang chinh (nhu 1 nha kho that su): cac cot hang xep
							// CHONG CAO 8 tang, dung tung hang rieng theo cot (loai hang doc tu
							// database, UseInGrid=true) - hang xep ke, hang de loi di - cho
							// ~1000 kien hang nhung van "gon gang" (co loi di ro rang giua cac
							// day ke, khong chong len nhau) thay vi 1 dong hang lon xon. Bo qua
							// khu vuc gan cua/trung tam (da co do dac dot 1) va goc cau thang.
							BuildWarehouseGrid(a, 450f,
								new Vector3(-385, 0, -385), 90f, new Vector3(0, 0, 425), 80f, 200f,
								50f, 50f, 60f, 10, 17f,
								WarehouseDatabase.Instance.GetGridRecipe());

							// Bang go treo tren cua ra vao, hien so luong san pham (trung ga...)
							// da duoc NPC cham nuoi thu hoach va cat vao kho (xem FarmStorage,
							// PoultryKeeperNpc.HarvestNearbyEggs) - dap ung yeu cau "quy hoach lai
							// nha kho de dem so luong san pham".
							_world.AddChild(new FarmStorageBoard { Position = a + new Vector3(0, 130, 180) });
						});
					BuildRoom(floor2Anchor, 340f, 140f,
						wallColor: new Color(0.45f, 0.25f, 0.18f),
						floorColor: new Color(0.4f, 0.28f, 0.16f),
						rugColor: null,
						backTarget: groundAnchor, backIsExit: false, upTarget: null,
						furnish: a =>
						{
							// Tuong Bac
							AddDecor(_hayScene, a + new Vector3(-100, 0, -150), 115f, 20f);
							AddDecor(_sackScene, a + new Vector3(-30, 0, -150), 5f);
							AddDecor(_hayScene, a + new Vector3(50, 0, -145), 112f, -30f);
							// Tuong Tay
							AddDecor(_sackScene, a + new Vector3(-150, 0, -60), 5f, 20f);
							AddDecor(_crateScene, a + new Vector3(-150, 0, 20), 95f);
							AddDecor(_barrelScene, a + new Vector3(-150, 0, 90), 95f);
							// Nong san & rom chat quanh gac lung (tranh goc Dong Nam la cau thang xuong)
							AddDecor(_pumpkinScene, a + new Vector3(0, 0, -100), 5.3f);
							AddDecor(_pumpkinScene, a + new Vector3(25, 0, -85), 5.3f, 30f);
							AddDecor(_wheatScene, a + new Vector3(-60, 0, 100), 11f);
							AddDecor(_wheatScene, a + new Vector3(-30, 0, 110), 11f, 25f);
							AddDecor(_woodLogScene, a + new Vector3(60, 0, -60), 5.5f);
							AddDecor(_sackScene, a + new Vector3(90, 0, 0), 5f, -15f);
							// Chat them ~24 kien hang nua tren gac (tranh goc cau thang xuong)
							ScatterBarnStock(a, 170f, 4002,
								new Vector3(105, 0, 105), 95f, new Vector3(9999, 0, 9999), 0f,
								(_sackScene, 5f, 7), (_crateScene, 95f, 5), (_barrelScene, 95f, 5),
								(_hayScene, 112f, 4), (_pumpkinScene, 5.3f, 2), (_wheatScene, 11f, 1));
						});
					break;

				case RoomKind.TownHall:
					// Phong rong, tuong da xam, den chum treo giua tran - trang trong khac han
					// nha o / nha kho, dung ghe dai xep doi xung nhu 1 sanh hop. Tang 2 la phong
					// hop nho hon phia tren.
					BuildRoom(groundAnchor, 440f, 200f,
						wallColor: new Color(0.62f, 0.6f, 0.58f),
						floorColor: new Color(0.4f, 0.36f, 0.32f),
						rugColor: new Color(0.5f, 0.1f, 0.12f),
						backTarget: default, backIsExit: true, upTarget: floor2Anchor,
						furnish: a =>
						{
							AddDecor(_tableScene, a + new Vector3(0, 0, -20), 15f);
							AddDecor(_benchScene, a + new Vector3(-130, 0, -60), 105f, 90f);
							AddDecor(_benchScene, a + new Vector3(130, 0, -60), 105f, -90f);
							AddDecor(_benchScene, a + new Vector3(-130, 0, 60), 105f, 90f);
							AddDecor(_benchScene, a + new Vector3(130, 0, 60), 105f, -90f);
							AddDecor(_rugScene, a + new Vector3(0, 0, -10), 14f);
							AddDecor(_crateScene, a + new Vector3(-160, 0, -160), 100f);
							AddDecor(_crateScene, a + new Vector3(160, 0, -160), 100f);
						});
					BuildRoom(floor2Anchor, 360f, 170f,
						wallColor: new Color(0.66f, 0.63f, 0.6f),
						floorColor: new Color(0.44f, 0.4f, 0.35f),
						rugColor: new Color(0.45f, 0.12f, 0.14f),
						backTarget: groundAnchor, backIsExit: false, upTarget: null,
						furnish: a =>
						{
							AddDecor(_tableScene, a + new Vector3(0, 0, 0), 13f);
							AddDecor(_benchScene, a + new Vector3(-90, 0, -80), 95f, 90f);
							AddDecor(_benchScene, a + new Vector3(90, 0, -80), 95f, -90f);
							// Giuong truc ca canh sat (theo yeu cau: NPC ngu phai nam tren giuong
							// that, khong dung truoc cua) - offset khop voi bedLocalOffset cho
							// RoomKind.TownHall trong AddBuildingEntrance.
							AddDecor(_bedScene, a + new Vector3(-140, 0, 100), 9f, 90f);
						});
					break;

				case RoomKind.Village:
				default:
					// 3 bien the do dac/mau sac khac nhau xoay vong theo thu tu nha, moi nha co
					// giuong/ban rieng theo bien the - khong phai 9-10 can nha giong het nhau.
					// Tang tret la khong gian sinh hoat, tang 2 la phong ngu rieng.
					int variant = _nextInteriorIndex % 3;
					var palette = variant == 0
						? new Color(0.8f, 0.7f, 0.55f)
						: variant == 1 ? new Color(0.7f, 0.75f, 0.68f) : new Color(0.78f, 0.62f, 0.55f);
					var rug = variant == 0
						? new Color(0.4f, 0.35f, 0.55f)
						: variant == 1 ? new Color(0.35f, 0.45f, 0.4f) : new Color(0.5f, 0.35f, 0.3f);
					BuildRoom(groundAnchor, 380f, 140f,
						wallColor: palette,
						floorColor: new Color(0.47f, 0.32f, 0.18f),
						rugColor: rug,
						backTarget: default, backIsExit: true, upTarget: floor2Anchor,
						furnish: a =>
						{
							if (variant == 0)
							{
								AddDecor(_tableScene, a + new Vector3(110, 0, 110), 11f);
								AddDecor(_chairScene, a + new Vector3(110, 0, 145), 10f, 180f);
								AddDecor(_bagScene, a + new Vector3(125, 0, -120), 90f);
								AddDecor(_barrelScene, a + new Vector3(-125, 0, -120), 90f);
							}
							else if (variant == 1)
							{
								AddDecor(_tableScene, a + new Vector3(-110, 0, -110), 11f);
								AddDecor(_chairScene, a + new Vector3(-110, 0, -75), 10f, 180f);
								AddDecor(_chairScene, a + new Vector3(-75, 0, -110), 10f, 90f);
								AddDecor(_barrelScene, a + new Vector3(120, 0, 110), 90f);
							}
							else
							{
								AddDecor(_fireplaceScene, a + new Vector3(0, 0, 135), 8f, 180f);
								AddDecor(_bagScene, a + new Vector3(-120, 0, -110), 90f);
								AddDecor(_barrelScene, a + new Vector3(-130, 0, 130), 90f, -20f);
							}
						});
					BuildRoom(floor2Anchor, 260f, 120f,
						wallColor: palette,
						floorColor: new Color(0.5f, 0.36f, 0.2f),
						rugColor: rug,
						backTarget: groundAnchor, backIsExit: false, upTarget: null,
						furnish: a =>
						{
							AddDecor(_bedScene, a + new Vector3(-70, 0, -75), 9f, 90f);
						});
					break;
			}
		}

		// Khung phong dung chung (san/tran/tuong/den chum 3D/den/tham/cua) - tham so mau sac/
		// kich thuoc/do dac khac nhau theo tung loai cong trinh (xem BuildRoomForKind).
		// backTarget/backIsExit: cua o tuong nam - IsExit=true dan ra ngoai troi (tang tret),
		// false dan toi backTarget (vd cau thang xuong tang duoi). upTarget: neu co, them cau
		// thang that (Quaternius Stairs, CC0) + 1 diem [E] de len upTarget (vd tang tren).
		private void BuildRoom(Vector3 anchor, float roomSize, float wallHeight, Color wallColor, Color floorColor, Color? rugColor,
			Vector3 backTarget, bool backIsExit, Vector3? upTarget, System.Action<Vector3> furnish)
		{
			var floor = new MeshInstance3D
			{
				Name = "InteriorFloor",
				Mesh = new PlaneMesh { Size = new Vector2(roomSize, roomSize) },
				Position = anchor,
				MaterialOverride = GetCachedMaterial(floorColor)
			};
			_world.AddChild(floor);

			var floorBody = new StaticBody3D { Name = "InteriorFloorCollision" };
			floorBody.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(roomSize, 8f, roomSize) },
				Position = anchor + Vector3.Down * 4f
			});
			_world.AddChild(floorBody);

			// Luoi an toan: 1 tam chan rong hon va thap hon san chinh mot chut - neu vi ly do
			// gi nguoi choi lot qua san chinh (vd cham vao dung luc rat khit khi vua vao phong)
			// se rung ngay tai day thay vi roi mai vao khoang khong duoi long dat.
			var safetyBody = new StaticBody3D { Name = "InteriorSafetyFloor" };
			safetyBody.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(roomSize * 2f, 20f, roomSize * 2f) },
				Position = anchor + Vector3.Down * 70f
			});
			_world.AddChild(safetyBody);

			var ceiling = new MeshInstance3D
			{
				Name = "InteriorCeiling",
				Mesh = new PlaneMesh { Size = new Vector2(roomSize, roomSize) },
				Position = anchor + Vector3.Up * wallHeight,
				RotationDegrees = new Vector3(180, 0, 0),
				MaterialOverride = GetCachedMaterial(floorColor.Darkened(0.4f), 1f)
			};
			_world.AddChild(ceiling);

			AddInteriorWall(anchor + new Vector3(0, wallHeight / 2f, -roomSize / 2f), new Vector3(roomSize, wallHeight, 8f), wallColor);
			AddInteriorWall(anchor + new Vector3(0, wallHeight / 2f, roomSize / 2f), new Vector3(roomSize, wallHeight, 8f), wallColor);
			AddInteriorWall(anchor + new Vector3(-roomSize / 2f, wallHeight / 2f, 0), new Vector3(8f, wallHeight, roomSize), wallColor);
			AddInteriorWall(anchor + new Vector3(roomSize / 2f, wallHeight / 2f, 0), new Vector3(8f, wallHeight, roomSize), wallColor);

			// Anh sang am trong nha (den ngoai + Sun ngoai troi khong chieu toi day vi bi tran chan).
			// Phong lon (toi 440 don vi) nen 1 den giua khong du toi goc tuong - them 4 den goc.
			var lightHeight = wallHeight * 0.75f;
			AddInteriorLight(anchor + Vector3.Up * lightHeight, roomSize * 0.8f, 3.2f);
			float cornerOffset = roomSize * 0.32f;
			AddInteriorLight(anchor + new Vector3(-cornerOffset, lightHeight, -cornerOffset), roomSize * 0.6f, 1.8f);
			AddInteriorLight(anchor + new Vector3(cornerOffset, lightHeight, -cornerOffset), roomSize * 0.6f, 1.8f);
			AddInteriorLight(anchor + new Vector3(-cornerOffset, lightHeight, cornerOffset), roomSize * 0.6f, 1.8f);
			AddInteriorLight(anchor + new Vector3(cornerOffset, lightHeight, cornerOffset), roomSize * 0.6f, 1.8f);

			// Den chum 3D that (Quaternius Chandelier, CC0) treo o dung vi tri den chinh giua
			// tran - moi phong vao deu co, dung nhu yeu cau "den sang la den chum 3D".
			if (_chandelierScene != null)
			{
				var chandelier = _chandelierScene.Instantiate<Node3D>();
				chandelier.Position = anchor + Vector3.Up * lightHeight;
				chandelier.Scale = Vector3.One * 15f;
				_world.AddChild(chandelier);
			}

			if (rugColor.HasValue)
			{
				var rug = new MeshInstance3D
				{
					Mesh = new PlaneMesh { Size = new Vector2(roomSize * 0.4f, roomSize * 0.27f) },
					Position = anchor + Vector3.Up * 0.5f,
					MaterialOverride = GetCachedMaterial(rugColor.Value, 1f)
				};
				_world.AddChild(rug);
			}

			// Cua vao (mat trong) - CHI o tang co cua thoat that (backIsExit=true) - dat sat
			// tuong doi dien cua thoat, cho cam giac day la "cua ban vua di qua" thay vi mot
			// can phong khong ro loi vao/ra. Tang tren khong co cua nay (xem cau thang xuong
			// ben duoi thay the, hop ly hon vi tang tren khong the co "cua ra ngoai troi").
			if (backIsExit && _doorScene != null)
			{
				var innerDoor = _doorScene.Instantiate<Node3D>();
				innerDoor.Position = anchor + new Vector3(0, 0, roomSize / 2f - 6f);
				innerDoor.RotationDegrees = new Vector3(0, 180, 0);
				innerDoor.Scale = Vector3.One * 55f;
				_world.AddChild(innerDoor);
			}

			furnish(anchor);

			// Cau thang len tang tren (Quaternius Stairs, CC0) - dat o goc phong (Tay Bac), xa
			// noi that va xa cua chinh o tuong Nam. Vung kich hoat bao TRON ca chan cau thang
			// (ban kinh lon hon than cau thang) va TU DONG dua len tang tren ngay khi cham vao
			// (khong can bam [E]) - giong nhu buoc chan len bac thang dau tien la tu nhien di
			// len, khong phai mot thao tac rieng nhu mo cua.
			if (upTarget.HasValue && _stairsScene != null)
			{
				var stairPos = anchor + new Vector3(-(roomSize / 2f - 65f), 0, -(roomSize / 2f - 65f));
				AddStairs(stairPos, 90f);
				AddBuildingDoor(stairPos, 65f, isExit: false, upTarget.Value, isFloorChange: true, isAutoTrigger: true);
			}

			if (backIsExit)
			{
				// Tang co cua ra ngoai that (tang tret): nhan [E] gan cua o tuong Nam de ra ngoai troi.
				AddBuildingDoor(anchor + new Vector3(0, 0, roomSize / 2f - 25f), 55f, isExit: true);
			}
			else
			{
				// Tang tren: cau thang XUONG that (cung model Stairs, xoay nguoc huong voi cau
				// thang len) o goc Dong Nam - doi dien cau thang len o goc Tay Bac de khong dam
				// vao do dac. Tu dong xuong tang duoi ngay khi cham vao, giong het cau thang len.
				var downPos = anchor + new Vector3(roomSize / 2f - 65f, 0, roomSize / 2f - 65f);
				AddStairs(downPos, -90f);
				AddBuildingDoor(downPos, 65f, isExit: false, backTarget, isFloorChange: true, isAutoTrigger: true);
			}
		}

		// Model cau thang (Quaternius Stairs, CC0) khong co tam mat bao (bbox) nam o goc toa do
		// rieng cua no - tam that lech ve phia +Z khoang 2.9 don vi (o scale goc). Neu dat thang
		// "Position" cho model nay, sau khi xoay 90 do no se bi LECH SANG NGANG rat nhieu (~78
		// don vi o scale 27), nhin nhu nam GIUA phong thay vi trong goc. Bao boc trong 1 Node3D
		// trung gian roi bu lai dung do lech nay de tam cau thang luon nam DUNG vi tri da chi
		// dinh, bat ke xoay huong nao.
		private void AddStairs(Vector3 pos, float rotationYDegrees, float scale = 27f)
		{
			if (_stairsScene == null) return;
			var wrapper = new Node3D
			{
				Position = pos,
				RotationDegrees = new Vector3(0, rotationYDegrees, 0),
				Scale = Vector3.One * scale
			};
			_world.AddChild(wrapper);
			var stairs = _stairsScene.Instantiate<Node3D>();
			stairs.Position = new Vector3(0, -0.69126f, -2.87656f);
			wrapper.AddChild(stairs);
		}

		// Xep hang GON GANG theo tung HANG rieng cho moi loai (giong 1 kho hang that su duoc sap
		// xep, khong phai 1 dong do vat vut ngau nhien) - moi loai hang chiem 1 "hang" (aisle)
		// rieng, cac mon cach deu nhau (itemSpacing), tu dong xuong hang moi (rowSpacing) khi het
		// cho ngang; huong xoay gan nhu GIONG NHAU trong cung 1 hang (chi lech ngau nhien rat nho
		// ±8 do cho tu nhien, khong con xoay lung tung 360 do nhu truoc), va XEN KE 0/180 do giua
		// cac loai de nhin da dang hon (nhu 2 day ke doi mat nhau). Seed co dinh -> luon giong
		// nhau moi lan vao lai, khong doi lung tung. Tranh 2 vung tron (goc cau thang, khu vuc
		// cua ra vao) bang cach BO QUA vi tri do (khong lui lai) va tiep tuc o o tiep theo.
		private void ScatterBarnStock(Vector3 anchor, float roomHalf, int seed,
			Vector3 avoidA, float avoidARadius, Vector3 avoidB, float avoidBRadius,
			params (PackedScene scene, float scale, int count)[] items)
		{
			var rng = new RandomNumberGenerator { Seed = (ulong)seed };
			const float itemSpacing = 42f;
			const float rowSpacing = 50f;
			const float margin = 35f;
			float minX = -roomHalf + margin, maxX = roomHalf - margin, maxZ = roomHalf - margin;
			float z = -roomHalf + margin;
			float x = minX;
			float baseRotation = 0f;

			foreach (var (scene, scale, count) in items)
			{
				if (scene == null) continue;
				// Bat dau 1 hang MOI rieng cho loai hang nay NEU con du cho trong phong - neu
				// phong chat (vd tang 2 nho hon tang tret), tiep tuc ngay trong hang hien tai
				// thay vi de hang tran ra ngoai tuong.
				if (x > minX && z + rowSpacing <= maxZ)
				{
					x = minX;
					z += rowSpacing;
				}

				int placed = 0;
				int guard = 0;
				while (placed < count && guard < count * 6 + 20)
				{
					guard++;
					if (x > maxX)
					{
						x = minX;
						z += rowSpacing;
					}
					var local = new Vector3(x, 0, z);
					x += itemSpacing;
					if (local.DistanceTo(avoidA) < avoidARadius || local.DistanceTo(avoidB) < avoidBRadius)
						continue; // bo qua o nay, khong lui lai - di tiep sang vi tri ke tiep

					float rot = baseRotation + rng.RandfRange(-8f, 8f);
					AddDecor(scene, anchor + local, scale * rng.RandfRange(0.95f, 1.05f), rot);
					placed++;
				}
				baseRotation = baseRotation == 0f ? 180f : 0f;
			}
		}

		// Ke hang nha kho that su: 1 luoi cot hang, moi cot la 1 chong hang xep CAO "layers"
		// tang (1 loai hang/cot, xoay vong qua shelfTypes de co nhieu loai khac nhau tung day).
		// CHI dat cot o hang chan (rowIdx%2==0) - hang le bo trong lam LOI DI - giu bo cuc gon
		// gang, di lai duoc giua cac day ke thay vi 1 khoi hang dac kin ca phong. Tranh vung
		// tron quanh cua/cau thang va 1 vung tron trung tam (noi da co do dac dot 1 dat rieng).
		private void BuildWarehouseGrid(Vector3 anchor, float roomHalf,
			Vector3 avoidA, float avoidARadius, Vector3 avoidB, float avoidBRadius, float centerExcludeRadius,
			float rowSpacing, float colSpacing, float margin, int layers, float layerHeight,
			params (PackedScene scene, float scale)[] shelfTypes)
		{
			int typeIndex = 0;
			int rowIdx = 0;
			for (float z = -roomHalf + margin; z <= roomHalf - margin; z += rowSpacing, rowIdx++)
			{
				if (rowIdx % 2 != 0) continue; // hang le = loi di, khong dat hang
				for (float x = -roomHalf + margin; x <= roomHalf - margin; x += colSpacing)
				{
					var basePos = new Vector3(x, 0, z);
					if (basePos.Length() < centerExcludeRadius) continue;
					if (basePos.DistanceTo(avoidA) < avoidARadius) continue;
					if (basePos.DistanceTo(avoidB) < avoidBRadius) continue;

					var (scene, scale) = shelfTypes[typeIndex % shelfTypes.Length];
					typeIndex++;
					if (scene == null) continue;
					for (int layer = 0; layer < layers; layer++)
						AddDecor(scene, anchor + basePos + Vector3.Up * (layer * layerHeight), scale);
				}
			}
		}

		private void AddInteriorLight(Vector3 pos, float range, float energy)
		{
			_world.AddChild(new OmniLight3D
			{
				Position = pos,
				LightColor = new Color(1f, 0.9f, 0.72f),
				LightEnergy = energy,
				OmniRange = range
			});
		}

		private void AddInteriorWall(Vector3 center, Vector3 size, Color color)
		{
			var mesh = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = size },
				Position = center,
				MaterialOverride = GetCachedMaterial(color)
			};
			_world.AddChild(mesh);

			var body = new StaticBody3D { Position = center };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
			_world.AddChild(body);
		}

		// Ve mot doan duong dat phang noi 2 diem (dung mau, khong can texture lap).
		private void AddPath(Vector3 from, Vector3 to, float width)
		{
			var mid = (from + to) / 2f;
			float length = from.DistanceTo(to);
			var mesh = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(width, length) } };
			mesh.Position = mid + Vector3.Up * 0.1f; // nhinh nhe tren co, tranh z-fighting
			mesh.RotateY(Mathf.Atan2(to.X - from.X, to.Z - from.Z));
			mesh.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.55f, 0.45f, 0.3f),
				Roughness = 1f
			};
			_world.AddChild(mesh);
		}

		// Duong noi nong trai - thi tran: nen dat phang mong (dam bao khong ho suot quang duong
		// dai) + rai tam duong da mon 3D that (Quaternius Path Straight, CC0 - nho gon, phong
		// cach dong que, hop voi cac asset khac) len tren, cong go bac giua duong lam diem nhan.
		private void AddRoad(Vector3 from, Vector3 to)
		{
			const float nativeLength = 0.901f; // truc dai la +Z cua model
			const float nativeWidth = 0.496f;
			const float targetTileLength = 45f; // nho gon hon duong cu (50 -> ~25 be rong)
			float tileScale = targetTileLength / nativeLength;
			float roadWidth = nativeWidth * tileScale;

			AddPath(from, to, roadWidth);

			if (_roadTileScene != null)
			{
				float dist = from.DistanceTo(to);
				int count = Mathf.Max(1, Mathf.RoundToInt(dist / targetTileLength));
				float actualTileLength = dist / count;
				float actualScale = actualTileLength / nativeLength;
				Vector3 dir = (to - from).Normalized();
				float angleDeg = Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z));
				for (int i = 0; i < count; i++)
				{
					var inst = _roadTileScene.Instantiate<Node3D>();
					inst.Position = from + dir * (actualTileLength * (i + 0.5f)) + Vector3.Up * 0.15f;
					inst.RotationDegrees = new Vector3(0, angleDeg, 0);
					inst.Scale = Vector3.One * actualScale;
					_world.AddChild(inst);
				}
			}

			var pathDir = (to - from).Normalized();
			var bridgeAngle = Mathf.RadToDeg(Mathf.Atan2(-pathDir.Z, pathDir.X));
			AddDecor(_bridgeScene, (from + to) / 2f, 19f, bridgeAngle);
		}

		// Rai lien tiep mot doan hang rao go (Quaternius Fence, truc dai la +X cua model)
		// tu diem nay sang diem kia.
		private void AddFenceLine(Vector3 from, Vector3 to, PackedScene fenceScene)
		{
			if (fenceScene == null) return;
			const float nativeLength = 5.89f;
			const float targetSegment = 100f; // hang rao to hon, cao ~1/2 nguoi choi thay vi lun thun

			float dist = from.DistanceTo(to);
			int count = Mathf.Max(1, Mathf.RoundToInt(dist / targetSegment));
			// Chia deu khop khit tu "from" den "to" (khong dung targetSegment truc tiep) de mep
			// ngoai cua manh dau/cuoi roi dung vao "from"/"to" (cot goc) - khong tam manh tai goc,
			// neu khong manh se tho ra ngoai cot mot nua chieu dai.
			float actualSegment = dist / count;
			float fenceScale = actualSegment / nativeLength;

			Vector3 dir = (to - from).Normalized();
			float angleDeg = Mathf.RadToDeg(Mathf.Atan2(-dir.Z, dir.X));
			for (int i = 0; i < count; i++)
			{
				var segPos = from + dir * (actualSegment * (i + 0.5f));
				var inst = fenceScene.Instantiate<Node3D>();
				inst.Position = segPos;
				inst.RotationDegrees = new Vector3(0, angleDeg, 0);
				inst.Scale = Vector3.One * fenceScale;
				_world.AddChild(inst);

				// Va cham dac cho tung doan hang rao - truoc day hang rao chi la hinh anh, ca
				// nguoi choi lan dong vat (bo) deu co the di xuyen qua. Hop va cham xoay dung
				// theo huong doan hang rao, dai bang doan do.
				var body = new StaticBody3D();
				body.Position = segPos;
				body.RotationDegrees = new Vector3(0, angleDeg, 0);
				body.AddChild(new CollisionShape3D
				{
					Shape = new BoxShape3D { Size = new Vector3(actualSegment, 50f, 8f) },
					Position = Vector3.Up * 25f
				});
				_world.AddChild(body);
			}
		}

		private void BuildFarmFence()
		{
			float minX = FarmOrigin.X - 42f;
			float maxX = FarmOrigin.X + (FarmGridW - 1) * FarmSpacing + 42f;
			float minZ = FarmOrigin.Z - 42f;
			float maxZ = FarmGatePos.Z;
			float gateX = FarmGatePos.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
			// Nam - chua cong o giua huong ra duong sang lang
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 20f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 20f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			// Khoang ho giua 2 doan hang rao tren chinh la loi vao (khong co model cong rieng)

			// Cot go o dung 4 goc de che diem giao nhau cua 2 doan hang rao vuong goc
			// (2 tam go mong cham nhau ngay tai goc nhin bi det/xau neu khong co cot).
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));
		}

		private void AddFencePost(Vector3 pos)
		{
			const float postRadius = 4f;
			const float postHeight = 24f;
			var post = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = postRadius, BottomRadius = postRadius * 1.15f, Height = postHeight },
				Position = pos + Vector3.Up * (postHeight / 2f),
				MaterialOverride = GetCachedMaterial(new Color(0.3f, 0.2f, 0.1f), 1f)
			};
			_world.AddChild(post);
		}

		// Cot den duong 3D that (Post Lantern by Kay Lousberg, CC0) + 1 den vang am phat sang
		// that su tu dinh den (nhin ro nhat vao ban dem qua he thong ngay-dem da co) + va cham
		// dac (mong) de nguoi choi khong xuyen qua duoc cot.
		private void AddStreetLamp(Vector3 pos, float rotationYDegrees)
		{
			if (_lampPostScene != null)
			{
				var lamp = _lampPostScene.Instantiate<Node3D>();
				lamp.Position = pos;
				lamp.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
				lamp.Scale = Vector3.One * 18f;
				_world.AddChild(lamp);

				// Cho phan "kinh den" TU PHAT SANG that su (emission) khi den bat - khong chi
				// dua vao anh sang OmniLight chieu ra xung quanh (co the qua nho/mo, kho nhan
				// biet ro la "den dang sang" hay khong). Neu khong tim thay dung ten mesh con
				// (tuy phien ban model), du phong sang toan bo cot den.
				var glowNode = FindNodeByName(lamp, "post_lantern_lantern") as MeshInstance3D
					?? FindNodeByName(lamp, "post_lantern") as MeshInstance3D;
				if (glowNode != null)
				{
					var glowMat = new StandardMaterial3D
					{
						AlbedoColor = new Color(1f, 0.85f, 0.5f),
						EmissionEnabled = true,
						Emission = new Color(1f, 0.7f, 0.3f),
						EmissionEnergyMultiplier = 0f // ApplyStreetLampState() dat lai dung gio ngay ben duoi
					};
					glowNode.MaterialOverride = glowMat;
					_streetLampGlowMats.Add(glowMat);
				}
			}

			// Tang manh do sang/tam voi so voi ban dau - gia tri cu (1.6/110) co the qua yeu de
			// nhan ra ro rang giua khung canh ban dem da co san sang mo tu bau troi/anh sang moi.
			var light = new OmniLight3D
			{
				Position = pos + Vector3.Up * 58f,
				LightColor = new Color(1f, 0.75f, 0.4f),
				LightEnergy = 6f,
				OmniRange = 270f // tang 50% (180 -> 270) theo yeu cau
			};
			_world.AddChild(light);
			_streetLamps.Add(light);

			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D
			{
				Shape = new CylinderShape3D { Radius = 4f, Height = 60f },
				Position = Vector3.Up * 30f
			});
			_world.AddChild(body);
		}

		// Them 1 DEN GIUA CHUONG (canh mang an, noi vat nuoi hay tu tap) - CHI 2 den o cong
		// (OmniRange=180, xem AddStreetLamp) la KHONG DU de chieu sang het chuong doi voi cac
		// chuong lon (half toi 192, tuc canh XA cong nhat cach toi 2*192=384 don vi): ve dem,
		// phan xa cong toi muc gan nhu toi den, kho thay vat nuoi dang dung o do - day rat co the
		// la ly do cac chuong bao "van con trong" du code van sinh du con vat. Dung CHUNG he
		// thong bat/tat den duong (_streetLamps) nen tu dong sang dung 18h-6h nhu cac den khac.
		private void AddPenCenterLight(Vector3 center, float half)
		{
			var light = new OmniLight3D
			{
				Position = center + Vector3.Up * 55f,
				LightColor = new Color(1f, 0.82f, 0.52f),
				LightEnergy = 5f,
				OmniRange = half * 2.4f // tang 50% (1.6 -> 2.4) theo yeu cau
			};
			_world.AddChild(light);
			_streetLamps.Add(light);
		}

		// Vung thong bao ten cong trinh (xem BuildingLabelZone.cs/HUD.ShowBuildingName) - nguoi
		// choi lai gan trong pham vi "radius" se thay ten cong trinh hien tren man hinh, ra khoi
		// pham vi thi tu an di. Ap dung cho cac cong trinh/dia diem NOI BAT (nha, chuong trai,
		// thap canh, coi xay gio...) - KHONG ap dung cho tung can nha dan lap lai trong khu do
		// thi/lang que Phap hay tung chuong ve tinh rieng le, tranh hien thong bao lien tuc gay
		// roi mat khi di qua khu vuc day nha giong het nhau.
		private void AddBuildingLabelZone(Vector3 pos, float radius, string name)
		{
			if (_buildingLabelZoneScene == null) return;
			var zone = _buildingLabelZoneScene.Instantiate<BuildingLabelZone>();
			zone.Position = pos;
			var collision = zone.GetNodeOrNull<CollisionShape3D>("Collision");
			// QUAN TRONG: tao SphereShape3D MOI thay vi sua truc tiep collision.Shape - sub-
			// resource nap tu .tscn (SphereShape3D_1) duoc CHIA SE giua MOI lan Instantiate() cua
			// CUNG 1 PackedScene (Godot mac dinh KHONG tach rieng resource cho tung instance tru
			// khi danh dau resource_local_to_scene) - neu sua truc tiep, moi zone da dat truoc do
			// se BI DOI THEO ban kinh cua zone dat SAU CUNG (tat ca dung chung 1 Shape resource).
			if (collision != null) collision.Shape = new SphereShape3D { Radius = radius };
			zone.BuildingLabel = name;
			_world.AddChild(zone);
		}

		private static Node FindNodeByName(Node root, string name)
		{
			if (root.Name == name) return root;
			foreach (Node child in root.GetChildren())
			{
				var found = FindNodeByName(child, name);
				if (found != null) return found;
			}
			return null;
		}

		private readonly System.Collections.Generic.List<OmniLight3D> _streetLamps = new();
		private readonly System.Collections.Generic.List<StandardMaterial3D> _streetLampGlowMats = new();

		private static bool IsStreetLampHour(int hour) => hour >= 18 || hour < 6;

		// Goi khi vua dat xong ca 4 cot (trang thai ban dau, dung gio hien tai) VA moi lan gio
		// thuc su thay doi (GameManager.HourChanged) - bat/tat CA den chieu sang lan phan kinh
		// den tu phat sang cung luc, sang tu 18h toi den 6h sang theo dong ho may tinh THAT.
		private void SetStreetLampsOn(bool on)
		{
			foreach (var lamp in _streetLamps)
				if (IsInstanceValid(lamp)) lamp.Visible = on;
			foreach (var mat in _streetLampGlowMats)
				mat.EmissionEnergyMultiplier = on ? 4f : 0f;
		}

		private void OnStreetLampHourChanged(int hour) => SetStreetLampsOn(IsStreetLampHour(hour));

		// Chuong cho chung, gan nha nong dan - moi con cho (ca con theo nguoi choi lan may con
		// chay rong) sau 12h dem den 6h sang deu tu dong ve day ngu.
		private static readonly Vector3 KennelPos = FarmhousePos + new Vector3(110, 0, 130);

		private void SpawnPlayer()
		{
			var player = _playerScene.Instantiate<Player>();
			// Dung Position (LOCAL, an toan truoc khi vao scene tree) chu khong phai
			// GlobalPosition - GlobalPosition can node da o TRONG tree de tinh qua chuoi cha, dat
			// truoc AddChild se bao loi "!is_inside_tree()" (xem cach lam dung cua SpawnCow/
			// SpawnChicken...). _world khong xoay/lech nen Position == GlobalPosition o day.
			// Phai dung ngoai vung va cham dac cua nha (~113 don vi moi huong) - xem AddDecor/FarmhouseFootprint
			player.Position = FarmhousePos + new Vector3(0, 0, 140);
			_world.AddChild(player);

			AddDogHouse(KennelPos, -30f);

			// Cho 3D luon di theo nguoi choi (Quaternius Shiba Inu, CC0) - dat ngay canh diem
			// xuat phat, tu tim nguoi choi qua group "player" (xem Dog.cs).
			var dog = _dogScene.Instantiate<Dog>();
			dog.Position = player.Position + new Vector3(30, 0, 20);
			dog.KennelPos = KennelPos;
			_world.AddChild(dog);

			SpawnFarmDogs();
			SpawnFarmCats();
		}

		// 5 con cho khac (nhieu giong khac nhau - Husky/Wolf/Shiba Inu, deu CC0 Quaternius) tu do
		// chay rong quanh nong trai (khac voi con Dog theo nguoi choi), sau 12h dem cung ve
		// chung 1 chuong ngu qua Dog.cs/FarmDog.cs.
		private void SpawnFarmDogs()
		{
			string[] breedPaths =
			{
				"res://assets3d/quaternius/animals/husky.glb",
				"res://assets3d/quaternius/animals/wolf.glb",
				"res://assets3d/quaternius/animals/dog.glb",
				"res://assets3d/quaternius/animals/husky.glb",
				"res://assets3d/quaternius/animals/wolf.glb",
			};
			var homeCenter = FarmhousePos + new Vector3(0, 0, 60);
			var rng = new RandomNumberGenerator();
			rng.Randomize();

			for (int i = 0; i < breedPaths.Length; i++)
			{
				var farmDog = _farmDogScene.Instantiate<FarmDog>();
				farmDog.ModelPath = breedPaths[i];
				farmDog.HomeCenter = homeCenter;
				farmDog.KennelPos = KennelPos;
				farmDog.Position = homeCenter + new Vector3(rng.RandfRange(-120, 120), 0, rng.RandfRange(-120, 120));
				_world.AddChild(farmDog);
			}
		}

		// Chuong meo rieng (khac vi tri chuong cho) - 10 con meo (Quaternius, CC0) tu do chay
		// rong quanh nong trai, sau 12h dem den 6h sang tu ve day ngu (xem FarmCat.cs).
		private static readonly Vector3 CatKennelPos = FarmhousePos + new Vector3(-110, 0, 130);

		private void SpawnFarmCats()
		{
			AddCatHouse(CatKennelPos, 30f);

			var homeCenter = FarmhousePos + new Vector3(0, 0, 60);
			var rng = new RandomNumberGenerator();
			rng.Randomize();

			for (int i = 0; i < 10; i++)
			{
				var cat = _farmCatScene.Instantiate<FarmCat>();
				cat.HomeCenter = homeCenter;
				cat.KennelPos = CatKennelPos;
				cat.Position = homeCenter + new Vector3(rng.RandfRange(-150, 150), 0, rng.RandfRange(-150, 150));
				_world.AddChild(cat);
			}
		}

		// Ruong chia thanh 10 KHU rieng biet ("tang len 10 khu" theo dung yeu cau) - moi khu la
		// 1 giai cot doc (het chieu sau 6 hang) chuyen trong 1 LOAI CAY rieng, dung y tuong "chia
		// dat thanh cac khu trong rieng biet" trong yeu cau truoc (Wheat/Carrot/Potato/Cabbage...).
		// Chia theo cot (khong doi vi tri/kich thuoc hang rao/cong ruong hien co - AN TOAN, vi
		// rat nhieu he thong khac (tuong da 10 hecta, kenh nuoc, cong lang...) da dinh vi theo
		// toa do field hien tai, doi kich thuoc field se lam sai lech hang loat).
		private static readonly string[] FieldZoneSeeds =
		{
			"pumpkin_seed", "tomato_seed", "wheat_seed", "carrot_seed", "potato_seed",
			"cabbage_seed", "pumpkin_seed", "tomato_seed", "wheat_seed", "carrot_seed",
		};
		private const int FieldZoneCount = 10;

		private void BuildFarm()
		{
			// Luoi ruong 12x6 ngay truoc nha nong dan (kieu Stardew Valley)
			for (int gx = 0; gx < FarmGridW; gx++)
			{
				int zone = gx * FieldZoneCount / FarmGridW;
				for (int gz = 0; gz < FarmGridH; gz++)
				{
					var plot = _farmScene.Instantiate<FarmPlot>();
					plot.GridX = gx;
					plot.GridY = gz;
					plot.Position = FarmOrigin + new Vector3(gx * FarmSpacing, 0, gz * FarmSpacing);
					plot.DefaultSeedId = FieldZoneSeeds[zone];
					// Loai dat: phan bo CO DINH theo mau tren luoi (khong ngau nhien, giu nguyen
					// moi lan tai lai) - da so binh thuong, xen ke cac loai dac biet (theo dung
					// yeu cau "chia dat thanh nhieu loai"). Xem FarmPlot.SoilType.
					plot.Soil = (gx % 5, gz % 3) switch
					{
						(0, 0) => SoilType.Fertile,
						(2, 1) => SoilType.Dry,
						(4, 2) => SoilType.Wet,
						(1, 2) => SoilType.Toxic,
						(3, 1) => SoilType.Special,
						_ => SoilType.Normal,
					};
					_world.AddChild(plot);
				}
			}

			AddFieldZoneMarkers();
		}

		// 1 bien go nho o dau moi khu (canh Bac cua ruong), ghi ten loai cay trong khu do, cho
		// nguoi choi de nhan biet 10 khu rieng biet.
		private void AddFieldZoneMarkers()
		{
			var postMat = GetCachedMaterial(new Color(0.32f, 0.22f, 0.12f), 1f);
			for (int zone = 0; zone < FieldZoneCount; zone++)
			{
				// Tim cot Bac nhat (gz=0) thuoc khu nay de dat bien.
				int gx = -1;
				for (int x = 0; x < FarmGridW; x++)
				{
					if (x * FieldZoneCount / FarmGridW == zone) { gx = x; break; }
				}
				if (gx < 0) continue;

				var markerPos = FarmOrigin + new Vector3(gx * FarmSpacing, 0, -FarmSpacing * 0.6f);
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 3f, Height = 30f },
					Position = markerPos + Vector3.Up * 15f,
					MaterialOverride = postMat
				});
				var seedDef = ItemDatabase.Instance?.GetItem(FieldZoneSeeds[zone]);
				string cropName = seedDef != null ? ItemDatabase.Instance.GetDisplayName(FieldZoneSeeds[zone]) : FieldZoneSeeds[zone];
				var label = new Label3D
				{
					Text = cropName,
					Position = markerPos + Vector3.Up * 32f,
					Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
					FontSize = 28,
					OutlineSize = 6,
					PixelSize = 0.12f,
					Modulate = new Color(1f, 0.95f, 0.8f)
				};
				_world.AddChild(label);
			}
		}

		private void SpawnNpcs()
		{
			// Ong gia lang - giao nhiem vu don rung
			var elder = _npcScene.Instantiate<NPC>();
			elder.NpcId = "elder";
			elder.NpcName = "Ông Già Làng";
			elder.QuestToGive = "q_clear_mud";
			elder.DialogueLow = new[] { "Chào người lạ. Vùng này dạo này nhiều quái bùn lắm." };
			elder.DialogueMid = new[] { "Cậu giúp làng thì tốt quá. Diệt lũ quái bùn giúp ta." };
			elder.DialogueHigh = new[] { "Ta tin cậu. Nghe đồn Hang Gai Tím phía đông có kho báu..." };
			elder.DialogueLowEn = new[] { "Welcome, stranger. This area's been crawling with mud monsters lately." };
			elder.DialogueMidEn = new[] { "Good of you to help the village. Go clear out those mud monsters for me." };
			elder.DialogueHighEn = new[] { "I trust you now. Word is there's treasure in Purple Thorn Cave, to the east..." };
			elder.Position = VillageAnchor + new Vector3(-110, 0, -20);
			_world.AddChild(elder);

			// Thuong nhan - cua hang hat giong & do
			var merchant = _npcScene.Instantiate<NPC>();
			merchant.NpcId = "merchant";
			merchant.NpcName = "Thương Nhân";
			merchant.QuestToGive = "q_first_harvest";
			merchant.ShopItems = new[]
			{
				"pumpkin_seed", "tomato_seed", "wheat_seed", "carrot_seed", "potato_seed", "cabbage_seed",
				"pumpkin_seed_premium", "tomato_seed_premium", "wheat_seed_premium", "carrot_seed_premium", "potato_seed_premium", "cabbage_seed_premium",
				"fertilizer_basic", "pesticide", "potion", "thucan_giasuc", "may_tuoi_tu_dong",
			};
			merchant.DialogueLow = new[] { "Mua gì không? Hạt giống tốt đây!" };
			merchant.DialogueMid = new[] { "Khách quen rồi! Xem hàng đi." };
			merchant.DialogueHigh = new[] { "Bạn tốt, ta sẽ để giá rẻ cho cậu." };
			merchant.DialogueLowEn = new[] { "Buying something? I've got fine seeds right here!" };
			merchant.DialogueMidEn = new[] { "A regular customer now! Take a look at my wares." };
			merchant.DialogueHighEn = new[] { "For a good friend like you, I'll drop the price." };
			merchant.Position = VillageAnchor + new Vector3(110, 0, -20);
			_world.AddChild(merchant);

			// Tho ren - ban/bo do vu khi & giap
			var blacksmith = _npcScene.Instantiate<NPC>();
			blacksmith.NpcId = "blacksmith";
			blacksmith.NpcName = "Thợ Rèn";
			blacksmith.ShopItems = new[] { "sword", "shield", "ring", "pickaxe", "hoe", "can_cau", "cuoc_bac", "cuoc_vang", "pickaxe_bac", "pickaxe_vang" };
			blacksmith.DialogueLow = new[] { "Muốn vũ khí tốt thì tìm đúng người rồi đấy. Nhưng ta chưa quen cậu lắm." };
			blacksmith.DialogueMid = new[] { "Thép tốt cần lửa tốt. Cậu ghé thường xuyên nhỉ." };
			blacksmith.DialogueHigh = new[] { "Vì tình bạn, ta sẽ rèn cho cậu món đồ ngon nhất xưởng." };
			blacksmith.DialogueLowEn = new[] { "Looking for good weapons, you've found the right person. But I don't know you well yet." };
			blacksmith.DialogueMidEn = new[] { "Good steel needs a good fire. You stop by often, don't you." };
			blacksmith.DialogueHighEn = new[] { "For our friendship, I'll forge you the finest piece in the whole shop." };
			blacksmith.Position = VillageAnchor + new Vector3(-110, 0, 100);
			_world.AddChild(blacksmith);

			// Ba lang thao duoc - ban thuoc hoi mau
			var herbalist = _npcScene.Instantiate<NPC>();
			herbalist.NpcId = "herbalist";
			herbalist.NpcName = "Bà Lang Thảo Dược";
			herbalist.ShopItems = new[] { "potion" };
			herbalist.DialogueLow = new[] { "Thảo dược trong vườn ta có thể cứu mạng người đấy, nhưng phải biết dùng lúc." };
			herbalist.DialogueMid = new[] { "Cậu lại ghé mua thuốc à? Ta sẽ bớt chút đỉnh giá." };
			herbalist.DialogueHigh = new[] { "Ta sẽ dạy cậu vài bài thuốc bí truyền, bạn trẻ." };
			herbalist.DialogueLowEn = new[] { "The herbs in my garden can save a life, but only if you know when to use them." };
			herbalist.DialogueMidEn = new[] { "Back for more medicine? I'll knock a little off the price for you." };
			herbalist.DialogueHighEn = new[] { "I'll teach you a few secret remedies, young one." };
			herbalist.Position = VillageAnchor + new Vector3(110, 0, 100);
			_world.AddChild(herbalist);

			// Nguoi gac rung - giao nhiem vu san quai Gai Tim ngoai hoang da
			var ranger = _npcScene.Instantiate<NPC>();
			ranger.NpcId = "ranger";
			ranger.NpcName = "Người Gác Rừng";
			ranger.QuestToGive = "q_spiky_hunt";
			ranger.DialogueLow = new[] { "Vùng hoang dã phía bắc đầy quái vật lắm. Coi chừng đấy." };
			ranger.DialogueMid = new[] { "Cậu đã chứng tỏ bản lĩnh rồi. Rừng sâu còn nhiều bí mật." };
			ranger.DialogueHigh = new[] { "Ta tin cậu đủ sức đối mặt với bầy quái Gai Tím. Đi săn đi." };
			ranger.DialogueLowEn = new[] { "The wilds to the north are crawling with monsters. Be careful out there." };
			ranger.DialogueMidEn = new[] { "You've proven your mettle. The deep forest still holds plenty of secrets." };
			ranger.DialogueHighEn = new[] { "I trust you can handle the Purple Thorn pack now. Go hunt them down." };
			ranger.Position = VillageAnchor + new Vector3(0, 0, 150);
			_world.AddChild(ranger);

			// Chu quan tro - ban thuoc/do uong, tro chuyen phiem
			var innkeeper = _npcScene.Instantiate<NPC>();
			innkeeper.NpcId = "innkeeper";
			innkeeper.NpcName = "Chủ Quán Trọ";
			innkeeper.ShopItems = new[] { "potion" };
			innkeeper.DialogueLow = new[] { "Chào mừng đến quán trọ. Người lạ ít khi ghé qua đây." };
			innkeeper.DialogueMid = new[] { "Uống chút gì cho khỏe đi, khách quen!" };
			innkeeper.DialogueHigh = new[] { "Chuyện phiếm với cậu vui thật. Lần sau ta mời rượu ngon." };
			innkeeper.DialogueLowEn = new[] { "Welcome to the inn. We don't get many strangers passing through." };
			innkeeper.DialogueMidEn = new[] { "Have a drink, on the house for a regular!" };
			innkeeper.DialogueHighEn = new[] { "I do enjoy our little chats. Next round's on me, good wine and all." };
			innkeeper.Position = VillageAnchor + new Vector3(-270, 0, 60);
			_world.AddChild(innkeeper);

			// Tho moc - sua chua/ban do go, mua go nguoi choi chat duoc (he thong ban do da co san)
			var carpenter = _npcScene.Instantiate<NPC>();
			carpenter.NpcId = "carpenter";
			carpenter.NpcName = "Thợ Mộc";
			carpenter.ShopItems = new[] { "shield", "da", "wood" };
			carpenter.DialogueLow = new[] { "Có gỗ tốt thì mang đến đây, ta mua hết." };
			carpenter.DialogueMid = new[] { "Gỗ cậu chắc chất lượng đấy. Còn bao nhiêu mang tới nhé." };
			carpenter.DialogueHigh = new[] { "Ta sẽ đóng cho cậu một món đồ gỗ đẹp nhất xưởng làng." };
			carpenter.DialogueLowEn = new[] { "Got good timber? Bring it here, I'll buy it all." };
			carpenter.DialogueMidEn = new[] { "Your wood's solid quality. Bring me whatever else you've got." };
			carpenter.DialogueHighEn = new[] { "I'll craft you the finest piece of woodwork in the whole village." };
			carpenter.Position = VillageAnchor + new Vector3(270, 0, 60);
			_world.AddChild(carpenter);

			// Hoc gia - nhan vat ke chuyen/lore, khong ban hang, khong nhiem vu
			var scholar = _npcScene.Instantiate<NPC>();
			scholar.NpcId = "scholar";
			scholar.NpcName = "Học Giả";
			scholar.DialogueLow = new[] { "Ta dành cả đời nghiên cứu vùng đất này. Có gì thắc mắc cứ hỏi ta." };
			scholar.DialogueMid = new[] { "Cậu ngày càng gắn gũi với vùng đất này rồi đấy." };
			scholar.DialogueHigh = new[] { "Trong sách có kể về một hiệp sĩ bảo vệ khu vườn huyền thoại... có lẽ là cậu." };
			scholar.DialogueLowEn = new[] { "I've spent my whole life studying this land. Ask me anything." };
			scholar.DialogueMidEn = new[] { "You're growing more connected to this land every day." };
			scholar.DialogueHighEn = new[] { "The old books speak of a legendary knight who guards a garden... perhaps it's you." };
			scholar.Position = VillageAnchor + new Vector3(0, 0, 320);
			_world.AddChild(scholar);

			// Tho may - ban trang phuc/phu kien (dung lai vat pham giap co san)
			var tailor = _npcScene.Instantiate<NPC>();
			tailor.NpcId = "tailor";
			tailor.NpcName = "Thợ May";
			tailor.ShopItems = new[] { "ring", "shield" };
			tailor.DialogueLow = new[] { "Vải vóc tồi cũng có thể may cho cậu một bộ đẹp." };
			tailor.DialogueMid = new[] { "Đồ cậu mặc cũng khá đấy, nhưng để ta chỉnh lại chút." };
			tailor.DialogueHigh = new[] { "Riêng cho cậu, ta sẽ may món đồ đặc biệt nhất tiệm." };
			tailor.DialogueLowEn = new[] { "Even plain cloth can be sewn into something fine for you." };
			tailor.DialogueMidEn = new[] { "That outfit of yours isn't bad, but let me take it in a little." };
			tailor.DialogueHighEn = new[] { "Just for you, I'll sew the finest piece in the whole shop." };
			tailor.Position = VillageAnchor + new Vector3(-420, 0, 210);
			_world.AddChild(tailor);

			// Nguoi chan cuu - flavor, khong ban hang/nhiem vu
			var shepherd = _npcScene.Instantiate<NPC>();
			shepherd.NpcId = "shepherd";
			shepherd.NpcName = "Người Chăn Cừu";
			shepherd.DialogueLow = new[] { "Đàn cừu của ta thích gặm cỏ ngoài đồng lắm." };
			shepherd.DialogueMid = new[] { "Cậu có vẻ hợp với cuộc sống đồng quê rồi đấy." };
			shepherd.DialogueHigh = new[] { "Bao giờ rảnh, ghé thăm đàn cừu của ta nhé." };
			shepherd.DialogueLowEn = new[] { "My sheep love grazing out in the fields." };
			shepherd.DialogueMidEn = new[] { "Seems like you're taking to country life just fine." };
			shepherd.DialogueHighEn = new[] { "Come visit my flock whenever you're free." };
			shepherd.Position = VillageAnchor + new Vector3(420, 0, 210);
			_world.AddChild(shepherd);
		}

		// Khu do thi: 50 can nha 3D + 1 tru canh sat rieng biet, bao quanh loi khu trung tam thi
		// tran hien co (Toa Thi Chinh + 10 nha dan + NPC ban hang/nhiem vu) ma KHONG dung cham -
		// sap xep theo luoi (spacing 230 don vi) nhung LOAI BO cac o roi vao vung trung tam va
		// sap xep theo khoang cach tu tam (gan nhat truoc) de tao bo cuc do thi lan toa tu nhien
		// thay vi 1 khoi vuong cung nhac. Nam TRONG TownGroundSize (3500, xem DrawTownGround) nen
		// khong can vung loai tru WorldStreamer.
		private void BuildCityDistrict()
		{
			const float spacing = 230f;
			const int halfGrid = 8;
			const float coreX = 560f, coreZMin = -260f, coreZMax = 460f;
			const float bound = TownGroundSize / 2f - 260f;

			var candidates = new List<Vector2>();
			for (int gz = -halfGrid; gz <= halfGrid; gz++)
			{
				for (int gx = -halfGrid; gx <= halfGrid; gx++)
				{
					float x = gx * spacing, z = gz * spacing;
					if (Mathf.Abs(x) < coreX && z > coreZMin && z < coreZMax) continue; // khu trung tam
					if (Mathf.Abs(x) > bound || Mathf.Abs(z) > bound) continue;
					candidates.Add(new Vector2(x, z));
				}
			}
			candidates.Sort((a, b) => a.Length().CompareTo(b.Length()));

			// Tru canh sat: chon o gan nhat o phia TAY (huong duong tu nong trai toi) cua khu
			// trung tam - cong trinh RIENG BIET, dung model nha nong dan (house_v2.glb, to/chi
			// tiet hon SmallBarn) de noi bat, khac han cac can nha dan lap lai.
			Vector2 policePlot = candidates[0];
			foreach (var c in candidates)
			{
				if (c.X < -coreX && Mathf.Abs(c.Y) < 300f) { policePlot = c; break; }
			}
			var policePos = VillageAnchor + new Vector3(policePlot.X, 0, policePlot.Y);
			AddDecor(_farmhouseScene, policePos, 66f, 90f, FarmhouseFootprint);
			var policeInterior = AddBuildingEntrance(policePos, 90f, 130f, 100f, RoomKind.TownHall);
			AddBuildingLabelZone(policePos, 160f, "label.police_post");
			_cityHousePositions.Add(policePos + new Vector3(0, 0, 55));
			_cityHouseInteriors.Add(policeInterior);

			var rng = new RandomNumberGenerator { Seed = 7002 };
			int placed = 0;
			foreach (var c in candidates)
			{
				if (c == policePlot) continue;
				if (placed >= 50) break;
				var pos = VillageAnchor + new Vector3(c.X, 0, c.Y);
				float rotY = rng.RandiRange(0, 3) * 90f;
				AddDecor(_smallBarnScene, pos, 12f, rotY, SmallBarnFootprint);
				var interiorPos = AddBuildingEntrance(pos, rotY, 80f, 50f, RoomKind.Village);
				_cityHousePositions.Add(pos);
				_cityHouseInteriors.Add(interiorPos);
				placed++;
			}
		}

		// Nguoi dan thi tran "song that": tu do di dao khap khu do thi ban ngay, ve nha ngu ban
		// dem (xem TownCitizenNpc.cs). Moi nguoi duoc gan 1 can nha rieng (tu danh sach
		// _cityHousePositions da dung o BuildCityDistrict) va 1 trong vai bo hoi thoai flavor -
		// KHONG phai NPC nhiem vu/cua hang (da co 10 NPC do rieng o SpawnNpcs), chi de thi tran
		// trong "song dong" nhu yeu cau.
		private void SpawnTownCitizens()
		{
			if (_cityHousePositions.Count == 0) return;

			(string name, string[] low, string[] mid, string[] high, string[] lowEn, string[] midEn, string[] highEn)[] flavors =
			{
				("Người Bán Hàng Rong",
					new[] { "Mua đi, mua đi! Hàng tươi mới về sáng nay!" },
					new[] { "Chào khách quen! Hôm nay trời đẹp nhỉ." },
					new[] { "Cậu mà ghé là ta vui cả ngày." },
					new[] { "Come buy, come buy! Fresh stock just came in this morning!" },
					new[] { "Hello again, regular! Lovely weather today, isn't it." },
					new[] { "Seeing you stop by makes my whole day." }),
				("Chủ Tiệm Bánh",
					new[] { "Bánh mới ra lò, thơm lắm!" },
					new[] { "Lại ghé tiệm bánh ta à? Vào đi!" },
					new[] { "Để ta biếu cậu ổ bánh ngon nhất." },
					new[] { "Fresh out of the oven, smells wonderful!" },
					new[] { "Back at my bakery again? Come on in!" },
					new[] { "Let me give you our finest loaf, on the house." }),
				("Em Học Sinh",
					new[] { "Chào anh/chị! Em đang trên đường đến trường." },
					new[] { "Em thấy anh/chị hoài, quen mặt rồi!" },
					new[] { "Anh/chị kể chuyện phiêu lưu cho em nghe đi!" },
					new[] { "Hi there! I'm just on my way to school." },
					new[] { "I see you all the time now, we're practically friends!" },
					new[] { "Tell me about your adventures, please!" }),
				("Cụ Già Trong Xóm",
					new[] { "Thị trấn này ta sống cả đời rồi đấy." },
					new[] { "Gặp cậu ta thấy vui, nhớ hồi con cháu." },
					new[] { "Ta sẽ kể cậu nghe chuyện xưa của thị trấn..." },
					new[] { "I've lived in this town my whole life." },
					new[] { "Seeing you reminds me of my own grandchildren, back in the day." },
					new[] { "Let me tell you an old tale about this town..." }),
				("Người Lao Động",
					new[] { "Ngày nào cũng phải làm việc chăm chỉ thôi." },
					new[] { "Cậu cũng siêng năng nhỉ, phục cho cậu." },
					new[] { "Nghỉ ngơi chút đi, bạn với cậu vui thật." },
					new[] { "Every day means hard work, that's just how it is." },
					new[] { "You're quite the hard worker yourself, I admire that." },
					new[] { "Take a rest for a bit. I really enjoy your company." }),
				("Bà Nội Trợ",
					new[] { "Ta đang đi chợ mua đồ nấu cơm đây." },
					new[] { "Hôm nào ghé nhà ta ăn cơm nhé!" },
					new[] { "Cậu như người trong nhà ta rồi đấy." },
					new[] { "I'm off to the market to get supper ingredients." },
					new[] { "Come by for dinner sometime, won't you!" },
					new[] { "You feel like family to me now." }),
			};

			var homeRng = new RandomNumberGenerator { Seed = 7003 };
			int citizenCount = Mathf.Min(18, _cityHousePositions.Count);
			var usedHomes = new System.Collections.Generic.HashSet<int>();

			for (int i = 0; i < citizenCount; i++)
			{
				int homeIdx;
				do { homeIdx = homeRng.RandiRange(1, _cityHousePositions.Count - 1); }
				while (!usedHomes.Add(homeIdx) && usedHomes.Count < _cityHousePositions.Count);

				var flavor = flavors[i % flavors.Length];
				var citizen = _citizenScene.Instantiate<TownCitizenNpc>();
				citizen.NpcId = $"citizen_{i}";
				citizen.NpcName = flavor.name;
				citizen.DialogueLow = flavor.low;
				citizen.DialogueMid = flavor.mid;
				citizen.DialogueHigh = flavor.high;
				citizen.DialogueLowEn = flavor.lowEn;
				citizen.DialogueMidEn = flavor.midEn;
				citizen.DialogueHighEn = flavor.highEn;
				citizen.WanderCenter = VillageAnchor;
				citizen.HomePos = _cityHousePositions[homeIdx] + new Vector3(0, 0, 55);
				citizen.InteriorHomePos = _cityHouseInteriors[homeIdx];
				_world.AddChild(citizen);
			}

			// 1 nguoi "cong an" rieng, gan lien voi Tru Canh Sat (nha o chinh la tru canh sat -
			// index 0 trong danh sach), di lai (tuan tra) quanh khu vuc tru thay vi ca thi tran.
			var guard = _citizenScene.Instantiate<TownCitizenNpc>();
			guard.NpcId = "town_guard";
			guard.NpcName = "Chú Công An";
			guard.DialogueLow = new[] { "Giữ gìn trật tự thị trấn là trách nhiệm của ta." };
			guard.DialogueMid = new[] { "Cậu là người tốt, ta yên tâm rồi." };
			guard.DialogueHigh = new[] { "Có chuyện gì cần giúp, cứ tìm ta ở Trụ Cảnh Sát nhé." };
			guard.DialogueLowEn = new[] { "Keeping order in this town is my responsibility." };
			guard.DialogueMidEn = new[] { "You're a good sort, I can rest easy now." };
			guard.DialogueHighEn = new[] { "Need help with anything, find me at the police post." };
			guard.WanderRadius = 220f;
			guard.WanderCenter = _cityHousePositions[0];
			guard.HomePos = _cityHousePositions[0];
			guard.InteriorHomePos = _cityHouseInteriors[0];
			_world.AddChild(guard);
		}

		private void SpawnEnemies()
		{
			// Quai tap trung o vung hoang da PHIA BAC, NGOAI tuong da 10 hecta quanh nong trai
			// (xem FarmWallCenter/FarmWallHalfSize - canh Bac cua tuong o Z = 390-3162.5 =
			// -2772.5) - dam bao quai khong con xuat hien BEN TRONG pham vi nong trai da duoc
			// rao lai.
			SpawnEnemy("mud_monster", new Vector3(-80, 0, -2900));
			SpawnEnemy("mud_monster", new Vector3(60, 0, -2950));
			SpawnEnemy("mud_monster", new Vector3(180, 0, -2880));
			SpawnEnemy("spiky_monster", new Vector3(120, 0, -3020));
		}

		private void SpawnEnemy(string id, Vector3 pos)
		{
			var e = _enemyScene.Instantiate<Enemy>();
			e.EnemyId = id;
			e.Position = pos;
			e.StatMultiplier = Enemy.SeasonalMultiplier();
			_world.AddChild(e);
		}

		// Trang trai bo: 1 khu rieng co hang rao rieng, cach xa nha kho ~85 don vi de khong
		// chong lan. Bo (Quaternius Farm Animal Pack, CC0) tu do di lai trong hang rao va tu
		// dong den mang an luc 12h trua/16h chieu theo dong ho THAT (xem Cow.cs).
		// Khong con la hang so co dinh - duoc BuildAnimalPenDistrict GIAI QUYET luc runtime qua
		// FindOpenSpot(searchCenter: LivestockZoneOrigin) de chuong nay nam GAN Khu Chan Nuoi
		// thay vi 1 toa do rai rac rieng (xem quy hoach 5 khu vuc).
		private Vector3 CowPastureCenter;
		private const float CowPastureHalf = 192f; // tang 20% (160 -> 192)

		private void BuildCowPasture()
		{
			float minX = CowPastureCenter.X - CowPastureHalf;
			float maxX = CowPastureCenter.X + CowPastureHalf;
			float minZ = CowPastureCenter.Z - CowPastureHalf;
			float maxZ = CowPastureCenter.Z + CowPastureHalf;
			float gateX = CowPastureCenter.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
			// Nam - chua cong o giua
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 22f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 22f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));

			var troughPos = CowPastureCenter;
			AddFeedTrough(troughPos);

			// 2 cot den hai ben cong chuong bo (giong cong ruong)
			AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
			AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);
			AddPenCenterLight(CowPastureCenter, CowPastureHalf);
			AddBuildingLabelZone(CowPastureCenter, CowPastureHalf + 20f, "label.cow_pasture");

			// 12 con (tang tu 4 theo yeu cau) - rai deu theo vong tron trong hang rao, ban kinh
			// nho hon PastureHalfExtent de khong dung sat hang rao.
			var cowRng = new RandomNumberGenerator { Seed = 9700 };
			for (int i = 0; i < 12; i++)
			{
				float angle = Mathf.Tau * i / 12f;
				float radius = cowRng.RandfRange(50f, CowPastureHalf - 40f);
				var pos = CowPastureCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				SpawnCow(pos, isAdult: true);
			}
		}

		// Nha o cho nguoi cham bo (SmallBarn - cung model/he thong cua+noi that 2 tang da dung
		// cho ca 12 cong trinh khac, xem AddBuildingEntrance) + NPC AI di lam theo gio hanh
		// chinh that (6h-18h) - xem FarmhandNpc.cs.
		// Khong con la hang so co dinh - tu giai quyet luc runtime trong BuildCowherd() qua
		// FindOpenSpot(searchCenter: HousingZoneAnchor) de nha Etienne nam trong Khu Nha O NPC.
		private Vector3 CowherdHousePos;

		// Ten rieng cho NPC nhan vien trang trai (theo yeu cau "dat ten cho toan bo NPC") - phong
		// cach Phap, khop voi Jean/Marcel/Antoine/Henri da co san va chu de "vung que Phap" cua
		// game. Dung PickStaffName(index) de gan TEN THAT thay vi chi hien CHUC VU chung chung
		// nhu truoc (vd "Nguoi Cham Bo" -> ten rieng, chuc vu chuyen sang loi thoai).
		private static readonly string[] FarmStaffNames =
		{
			"Etienne", "Baptiste", "Severin", "Theodore", "Augustin", "Gaston", "Hubert", "Leon", "Emile", "Fernand",
			"Gustave", "Adrien", "Cyril", "Denis", "Eugene", "Firmin", "Honore", "Ignace", "Justin", "Lucien",
			"Modeste", "Norbert", "Octave", "Prosper", "Quentin", "Sylvestre", "Urbain", "Valentin", "Wilfrid", "Alphonse",
			"Amelie", "Beatrice", "Celestine", "Delphine", "Eugenie", "Fleurette", "Ghislaine", "Henriette", "Isabelle", "Josephine",
			"Lucienne", "Marguerite", "Nadine", "Odile", "Pauline", "Rosalie", "Solange", "Therese", "Valerie", "Yvette",
			"Adele", "Blanche", "Colette", "Denise", "Emilie", "Francine", "Genevieve", "Helene", "Irene", "Jacqueline",
		};
		private static string PickStaffName(int index) => FarmStaffNames[((index % FarmStaffNames.Length) + FarmStaffNames.Length) % FarmStaffNames.Length];

		private void BuildCowherd()
		{
			CowherdHousePos = NextHousingCottagePos(10801);
			AddDecor(_smallBarnScene, CowherdHousePos, 12f, 90f, SmallBarnFootprint);
			var interiorHomePos = AddBuildingEntrance(CowherdHousePos, 90f, 80f, 50f, RoomKind.Village);
			AddBuildingLabelZone(CowherdHousePos, 100f, "label.cowherd_house");

			var npc = _farmhandScene.Instantiate<FarmhandNpc>();
			npc.NpcId = "cowherd";
			npc.NpcName = "Etienne";
			npc.DialogueLow = new[] { "Chào, ta là người được thuê chăm đàn bò ở đây. Giờ hành chính 6 giờ sáng tới 6 giờ tối." };
			npc.DialogueMid = new[] { "Đàn bò dạo này khỏe re, ăn uống đầy đủ cả." };
			npc.DialogueHigh = new[] { "Cậu hãy ghé qua chuồng bò xem, thỉnh thoảng ta để lại chút sữa tươi đấy." };
			npc.DialogueLowEn = new[] { "Hello, I'm hired to look after the cows here. Working hours are 6 AM to 6 PM." };
			npc.DialogueMidEn = new[] { "The herd's doing great lately, well fed and healthy." };
			npc.DialogueHighEn = new[] { "Stop by the cow barn sometime, I sometimes leave a bit of fresh milk for you." };
			npc.HomePos = CowherdHousePos + new Vector3(0, 0, 55);
			// Ngoai gio lam (sau 18h), NPC di vao HAN BEN TRONG nha (dung phong noi that that
			// da xay qua AddBuildingEntrance) de ngu, khong dung ngoai san.
			npc.InteriorHomePos = interiorHomePos;
			npc.WorkPos = CowPastureCenter + new Vector3(0, 0, -40);
			npc.TroughPos = CowPastureCenter;
			_world.AddChild(npc);
		}

		// Chuong ngua: 1 khu rieng co hang rao rieng, phia bac chuong bo (cach ~80 don vi de
		// khong chong lan). Ngua (Quaternius Farm Animal Pack, CC0) tu do di lai trong hang rao
		// va tu dong den mang an luc 12h trua/16h chieu theo dong ho THAT (xem Horse.cs).
		// Z = -724 (lui them 74 so voi -650) - sau khi CowPastureHalf/HorseStableHalf tang 20%
		// (160->192), khoang cach toi chuong bo bi thu hep. Lui de khoi phuc ~90 don vi.
		private Vector3 HorseStableCenter; // xem ghi chu tren CowPastureCenter
		private const float HorseStableHalf = 192f; // tang 20% (160 -> 192)

		private void BuildHorseStable()
		{
			float minX = HorseStableCenter.X - HorseStableHalf;
			float maxX = HorseStableCenter.X + HorseStableHalf;
			float minZ = HorseStableCenter.Z - HorseStableHalf;
			float maxZ = HorseStableCenter.Z + HorseStableHalf;
			float gateX = HorseStableCenter.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
			// Nam - chua cong o giua
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 22f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 22f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));

			AddFeedTrough(HorseStableCenter);

			// 2 cot den hai ben cong chuong ngua (giong cong ruong/chuong bo)
			AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
			AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);
			AddPenCenterLight(HorseStableCenter, HorseStableHalf);
			AddBuildingLabelZone(HorseStableCenter, HorseStableHalf + 20f, "label.horse_stable");

			Vector3[] horseStarts =
			{
				HorseStableCenter + new Vector3(-70, 0, -60),
				HorseStableCenter + new Vector3(70, 0, -50),
				HorseStableCenter + new Vector3(-50, 0, 70),
				HorseStableCenter + new Vector3(60, 0, 60),
			};
			foreach (var pos in horseStarts)
			{
				var horse = _horseScene.Instantiate<Horse>();
				horse.Position = pos;
				horse.TroughPosition = HorseStableCenter;
				horse.HomeCenter = HorseStableCenter;
				horse.PastureHalfExtent = HorseStableHalf - 35f;
				_world.AddChild(horse);
			}
		}

		// Nha o cho nguoi cham ngua (SmallBarn - cung he thong cua+noi that 2 tang) + NPC AI di
		// lam theo gio hanh chinh that (6h-18h) - xem StablehandNpc.cs.
		private Vector3 StablehandHousePos; // xem ghi chu tren CowherdHousePos

		private void BuildStablehand()
		{
			StablehandHousePos = NextHousingCottagePos(10802);
			AddDecor(_smallBarnScene, StablehandHousePos, 12f, 90f, SmallBarnFootprint);
			var interiorHomePos = AddBuildingEntrance(StablehandHousePos, 90f, 80f, 50f, RoomKind.Village);
			AddBuildingLabelZone(StablehandHousePos, 100f, "label.stablehand_house");

			var npc = _stablehandScene.Instantiate<StablehandNpc>();
			npc.NpcId = "stablehand";
			npc.NpcName = "Baptiste";
			npc.DialogueLow = new[] { "Chào, ta là người được thuê chăm đàn ngựa ở đây. Giờ hành chính 6 giờ sáng tới 6 giờ tối." };
			npc.DialogueMid = new[] { "Đàn ngựa dạo này khỏe re, chạy nhanh lắm." };
			npc.DialogueHigh = new[] { "Cậu muốn cưỡi ngựa thì cứ ghé chuồng hỏi ta nhé." };
			npc.DialogueLowEn = new[] { "Hello, I'm hired to look after the horses here. Working hours are 6 AM to 6 PM." };
			npc.DialogueMidEn = new[] { "The horses are doing great lately, fast as ever." };
			npc.DialogueHighEn = new[] { "Fancy a ride? Just come by the stable and ask me." };
			npc.HomePos = StablehandHousePos + new Vector3(0, 0, 55);
			npc.InteriorHomePos = interiorHomePos;
			npc.WorkPos = HorseStableCenter + new Vector3(0, 0, -40);
			_world.AddChild(npc);
		}

		// Chuong ga: 1 khu rieng co hang rao rieng, phia nam chuong ngua (cach ~80 don vi de
		// khong chong lan, giong khoang cach giua chuong bo va chuong ngua). Ga (Quaternius, CC0)
		// tu do di lai trong hang rao va tu dong den cho thuc an luc 12h trua/16h chieu theo dong
		// ho THAT (xem Chicken.cs). Pham vi nho hon bo/ngua vi ga la con vat nho, khong can san
		// rong.
		// Z = -1050 (lui them 60 don vi ve phia bac so voi -990 truoc day) - sau khi chuong ga
		// tang kich thuoc 50% (ChickenCoopHalf 100->150), khoang cach toi chuong ngua bi thu hep
		// tu 80 xuong 30 don vi. Lui vi tri de khoi phuc khoang cach ~90 don vi, du rong de di
		// chuyen/tiep can cong.
		// Z = -1186 (lui them tu -1050) - khoang cach toi chuong ngua (moi lui ve -724, ban kinh
		// moi 192) can duy tri ~90 don vi sau khi ChickenCoopHalf tang 20% (150->180).
		private Vector3 ChickenCoopCenter; // xem ghi chu tren CowPastureCenter
		private const float ChickenCoopHalf = 180f; // tang 50% (100->150) roi tang tiep 20% (150 -> 180)

		private void BuildChickenCoop()
		{
			float minX = ChickenCoopCenter.X - ChickenCoopHalf;
			float maxX = ChickenCoopCenter.X + ChickenCoopHalf;
			float minZ = ChickenCoopCenter.Z - ChickenCoopHalf;
			float maxZ = ChickenCoopCenter.Z + ChickenCoopHalf;
			float gateX = ChickenCoopCenter.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene); // bac
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene); // tay
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene); // dong
			// Nam - chua cong o giua
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 22f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 22f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));

			// Chuong ga that su (mai che, dat sat canh bac cua khu) + cho do thuc an o giua san.
			AddDecor(_chickenCoopScene, ChickenCoopCenter + new Vector3(0, 0, -55), 14f, 0f, new Vector2(2.41f, 2.21f));
			var feedPos = ChickenCoopCenter + new Vector3(0, 0, 25);
			AddChickenFeeder(feedPos);

			// 2 cot den hai ben cong chuong ga (giong cong chuong bo/chuong ngua)
			AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
			AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);
			AddPenCenterLight(ChickenCoopCenter, ChickenCoopHalf);
			AddBuildingLabelZone(ChickenCoopCenter, ChickenCoopHalf + 20f, "label.chicken_coop");

			// 30 con (tang tu 10 theo yeu cau).
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			for (int i = 0; i < 30; i++)
			{
				float angle = rng.RandfRange(0f, Mathf.Tau);
				float radius = rng.RandfRange(20f, ChickenCoopHalf - 25f);
				var pos = ChickenCoopCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
				SpawnChicken(pos, feedPos);
			}
		}

		// homeCenterOverride/pastureHalfOverride: giong SpawnCow o tren - cho phep dung ham nay
		// cho CAC CHUONG GA KHAC (xem BuildAnimalPenDistrict), tranh loi ga hard-code "nha" ve
		// chuong ga GOC o xa (gay chuong moi nhin "trong khong co con nao" du da sinh du so luong).
		private void SpawnChicken(Vector3 pos, Vector3 feedPos, Vector3? homeCenterOverride = null, float? pastureHalfOverride = null)
		{
			if (_chickenScene == null) { GD.PushError("Khong tai duoc Chicken.tscn"); return; }
			var chicken = _chickenScene.Instantiate<Chicken>();
			chicken.Position = pos;
			chicken.FeedPosition = feedPos;
			chicken.HomeCenter = homeCenterOverride ?? ChickenCoopCenter;
			chicken.PastureHalfExtent = (pastureHalfOverride ?? ChickenCoopHalf) - 20f;
			_world.AddChild(chicken);
		}

		// Cho do thuc an cho ga: khac mang cho bo/ngua (khay go cao), ga mo thuc an SAT MAT DAT -
		// dung 1 vanh go thap + dong hat mau vang rai ngang tren nen, khong tim duoc model CC0
		// phu hop rieng nen dung go primitive (giong cach lam mang an bo/ngua).
		private void AddChickenFeeder(Vector3 pos)
		{
			var woodMat = GetCachedMaterial(new Color(0.42f, 0.28f, 0.15f), 0.9f);
			var grainMat = GetCachedMaterial(new Color(0.88f, 0.75f, 0.35f), 1f);

			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 26f, BottomRadius = 28f, Height = 4f },
				Position = pos + Vector3.Up * 2f,
				MaterialOverride = woodMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 21f, BottomRadius = 21f, Height = 3f },
				Position = pos + Vector3.Up * 4.5f,
				MaterialOverride = grainMat
			});

			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D
			{
				Shape = new CylinderShape3D { Radius = 27f, Height = 8f },
				Position = Vector3.Up * 4f
			});
			_world.AddChild(body);
		}

		// Nha o cho nguoi cham ga (SmallBarn - cung he thong cua+noi that 2 tang) + NPC AI di
		// lam theo gio hanh chinh that (6h-18h) - xem PoultryKeeperNpc.cs.
		private Vector3 PoultryKeeperHousePos; // xem ghi chu tren CowherdHousePos

		private void BuildPoultryKeeper()
		{
			PoultryKeeperHousePos = NextHousingCottagePos(10803);
			AddDecor(_smallBarnScene, PoultryKeeperHousePos, 12f, 90f, SmallBarnFootprint);
			var interiorHomePos = AddBuildingEntrance(PoultryKeeperHousePos, 90f, 80f, 50f, RoomKind.Village);
			AddBuildingLabelZone(PoultryKeeperHousePos, 100f, "label.poultry_keeper_house");

			if (_poultryKeeperScene == null) { GD.PushError("Khong tai duoc PoultryKeeperNpc.tscn"); return; }
			var npc = _poultryKeeperScene.Instantiate<PoultryKeeperNpc>();
			npc.NpcId = "poultrykeeper";
			npc.NpcName = "Severin";
			npc.DialogueLow = new[] { "Chào, ta là người được thuê chăm đàn gà ở đây. Giờ hành chính 6 giờ sáng tới 6 giờ tối." };
			npc.DialogueMid = new[] { "Đàn gà dạo này đẻ trứng đều lắm." };
			npc.DialogueHigh = new[] { "Cậu hãy ghé qua chuồng gà xem, thỉnh thoảng ta để lại vài quả trứng tươi đấy." };
			npc.DialogueLowEn = new[] { "Hello, I'm hired to look after the chickens here. Working hours are 6 AM to 6 PM." };
			npc.DialogueMidEn = new[] { "The hens are laying eggs steadily lately." };
			npc.DialogueHighEn = new[] { "Stop by the chicken coop sometime, I sometimes leave a few fresh eggs for you." };
			npc.HomePos = PoultryKeeperHousePos + new Vector3(0, 0, 55);
			npc.InteriorHomePos = interiorHomePos;
			npc.WorkPos = ChickenCoopCenter + new Vector3(0, 0, 25);
			_world.AddChild(npc);
		}

		// Cao nguyen (plateau) hoang da, cach hang rao nong trai it nhat 10m (200 don vi, quy
		// doi 20 don vi/met) ve phia dong - nam NGOAI vung reserved cua WorldStreamer (xem
		// WorldStreamer.ReservedZones), nen dang ky rieng vung LOAI TRU (ExclusionZones) de
		// cay/da/quai ngau nhien cua vung hoang da khong moc xuyen qua chan cao nguyen.
		private void BuildPlateaus()
		{
			AddPlateau(new Vector3(1600, 0, -350), 240f, 200f, 4, 5101);
			AddPlateau(new Vector3(2650, 0, 750), 210f, 170f, 3, 5102);
			AddPlateau(new Vector3(1750, 0, 1950), 260f, 220f, 5, 5103);
		}

		// Ham mo (khai thac khoang san, quan trong mua Dong - xem GameManager.Season) - 1 cong
		// ngoai troi (gan Cao Nguyen 1, hop chu de "nui da", cach du xa de khong dam vao ban kinh
		// loai tru cua no) dan toi 3 TANG ham sau dan, TAI SU DUNG NGUYEN VEN he thong phong noi
		// that + cau thang da co (BuildRoom - cung ham dung cho tang 2 nha o) thay vi dung he
		// thong scene/dia hinh hang dong rieng (game khong co co che doi scene, tat ca luon o
		// CHUNG 1 _world - xem AddBuildingEntrance) - CHI doi mau tuong/san sang da/nau xam de doc
		// ra "hang dong" thay vi nha o. Day la phong noi that co san doi mau, KHONG PHAI dia hinh
		// hang dao thu cong rieng.
		private static readonly Vector3 MineEntrancePos = new(2500, 0, -400);

		// Tien ich tao 1 muc DecorOption (xem WorldStreamer.RegionProfile.DecorOptions) - GD.Load
		// duoc Godot tu cache theo duong dan nen goi lap lai voi cung 1 path la re, khong can 1
		// field PackedScene rieng cho tung khu nhu WorldStreamer tu lam voi bo _decorOptions cua no.
		private static (PackedScene scene, float minScale, float maxScale, bool isTree) MakeDecor(string path, float min, float max, bool isTree = false)
			=> (GD.Load<PackedScene>(path), min, max, isTree);

		// ==== 11 khu vuc "the gioi mo" (xem yeu cau nguoi dung) - Mo (khai thac khoang san) DA
		// XAY XONG rieng (xem BuildMine ben duoi), day la 10 khu CON LAI. Tat ca dat vong quanh
		// nong trai o ban kinh ~7200 don vi (du xa khoi vung loai tru cay/quai cua tuong da 10
		// hecta - r4585.6 quanh (202,390), xem BuildFarmStoneWall) chia deu 11 huong (bao gom ca
		// Mo) de khong khu nao dam vao khu nao. ====
		private static readonly Vector3 MountainRegionCenter = new(200, 0, -6800);
		private static readonly Vector3 ForestRegionCenter = new(-3600, 0, -5700);
		private static readonly Vector3 FieldRegionCenter = new(-5200, 0, 5100);
		private static readonly Vector3 LakeRegionCenter = new(-6300, 0, -2650);
		private static readonly Vector3 RiverRegionCenter = new(2300, 0, 7000);
		private static readonly Vector3 VillageRegionCenter = new(5700, 0, 5000);
		private static readonly Vector3 BigCityRegionCenter = new(-1800, 0, 7300);
		private static readonly Vector3 RuinsRegionCenter = new(6700, 0, -2650);
		private static readonly Vector3 CemeteryRegionCenter = new(7000, 0, 1400);
		private static readonly Vector3 SwampRegionCenter = new(-6900, 0, 1400);
		private static readonly Vector3 CaveRegionCenter = new(4100, 0, -5650);

		// Nui - da xam doc, quai Yeu Tinh Nui Da, tai nguyen Quang Nui, diem den la 1 dinh nui
		// lon co the leo len (tai su dung AddPlateau).
		private void BuildMountainRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Núi", Center = MountainRegionCenter, HalfSize = 1500f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/rock_1.glb", 20f, 30f),
					MakeDecor("res://assets3d/quaternius/nature/rock_2.glb", 20f, 30f),
					MakeDecor("res://assets3d/quaternius/nature/tree_birch_1.glb", 26f, 34f, true),
				},
				MinDecor = 5, MaxDecor = 10,
				EnemyTable = new (string, float)[] { ("mountain_troll", 1f) },
				EnemyChance = 0.55f, EnemyStatMultiplier = 1.3f,
			});

			AddPlateau(MountainRegionCenter, 300f, 260f, 6, 6101); // tu dang ky ExclusionZone rieng
			var rng = new RandomNumberGenerator { Seed = 6102 };
			for (int i = 0; i < 6; i++)
			{
				float angle = Mathf.Tau * i / 6f;
				var pos = MountainRegionCenter + new Vector3(Mathf.Cos(angle) * 500f, 0, Mathf.Sin(angle) * 500f);
				AddOreNode(pos, "quang_nui", hp: 25, dropAmount: 2, regrowDays: 3, new Color(0.5f, 0.48f, 0.45f));
			}
			AddBuildingLabelZone(MountainRegionCenter, 400f, "label.mountain");
		}

		// Rung - cay day dac (mat do cao hon vung hoang da mac dinh), quai Soi Rung, tai nguyen
		// Thao Duoc Rung (tai su dung FruitTree - "cay" thao duoc, chap nhan hinh dang cay thay
		// vi bui co rieng, xem gioi han da neu trong ke hoach).
		private void BuildForestRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Rừng", Center = ForestRegionCenter, HalfSize = 1500f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/tree_normal_1.glb", 34f, 44f, true),
					MakeDecor("res://assets3d/quaternius/nature/tree_normal_2.glb", 34f, 44f, true),
					MakeDecor("res://assets3d/quaternius/nature/tree_maple_1.glb", 34f, 44f, true),
					MakeDecor("res://assets3d/quaternius/nature/tree_maple_2.glb", 34f, 44f, true),
					MakeDecor("res://assets3d/kenney/nature/plant_bush.glb", 14f, 20f),
				},
				MinDecor = 8, MaxDecor = 14,
				EnemyTable = new (string, float)[] { ("forest_wolf", 1f) },
				EnemyChance = 0.5f, EnemyStatMultiplier = 1.1f,
			});

			WorldStreamer.ExclusionZones.Add((ForestRegionCenter, 260f));
			var rng = new RandomNumberGenerator { Seed = 6201 };
			for (int i = 0; i < 8; i++)
			{
				float angle = Mathf.Tau * i / 8f;
				float radius = rng.RandfRange(60f, 200f);
				var pos = ForestRegionCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				AddFruitTree(pos, "thao_duoc_rung", new Color(0.4f, 0.7f, 0.3f), rng);
			}
			AddBuildingLabelZone(ForestRegionCenter, 260f, "label.deep_forest");
		}

		// Dong ruong - co/hoa dai thua thot, mo, AN TOAN (khong co quai rieng) - diem den la 1 bu
		// nhin (tai su dung scarecrow.glb da co san).
		private void BuildFieldRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Đồng Ruộng", Center = FieldRegionCenter, HalfSize = 1300f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/kenney/nature/flower_yellowA.glb", 7f, 11f),
					MakeDecor("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f),
					MakeDecor("res://assets3d/kenney/nature/plant_bush.glb", 10f, 14f),
				},
				MinDecor = 3, MaxDecor = 6,
			});

			WorldStreamer.ExclusionZones.Add((FieldRegionCenter, 120f));
			if (_scarecrowScene != null)
			{
				var scarecrow = _scarecrowScene.Instantiate<Node3D>();
				scarecrow.Position = FieldRegionCenter;
				scarecrow.Scale = Vector3.One * 20f;
				_world.AddChild(scarecrow);
			}
			AddBuildingLabelZone(FieldRegionCenter, 260f, "label.fields");
		}

		// Ho - mat nuoc vuong lon (tai su dung BuildWaterRegion) + 1 he sinh thai THAT (xem
		// WaterEcosystem.cs): thap nuoc cap nguon sach, sen/rong ven bo, dong vat hoang da
		// (Utility AI), ben thuyen. Quai Ran Ho van giu nguyen (nguy hiem tu nhien, khong doi
		// khang voi he sinh thai). "Ca" gio CAU duoc qua can cau (xem Player.TryFish) thay vi hai
		// nhu cay - da bo 4 "cay ca" FruitTree cu (AddFruitTree) vi trung mục dich voi co che moi.
		private void BuildLakeRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Hồ", Center = LakeRegionCenter, HalfSize = 1300f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/tree_birch_1.glb", 30f, 40f, true),
					MakeDecor("res://assets3d/quaternius/nature/rock_1.glb", 14f, 20f),
					MakeDecor("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f),
				},
				MinDecor = 4, MaxDecor = 8,
				EnemyTable = new (string, float)[] { ("lake_serpent", 1f) },
				EnemyChance = 0.4f, EnemyStatMultiplier = 1.15f,
			});

			BuildWaterRegion(LakeRegionCenter, 480f, 480f, new Color(0.2f, 0.45f, 0.65f), "label.lake");
			WaterEcosystem.Instance.LakeCenter = LakeRegionCenter;
			WaterEcosystem.Instance.LakeRadius = 460f;

			Vector3 dockPos = LakeRegionCenter + new Vector3(500f, 2f, 0);
			if (_stoneBridgeScene != null)
			{
				var dock = _stoneBridgeScene.Instantiate<Node3D>();
				dock.Position = dockPos;
				dock.Scale = Vector3.One * 20f;
				_world.AddChild(dock);
			}
			if (_boatScene != null)
			{
				var boat = _boatScene.Instantiate<Boat>();
				boat.Position = LakeRegionCenter + new Vector3(420f, 3f, 60f);
				boat.BoundsCenter = LakeRegionCenter;
				boat.BoundsRadius = 430f;
				_world.AddChild(boat);
				AddBoatHull(boat);
			}

			BuildWaterTower(LakeRegionCenter + new Vector3(-560f, 0, -260f));

			var rng = new RandomNumberGenerator { Seed = 6401 };
			for (int i = 0; i < 6; i++)
			{
				float angle = Mathf.Tau * i / 6f;
				var pos = LakeRegionCenter + new Vector3(Mathf.Cos(angle) * 380f, 1.6f, Mathf.Sin(angle) * 380f);
				AddPondPlant(pos, i % 2 == 0 ? "sen" : "rong", i % 2 == 0 ? new Color(0.85f, 0.55f, 0.7f) : new Color(0.3f, 0.5f, 0.25f), rng);
			}

			SpawnLakeWildlife(rng);
		}

		// Thap nuoc + ong dan xuong ho (xem WaterTower.cs) - dung hinh khoi nguyen thuy (khong
		// tim duoc model CC0 phu hop) giong ky thuat da dung cho mo/bia mo: tru tron (bon nuoc)
		// tren cot do, 1 "ong" (tru hep) noi xuong huong ho.
		private void BuildWaterTower(Vector3 anchor)
		{
			var pillarMat = GetCachedMaterial(new Color(0.55f, 0.54f, 0.5f), 0.7f);
			var tankMat = GetCachedMaterial(new Color(0.7f, 0.68f, 0.6f), 0.5f);
			var pipeMat = GetCachedMaterial(new Color(0.4f, 0.4f, 0.42f), 0.6f);

			const float pillarHeight = 140f;
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 14f, BottomRadius = 18f, Height = pillarHeight },
				Position = anchor + Vector3.Up * (pillarHeight / 2f),
				MaterialOverride = pillarMat,
			});
			var tankPos = anchor + Vector3.Up * (pillarHeight + 30f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 45f, BottomRadius = 45f, Height = 60f },
				Position = tankPos,
				MaterialOverride = tankMat,
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 48f, BottomRadius = 48f, Height = 6f },
				Position = tankPos + Vector3.Up * 33f,
				MaterialOverride = tankMat,
			});

			// Ong dan nghieng huong ve phia ho - dung y "BON NUOC -> ong -> HO NUOC" nguoi choi mo ta.
			Vector3 toLake = (LakeRegionCenter - anchor); toLake.Y = 0;
			Vector3 pipeDir = toLake.Normalized();
			Vector3 pipeStart = anchor + Vector3.Up * 20f + pipeDir * 20f;
			float pipeLen = toLake.Length() * 0.55f;
			var pipeMid = pipeStart + pipeDir * (pipeLen / 2f);
			var pipe = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 5f, BottomRadius = 5f, Height = pipeLen },
				Position = pipeMid + Vector3.Up * 2f,
				MaterialOverride = pipeMat,
			};
			// CylinderMesh mac dinh nam doc theo truc Y cuc bo - dung Basis THANG (khong qua
			// LookAt+xoay them, de tranh sai huong do ghep nhieu phep xoay) de truc Y trung
			// THANG voi pipeDir (nam ngang huong ve ho) thay vi huong len troi.
			Vector3 pipeRight = pipeDir.Cross(Vector3.Up);
			if (pipeRight.LengthSquared() < 0.0001f) pipeRight = Vector3.Right;
			pipeRight = pipeRight.Normalized();
			Vector3 pipeForward = pipeRight.Cross(pipeDir).Normalized();
			pipe.Basis = new Basis(pipeRight, pipeDir, pipeForward);
			_world.AddChild(pipe);

			var tower = new WaterTower { Position = anchor };
			tower.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 18f, Height = pillarHeight }, Position = Vector3.Up * (pillarHeight / 2f) });
			_world.AddChild(tower);
			AddBuildingLabelZone(anchor, 90f, "label.water_tower");
			WorldStreamer.ExclusionZones.Add((anchor, 90f));
		}

		// Cum sen/rong ven ho - tai su dung chu ky "chin -> hai -> moc lai" cua FruitTree.cs
		// nhung visual la LA NOI + HOA NHO thay vi than cay+tan la (hop ly hon cho thuc vat thuy
		// sinh thap, xem AddFruitTree o tren cho ban goc "cay lau nam").
		private void AddPondPlant(Vector3 pos, string itemId, Color plantColor, RandomNumberGenerator rng)
		{
			var padMat = GetCachedMaterial(new Color(0.22f, 0.42f, 0.2f), 0.8f);
			for (int i = 0; i < 3; i++)
			{
				float a = rng.RandfRange(0f, Mathf.Tau);
				float r = rng.RandfRange(0f, 10f);
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new CylinderMesh { TopRadius = 7f, BottomRadius = 7f, Height = 0.6f },
					Position = pos + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r),
					MaterialOverride = padMat,
				});
			}

			var flowerGroup = new Node3D { Position = pos + Vector3.Up * 3f };
			_world.AddChild(flowerGroup);
			var flowerMat = GetCachedMaterial(plantColor, 0.6f);
			for (int i = 0; i < 2; i++)
			{
				float a = rng.RandfRange(0f, Mathf.Tau);
				flowerGroup.AddChild(new MeshInstance3D
				{
					Mesh = new SphereMesh { Radius = 3f, Height = 5f },
					Position = new Vector3(Mathf.Cos(a) * 6f, 0, Mathf.Sin(a) * 6f),
					MaterialOverride = flowerMat,
				});
			}

			var plant = new FruitTree { Position = pos, RipenDays = 3, FruitItemId = itemId };
			plant.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 10f, Height = 4f } });
			_world.AddChild(plant);
			plant.Init(flowerGroup);
		}

		// Hinh thuyen don gian (khong co model CC0 phu hop) - than hinh hop + 2 dau nhon.
		private void AddBoatHull(Boat boat)
		{
			var hullMat = GetCachedMaterial(new Color(0.42f, 0.28f, 0.16f), 0.7f);
			boat.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(14f, 8f, 30f) },
				Position = Vector3.Up * 4f,
				MaterialOverride = hullMat,
			});
			boat.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(14f, 8f, 10f) },
				Position = new Vector3(0, 4f, 20f),
				MaterialOverride = hullMat,
			});
		}

		// Cau hinh tung loai dong vat hoang da (giong tinh than EnemyDef data-driven cua Enemy.cs)
		// - dung CHUNG 1 script WildAnimal.cs, khac nhau qua Role/model/tint/moi.
		private struct WildSpeciesConfig
		{
			public string SpeciesId; public WildRole Role; public string ModelPath; public float ModelScale;
			public string TintHex; public bool Aquatic; public bool Swims; public float WaterY; public string[] PreyGroups;
			public int InitialCount; public int MaxVisible;
		}

		private static readonly WildSpeciesConfig[] LakeWildlifeSpecies =
		{
			new WildSpeciesConfig { SpeciesId = "deer", Role = WildRole.Herbivore,
				ModelPath = "res://assets3d/polypizza/goat/goat.glb", ModelScale = 14.5f, TintHex = "#6b4a2f",
				InitialCount = 3, MaxVisible = 3 },
			new WildSpeciesConfig { SpeciesId = "rabbit", Role = WildRole.Herbivore,
				ModelPath = "res://assets3d/quaternius/animals/cat.glb", ModelScale = 2.2f, TintHex = "#a9895f",
				InitialCount = 4, MaxVisible = 4 },
			new WildSpeciesConfig { SpeciesId = "fox", Role = WildRole.Predator,
				ModelPath = "res://assets3d/quaternius/animals/dog.glb", ModelScale = 3.6f, TintHex = "#c9642a",
				PreyGroups = new[] { "wild_rabbit" }, InitialCount = 2, MaxVisible = 2 },
			new WildSpeciesConfig { SpeciesId = "wolf", Role = WildRole.Predator,
				ModelPath = "res://assets3d/quaternius/animals/wolf.glb", ModelScale = 4f, TintHex = "",
				PreyGroups = new[] { "wild_deer", "wild_rabbit" }, InitialCount = 1, MaxVisible = 1 },
			new WildSpeciesConfig { SpeciesId = "duck", Role = WildRole.Waterfowl,
				ModelPath = "res://assets3d/quaternius/animals/chicken.glb", ModelScale = 7.84f, TintHex = "#5a4a30",
				Aquatic = true, Swims = true, WaterY = 1.7f, InitialCount = 5, MaxVisible = 5 },
			new WildSpeciesConfig { SpeciesId = "fish", Role = WildRole.Fish,
				ModelPath = "", ModelScale = 1.4f, TintHex = "#8fa5b0",
				Aquatic = true, Swims = true, WaterY = -6f, InitialCount = 6, MaxVisible = 6 },
		};

		private void SpawnLakeWildlife(RandomNumberGenerator rng)
		{
			foreach (var cfg in LakeWildlifeSpecies)
				for (int i = 0; i < cfg.InitialCount; i++)
					SpawnOneWildlife(cfg, rng);
		}

		private void SpawnOneWildlife(WildSpeciesConfig cfg, RandomNumberGenerator rng)
		{
			var scene = cfg.Aquatic ? _wildAquaticScene : _wildAnimalScene;
			if (scene == null) return;
			var a = scene.Instantiate<WildAnimal>();
			a.SpeciesId = cfg.SpeciesId;
			a.Role = cfg.Role;
			a.ModelPath = cfg.ModelPath;
			a.ModelScale = cfg.ModelScale;
			a.TintHex = cfg.TintHex;
			a.PreySpeciesGroups = cfg.PreyGroups;
			a.SwimsOnWater = cfg.Swims;
			a.WaterSurfaceY = cfg.WaterY;
			a.HomeCenter = LakeRegionCenter;
			a.RoamRadius = cfg.Aquatic ? 380f : 700f;
			a.WaterEdgePos = LakeRegionCenter + new Vector3(rng.RandfRange(-400f, 400f), 1.6f, rng.RandfRange(-400f, 400f));

			float angle = rng.RandfRange(0f, Mathf.Tau);
			float radius = rng.RandfRange(0f, a.RoamRadius * 0.8f);
			var pos = LakeRegionCenter + new Vector3(Mathf.Cos(angle) * radius, cfg.Swims ? cfg.WaterY : 0f, Mathf.Sin(angle) * radius);
			a.Position = pos;
			_world.AddChild(a);
		}

		// Giu so ca the HIEN HINH quanh muc "MaxVisible" - CHI spawn lai neu quan the THAT (xem
		// WaterEcosystem.Population) van con du (>=5), khong hoi sinh vo han neu 1 loai da tuyet
		// chung cuc bo (dung y "Ecosystem Balance" nguoi choi mo ta: quan the co the that su ve 0).
		private void RespawnWildlife()
		{
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var cfg in LakeWildlifeSpecies)
			{
				int live = GetTree().GetNodesInGroup("wild_" + cfg.SpeciesId).Count;
				if (live >= cfg.MaxVisible) continue;
				if (WaterEcosystem.Instance.Get(cfg.SpeciesId) < 5f) continue;
				SpawnOneWildlife(cfg, rng);
			}
		}

		// Song - 1 doan nuoc dai/hep (don gian hoa - khong phai song uon luon tu nhien, xem gioi
		// han da neu trong ke hoach) bang qua 1 cau da, AN TOAN (khong co quai rieng).
		private void BuildRiverRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Sông", Center = RiverRegionCenter, HalfSize = 1300f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/tree_birch_2.glb", 30f, 40f, true),
					MakeDecor("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f),
				},
				MinDecor = 4, MaxDecor = 8,
			});

			BuildWaterRegion(RiverRegionCenter, 120f, 900f, new Color(0.22f, 0.48f, 0.62f), "label.river");
			if (_stoneBridgeScene != null)
			{
				var bridge = _stoneBridgeScene.Instantiate<Node3D>();
				bridge.Position = RiverRegionCenter + Vector3.Up * 2f;
				bridge.RotationDegrees = new Vector3(0, 90, 0);
				bridge.Scale = Vector3.One * 30f;
				_world.AddChild(bridge);
			}
			var rng = new RandomNumberGenerator { Seed = 6501 };
			for (int i = 0; i < 3; i++)
			{
				var pos = RiverRegionCenter + new Vector3(180f, 0, (i - 1) * 400f);
				AddFruitTree(pos, "ca", new Color(0.3f, 0.5f, 0.7f), rng);
			}
		}

		// Lang - khu dan cu NHE (xem BuildLightSettlement), gan nong trai hon Thanh Pho.
		private void BuildVillageRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Làng", Center = VillageRegionCenter, HalfSize = 900f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/kenney/nature/plant_bush.glb", 12f, 18f),
					MakeDecor("res://assets3d/kenney/nature/flower_yellowA.glb", 6f, 10f),
				},
				MinDecor = 3, MaxDecor = 6,
			});

			BuildLightSettlement(VillageRegionCenter, houseCount: 10, footprint: 700f, "label.village", "village_npc");
		}

		// Thanh Pho - khu dan cu NHE quy mo lon hon Lang (xem BuildLightSettlement) - van NHE hon
		// Khu Do Thi hien co (khong co noi that that su, xem gioi han da neu trong ke hoach).
		private void BuildBigCityRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Thành Phố", Center = BigCityRegionCenter, HalfSize = 1600f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/kenney/nature/plant_bush.glb", 10f, 14f),
				},
				MinDecor = 2, MaxDecor = 4,
			});

			BuildLightSettlement(BigCityRegionCenter, houseCount: 28, footprint: 1400f, "label.city", "city_npc");
		}

		// Ruins - cum phe tich (xem BuildRuinsCluster), quai Bong Ma Phe Tich.
		private void BuildRuinsRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Phế Tích", Center = RuinsRegionCenter, HalfSize = 1200f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/rock_2.glb", 14f, 20f),
					MakeDecor("res://assets3d/kenney/nature/grass_large.glb", 10f, 16f),
				},
				MinDecor = 4, MaxDecor = 8,
				EnemyTable = new (string, float)[] { ("ruins_wraith", 1f) },
				EnemyChance = 0.5f, EnemyStatMultiplier = 1.25f,
			});

			BuildRuinsCluster(RuinsRegionCenter);
		}

		// Nghia dia - bia mo rai rac (xem AddGravestone), quai Ma Nghia Dia (loot Xuong).
		private void BuildCemeteryRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Nghĩa Địa", Center = CemeteryRegionCenter, HalfSize = 1000f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/tree_birch_2.glb", 26f, 34f, true),
					MakeDecor("res://assets3d/kenney/nature/grass_large.glb", 8f, 12f),
				},
				MinDecor = 3, MaxDecor = 6,
				EnemyTable = new (string, float)[] { ("cemetery_ghost", 1f) },
				EnemyChance = 0.55f, EnemyStatMultiplier = 1.2f,
			});

			WorldStreamer.ExclusionZones.Add((CemeteryRegionCenter, 300f));
			var rng = new RandomNumberGenerator { Seed = 6801 };
			for (int i = 0; i < 16; i++)
			{
				float angle = Mathf.Tau * i / 16f;
				float radius = rng.RandfRange(60f, 240f);
				var pos = CemeteryRegionCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				AddGravestone(pos, rng.RandfRange(0f, 360f), rng);
			}
			AddDecor(_smallBarnScene, CemeteryRegionCenter, 12f, 0f, SmallBarnFootprint);
			AddBuildingLabelZone(CemeteryRegionCenter, 300f, "label.cemetery");
		}

		// Dam lay - nuoc duc xanh xam (tai su dung BuildWaterRegion, mau rieng), quai Quai Dam
		// Lay, tai nguyen Thao Duoc Dam Lay hiem (FruitTree).
		private void BuildSwampRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Đầm Lầy", Center = SwampRegionCenter, HalfSize = 1300f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/tree_birch_1.glb", 24f, 32f, true),
					MakeDecor("res://assets3d/kenney/nature/plant_bush.glb", 14f, 20f),
				},
				MinDecor = 6, MaxDecor = 11,
				EnemyTable = new (string, float)[] { ("swamp_lurker", 1f) },
				EnemyChance = 0.5f, EnemyStatMultiplier = 1.2f,
			});

			BuildWaterRegion(SwampRegionCenter, 380f, 380f, new Color(0.22f, 0.28f, 0.2f), "label.swamp");
			var rng = new RandomNumberGenerator { Seed = 6901 };
			for (int i = 0; i < 5; i++)
			{
				float angle = Mathf.Tau * i / 5f;
				var pos = SwampRegionCenter + new Vector3(Mathf.Cos(angle) * 480f, 0, Mathf.Sin(angle) * 480f);
				AddFruitTree(pos, "thao_duoc_dam_lay", new Color(0.3f, 0.45f, 0.25f), rng);
			}
		}

		// Hang dong - KHAC Mo (BuildMine, 3 tang khai thac khoang san): CHI 1 tang, quai Doi Hang
		// Dong, 1 kho bau OreNode hiem cuoi hang - tai su dung DUNG ky thuat cua Mo (mat tien da +
		// Y-stacked interior room qua BuildRoom).
		private void BuildCaveRegion()
		{
			WorldStreamer.Regions.Add(new WorldStreamer.RegionProfile
			{
				Name = "Hang Động", Center = CaveRegionCenter, HalfSize = 1200f,
				DecorOptions = new[]
				{
					MakeDecor("res://assets3d/quaternius/nature/rock_1.glb", 16f, 22f),
					MakeDecor("res://assets3d/quaternius/nature/rock_2.glb", 16f, 22f),
				},
				MinDecor = 4, MaxDecor = 8,
				EnemyTable = new (string, float)[] { ("cave_bat", 1f) },
				EnemyChance = 0.45f, EnemyStatMultiplier = 1.1f,
			});

			WorldStreamer.ExclusionZones.Add((CaveRegionCenter, 220f));
			var rockMat = GetCachedMaterial(new Color(0.28f, 0.27f, 0.26f), 1f);
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(150f, 80f, 55f) }, Position = CaveRegionCenter + Vector3.Up * 40f, MaterialOverride = rockMat });
			_world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 26f, BottomRadius = 26f, Height = 56f }, Position = CaveRegionCenter + Vector3.Up * 27f, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = GetCachedMaterial(new Color(0.05f, 0.05f, 0.05f), 1f) });
			var caveBody = new StaticBody3D { Position = CaveRegionCenter };
			caveBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(150f, 80f, 55f) }, Position = Vector3.Up * 40f });
			_world.AddChild(caveBody);
			AddBuildingLabelZone(CaveRegionCenter, 220f, "label.cave");

			var floorAnchor = new Vector3(CaveRegionCenter.X, 500f + _nextInteriorIndex * 900f, CaveRegionCenter.Z);
			_nextInteriorIndex++;
			AddBuildingDoor(CaveRegionCenter, 80f, isExit: false, floorAnchor);

			BuildRoom(floorAnchor, 320f, 130f, new Color(0.26f, 0.24f, 0.22f), new Color(0.18f, 0.17f, 0.15f), null, default, backIsExit: true, null,
				anchor2 =>
				{
					var rng = new RandomNumberGenerator { Seed = 6902 };
					for (int i = 0; i < 8; i++)
					{
						float angle = Mathf.Tau * i / 8f;
						float radius = rng.RandfRange(70f, 130f);
						var pos = anchor2 + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
						AddOreNode(pos, "quang_hang", hp: 45, dropAmount: 1, regrowDays: 6, new Color(0.45f, 0.4f, 0.55f));
					}
				});
		}

		private void BuildMine()
		{
			// Loai tru trang trai/coi xay/quai hoang da khoi khu vuc cua ham (giong BuildPlateaus).
			WorldStreamer.ExclusionZones.Add((MineEntrancePos, 260f));

			// Mat tien hang da don gian (khoi da xam + vom cua) - khong tim duoc model hang dong
			// CC0 phu hop nen dung primitive, giong cach lam silo/lo ren/nha kinh truoc do.
			var rockMat = GetCachedMaterial(new Color(0.32f, 0.3f, 0.29f), 1f);
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(180f, 90f, 60f) }, Position = MineEntrancePos + Vector3.Up * 45f, MaterialOverride = rockMat });
			_world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 30f, BottomRadius = 30f, Height = 62f }, Position = MineEntrancePos + Vector3.Up * 31f, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = GetCachedMaterial(new Color(0.06f, 0.06f, 0.06f), 1f) });
			var mineBody = new StaticBody3D { Position = MineEntrancePos };
			mineBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(180f, 90f, 60f) }, Position = Vector3.Up * 45f });
			_world.AddChild(mineBody);
			AddBuildingLabelZone(MineEntrancePos, 110f, "label.mine_shaft");

			// 3 tang, moi tang 1 do cao (Y) RIENG qua bo dem CHUNG _nextInteriorIndex (giong het
			// AddBuildingEntrance) - dam bao KHONG BAO GIO chong lan voi bat ky noi that nao khac
			// trong game du dat toi bao nhieu cong trinh di nua.
			Vector3 NextFloorAnchor()
			{
				var a = new Vector3(MineEntrancePos.X, 500f + _nextInteriorIndex * 900f, MineEntrancePos.Z);
				_nextInteriorIndex++;
				return a;
			}
			var floor1 = NextFloorAnchor();
			var floor2 = NextFloorAnchor();
			var floor3 = NextFloorAnchor();

			AddBuildingDoor(MineEntrancePos, 90f, isExit: false, floor1);

			var wallColor = new Color(0.3f, 0.27f, 0.24f);
			var floorColor = new Color(0.22f, 0.2f, 0.18f);

			BuildRoom(floor1, 360f, 140f, wallColor, floorColor, null, default, backIsExit: true, floor2,
				anchor => FurnishMineFloor(anchor, "dong_tho", hp: 20, regrowDays: 2, enemyStatMult: 1.2f, rockColor: new Color(0.55f, 0.35f, 0.2f)));
			BuildRoom(floor2, 380f, 150f, wallColor, floorColor, null, floor1, backIsExit: false, floor3,
				anchor => FurnishMineFloor(anchor, "sat_tho", hp: 35, regrowDays: 3, enemyStatMult: 1.5f, rockColor: new Color(0.5f, 0.32f, 0.25f)));
			BuildRoom(floor3, 400f, 160f, wallColor, floorColor, null, default, backIsExit: true, floor2,
				anchor => FurnishMineFloor(anchor, "da_quy", hp: 55, regrowDays: 5, enemyStatMult: 1.8f, rockColor: new Color(0.5f, 0.35f, 0.65f)));
		}

		// Rai OreNode + vai quai trong 1 tang ham - do quy (Hp/RegrowDays/loai quang) va do manh
		// quai TANG THEO TANG SAU (enemyStatMult), CONG DON voi he so mua Dong (xem
		// Enemy.SeasonalMultiplier - ca 2 yeu to nhan chung, khong thay the nhau).
		private void FurnishMineFloor(Vector3 anchor, string oreId, int hp, int regrowDays, float enemyStatMult, Color rockColor)
		{
			var rng = new RandomNumberGenerator { Seed = (ulong)(oreId.GetHashCode() ^ 0x5EED) };
			for (int i = 0; i < 10; i++)
			{
				float angle = Mathf.Tau * i / 10f;
				float radius = rng.RandfRange(80f, 150f);
				var pos = anchor + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				AddOreNode(pos, oreId, hp, dropAmount: 2, regrowDays, rockColor);
			}
			for (int i = 0; i < 3; i++)
			{
				var pos = anchor + new Vector3(rng.RandfRange(-120f, 120f), 0, rng.RandfRange(-120f, 120f));
				var e = _enemyScene.Instantiate<Enemy>();
				e.EnemyId = rng.Randf() < 0.6f ? "mud_monster" : "spiky_monster";
				e.Position = pos;
				e.StatMultiplier = enemyStatMult * Enemy.SeasonalMultiplier();
				_world.AddChild(e);
			}
		}

		// 1 khoi quang mo (StaticBody3D + script OreNode - xem OreNode.cs) - khoi da tho voi mau
		// tuy loai quang (dong/sat/da quy), khong tim duoc model quang CC0 phu hop nen dung
		// primitive, giong cach lam da khac trong game.
		private void AddOreNode(Vector3 pos, string oreId, int hp, int dropAmount, int regrowDays, Color rockColor)
		{
			var ore = new OreNode { Position = pos, MaxHp = hp, OreItemId = oreId, DropAmount = dropAmount, RegrowDays = regrowDays };
			var visual = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(24f, 20f, 22f) },
				Position = Vector3.Up * 10f,
				RotationDegrees = new Vector3(0, 20f, 0),
				MaterialOverride = GetCachedMaterial(rockColor, 0.6f)
			};
			ore.AddChild(visual);
			ore.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(24f, 20f, 22f) }, Position = Vector3.Up * 10f });
			_world.AddChild(ore);
			ore.Init(visual);
		}

		// ==== He thong "the gioi mo": ham dung chung cho 11 khu vuc moi (xem WorldStreamer.
		// RegionProfile) - moi khu = 1 RegionProfile (mat do/loai cay/bang quai rieng, dang ky
		// vao WorldStreamer.Regions) + 1 "diem den" xay tay o TAM khu (dung 1 trong 4 ham duoi
		// day tuy loai khu) + 1 ExclusionZone bao quanh diem den do (tranh cay/da/quai ngau nhien
		// choi len tren noi dung xay tay). ====

		// Mat nuoc hinh chu nhat (Ho vuong, Song dai/hep, Dam lay) - tai su dung DUNG cong thuc
		// mau nuoc da co (BuildWaterFeatures: albedo (0.2,0.45,0.65), roughness 0.15) lam mac
		// dinh, cho phep doi mau rieng (vd Dam lay nuoc duc hon).
		private static Shader _waterShader;

		// Nuoc CO SONG + phan chieu fresnel (xem assets/shaders/water.gdshader) - thay mau phang
		// truoc day. Moi vung nuoc tao 1 ShaderMaterial RIENG (khong dung chung _materialCache -
		// cache do danh cho StandardMaterial3D theo (Color,roughness), khong hop voi ShaderMaterial)
		// vi moi vung nuoc (ho/song/dam lay) can mau nong/sau RIENG dua tren waterColor truyen vao.
		private ShaderMaterial MakeWaterMaterial(Color waterColor)
		{
			_waterShader ??= GD.Load<Shader>("res://assets/shaders/water.gdshader");
			var mat = new ShaderMaterial { Shader = _waterShader };
			mat.SetShaderParameter("shallow_color", new Color(waterColor.Lightened(0.25f), 0.82f));
			mat.SetShaderParameter("deep_color", new Color(waterColor.Darkened(0.55f), 0.95f));
			return mat;
		}

		private void BuildWaterRegion(Vector3 center, float halfX, float halfZ, Color waterColor, string label)
		{
			WorldStreamer.ExclusionZones.Add((center, Mathf.Max(halfX, halfZ) + 60f));
			var bedMat = GetCachedMaterial(new Color(0.32f, 0.3f, 0.22f), 1f);
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(halfX * 2f + 16f, 5f, halfZ * 2f + 16f) }, Position = center + Vector3.Down * 1f, MaterialOverride = bedMat });

			// Can DU luoi con (Subdivide) de song hien muot - qua thua se lam song nhin "gay khuc".
			// ~15 don vi/o luoi la muc can bang hop ly giua do muot va so dinh (tranh 1 mat nuoc
			// rat lon nhu Song 120x900 tao ra hang chuc nghin dinh).
			int subW = Mathf.Clamp((int)(halfX * 2f / 15f), 4, 80);
			int subD = Mathf.Clamp((int)(halfZ * 2f / 15f), 4, 80);
			var waterMesh = new PlaneMesh { Size = new Vector2(halfX * 2f, halfZ * 2f), SubdivideWidth = subW, SubdivideDepth = subD };
			_world.AddChild(new MeshInstance3D { Mesh = waterMesh, Position = center + Vector3.Up * 1.5f, MaterialOverride = MakeWaterMaterial(waterColor) });
			AddBuildingLabelZone(center, Mathf.Max(halfX, halfZ) * 0.5f, label);
		}

		// 1 khu dan cu NHE (nha CHI la model trang tri, KHONG co noi that that su - dung Y NGUYEN
		// mau BuildFrenchCountryside/SpawnFrenchVillagers da chung minh giu chi phi node o muc
		// thap) - dung cho ca Lang (it nha) va Thanh Pho (nhieu nha hon).
		private static readonly (string name, string[] low, string[] mid, string[] high, string[] lowEn, string[] midEn, string[] highEn)[] SettlementFlavors =
		{
			("Người Bán Hàng Rong",
				new[] { "Mua bán gì không, khách quý?" },
				new[] { "Hàng hóa tôi đều tự tay làm ra cả." },
				new[] { "Lần sau ghé tôi sẽ bớt giá cho." },
				new[] { "Buying or selling, honored guest?" },
				new[] { "Everything I sell, I made with my own two hands." },
				new[] { "Come back next time, I'll give you a discount." }),
			("Bác Thợ Săn",
				new[] { "Vùng này thỉnh thoảng có quái xuất hiện, cẩn thận nhé." },
				new[] { "Tôi hay đi săn quanh đây, quen thuộc lắm." },
				new[] { "Cần gì cứ hỏi tôi, tôi biết rõ vùng này." },
				new[] { "Monsters show up around here now and then, so be careful." },
				new[] { "I hunt around these parts often, know it like the back of my hand." },
				new[] { "Need anything, just ask. I know this land well." }),
			("Cô Giáo Làng",
				new[] { "Chào anh, mới đến đây lần đầu à?" },
				new[] { "Trẻ con trong làng đều ngoan cả." },
				new[] { "Tôi rất vui khi có người ghé thăm." },
				new[] { "Hello there, is this your first time here?" },
				new[] { "The children in the village are all well-behaved." },
				new[] { "I'm always glad to have visitors." }),
			("Ông Lão",
				new[] { "Tôi sống ở đây lâu lắm rồi." },
				new[] { "Ngày xưa nơi này khác lắm, giờ đổi thay nhiều." },
				new[] { "Để tôi kể anh nghe vài chuyện xưa." },
				new[] { "I've lived here for a very long time." },
				new[] { "This place used to look quite different, so much has changed." },
				new[] { "Let me tell you a few stories from the old days." }),
		};

		private void BuildLightSettlement(Vector3 anchor, int houseCount, float footprint, string label, string idPrefix)
		{
			WorldStreamer.ExclusionZones.Add((anchor, footprint));
			AddBuildingLabelZone(anchor, footprint * 0.35f, label);

			(PackedScene scene, float min, float max)[] houseKinds =
			{
				(_cottageScene, 26f, 36f),
				(_villageHouseScene, 34f, 46f),
				(_farmhouseScene, 46f, 62f),
			};

			const float plotSpacing = 190f;
			int halfGrid = Mathf.CeilToInt(Mathf.Sqrt(houseCount)) + 2;
			var candidates = new List<Vector2>();
			for (int gz = -halfGrid; gz <= halfGrid; gz++)
				for (int gx = -halfGrid; gx <= halfGrid; gx++)
				{
					float x = gx * plotSpacing, z = gz * plotSpacing;
					if (Mathf.Abs(x) < 90f && Mathf.Abs(z) < 90f) continue; // chua trong tam cho ten khu
					candidates.Add(new Vector2(x, z));
				}
			candidates.Sort((a, b) => a.Length().CompareTo(b.Length()));

			var rng = new RandomNumberGenerator { Seed = (ulong)idPrefix.GetHashCode() };
			var housePositions = new List<Vector3>();
			for (int i = 0; i < houseCount && i < candidates.Count; i++)
			{
				var (scene, min, max) = houseKinds[rng.RandiRange(0, houseKinds.Length - 1)];
				if (scene == null) continue;
				var pos = anchor + new Vector3(candidates[i].X, 0, candidates[i].Y);
				housePositions.Add(pos);
				var inst = scene.Instantiate<Node3D>();
				inst.Position = pos;
				inst.RotationDegrees = new Vector3(0, rng.RandiRange(0, 3) * 90f, 0);
				inst.Scale = Vector3.One * rng.RandfRange(min, max);
				_world.AddChild(inst);
			}

			for (int i = 0; i < housePositions.Count; i++)
			{
				var flavor = SettlementFlavors[i % SettlementFlavors.Length];
				var villager = _citizenScene.Instantiate<TownCitizenNpc>();
				villager.NpcId = $"{idPrefix}_{i}";
				villager.NpcName = flavor.name;
				villager.DialogueLow = flavor.low;
				villager.DialogueMid = flavor.mid;
				villager.DialogueHigh = flavor.high;
				villager.DialogueLowEn = flavor.lowEn;
				villager.DialogueMidEn = flavor.midEn;
				villager.DialogueHighEn = flavor.highEn;
				villager.WanderCenter = anchor;
				villager.WanderRadius = footprint * 0.75f;
				villager.HomePos = housePositions[i] + new Vector3(0, 0, 45);
				villager.InteriorHomePos = housePositions[i] + new Vector3(0, 8, -35);
				_world.AddChild(villager);
			}
		}

		// Cum "phe tich do nat" - tai su dung bo Kenney town (tuong/mai module, DA CO SAN tren
		// dia nhung CHUA dung toi noi nao khac) rai/xoay/nga lech thanh dang do nat, thay vi tim
		// model "ruins" moi (khong can - bo nay du hop de lam phe tich).
		private void BuildRuinsCluster(Vector3 center)
		{
			WorldStreamer.ExclusionZones.Add((center, 260f));
			const string kenneyTown = "res://assets3d/kenney/town/Models/GLB format/";
			string[] pieces = { "wall-block.glb", "wall-arch.glb", "wall-door.glb", "wall-window-stone.glb", "roof-gable.glb", "pillar-stone.glb" };
			var rng = new RandomNumberGenerator { Seed = 9901 };
			for (int i = 0; i < 14; i++)
			{
				var scene = GD.Load<PackedScene>(kenneyTown + pieces[rng.RandiRange(0, pieces.Length - 1)]);
				if (scene == null) continue;
				float angle = Mathf.Tau * i / 14f;
				float radius = rng.RandfRange(60f, 220f);
				var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				var inst = scene.Instantiate<Node3D>();
				inst.Position = pos;
				// Xoay/nga LECH ngau nhien (khong dung khoi nhu tuong that) de doc ra dang do nat.
				inst.RotationDegrees = new Vector3(rng.RandfRange(-8f, 8f), rng.RandfRange(0f, 360f), rng.RandfRange(-6f, 6f));
				inst.Scale = Vector3.One * rng.RandfRange(11f, 16f);
				_world.AddChild(inst);
			}
			AddOreNode(center, "co_vat", hp: 40, dropAmount: 1, regrowDays: 30, new Color(0.55f, 0.5f, 0.35f));
			AddBuildingLabelZone(center, 260f, "label.ruins");
		}

		// 1 bia mo primitive (khong co model CC0 phu hop, dung nguyen tac da lam voi cac cong
		// trinh khac trong game) - khoi da dung + phan dinh tron.
		private void AddGravestone(Vector3 pos, float rotY, RandomNumberGenerator rng)
		{
			var stoneMat = GetCachedMaterial(new Color(0.45f, 0.45f, 0.42f), 0.9f);
			var body = new StaticBody3D { Position = pos, RotationDegrees = new Vector3(0, rotY, 0) };
			body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(16f, 24f, 4f) }, Position = Vector3.Up * 12f, MaterialOverride = stoneMat });
			body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 8f, BottomRadius = 8f, Height = 4f }, Position = Vector3.Up * 24f, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = stoneMat });
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(16f, 28f, 4f) }, Position = Vector3.Up * 14f });
			_world.AddChild(body);
		}

		// 1 cao nguyen: khoi hinh non cut (frustum - CylinderMesh voi TopRadius < BottomRadius)
		// tao mat nghieng THOAI (ti le run:rise = 2.2, tuong duong ~24 do - an toan hon nhieu so
		// voi gioi han leo duoc mac dinh cua CharacterBody3D la 45 do) de nguoi choi leo tu chan
		// len dinh mot cach tu nhien, khong can bac thang rieng. Tren mat phang dinh rai vai "go
		// dat" (mound) - phan LO RA tren mat luon > 100 don vi (5m) dung yeu cau, dung SphereMesh
		// ep dep lam mo phong go dat tu nhien, chim ~20% duoi mat cho khong lo goc day tron.
		// Khong tim duoc model dia hinh CC0 phu hop (dia hinh procedural, kich thuoc/vi tri can
		// tuy chinh chinh xac theo tung noi) nen dung primitive, giong cach lam mang an/cot hang
		// rao/mat troi truoc do.
		private void AddPlateau(Vector3 center, float topRadius, float height, int moundCount, int seed)
		{
			float bottomRadius = topRadius + height * 2.2f;

			var plateauMesh = new CylinderMesh { TopRadius = topRadius, BottomRadius = bottomRadius, Height = height };
			var plateau = new MeshInstance3D
			{
				Mesh = plateauMesh,
				Position = center + Vector3.Up * (height / 2f),
				MaterialOverride = GetCachedMaterial(new Color(0.46f, 0.38f, 0.3f), 1f)
			};
			_world.AddChild(plateau);

			// Va cham KHOP DUNG HINH DANG that (non cut, khong phai tru tron) de nguoi choi leo
			// duoc theo dung mat doc thoai.
			var body = new StaticBody3D { Position = plateau.Position };
			body.AddChild(new CollisionShape3D { Shape = plateauMesh.CreateConvexShape() });
			_world.AddChild(body);

			WorldStreamer.ExclusionZones.Add((center, bottomRadius + 40f));

			var rng = new RandomNumberGenerator { Seed = (ulong)seed };
			var moundMat = GetCachedMaterial(new Color(0.38f, 0.3f, 0.22f), 1f);
			for (int i = 0; i < moundCount; i++)
			{
				float moundHeight = rng.RandfRange(130f, 220f); // phan lo ra tren mat luon > 100 (5m)
				float moundRadius = moundHeight * rng.RandfRange(0.5f, 0.7f);
				float angle = rng.RandfRange(0f, Mathf.Tau);
				float dist = rng.RandfRange(0f, topRadius * 0.55f); // giu trong pham vi mat phang tren dinh
				var moundBase = center + Vector3.Up * height
					+ new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
				// Chim ~20% duoi mat, lo ra ~80% chieu cao (>100 vi moundHeight>=130) cho tu nhien.
				var moundPos = moundBase + Vector3.Up * (moundHeight * 0.3f);

				_world.AddChild(new MeshInstance3D
				{
					Mesh = new SphereMesh { Radius = moundRadius, Height = moundHeight },
					Position = moundPos,
					MaterialOverride = moundMat
				});

				var moundBody = new StaticBody3D { Position = moundPos };
				moundBody.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = moundRadius * 0.9f } });
				_world.AddChild(moundBody);
			}
		}

		// Canh dong hoa huong duong, cach hang rao nong trai 100m (2000 don vi, quy doi 20 don
		// vi/met) ve phia tay. Dat o dai Z DUONG (138-642, giong hang rao ruong) de KHONG chong
		// lan voi cum chuong trai bo/ngua/ga (cung o phia tay nhung dai Z AM, -250 den -990).
		// O khoang cach nay da nam NGOAI pham vi San chinh (DrawGround, 3000x3000 quanh goc toa
		// do, toi da +-1500) nen roi vao vung hoang da cua WorldStreamer - phai dang ky vung
		// loai tru (giong Main.BuildPlateaus) de cay/da/quai ngau nhien khong moc xuyen qua canh
		// dong (nen dat/co van sinh binh thuong qua tung chunk, chi decor bi chan).
		private static readonly Vector3 SunflowerFieldCenter = new(-2552, 0, 390);
		private const float SunflowerFieldHalfX = 250f;
		private const float SunflowerFieldHalfZ = 250f;

		private void BuildSunflowerField()
		{
			float minX = SunflowerFieldCenter.X - SunflowerFieldHalfX;
			float maxX = SunflowerFieldCenter.X + SunflowerFieldHalfX;
			float minZ = SunflowerFieldCenter.Z - SunflowerFieldHalfZ;
			float maxZ = SunflowerFieldCenter.Z + SunflowerFieldHalfZ;

			WorldStreamer.ExclusionZones.Add((SunflowerFieldCenter, 395f));
			AddBuildingLabelZone(SunflowerFieldCenter, 350f, "label.sunflower_field");

			// Nen dat mau nau sam trai dai duoi ca canh dong - phan biet ro voi co xanh xung
			// quanh, giong 1 canh dong that su duoc canh tac chu khong phai hoa moc hoang.
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PlaneMesh { Size = new Vector2(SunflowerFieldHalfX * 2f, SunflowerFieldHalfZ * 2f) },
				Position = SunflowerFieldCenter + Vector3.Up * 0.4f,
				MaterialOverride = GetCachedMaterial(new Color(0.32f, 0.22f, 0.13f), 1f)
			});

			// Trong theo HANG deu (giong canh tac that su) + lech ngau nhien nho de khong qua
			// may moc - seed co dinh nen bo cuc giu nguyen moi lan tai lai.
			var rng = new RandomNumberGenerator { Seed = 6001 };
			const float rowSpacing = 46f;
			const float plantSpacing = 42f;
			for (float z = minZ + 25f; z <= maxZ - 25f; z += rowSpacing)
			{
				for (float x = minX + 25f; x <= maxX - 25f; x += plantSpacing)
				{
					var pos = new Vector3(x + rng.RandfRange(-6f, 6f), 0, z + rng.RandfRange(-6f, 6f));
					AddSunflower(pos, rng);
				}
			}
		}

		// 1 cay hoa huong duong: khong tim duoc model CC0 phu hop rieng (tim ky tren poly.pizza,
		// chi co goi "Flower"/"Flowers" chung chung CC0, khong co "Sunflower" CC0 rieng) nen dung
		// primitive - than cay (CylinderMesh mau xanh) + dau hoa la 2 lop dia (dia vang lon lam
		// canh hoa, dia nau nho hon lam nhuy o giua) nam GAN NHU NAM NGANG (huong duong thuong
		// "quay mat ve phia mat troi"/huong len troi) chi nghieng nhe ngau nhien cho tu nhien.
		private void AddSunflower(Vector3 pos, RandomNumberGenerator rng)
		{
			float stemHeight = rng.RandfRange(75f, 105f);
			float headRadius = rng.RandfRange(16f, 22f);

			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 3f, Height = stemHeight },
				Position = pos + Vector3.Up * (stemHeight / 2f),
				MaterialOverride = GetCachedMaterial(new Color(0.25f, 0.42f, 0.15f), 0.9f)
			});

			var headAnchor = new Node3D
			{
				Position = pos + Vector3.Up * stemHeight,
				RotationDegrees = new Vector3(rng.RandfRange(-12f, 12f), rng.RandfRange(0f, 360f), rng.RandfRange(-6f, 6f))
			};
			_world.AddChild(headAnchor);

			headAnchor.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = headRadius, BottomRadius = headRadius, Height = 4f },
				MaterialOverride = GetCachedMaterial(new Color(0.95f, 0.78f, 0.1f), 0.8f)
			});
			headAnchor.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = headRadius * 0.5f, BottomRadius = headRadius * 0.5f, Height = 4.6f },
				Position = Vector3.Up * 0.8f,
				MaterialOverride = GetCachedMaterial(new Color(0.32f, 0.22f, 0.08f), 1f)
			});
		}

		// "Vung que nuoc Phap": cach nong trai DUNG 10000m (200,000 don vi, quy doi 20 don
		// vi/met, tinh tu canh GAN NHAT cua vung toi FarmhousePos) ve phia tay, rong 4km (80,000
		// don vi) x 4km - 1 vung SINH THAI RIENG cho WorldStreamer (xem
		// WorldStreamer.FrenchRegionCenter/GenerateFrenchDecor: doi thap rai rac, nha kich thuoc
		// khac nhau thua thot, cang xa trung tam cang thanh "dat hoang", KHONG co quai vat) bao
		// quanh 1 "trung tam thi tran" nho DUNG SAN THAT (duong lat da + hang cay + cau da + nha
		// do quanh - phan nay MOI la noi choi thuc su, phan con lai cua 4km chi la khung canh nen
		// cho cam giac "vung rong lon"). Polyhaven (nguoi dung yeu cau) khong co model
		// nha/cau/vat lieu san sang dung cho Godot (toan model ta thuc PBR hang trieu tam giac,
		// vd 1 cay thong don le ~17 trieu tam giac - khong the dung truc tiep), nen dung nguon CC0
		// low-poly da kiem chung (poly.pizza) cho toan bo cong trinh, chi khac han cac model da
		// dung o nong trai/thi tran de vung nay co dien mao rieng.
		private static readonly Vector3 FrenchRegionCenter = FarmhousePos + new Vector3(-24000, 0, 0);
		private const float FrenchRegionHalfSize = 4000f;

		private void BuildFrenchCountryside()
		{
			WorldStreamer.FrenchRegionCenter = FrenchRegionCenter;
			WorldStreamer.FrenchRegionHalfSize = FrenchRegionHalfSize;

			// Trung tam thi tran nho (duong+cau+nha+cay dung san that) - loai tru khoi vung lazy
			// cua WorldStreamer (ban kinh du de bao het pham vi ben duoi) de khong bi chong lan
			// voi doi/nha/cay sinh ngau nhien.
			WorldStreamer.ExclusionZones.Add((FrenchRegionCenter, 1900f));

			// Duong chinh Dong-Tay + duong nhanh Bac-Nam, lat da (dung lai _roadTileScene) -
			// giao nhau dung tai tam vung.
			var mainStreetFrom = FrenchRegionCenter + new Vector3(-1300, 0, 0);
			var mainStreetTo = FrenchRegionCenter + new Vector3(1300, 0, 0);
			var crossStreetFrom = FrenchRegionCenter + new Vector3(0, 0, -900);
			var crossStreetTo = FrenchRegionCenter + new Vector3(0, 0, 900);
			AddRoad(mainStreetFrom, mainStreetTo);
			AddRoad(crossStreetFrom, crossStreetTo);

			// Hang cay chay doc 2 duong ("Hang cay chay doc duong" theo dung yeu cau) - trong
			// cach deu, lech sang 2 ben duong.
			var treeRng = new RandomNumberGenerator { Seed = 8001 };
			for (float x = mainStreetFrom.X + 60f; x <= mainStreetTo.X - 60f; x += 130f)
			{
				if (Mathf.Abs(x - FrenchRegionCenter.X) < 70f) continue; // tranh giao lo
				AddFrenchRoadsideTree(new Vector3(x, 0, FrenchRegionCenter.Z - 75f), treeRng);
				AddFrenchRoadsideTree(new Vector3(x, 0, FrenchRegionCenter.Z + 75f), treeRng);
			}
			for (float z = crossStreetFrom.Z + 60f; z <= crossStreetTo.Z - 60f; z += 130f)
			{
				if (Mathf.Abs(z - FrenchRegionCenter.Z) < 70f) continue;
				AddFrenchRoadsideTree(new Vector3(FrenchRegionCenter.X - 75f, 0, z), treeRng);
				AddFrenchRoadsideTree(new Vector3(FrenchRegionCenter.X + 75f, 0, z), treeRng);
			}

			// Cau da ("Cau da" theo dung yeu cau) - dat doc theo duong chinh, tai 2 diem canh
			// trung tam, nhu cau bac qua khe/suoi nho trong vung doi.
			if (_stoneBridgeScene != null)
			{
				foreach (float dx in new[] { -750f, 750f })
				{
					var bridge = _stoneBridgeScene.Instantiate<Node3D>();
					bridge.Position = FrenchRegionCenter + new Vector3(dx, 2f, 0);
					bridge.RotationDegrees = new Vector3(0, 90, 0);
					bridge.Scale = Vector3.One * 34f;
					_world.AddChild(bridge);
				}
			}

			// Nha KICH THUOC KHAC NHAU ("kich thuoc khac nhau" theo dung yeu cau) - 3 model rieng
			// (cottage/village house/farmhouse) x nhieu muc scale (nho/vua/lon). 100 vi tri (50
			// can dau tien co nguoi dan o - xem SpawnFrenchVillagers, 50 can sau la nha trong/bo
			// hoang, cung binh thuong o 1 vung que that) sap xep theo khoang cach tu tam (gan
			// nhat truoc, giong thuat toan BuildCityDistrict) de lan toa tu nhien, tranh khu giao
			// lo trung tam va 2 diem dat cau da.
			var houseRng = new RandomNumberGenerator { Seed = 8002 };
			(PackedScene scene, float min, float max)[] houseKinds =
			{
				(_cottageScene, 30f, 42f),
				(_villageHouseScene, 40f, 56f),
				(_farmhouseScene, 58f, 78f),
			};

			const float plotSpacing = 300f;
			const int plotHalfGrid = 8;
			const float crossroadHalf = 160f;
			Vector2[] bridgePts = { new(-750, 0), new(750, 0) };
			const float bridgeAvoidRadius = 140f;

			var plotCandidates = new List<Vector2>();
			for (int gz = -plotHalfGrid; gz <= plotHalfGrid; gz++)
			{
				for (int gx = -plotHalfGrid; gx <= plotHalfGrid; gx++)
				{
					float x = gx * plotSpacing, z = gz * plotSpacing;
					if (Mathf.Abs(x) < crossroadHalf && Mathf.Abs(z) < crossroadHalf) continue;
					bool nearBridge = false;
					foreach (var b in bridgePts)
						if (new Vector2(x, z).DistanceTo(b) < bridgeAvoidRadius) nearBridge = true;
					if (nearBridge) continue;
					plotCandidates.Add(new Vector2(x, z));
				}
			}
			plotCandidates.Sort((a, b) => a.Length().CompareTo(b.Length()));

			for (int i = 0; i < 100 && i < plotCandidates.Count; i++)
			{
				var off = new Vector3(plotCandidates[i].X, 0, plotCandidates[i].Y);
				var (scene, min, max) = houseKinds[houseRng.RandiRange(0, houseKinds.Length - 1)];
				if (scene == null) continue;
				var pos = FrenchRegionCenter + off;
				_frenchHousePositions.Add(pos);
				var inst = scene.Instantiate<Node3D>();
				inst.Position = pos;
				inst.RotationDegrees = new Vector3(0, houseRng.RandiRange(0, 3) * 90f, 0);
				inst.Scale = Vector3.One * houseRng.RandfRange(min, max);
				_world.AddChild(inst);
			}
		}

		// 50 nguoi dan vung que Phap ("sinh hoat song nhu con nguoi" theo dung yeu cau) - tai su
		// dung TownCitizenNpc.cs (da co san AI ngay/dem: ban ngay tu do di dao, toi ve nha) va he
		// thong hoi thoai NPC, gan cho tung nguoi 1 trong 50 can nha vua dung. KHAC voi cong dan
		// khu do thi (co phong noi that that su qua AddBuildingEntrance), nha vung que Phap CHI
		// la model trang tri (khong di qua he thong phong noi that) nen "ve nha ngu" duoc mo
		// phong don gian hon: dung o mot diem NGAY SAT SAU nha (khuat tam nhin tu duong) thay vi
		// vao han 1 phong rieng - giu chi phi dung san O MUC THAP (khong them 50 cap phong noi
		// that nua, tranh dong loat qua nhieu Resource/Node cung luc nhu da gap truoc day).
		private void SpawnFrenchVillagers()
		{
			if (_frenchHousePositions.Count == 0) return;

			(string name, string[] low, string[] mid, string[] high, string[] lowEn, string[] midEn, string[] highEn)[] flavors =
			{
				("Người Nông Dân",
					new[] { "Vụ mùa năm nay chắc sẽ được lắm." },
					new[] { "Chào anh bạn! Ghé thăm vùng quê chúng tôi à?" },
					new[] { "Tôi sẽ mời anh một bữa ăn đồng quê thật sự." },
					new[] { "This year's harvest looks like it'll be a good one." },
					new[] { "Hello, friend! Come to visit our countryside?" },
					new[] { "I'll treat you to a real country meal." }),
				("Người Xay Xay Bột",
					new[] { "Cối xay của tôi quay từ đời này sang đời khác rồi." },
					new[] { "Bột mì mới xay thơm lắm, anh có muốn thử không?" },
					new[] { "Gia đình tôi sẽ luôn chào đón anh." },
					new[] { "My mill has been turning for generation after generation." },
					new[] { "The flour's fresh ground and fragrant, care to try some?" },
					new[] { "My family will always welcome you." }),
				("Người Làm Vườn Nho",
					new[] { "Vườn nho trên đồi kia là của gia đình tôi." },
					new[] { "Rượu vang năm nay ngon lắm, anh nên thử." },
					new[] { "Tôi sẽ để dành cho anh chai rượu ngon nhất hầm." },
					new[] { "That vineyard on the hill belongs to my family." },
					new[] { "This year's wine is excellent, you should try it." },
					new[] { "I'll set aside the finest bottle in my cellar for you." }),
				("Người Chăn Cừu Vùng Quê",
					new[] { "Đàn cừu của tôi gặm cỏ trên những ngọn đồi thấp quanh đây." },
					new[] { "Anh đi dạo vùng quê này cũng quen mặt rồi." },
					new[] { "Ghé thăm đàn cừu của tôi bất cứ lúc nào." },
					new[] { "My flock grazes on the low hills around here." },
					new[] { "You've become a familiar face wandering this countryside." },
					new[] { "Come visit my sheep anytime." }),
				("Bà Cụ Làng Quê",
					new[] { "Tôi sống ở vùng quê này từ thuở bé." },
					new[] { "Gặp anh tôi vui lắm, nhớ con cháu quá." },
					new[] { "Để tôi kể anh nghe chuyện xưa của vùng đất này..." },
					new[] { "I've lived in this countryside since I was a child." },
					new[] { "Seeing you makes me happy, reminds me of my own grandchildren." },
					new[] { "Let me tell you an old tale of this land..." }),
				("Thợ Rèn Vùng Quê",
					new[] { "Vùng quê thanh bình nhưng vẫn cần thợ rèn giỏi." },
					new[] { "Anh ghé thăm xưởng rèn của tôi thường xuyên nhỉ." },
					new[] { "Vì quý anh, tôi sẽ rèn món đồ đặc biệt nhất." },
					new[] { "Peaceful as the countryside is, it still needs a good blacksmith." },
					new[] { "You stop by my forge quite often, don't you." },
					new[] { "Because I think highly of you, I'll forge something truly special." }),
			};

			int count = Mathf.Min(50, _frenchHousePositions.Count);

			for (int i = 0; i < count; i++)
			{
				var housePos = _frenchHousePositions[i];
				var flavor = flavors[i % flavors.Length];
				var villager = _citizenScene.Instantiate<TownCitizenNpc>();
				villager.NpcId = $"french_villager_{i}";
				villager.NpcName = flavor.name;
				villager.DialogueLow = flavor.low;
				villager.DialogueMid = flavor.mid;
				villager.DialogueHigh = flavor.high;
				villager.DialogueLowEn = flavor.lowEn;
				villager.DialogueMidEn = flavor.midEn;
				villager.DialogueHighEn = flavor.highEn;
				villager.WanderRadius = 1500f;
				villager.WanderCenter = FrenchRegionCenter;
				villager.HomePos = housePos + new Vector3(0, 0, 45);
				// Khong co phong noi that rieng (xem giai thich o tren) - "vao nha ngu" duoc mo
				// phong bang cach dung khuat ngay phia sau nha.
				villager.InteriorHomePos = housePos + new Vector3(0, 8, -35);
				_world.AddChild(villager);
			}
		}

		private void AddFrenchRoadsideTree(Vector3 pos, RandomNumberGenerator rng)
		{
			if (_treeScene == null && _treeScene2 == null) return;
			var scene = rng.Randf() < 0.5f ? _treeScene : _treeScene2;
			scene ??= _treeScene ?? _treeScene2;
			var inst = scene.Instantiate<Node3D>();
			inst.Position = pos;
			inst.RotateY(rng.RandfRange(0f, Mathf.Tau));
			inst.Scale = Vector3.One * rng.RandfRange(30f, 38f);
			_world.AddChild(inst);
		}

		// Tuong da 10 hecta bao quanh nong trai: 10 hecta = 100.000 m^2 = hinh vuong canh
		// ~316.2m (6325 don vi, quy doi 20 don vi/met, canh/2 = 3162.5). Boc quanh TOAN BO khu
		// vuc nong trai da xay tu truoc den gio (nha nong dan, ruong, nha kho, chuong bo/ngua/ga
		// + nha nguoi cham, canh dong huong duong) nhu 1 khu dat trang trai lon that su - tam dat
		// tai tam hang rao ruong hien co (KHONG doi vi tri/kich thuoc ruong that - ruong van giu
		// nguyen co che trong trot hien co, tuong chi la RANH GIOI dat bao quanh).
		// 5 diem neo "quy hoach lai toan bo trang trai thanh 5 khu vuc ro rang" (theo yeu cau):
		// Khu Chan Nuoi (Tay), Khu Nha O NPC - GOM TAT CA NPC ke ca linh gac (Bac), Khu Trong Trot
		// mo rong (Nam, canh vuon nho lon), Khu Nha Kho (Dong, sat Barn chinh). Khu San Xuat tai
		// dung OutbuildingsAnchor cu (gia tri khong doi). Cac ham dat vi tri qua FindOpenSpot gio
		// dung tham so searchCenter/searchRadius de LUON tim cho GAN diem neo cua khu minh thuoc
		// ve, thay vi tim ngau nhien khap tuong da (xem FindOpenSpot).
		private static readonly Vector3 LivestockZoneOrigin = new(-1100, 0, -1000);
		private static readonly Vector3 HousingZoneAnchor = new(900, 0, -1900);
		private static readonly Vector3 CropsExtensionAnchor = new(780, 0, 1220);
		private static readonly Vector3 StorageZoneAnchor = new(-880, 0, 120);

		private static readonly Vector3 FarmWallCenter = new(202, 0, 390);
		private const float FarmWallHalfSize = 3162.5f;

		private void BuildFarmStoneWall()
		{
			// Chan quai vat sinh trong pham vi tuong - tuong nay RONG HON han ReservedZones cu
			// (chi ~3000x3000 quanh GOC TOA DO, trong khi tuong nay rong toi 6325x6325 quanh
			// FarmWallCenter) nen can dang ky rieng, neu khong quai van "lot" duoc vao phan dat
			// moi them.
			WorldStreamer.NoEnemyZoneCenter = FarmWallCenter;
			WorldStreamer.NoEnemyZoneHalfSize = FarmWallHalfSize;

			// QUAN TRONG: chan LUON CA CAY/DA/DECOR hoang da cua WorldStreamer trong TOAN BO
			// pham vi tuong (khong chi rieng quai nhu tren) - ReservedZones (bo qua ca chunk) chi
			// phu ~3000x3000 quanh GOC TOA DO, con tuong nay rong toi 6325x6325 quanh
			// FarmWallCenter (202,390) NEN VANH NGOAI cua trang trai (tu ~1500 don vi tro ra,
			// dung noi cac chuong ve tinh/coi xay gio hay duoc dat qua FindOpenSpot) VAN nam
			// NGOAI ReservedZones - WorldStreamer se tu sinh cay/da hoang da O DAY MA KHONG HE
			// BIET gi ve cac chuong da dat (Main.cs va WorldStreamer la 2 he thong hoan toan
			// doc lap, khong chia se danh sach vi tri). Cay/da moc chen vao/xung quanh 1 chuong
			// se che khuat vat nuoi, day rat co the la nguyen nhan that su cua bao cao "chuong o
			// xa/gan tuong khong thay dong vat" - dang ky 1 vung loai tru DUY NHAT phu kin ca
			// buc tuong se chan hoan toan van de nay (dung ban kinh *1.45 > sqrt(2)=1.414 de phu
			// het ca 4 goc vuong cua tuong).
			WorldStreamer.ExclusionZones.Add((FarmWallCenter, FarmWallHalfSize * 1.45f));

			float minX = FarmWallCenter.X - FarmWallHalfSize;
			float maxX = FarmWallCenter.X + FarmWallHalfSize;
			float minZ = FarmWallCenter.Z - FarmWallHalfSize;
			float maxZ = FarmWallCenter.Z + FarmWallHalfSize;
			const float gateHalfWidth = 220f;

			// Cong Dong: dung DUNG noi con duong that (FarmGatePos -> VillageAnchor) cat qua
			// canh Dong cua tuong - tinh giao diem chinh xac (khong hard-code) de duong khong
			// bao gio bi tuong chan, du toa do nguon co doi sau nay.
			Vector3 roadDir = VillageAnchor - FarmGatePos;
			float tGate = (maxX - FarmGatePos.X) / roadDir.X;
			float eastGateZ = FarmGatePos.Z + tGate * roadDir.Z;

			// THEM 1 CONG O GIUA moi canh Bac/Tay/Nam (khong chi rieng canh Dong noi duong lang
			// di qua) - neu chi co 1 cong duy nhat, nguoi choi di ra huong khac se bi "nhot" hoan
			// toan trong 10 hecta, khong the ra ngoai duoc.
			float midX = (minX + maxX) / 2f;
			float midZ = (minZ + maxZ) / 2f;

			// Bac - cong o giua
			AddStoneWallLine(new Vector3(minX, 0, minZ), new Vector3(midX - gateHalfWidth, 0, minZ));
			AddStoneWallLine(new Vector3(midX + gateHalfWidth, 0, minZ), new Vector3(maxX, 0, minZ));
			// Tay - cong o giua
			AddStoneWallLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, midZ - gateHalfWidth));
			AddStoneWallLine(new Vector3(minX, 0, midZ + gateHalfWidth), new Vector3(minX, 0, maxZ));
			// Nam - cong o giua
			AddStoneWallLine(new Vector3(minX, 0, maxZ), new Vector3(midX - gateHalfWidth, 0, maxZ));
			AddStoneWallLine(new Vector3(midX + gateHalfWidth, 0, maxZ), new Vector3(maxX, 0, maxZ));
			// Dong - cong dung tai giao diem duong that
			AddStoneWallLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, eastGateZ - gateHalfWidth));
			AddStoneWallLine(new Vector3(maxX, 0, eastGateZ + gateHalfWidth), new Vector3(maxX, 0, maxZ));

			// Cong chao 3D bang da tai CA 4 cong (theo yeu cau) - xem AddStoneGateArch. outwardDir
			// la huong "ra ngoai" tuong (vuong goc voi huong doc tuong) - dung de tinh canh cua go
			// mo VE PHIA NAO (ap sat mat ngoai tuong khi mo het, giong cua that).
			AddStoneGateArch(new Vector3(midX, 0, minZ), Vector3.Right, Vector3.Forward, gateHalfWidth, "label.gate_north");
			AddStoneGateArch(new Vector3(minX, 0, midZ), Vector3.Back, Vector3.Left, gateHalfWidth, "label.gate_west");
			AddStoneGateArch(new Vector3(midX, 0, maxZ), Vector3.Right, Vector3.Back, gateHalfWidth, "label.gate_south");
			// Dong: cong chinh (noi con duong that di vao lang) - bang ten rieng, "chao mung".
			AddStoneGateArch(new Vector3(maxX, 0, eastGateZ), Vector3.Back, Vector3.Right, gateHalfWidth, "label.farm_welcome_sign");
		}

		// Cong chao 3D bang da, phong cach chau Au trung co (kham khao Gatehouse/vong thanh
		// trung co - tru da + lanh tho + RANG CUA (crenellation/merlon) tren dinh tru, dac trung
		// thi giac ro nhat cua kien truc lau dai chau Au, xem
		// https://historiceuropeancastles.com/castle-gatehouse/) + 2 CANH CUA GO that (xem
		// FarmGateDoor.cs) treo tren ban le, tu mo khi nguoi choi lai gan - THAY THE hoan toan
		// khoang trong co dinh truoc day ("nguoi choi co the ra vao cua trang trai"). Khong tim
		// duoc model "cong chao" CC0 nao dung phong cach da tu nhien khop voi stone_wall.glb dang
		// dung cho ca buc tuong, nen xay tu khoi hop nguyen ban (giong cach lam silo/lo ren o
		// BuildFarmOutbuildings) - dung DUNG 1 mau da xam nhat quan.
		private static readonly Color StoneGateColor = new(0.56f, 0.54f, 0.5f);
		private static readonly Color GateDoorWoodColor = new(0.32f, 0.2f, 0.11f);
		private static readonly Color GateDoorIronColor = new(0.16f, 0.15f, 0.14f);

		private void AddStoneGateArch(Vector3 gateCenter, Vector3 alongDir, Vector3 outwardDir, float openHalfWidth, string signText)
		{
			var dir = alongDir.Normalized();
			var outward = outwardDir.Normalized();
			float angleDeg = Mathf.RadToDeg(Mathf.Atan2(-dir.Z, dir.X));
			float outwardAngleDeg = Mathf.RadToDeg(Mathf.Atan2(-outward.Z, outward.X));
			var stoneMat = GetCachedMaterial(StoneGateColor, 1f);

			const float pillarHeight = 130f;
			const float pillarWidth = 50f;
			const float pillarDepth = 50f;
			const float lintelHeight = 35f;
			const float lintelDepth = 60f;
			// Tru dat GAN SAT mep khoang trong (con 6 don vi ho voi tuong da that su) - khong
			// choan loi di (khoang cach giua 2 mep trong tru con lai ~328 don vi, du rong).
			float pillarOffset = openHalfWidth - pillarWidth * 0.5f - 6f;

			foreach (float side in new[] { -1f, 1f })
			{
				var pos = gateCenter + dir * (pillarOffset * side) + Vector3.Up * (pillarHeight / 2f);
				var rot = new Vector3(0, angleDeg, 0);
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = new Vector3(pillarWidth, pillarHeight, pillarDepth) },
					Position = pos,
					RotationDegrees = rot,
					MaterialOverride = stoneMat
				});
				var body = new StaticBody3D { Position = pos, RotationDegrees = rot };
				body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(pillarWidth, pillarHeight, pillarDepth) } });
				_world.AddChild(body);

				// Rang cua (crenellation) tren dinh tru - 3 khoi vuong nho nhoi len tren dinh,
				// dac trung "lau dai trung co" ro rang nhat khi nhin tu xa, du chi la 1 chi tiet
				// nho (khong xay rang cua doc toan bo tuong da - pham vi qua lon, xem ghi chu o
				// BuildFarmStoneWall).
				foreach (float merlonOffset in new[] { -pillarWidth * 0.3f, 0f, pillarWidth * 0.3f })
				{
					_world.AddChild(new MeshInstance3D
					{
						Mesh = new BoxMesh { Size = new Vector3(12f, 16f, pillarDepth + 4f) },
						Position = pos + Vector3.Up * (pillarHeight / 2f + 8f) + (dir.Cross(Vector3.Up)).Normalized() * merlonOffset,
						RotationDegrees = rot,
						MaterialOverride = stoneMat
					});
				}
			}

			// Da ngang (lanh tho) noi 2 dinh tru - trai dai tu mep ngoai tru nay sang mep ngoai
			// tru kia, cao HON tuong that su de tao dang 1 CONG CHAO ro rang khi nhin tu xa.
			float lintelSpan = pillarOffset * 2f + pillarWidth;
			var lintelPos = gateCenter + Vector3.Up * (pillarHeight + lintelHeight / 2f);
			var lintelRot = new Vector3(0, angleDeg, 0);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(lintelSpan, lintelHeight, lintelDepth) },
				Position = lintelPos,
				RotationDegrees = lintelRot,
				MaterialOverride = stoneMat
			});
			var lintelBody = new StaticBody3D { Position = lintelPos, RotationDegrees = lintelRot };
			lintelBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(lintelSpan, lintelHeight, lintelDepth) } });
			_world.AddChild(lintelBody);

			// Bang ten khac chu tren da ngang, de doc tu ca 2 huong tiep can.
			_world.AddChild(new Label3D
			{
				Text = Loc.T(signText),
				Position = gateCenter + Vector3.Up * (pillarHeight - 6f),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 40,
				OutlineSize = 8,
				PixelSize = 0.16f,
				Modulate = new Color(0.98f, 0.92f, 0.65f)
			});

			// 2 canh cua go that (xem FarmGateDoor.cs) - ban le dat SAT mep trong tru da, moi
			// canh choan gan het nua khoang trong (tru 6 don vi ho o giua). Dong: canh trai co
			// local+X huong theo +dir (angleDeg); canh phai huong theo -dir (angleDeg+180). Mo:
			// xoay toi khi local+X huong theo outward (tinh CHINH XAC qua Atan2, khong doan 90
			// do co dinh) - canh cua "ap" vao mat ngoai tuong khi mo het, giong cua that.
			const float doorHeight = 110f;
			float doorLeafWidth = openHalfWidth - pillarWidth * 0.5f - 12f;
			float hingeInset = pillarOffset - pillarWidth * 0.5f;

			float leftClosedDeg = angleDeg;
			float leftOpenSwing = Mathf.Wrap(outwardAngleDeg - leftClosedDeg, -180f, 180f);
			AddGateDoorLeaf(gateCenter - dir * hingeInset, leftClosedDeg, leftOpenSwing, doorLeafWidth, doorHeight);

			float rightClosedDeg = angleDeg + 180f;
			float rightOpenSwing = Mathf.Wrap(outwardAngleDeg - rightClosedDeg, -180f, 180f);
			AddGateDoorLeaf(gateCenter + dir * hingeInset, rightClosedDeg, rightOpenSwing, doorLeafWidth, doorHeight);
		}

		// 1 canh cua go cho Cong Chao (xem AddStoneGateArch) - hingePos la vi tri BAN LE (canh
		// cua treo va xoay quanh diem nay), mesh+va cham lech ra tu ban le doc truc X CUC BO cua
		// FarmGateDoor (sau khi xoay ClosedYRotationDeg, X cuc bo huong VAO khoang trong cong).
		private void AddGateDoorLeaf(Vector3 hingePos, float closedYRotationDeg, float openSwingDeg, float doorWidth, float doorHeight)
		{
			var door = new FarmGateDoor
			{
				Position = hingePos,
				RotationDegrees = new Vector3(0, closedYRotationDeg, 0),
				ClosedYRotationDeg = closedYRotationDeg,
				OpenSwingDeg = openSwingDeg,
			};
			_world.AddChild(door);

			var woodMat = GetCachedMaterial(GateDoorWoodColor, 0.85f);
			var doorMeshPos = new Vector3(doorWidth / 2f, doorHeight / 2f, 0f);
			door.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(doorWidth, doorHeight, 6f) },
				Position = doorMeshPos,
				MaterialOverride = woodMat,
			});

			// Thanh sat ngang gia lap tren mat go (chi tiet "cua trung co" - go + dai sat) - 2
			// thanh o 1/4 va 3/4 chieu cao, trai het chieu rong canh cua.
			var ironMat = GetCachedMaterial(GateDoorIronColor, 0.4f);
			foreach (float heightFrac in new[] { 0.25f, 0.75f })
			{
				door.AddChild(new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = new Vector3(doorWidth * 0.94f, 6f, 7.5f) },
					Position = new Vector3(doorWidth / 2f, doorHeight * heightFrac, 0f),
					MaterialOverride = ironMat,
				});
			}

			door.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(doorWidth, doorHeight, 6f) },
				Position = doorMeshPos,
			});
		}

		// 4 coi xay gio 3D rai trong pham vi tuong da (theo yeu cau) - tu tim cho trong
		// (FindOpenSpot/_extraPenZones, giong cach dat cac chuong ve tinh) de khong bao gio chong
		// len chuong trai/nha cua/vuon nho da co san, du dat luc nao trong _Ready().
		private void BuildWindmills()
		{
			var avoid = KnownOccupiedZonesExcluding(OutbuildingsAnchor);
			var rng = new RandomNumberGenerator { Seed = 10300 };
			const float windmillScale = 25f;
			const float footprintRadius = 140f; // than + canh quat khi quay + le an toan

			// Quy hoach lai: 4 coi xay gio TRONG tuong gio thuoc Khu San Xuat (che bien) - tim gan
			// OutbuildingsAnchor thay vi ngau nhien khap tuong da.
			//
			// searchRadius=1400 (truoc day 500->700->1000, moi lan deu giam dan canh bao nhung
			// khong het han: 1000 van con 1 lan cham rat nho ~5/140, do FindOpenSpot chi thu 600
			// vi tri NGAU NHIEN - khi vung "that su trong" chi la 1 phan nho cua toan bo vong tron
			// tim kiem (phan lon phia Tay bi vung dat rieng nong trai 780 quanh (202,390) chan,
			// xem ghi chu duoi day), xac suat 600 lan random deu roi dung vao phan trong con lai
			// khong phai 100% dam bao. OutbuildingsAnchor(850,280) chi cach tam nong trai ~657 don
			// vi - GAN HON ca ban kinh 780 do, nghia la CHINH diem neo da nam trong vung cam. 1400
			// mo rong dang ke phan dat that su trong (phia Dong/Nam), van an toan xa tuong
			// (FarmWallHalfSize=3162.5).
			for (int i = 0; i < 4; i++)
			{
				var pos = FindOpenSpot(avoid, footprintRadius, rng, searchCenter: OutbuildingsAnchor, searchRadius: 1400f);
				avoid.Add((pos, footprintRadius));
				_extraPenZones.Add((pos, footprintRadius));
				float rotY = rng.RandfRange(0f, 360f);
				AddWindmill(pos, rotY, windmillScale);
				AddBuildingLabelZone(pos, footprintRadius, "label.windmill");
			}
		}

		// 6 coi xay gio THEM NUA, lan nay rai NGOAI tuong da (theo yeu cau "xung quanh trang
		// trai", khac voi BuildWindmills o tren la "trong trang trai") - random doc theo CHU VI
		// HINH VUONG cua tuong (khong dung ban kinh co dinh tu tam: tuong la hinh VUONG nen 1
		// vong tron ban kinh co dinh se lam diem o huong CHEO GOC van con nam o TRONG tuong; phai
		// "di bo" doc 4 canh tuong roi day ra ngoai theo dung phap tuyen canh do). Tranh dam vao
		// vuon nho lon/cao nguyen/khu phu tro da co (dung chung KnownOccupiedZones/_extraPenZones
		// nhu BuildWindmills).
		private void BuildOuterWindmills()
		{
			var avoid = KnownOccupiedZones();
			var rng = new RandomNumberGenerator { Seed = 10400 };
			const float windmillScale = 25f;
			const float footprintRadius = 140f;
			const float outsideMargin = 260f; // day ra ngoai tuong bao xa

			float minX = FarmWallCenter.X - FarmWallHalfSize, maxX = FarmWallCenter.X + FarmWallHalfSize;
			float minZ = FarmWallCenter.Z - FarmWallHalfSize, maxZ = FarmWallCenter.Z + FarmWallHalfSize;
			float side = 2f * FarmWallHalfSize;
			float perimeter = 4f * side;

			for (int i = 0; i < 6; i++)
			{
				Vector3 pos = default;
				bool found = false;
				for (int tries = 0; tries < 200; tries++)
				{
					float t = rng.RandfRange(0f, perimeter);
					Vector3 edgePos, outward;
					if (t < side) { edgePos = new Vector3(minX + t, 0, minZ); outward = new Vector3(0, 0, -1); } // canh Bac
					else if (t < 2 * side) { edgePos = new Vector3(maxX, 0, minZ + (t - side)); outward = new Vector3(1, 0, 0); } // canh Dong
					else if (t < 3 * side) { edgePos = new Vector3(maxX - (t - 2 * side), 0, maxZ); outward = new Vector3(0, 0, 1); } // canh Nam
					else { edgePos = new Vector3(minX, 0, maxZ - (t - 3 * side)); outward = new Vector3(-1, 0, 0); } // canh Tay

					var candidate = edgePos + outward * outsideMargin;
					bool ok = true;
					foreach (var (c, r) in avoid)
						if (new Vector2(candidate.X - c.X, candidate.Z - c.Z).Length() < r + footprintRadius) { ok = false; break; }
					if (ok) { pos = candidate; found = true; break; }
				}
				if (!found) continue;

				avoid.Add((pos, footprintRadius));
				_extraPenZones.Add((pos, footprintRadius));
				AddWindmill(pos, rng.RandfRange(0f, 360f), windmillScale);
				AddBuildingLabelZone(pos, footprintRadius, "label.windmill");
			}
		}

		// Dat 1 coi xay gio: tach rieng node canh quat ("...Blades...") ra lam con cua 1 pivot
		// (WindmillBlades) dat DUNG TAM AABB cuc bo cua rieng canh quat (khong phai tam ca coi
		// xay) - neu khong, xoay se lam canh quat "vay" quanh chan thap thay vi quay tai cho
		// giong coi xay that. Truc quay TU DONG chon theo chieu MONG NHAT cua AABB canh quat
		// (canh quat la 1 dia phang, huong mong nhat chinh la truc quay that su).
		private void AddWindmill(Vector3 pos, float rotationYDegrees, float scale)
		{
			if (_windmillScene == null) return;
			var inst = _windmillScene.Instantiate<Node3D>();
			inst.Position = pos;
			inst.RotationDegrees = new Vector3(0, rotationYDegrees, 0);
			inst.Scale = Vector3.One * scale;
			_world.AddChild(inst);

			MeshInstance3D blades = null;
			foreach (var child in inst.GetChildren())
				if (child is MeshInstance3D mi && mi.Name.ToString().Contains("Blades")) { blades = mi; break; }

			if (blades != null)
			{
				var aabb = blades.GetAabb();
				var pivotLocal = aabb.GetCenter();
				var size = aabb.Size;
				Vector3 axis = size.X <= size.Y && size.X <= size.Z ? Vector3.Right
					: size.Z <= size.X && size.Z <= size.Y ? Vector3.Back
					: Vector3.Up;

				var pivot = new WindmillBlades { Position = pivotLocal, SpinAxis = axis };
				inst.AddChild(pivot);
				inst.RemoveChild(blades);
				pivot.AddChild(blades);
				blades.Position = -pivotLocal;
			}

			// Va cham dang tru tron gan dung than thap that (khong tinh canh quat - canh quat tu
			// do quay, khong can chan nguoi choi).
			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D
			{
				Shape = new CylinderShape3D { Radius = 2.5f * scale, Height = 8.86f * scale },
				Position = Vector3.Up * (8.86f * scale / 2f)
			});
			_world.AddChild(body);
		}

		// 4 thap canh 3D (theo yeu cau) dat DUNG 4 GOC tuong da 10 hecta - vi tri co dien hinh
		// nhat cho thap canh phong thu, bao quat duoc ca 2 canh tuong ke nhau tu 1 diem. Moi thap
		// co 1 dong LUA CANH GAC that su tren dinh, tu dong bat/tat theo dung khung gio da yeu
		// cau (18h - 6h sang) - dung chung CHINH XAC he thong bat/tat cua den duong da co san
		// (_streetLamps/_streetLampGlowMats/OnStreetLampHourChanged - xem AddStreetLamp) thay vi
		// tao rieng 1 co che gio giac moi, vi khung gio yeu cau GIONG HET nhau.
		private void BuildWatchTowers()
		{
			if (_watchTowerScene == null) return;
			float minX = FarmWallCenter.X - FarmWallHalfSize, maxX = FarmWallCenter.X + FarmWallHalfSize;
			float minZ = FarmWallCenter.Z - FarmWallHalfSize, maxZ = FarmWallCenter.Z + FarmWallHalfSize;
			const float towerScale = 140f;
			const float towerNativeHeight = 1.4625f; // chieu cao that cua scene sau khi import (xem ghi chu luc tai ve)
			float towerHeight = towerNativeHeight * towerScale;

			Vector3[] corners =
			{
				new(minX, 0, minZ), new(maxX, 0, minZ),
				new(minX, 0, maxZ), new(maxX, 0, maxZ),
			};
			foreach (var corner in corners)
			{
				var inst = _watchTowerScene.Instantiate<Node3D>();
				inst.Position = corner;
				inst.RotationDegrees = new Vector3(0, 45f, 0);
				inst.Scale = Vector3.One * towerScale;
				_world.AddChild(inst);

				var body = new StaticBody3D { Position = corner };
				body.AddChild(new CollisionShape3D
				{
					Shape = new CylinderShape3D { Radius = 40f, Height = towerHeight },
					Position = Vector3.Up * (towerHeight / 2f)
				});
				_world.AddChild(body);

				AddBeaconFire(corner + Vector3.Up * (towerHeight - 8f));
				AddBuildingLabelZone(corner, 120f, "label.watchtower");
			}
		}

		// Dong lua canh gac tren dinh thap: 1 cai nia da + 3 "ngon lua" hinh non chong lech nhau
		// (ky thuat "low-poly fire" pho bien - vai hinh non mau cam/do chong len nhau trong hon
		// 1 khoi tron duy nhat) + 1 OmniLight3D am. Vat lieu ngon lua duoc dang ky vao
		// _streetLampGlowMats/_streetLamps CHUNG voi den duong, nen tu dong sang dung 18h-6h sang
		// ma khong can code bat/tat rieng.
		private void AddBeaconFire(Vector3 pos)
		{
			var bowlMat = GetCachedMaterial(new Color(0.28f, 0.27f, 0.25f), 0.6f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 16f, BottomRadius = 12f, Height = 10f },
				Position = pos,
				MaterialOverride = bowlMat
			});

			var flameMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(1f, 0.55f, 0.15f),
				EmissionEnabled = true,
				Emission = new Color(1f, 0.42f, 0.08f),
				EmissionEnergyMultiplier = 0f // SetStreetLampsOn dat lai dung theo gio (xem AddStreetLamp)
			};
			var flameRng = new RandomNumberGenerator { Seed = (uint)(pos.X * 7 + pos.Z * 13) };
			for (int i = 0; i < 3; i++)
			{
				float r = 5f + i * 1.4f;
				float h = 20f - i * 4f;
				var jitter = new Vector3(flameRng.RandfRange(-4f, 4f), 0, flameRng.RandfRange(-4f, 4f));
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = r, Height = h },
					Position = pos + jitter + Vector3.Up * (5f + h / 2f),
					MaterialOverride = flameMat
				});
			}
			_streetLampGlowMats.Add(flameMat);

			var light = new OmniLight3D
			{
				Position = pos + Vector3.Up * 14f,
				LightColor = new Color(1f, 0.5f, 0.15f),
				LightEnergy = 9f,
				OmniRange = 340f
			};
			_world.AddChild(light);
			_streetLamps.Add(light);
		}

		// Xep khoi da doc 1 duong thang (giong AddFenceLine/AddRoad) - tu dong chia deu so
		// manh khop khit tu "from" den "to", kem va cham dac chan nguoi choi/dong vat.
		private void AddStoneWallLine(Vector3 from, Vector3 to)
		{
			if (_stoneWallScene == null) return;
			const float nativeLength = 1.56f; // do dai that cua model (xem ghi chu khi tai ve)
			const float targetSegment = 130f;

			float dist = from.DistanceTo(to);
			if (dist < 1f) return;
			int count = Mathf.Max(1, Mathf.RoundToInt(dist / targetSegment));
			float actualSegment = dist / count;
			float wallScale = actualSegment / nativeLength;

			Vector3 dir = (to - from).Normalized();
			float angleDeg = Mathf.RadToDeg(Mathf.Atan2(-dir.Z, dir.X));
			for (int i = 0; i < count; i++)
			{
				var segPos = from + dir * (actualSegment * (i + 0.5f));
				var inst = _stoneWallScene.Instantiate<Node3D>();
				inst.Position = segPos;
				inst.RotationDegrees = new Vector3(0, angleDeg, 0);
				inst.Scale = Vector3.One * wallScale;
				_world.AddChild(inst);

				var body = new StaticBody3D { Position = segPos, RotationDegrees = new Vector3(0, angleDeg, 0) };
				body.AddChild(new CollisionShape3D
				{
					Shape = new BoxShape3D { Size = new Vector3(actualSegment, 40f, 20f) },
					Position = Vector3.Up * 20f
				});
				_world.AddChild(body);
			}
		}

		// Nha o cho nguoi lam ruong thue (SmallBarn - cung he thong cua+noi that 2 tang da dung
		// cho cac cong trinh khac) + NPC AI di lam theo gio hanh chinh that (6h-18h), tuan tu
		// cham soc TOAN BO canh dong (trong/tuoi/thu hoach that su - xem FarmWorkerNpc.cs). Dat
		// gan ruong nhung ngoai hang rao, tranh nha kho.
		private Vector3 FarmWorkerHousePos; // xem ghi chu tren CowherdHousePos

		private void BuildFarmWorker()
		{
			FarmWorkerHousePos = NextHousingCottagePos(10804);
			AddDecor(_smallBarnScene, FarmWorkerHousePos, 12f, 0f, SmallBarnFootprint);
			var interiorHomePos = AddBuildingEntrance(FarmWorkerHousePos, 0f, 80f, 50f, RoomKind.Village);
			AddBuildingLabelZone(FarmWorkerHousePos, 100f, "label.farm_worker_house");

			var npc = _farmWorkerScene.Instantiate<FarmWorkerNpc>();
			npc.NpcId = "farmworker";
			npc.NpcName = "Theodore";
			npc.DialogueLow = new[] { "Chào, ta được thuê làm ruộng cho anh. Giờ hành chính 6 giờ sáng tới 6 giờ tối." };
			npc.DialogueMid = new[] { "Cánh đồng dạo này tốt tươi lắm, ta chăm sóc kỹ cả." };
			npc.DialogueHigh = new[] { "Thu hoạch được gì ta đều mang về giao cho anh đầy đủ." };
			npc.DialogueLowEn = new[] { "Hello, I'm hired to work the fields for you. Working hours are 6 AM to 6 PM." };
			npc.DialogueMidEn = new[] { "The fields are looking lush lately, I tend to them carefully." };
			npc.DialogueHighEn = new[] { "Whatever we harvest, I bring it all back to you in full." };
			npc.HomePos = FarmWorkerHousePos + new Vector3(0, 0, 55);
			npc.InteriorHomePos = interiorHomePos;
			var fieldWorkPos = FarmOrigin + new Vector3((FarmGridW - 1) * FarmSpacing / 2f, 0, (FarmGridH - 1) * FarmSpacing / 2f);
			npc.WorkPos = fieldWorkPos;
			_world.AddChild(npc);

			// Them 1 nam + 2 nu quan ly/cham soc RIENG ruong chinh (theo dung yeu cau) - cung
			// WorkPos voi Theodore nen ca 4 nguoi cung tuan tra/cham soc toan bo luoi 72 o, NPC
			// Task Board (xem NpcTaskBoard.cs) tu chia o dat cho tung nguoi, khong ai lam trung.
			(string id, string name, string diaLow, string diaMid, string diaHigh, string diaLowEn, string diaMidEn, string diaHighEn, int houseSeed, float scale)[] extraFieldWorkers =
			{
				("farmworker_2", "Julien",
					"Chào anh, tôi cũng làm ruộng ở đây, phụ Theodore chăm sóc cánh đồng.",
					"Ruộng rộng thế này một mình Theodore lo không xuể, có tôi phụ đỡ vất vả hơn nhiều.",
					"Anh cứ yên tâm, cả nhóm chúng tôi thay phiên nhau trông coi ruộng suốt ngày.",
					"Hello, I work the fields here too, helping Theodore look after the crops.",
					"A field this big is too much for Theodore alone - having me around makes it a lot easier.",
					"Don't worry, the whole team of us takes turns watching over the fields all day.",
					10806, 22f),
				("farmworker_3", "Margot",
					"Chào anh, tôi là một trong những người chăm ruộng chính ở đây.",
					"Tôi thích nhìn cây lớn lên mỗi ngày, công việc này hợp với tôi lắm.",
					"Mùa nào thức nấy, tôi luôn cố gắng để ruộng cho năng suất tốt nhất.",
					"Hello, I'm one of the workers tending the main fields here.",
					"I love watching the crops grow a little more each day, this work suits me well.",
					"Whatever the season, I always try to get the best yield from the fields.",
					10807, 20f),
				("farmworker_4", "Camille",
					"Chào anh, tôi cùng mọi người chăm sóc ruộng chính của trang trại.",
					"Tưới nước, bắt sâu, bón phân - việc nào tôi cũng làm quen tay cả rồi.",
					"Nhìn ruộng xanh tốt như vầy là tôi thấy công sức mình bỏ ra xứng đáng.",
					"Hello, I help take care of the farm's main fields along with the others.",
					"Watering, pest control, fertilizing - I've gotten the hang of all of it by now.",
					"Seeing the fields this lush makes all the work feel worth it.",
					10808, 20f),
			};

			foreach (var w in extraFieldWorkers)
			{
				var housePos = NextHousingCottagePos(w.houseSeed);
				AddDecor(_smallBarnScene, housePos, 12f, 0f, SmallBarnFootprint);
				var interior = AddBuildingEntrance(housePos, 0f, 80f, 50f, RoomKind.Village);
				AddBuildingLabelZone(housePos, 100f, "label.farm_worker_house");

				var w2 = _farmWorkerScene.Instantiate<FarmWorkerNpc>();
				w2.NpcId = w.id;
				w2.NpcName = w.name;
				w2.ModelScale = w.scale;
				w2.DialogueLow = new[] { w.diaLow };
				w2.DialogueMid = new[] { w.diaMid };
				w2.DialogueHigh = new[] { w.diaHigh };
				w2.DialogueLowEn = new[] { w.diaLowEn };
				w2.DialogueMidEn = new[] { w.diaMidEn };
				w2.DialogueHighEn = new[] { w.diaHighEn };
				w2.HomePos = housePos + new Vector3(0, 0, 55);
				w2.InteriorHomePos = interior;
				w2.WorkPos = fieldWorkPos;
				_world.AddChild(w2);
			}
		}

		// Chuong cuu + heo dung chung (khu chan nuoi phu), tiep noi cum chuong bo/ngua/ga ve
		// phia nam (Z am hon), cach chuong ga ~110 don vi de khong chong lan.
		// Z = -1440 (lui them 110 don vi so voi -1330 truoc day) - dam bao khoang cach toi
		// chuong ga (moi lui ve -1050, ban kinh 150) van ~90 don vi, khong bi ep sat.
		// Z = -1636 (lui them tu -1440) - khoang cach toi chuong ga (moi lui ve -1186, ban kinh
		// moi 180) can duy tri ~90 don vi sau khi SheepPigPastureHalf tang 20% (150->180).
		private Vector3 SheepPigPastureCenter; // xem ghi chu tren CowPastureCenter
		private const float SheepPigPastureHalf = 180f; // tang 20% (150 -> 180)

		private void BuildSheepPigPasture()
		{
			float minX = SheepPigPastureCenter.X - SheepPigPastureHalf;
			float maxX = SheepPigPastureCenter.X + SheepPigPastureHalf;
			float minZ = SheepPigPastureCenter.Z - SheepPigPastureHalf;
			float maxZ = SheepPigPastureCenter.Z + SheepPigPastureHalf;
			float gateX = SheepPigPastureCenter.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene);
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 22f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 22f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));

			AddFeedTrough(SheepPigPastureCenter);
			AddStreetLamp(new Vector3(gateX - 35, 0, maxZ), 90f);
			AddStreetLamp(new Vector3(gateX + 35, 0, maxZ), -90f);
			AddPenCenterLight(SheepPigPastureCenter, SheepPigPastureHalf);
			AddBuildingLabelZone(SheepPigPastureCenter, SheepPigPastureHalf + 20f, "label.sheep_pig_pen");

			// 20 cuu + 10 heo (tang tu 2+2 theo yeu cau) - rai deu vong tron trong hang rao.
			var spRng = new RandomNumberGenerator { Seed = 9701 };
			for (int i = 0; i < 20; i++)
			{
				float angle = Mathf.Tau * i / 20f;
				float radius = spRng.RandfRange(40f, SheepPigPastureHalf - 35f);
				var sheep = _sheepScene.Instantiate<Sheep>();
				sheep.Position = SheepPigPastureCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				sheep.TroughPosition = SheepPigPastureCenter;
				sheep.HomeCenter = SheepPigPastureCenter;
				sheep.PastureHalfExtent = SheepPigPastureHalf - 35f;
				_world.AddChild(sheep);
			}
			for (int i = 0; i < 10; i++)
			{
				float angle = Mathf.Tau * i / 10f + 0.3f;
				float radius = spRng.RandfRange(30f, SheepPigPastureHalf - 45f);
				var pig = _pigScene.Instantiate<Pig>();
				pig.Position = SheepPigPastureCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
				pig.TroughPosition = SheepPigPastureCenter;
				pig.HomeCenter = SheepPigPastureCenter;
				pig.PastureHalfExtent = SheepPigPastureHalf - 35f;
				_world.AddChild(pig);
			}
		}

		// Vuon cay an qua: 5 loai (tao/le/anh dao/dao/hat de) trong theo luoi thua, moi cay la 1
		// than + 1 tan la hinh cau + vai qua nho mau rieng - khong tim duoc model cay an qua CC0
		// phu hop (da tim ky) nen dung primitive, giong cach lam mang an/cot hang rao truoc do.
		// Moi cay la CAY LAU NAM hai duoc that su (xem FruitTree.cs) - qua AN khi chua chin, tu
		// HIEN ra sau RipenDays ngay, hai xong tu moc lai (khong can trong lai).
		// Quy hoach lai: doi ve Khu Trong Trot (canh ruong chinh + vuon nho lon o phia Nam) thay
		// vi vi tri cu (canh cum chuong trai cu, gio da chuyen sang Khu Chan Nuoi rieng).
		private static readonly Vector3 OrchardCenter = CropsExtensionAnchor;
		private static readonly (string name, string itemId, Color fruitColor)[] FruitKinds =
		{
			("Táo", "apple", new Color(0.75f, 0.12f, 0.1f)),
			("Lê", "pear", new Color(0.75f, 0.78f, 0.25f)),
			("Anh đào", "cherry", new Color(0.55f, 0.05f, 0.15f)),
			("Đào", "peach", new Color(0.9f, 0.55f, 0.25f)),
			("Hạt dẻ", "chestnut", new Color(0.4f, 0.24f, 0.12f)),
		};

		private void BuildOrchard()
		{
			AddBuildingLabelZone(OrchardCenter, 160f, "label.orchard");
			var rng = new RandomNumberGenerator { Seed = 9001 };
			int idx = 0;
			for (int row = -1; row <= 1; row++)
			{
				for (int col = -2; col <= 2; col++)
				{
					var pos = OrchardCenter + new Vector3(col * 75f + rng.RandfRange(-8f, 8f), 0, row * 85f + rng.RandfRange(-8f, 8f));
					var (_, itemId, fruitColor) = FruitKinds[idx % FruitKinds.Length];
					AddFruitTree(pos, itemId, fruitColor, rng);
					idx++;
				}
			}
		}

		private void AddFruitTree(Vector3 pos, string fruitItemId, Color fruitColor, RandomNumberGenerator rng)
		{
			float trunkHeight = rng.RandfRange(48f, 62f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 4.5f, BottomRadius = 6f, Height = trunkHeight },
				Position = pos + Vector3.Up * (trunkHeight / 2f),
				MaterialOverride = GetCachedMaterial(new Color(0.35f, 0.24f, 0.14f), 0.9f)
			});
			float canopyRadius = rng.RandfRange(26f, 34f);
			var canopyPos = pos + Vector3.Up * (trunkHeight + canopyRadius * 0.6f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = canopyRadius, Height = canopyRadius * 1.9f },
				Position = canopyPos,
				MaterialOverride = GetCachedMaterial(new Color(0.22f, 0.42f, 0.16f), 1f)
			});

			// Nhom qua RIENG (Node3D) de FruitTree.cs co the AN/HIEN toan bo cung luc (chua chin
			// = an, chin = hien) - xem FruitTree.Init.
			var fruitGroup = new Node3D();
			_world.AddChild(fruitGroup);
			var fruitMat = GetCachedMaterial(fruitColor, 0.7f);
			for (int i = 0; i < 5; i++)
			{
				float a = rng.RandfRange(0f, Mathf.Tau);
				float r = canopyRadius * 0.85f;
				var fruitPos = canopyPos + new Vector3(Mathf.Cos(a) * r, rng.RandfRange(-10f, 8f), Mathf.Sin(a) * r);
				fruitGroup.AddChild(new MeshInstance3D
				{
					Mesh = new SphereMesh { Radius = 3.6f, Height = 7.2f },
					Position = fruitPos,
					MaterialOverride = fruitMat
				});
			}

			var tree = new FruitTree { Position = pos + Vector3.Up * (trunkHeight / 2f), FruitItemId = fruitItemId };
			tree.AddChild(new CollisionShape3D
			{
				Shape = new CylinderShape3D { Radius = 6f, Height = trunkHeight }
			});
			_world.AddChild(tree);
			tree.Init(fruitGroup);
		}

		// Vuon nho ("dac trung Phap") - rai theo hang, dung model Grapes (Kenney, CC0) tren coc
		// go don gian lam gian nho.
		private static readonly Vector3 VineyardCenter = CropsExtensionAnchor + new Vector3(250, 0, 260); // xem ghi chu OrchardCenter

		private void BuildVineyard()
		{
			AddBuildingLabelZone(VineyardCenter, 130f, "label.vineyard");
			var postMat = GetCachedMaterial(new Color(0.3f, 0.2f, 0.1f), 1f);
			var rng = new RandomNumberGenerator { Seed = 9002 };
			for (int row = -1; row <= 1; row++)
			{
				for (int col = -3; col <= 3; col++)
				{
					var pos = VineyardCenter + new Vector3(col * 34f, 0, row * 60f);
					_world.AddChild(new MeshInstance3D
					{
						Mesh = new CylinderMesh { TopRadius = 2f, BottomRadius = 2.4f, Height = 26f },
						Position = pos + Vector3.Up * 13f,
						MaterialOverride = postMat
					});
					if (_grapesScene != null)
					{
						var vine = _grapesScene.Instantiate<Node3D>();
						vine.Position = pos + Vector3.Up * 16f;
						vine.Scale = Vector3.One * rng.RandfRange(4.5f, 6f);
						vine.RotateY(rng.RandfRange(0f, Mathf.Tau));
						_world.AddChild(vine);

						// Gian nho NAY la CAY LAU NAM hai duoc that su (xem FruitTree.cs) - dung
						// CHINH model nho lam "fruit visual" de an/hien (khac vuon cay tren, o day
						// khong co tan la rieng de giu lai khi chua chin nen an ca gian nho luon).
						var vineTree = new FruitTree { Position = pos + Vector3.Up * 16f, FruitItemId = "grape" };
						vineTree.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 10f, Height = 30f } });
						_world.AddChild(vineTree);
						vineTree.Init(vine);
					}
				}
			}
		}

		// To ong: khong tim duoc model "hive" CC0 phu hop nen dung primitive (chong hop go
		// trang - kieu hop Langstroth that ngoai doi) + vai con ong (Quaternius Bee, CC0) bay
		// lon von quanh to bang tween don gian (khong can AI di chuyen day du nhu dong vat lon).
		private void BuildBeehive()
		{
			var hiveMat = GetCachedMaterial(new Color(0.85f, 0.8f, 0.68f), 0.8f);
			var roofMat = GetCachedMaterial(new Color(0.5f, 0.15f, 0.1f), 0.9f);
			Vector3[] hivePositions = { OrchardCenter + new Vector3(0, 0, -110), OrchardCenter + new Vector3(70, 0, -120) };
			foreach (var hivePos in hivePositions)
			{
				for (int layer = 0; layer < 3; layer++)
				{
					_world.AddChild(new MeshInstance3D
					{
						Mesh = new BoxMesh { Size = new Vector3(20f, 12f, 20f) },
						Position = hivePos + Vector3.Up * (6f + layer * 12f),
						MaterialOverride = hiveMat
					});
				}
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new PrismMesh { Size = new Vector3(24f, 8f, 24f) },
					Position = hivePos + Vector3.Up * 40f,
					MaterialOverride = roofMat
				});
				var body = new StaticBody3D { Position = hivePos };
				body.AddChild(new CollisionShape3D
				{
					Shape = new BoxShape3D { Size = new Vector3(22f, 40f, 22f) },
					Position = Vector3.Up * 20f
				});
				_world.AddChild(body);

				if (_beeScene == null) continue;
				var beeRng = new RandomNumberGenerator();
				beeRng.Randomize();
				for (int i = 0; i < 3; i++)
				{
					var bee = _beeScene.Instantiate<Node3D>();
					bee.Position = hivePos + new Vector3(beeRng.RandfRange(-20f, 20f), beeRng.RandfRange(25f, 45f), beeRng.RandfRange(-20f, 20f));
					bee.Scale = Vector3.One * 9f;
					_world.AddChild(bee);
					var tw = bee.CreateTween().SetLoops();
					var basePos = bee.Position;
					tw.TweenProperty(bee, "position", basePos + new Vector3(14f, 6f, 10f), 1.4f)
						.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
					tw.TweenProperty(bee, "position", basePos, 1.4f)
						.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				}
			}
		}

		// Nha o + NPC cho "nguoi lam vuon/trang trai phu" - quan ly ca cuu/heo/vuon cay/vuon nho/
		// to ong, san xuat xoay vong nhieu loai san pham (len/tao/nho ep ruou/mat ong/sap ong).
		private Vector3 EstateWorkerHousePos; // xem ghi chu tren CowherdHousePos

		private void BuildEstateWorker()
		{
			EstateWorkerHousePos = NextHousingCottagePos(10805);
			AddDecor(_smallBarnScene, EstateWorkerHousePos, 12f, 90f, SmallBarnFootprint);
			var interiorHomePos = AddBuildingEntrance(EstateWorkerHousePos, 90f, 80f, 50f, RoomKind.Village);
			AddBuildingLabelZone(EstateWorkerHousePos, 100f, "label.estate_worker_house");

			var npc = _estateWorkerScene.Instantiate<EstateWorkerNpc>();
			npc.NpcId = "estateworker";
			npc.NpcName = "Augustin";
			// Quy hoach lai: chuong cuu/heo gio co nguoi cham rieng trong Khu Chan Nuoi (xem
			// BuildAnimalPenDistrict) nen vai tro cua Augustin thu hep lai CHI con Khu Trong Trot
			// (vuon cay/vuon nho/to ong), bo giam sat cuu/heo (trung lap, khong con can thiet).
			npc.DialogueLow = new[] { "Chào, ta phụ trách vườn cây, vườn nho và tổ ong ở đây." };
			npc.DialogueMid = new[] { "Vườn cây và vườn nho dạo này sai quả thật." };
			npc.DialogueHigh = new[] { "Thỉnh thoảng ta để lại chút táo, mật ong hay rượu vang cho anh." };
			npc.DialogueLowEn = new[] { "Hello, I look after the orchard, vineyard, and beehives here." };
			npc.DialogueMidEn = new[] { "The orchard and vineyard are really bearing fruit lately." };
			npc.DialogueHighEn = new[] { "I sometimes leave a bit of apple, honey, or wine for you." };
			npc.HomePos = EstateWorkerHousePos + new Vector3(0, 0, 55);
			npc.InteriorHomePos = interiorHomePos;
			npc.WorkPos = CropsExtensionAnchor;
			npc.WorkWanderRadius = 320f;
			npc.Products = new[] { "apple", "grape", "honey", "beeswax", "wine" };
			_world.AddChild(npc);
		}

		// He thong nuoc: gieng gan nha chinh + ao nho canh ruong + kenh tuoi noi gieng-ao toi
		// mep ruong (dai mau xanh don gian, mang tinh trang tri/chu de "co nguon nuoc" - he
		// thong tuoi thuc te van dung UseOn() nhu cu, kenh chi la canh quan cho dung yeu cau
		// "he thong nuoc" nhin thay duoc).
		private void BuildWaterFeatures()
		{
			var wellPos = FarmhousePos + new Vector3(90, 0, 50);
			if (_wellScene != null)
			{
				var well = _wellScene.Instantiate<Node3D>();
				well.Position = wellPos;
				well.Scale = Vector3.One * 30f;
				_world.AddChild(well);
				var body = new StaticBody3D { Position = wellPos };
				body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 16f, Height = 30f }, Position = Vector3.Up * 15f });
				_world.AddChild(body);
			}

			var pondCenter = FarmOrigin + new Vector3(-140, 0, 320);
			var waterMat = GetCachedMaterial(new Color(0.2f, 0.45f, 0.65f), 0.15f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 70f, BottomRadius = 72f, Height = 3f },
				Position = pondCenter + Vector3.Up * 0.5f,
				MaterialOverride = waterMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 78f, BottomRadius = 82f, Height = 5f },
				Position = pondCenter,
				MaterialOverride = GetCachedMaterial(new Color(0.55f, 0.48f, 0.36f), 1f)
			});

			// Kenh tuoi: 1 dai xanh noi ao toi mep ruong.
			var fieldEdge = FarmOrigin + new Vector3(-40, 0, FarmSpacing * 2);
			Vector3 canalDir = (fieldEdge - pondCenter);
			float canalLen = canalDir.Length();
			canalDir = canalDir.Normalized();
			float canalAngle = Mathf.RadToDeg(Mathf.Atan2(canalDir.X, canalDir.Z));
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(14f, 1f, canalLen) },
				Position = (pondCenter + fieldEdge) / 2f + Vector3.Up * 0.4f,
				RotationDegrees = new Vector3(0, canalAngle, 0),
				MaterialOverride = waterMat
			});
		}

		// Khu cong cu + kho cui: 1 goc nho canh nha kho, tai su dung cong cu da co (cuoc/binh
		// tuoi) + them riu/xeng/xe day, cong voi dong cui go (khuc go + riu cam vao goc).
		private void BuildToolAndWoodpileArea()
		{
			var toolAreaPos = BarnPos2Vec() + new Vector3(150, 0, -60);
			AddDecor(_hoeScene, toolAreaPos, 19f, 10f);
			AddDecor(_pitchforkScene, toolAreaPos + new Vector3(20, 0, 5), 10f, -15f);
			AddDecor(_wateringCanScene, toolAreaPos + new Vector3(40, 0, -5), 23f);
			if (_axeScene != null) AddDecor(_axeScene, toolAreaPos + new Vector3(-15, 0, 15), 22f, 30f);
			if (_shovelScene != null) AddDecor(_shovelScene, toolAreaPos + new Vector3(60, 0, 15), 22f, -20f);
			if (_cartScene != null) AddDecor(_cartScene, toolAreaPos + new Vector3(0, 0, 40), 36f, 15f);

			var woodpilePos = toolAreaPos + new Vector3(-90, 0, 0);
			var rng = new RandomNumberGenerator { Seed = 9101 };
			for (int i = 0; i < 12; i++)
			{
				var pos = woodpilePos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-20, 20));
				AddDecor(_woodLogScene, pos, rng.RandfRange(5f, 6.5f), rng.RandfRange(0f, 360f));
			}
			if (_axeScene != null) AddDecor(_axeScene, woodpilePos + new Vector3(0, 12, -30), 24f, 90f);
		}

		private Vector3 BarnPos2Vec() => new(-482, 0, 250);

		// Khu vuon nho canh nha chinh: hoa + bui thao duoc (dung model co san tu WorldStreamer -
		// flower_yellowA/plant_bush, Kenney CC0) - trang tri, chu de "thao duoc" (mint/rosemary/
		// lavender... khong co he thong alchemy rieng nen chi mang tinh canh quan).
		private void BuildHerbGarden()
		{
			var gardenCenter = FarmhousePos + new Vector3(-95, 0, 60);
			var rng = new RandomNumberGenerator { Seed = 9102 };
			for (int i = 0; i < 10; i++)
			{
				var pos = gardenCenter + new Vector3(rng.RandfRange(-45, 45), 0, rng.RandfRange(-35, 35));
				if (rng.Randf() < 0.5f && _flowerScene != null)
					AddDecor(_flowerScene, pos, rng.RandfRange(7f, 10f), rng.RandfRange(0f, 360f));
				else if (_herbBushScene != null)
					AddDecor(_herbBushScene, pos, rng.RandfRange(11f, 15f), rng.RandfRange(0f, 360f));
			}
		}

		// 20 NPC lam viec theo lich trinh nhieu giai doan trong ngay (6h thuc day, 7h an sang,
		// 8h lam ruong, 12h an trua, 13h lam viec, 18h cho gia suc an, 20h ve nha, 22h ngu - xem
		// ScheduledFarmNpc.cs), chia deu 5 vai tro (Farmer/Farmhand/Stable Master/Shepherd/
		// Gardener). O chung 2 nha tro cong nhan (10 nguoi/nha, tiep noi hang nha nhan vien tai
		// X=-1100 da co - CowherdHousePos/StablehandHousePos/PoultryKeeperHousePos/
		// EstateWorkerHousePos) thay vi 20 can nha rieng.
		// Quy hoach lai: 2 doanh trai gio nam trong Khu Nha O NPC (offset co dinh tu
		// HousingZoneAnchor, cach nhau du xa de khong chong lan 2 toa nha ~100 don vi ban kinh).
		private static readonly Vector3 WorkerDorm1Pos = HousingZoneAnchor + new Vector3(-500, 0, -300);
		private static readonly Vector3 WorkerDorm2Pos = HousingZoneAnchor + new Vector3(-500, 0, -50);

		private void BuildWorkerDormsAndStaff()
		{
			var dorm1Interior = AddDecorAndEntrance(WorkerDorm1Pos);
			var dorm2Interior = AddDecorAndEntrance(WorkerDorm2Pos);
			// Dang ky vao avoid-list CHUNG de cac tim kiem Khu Nha O khac chay SAU (doanh trai Cam
			// Ve, doanh trai nguoi cham chuong, nha Jean/Marcel/Antoine, nha Etienne/Baptiste/...)
			// khong vo tinh de len 2 toa nha nay.
			_extraPenZones.Add((WorkerDorm1Pos, 100f));
			_extraPenZones.Add((WorkerDorm2Pos, 100f));

			var fieldWorkPos = FarmOrigin + new Vector3((FarmGridW - 1) * FarmSpacing / 2f, 0, (FarmGridH - 1) * FarmSpacing / 2f);

			(string role, string name, string[] low, string[] mid, string[] high, string[] lowEn, string[] midEn, string[] highEn, bool fieldWork, Vector3 workPos, Vector3 feedPos)[] roles =
			{
				("farmer", "Nông Dân",
					new[] { "Tôi là nông dân, mỗi ngày gieo hạt và chăm sóc ruộng." },
					new[] { "Ruộng dạo này lên tốt lắm, anh ghé xem thử." },
					new[] { "Nông sản thu hoạch được tôi giao hết cho anh." },
					new[] { "I'm a farmer, sowing seeds and tending the fields every day." },
					new[] { "The fields are coming along great lately, come take a look." },
					new[] { "Whatever crops we harvest, I hand them all over to you." },
					true, fieldWorkPos, CowPastureCenter),
				("farmhand", "Người Làm Ruộng",
					new[] { "Tôi phụ giúp tưới nước, chăm sóc thêm cho ruộng." },
					new[] { "Cùng anh chăm ruộng thật vui." },
					new[] { "Có gì cần tôi giúp cứ gọi nhé." },
					new[] { "I help out with watering and extra care for the fields." },
					new[] { "Working the fields alongside you is a real joy." },
					new[] { "Need a hand with anything, just call me." },
					true, fieldWorkPos, CowPastureCenter),
				("stablemaster", "Người Chăn Ngựa Phụ",
					new[] { "Tôi phụ giúp chăm sóc đàn ngựa trong chuồng." },
					new[] { "Đàn ngựa dạo này khỏe re nhờ có thêm người chăm." },
					new[] { "Anh muốn cưỡi ngựa thì ké chuồng hỏi tôi." },
					new[] { "I help take care of the horses in the stable." },
					new[] { "The horses are in great shape now that there's extra help." },
					new[] { "Fancy a ride? Come find me at the stable." },
					false, HorseStableCenter, HorseStableCenter),
				("shepherd_farm", "Người Chăn Cừu Trang Trại",
					new[] { "Tôi chăm đàn cừu và heo trong chuồng phía nam." },
					new[] { "Đàn cừu dạo này lông dày đủ lắm." },
					new[] { "Ghé chuồng cừu chơi, tôi chỉ cho anh xem." },
					new[] { "I tend the sheep and pigs in the pen to the south." },
					new[] { "The sheep's wool is coming in nice and thick lately." },
					new[] { "Come by the sheep pen, I'll show you around." },
					false, SheepPigPastureCenter, SheepPigPastureCenter),
				("gardener_farm", "Người Làm Vườn Phụ",
					new[] { "Tôi phụ chăm sóc vườn cây và vườn nho." },
					new[] { "Vườn dạo này sai quả, ghé hái thử đi." },
					new[] { "Trái chín tôi để dành phần anh đấy." },
					new[] { "I help tend the orchard and the vineyard." },
					new[] { "The trees are heavy with fruit lately, come pick some." },
					new[] { "I've been saving the ripe fruit for you." },
					false, OrchardCenter, OrchardCenter),
			};

			int total = 20;
			for (int i = 0; i < total; i++)
			{
				var r = roles[i % roles.Length];
				bool firstDorm = i < total / 2;
				var dormPos = firstDorm ? WorkerDorm1Pos : WorkerDorm2Pos;
				var dormInterior = firstDorm ? dorm1Interior : dorm2Interior;
				int slot = firstDorm ? i : i - total / 2;

				var npc = _scheduledFarmNpcScene.Instantiate<ScheduledFarmNpc>();
				npc.NpcId = $"{r.role}_{i}";
				npc.NpcName = PickStaffName(35 + i); // +35: tranh trung ten voi 5 vai tro co dinh (0-4) va 28 nguoi cham chuong tap trung (5-32)
				npc.DialogueLow = r.low;
				npc.DialogueMid = r.mid;
				npc.DialogueHigh = r.high;
				npc.DialogueLowEn = r.lowEn;
				npc.DialogueMidEn = r.midEn;
				npc.DialogueHighEn = r.highEn;
				npc.DoesFieldWork = r.fieldWork;
				npc.WorkPos = r.workPos;
				npc.FeedPos = r.feedPos;
				npc.HomePos = dormPos + new Vector3(0, 0, 55);
				// Lech nho vi tri ngu trong noi that chung de khong chong het len nhau.
				npc.InteriorHomePos = dormInterior + new Vector3((slot % 5 - 2) * 20f, 0, (slot / 5 - 0.5f) * 20f);
				_world.AddChild(npc);
			}
		}

		private Vector3 AddDecorAndEntrance(Vector3 dormPos)
		{
			AddDecor(_farmhouseScene, dormPos, 50f, 90f, FarmhouseFootprint);
			return AddBuildingEntrance(dormPos, 90f, 100f, 70f, RoomKind.Village);
		}

		// Lay Mesh THAT tu ben trong 1 PackedScene (vd model .glb) de dung voi MultiMeshInstance3D
		// - MultiMesh can 1 Mesh resource, khong nhan PackedScene truc tiep. Dung cho cac vat the
		// SO LUONG CUC LON (vd hang ngan cay nho vuon nho) - CHI 1 draw call cho ca ngan ban sao,
		// KHONG tao rieng tung Node3D/PackedScene.Instantiate() cho moi ban sao (dung theo yeu
		// cau "khong spawn tat ca thanh Actor/GameObject rieng" - tranh dong loat tao qua nhieu
		// Node/Resource cung luc, dung nguyen nhan gay crash da gap va sua truoc do trong phien
		// lam viec nay).
		private static Mesh ExtractMesh(PackedScene scene)
		{
			if (scene == null) return null;
			var inst = scene.Instantiate<Node3D>();
			var mesh = FindFirstMesh(inst);
			inst.QueueFree();
			return mesh;
		}

		private static Mesh FindFirstMesh(Node root)
		{
			if (root is MeshInstance3D mi && mi.Mesh != null) return mi.Mesh;
			foreach (Node child in root.GetChildren())
			{
				var found = FindFirstMesh(child);
				if (found != null) return found;
			}
			return null;
		}

		// Tao 1 MultiMeshInstance3D chua N ban sao cua 1 mesh, moi ban sao co transform rieng
		// (vi tri/xoay/scale) - dung cho vat trang tri SO LUONG LON (khong can va cham/AI rieng).
		private MultiMeshInstance3D AddMultiMeshScatter(Mesh mesh, List<Transform3D> transforms, Material overrideMat = null)
		{
			if (mesh == null || transforms.Count == 0) return null;
			var mm = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				Mesh = mesh,
				InstanceCount = transforms.Count
			};
			for (int i = 0; i < transforms.Count; i++)
				mm.SetInstanceTransform(i, transforms[i]);

			var node = new MultiMeshInstance3D { Multimesh = mm };
			if (overrideMat != null) node.MaterialOverride = overrideMat;
			_world.AddChild(node);
			return node;
		}

		// 4 NHAN VIEN QUAN TRONG cua trang trai (theo yeu cau), moi nguoi 1 cong viec cu the:
		//   - Jean (quan gia, 55 tuoi): di tuan qua cac diem moc chinh, tinh cach diem tinh/ky
		//     luat/thuc te/khong thich lang phi/thinh thoang phan nan - xem FarmStewardNpc.cs
		//     de biet ro PHAM VI THAT SU (loi thoai phan anh vai tro, KHONG phai 1 AI trung tam
		//     dieu khien cac NPC khac).
		//   - Marcel (tho sua chua): theo doi 5 FenceMarker (Hp hao mon moi ngay that), tu di
		//     lay go -> lay bua -> den hang rao hu nhat -> sua -> ve kho - xem RepairmanNpc.cs.
		//   - Antoine (quan ly kho): dung ngay tai kho, moi lan noi chuyen doc SO LIEU THAT tu
		//     FarmStorage (da noi FarmPlot.Harvest/FarmhandNpc.DoWorkWander vao de co so lieu
		//     that thay vi gia lap) - xem WarehouseManagerNpc.cs.
		//   - Henri (bao ve): ban ngay tuan tra cong/hang rao, troi mua chuyen sang khu dung cu
		//     (dung GameManager.IsRaining moi them), ban dem di DUNG lo trinh 5 diem theo thu tu
		//     yeu cau - xem GuardNpc.cs.
		private void BuildFarmStaff()
		{
			var avoid = KnownOccupiedZonesExcluding(HousingZoneAnchor);
			var rng = new RandomNumberGenerator { Seed = 10600 };

			// Quy hoach lai: nha cua Jean/Marcel/Antoine gio thuoc Khu Nha O NPC - tim gan
			// HousingZoneAnchor thay vi ngau nhien khap tuong da.
			Vector3 NextHousePos()
			{
				var pos = FindOpenSpot(avoid, 90f, rng, searchCenter: HousingZoneAnchor, searchRadius: 900f);
				avoid.Add((pos, 90f));
				_extraPenZones.Add((pos, 90f));
				return pos;
			}

			var toolAreaPos = BarnPos2Vec() + new Vector3(150, 0, -60);
			var woodpilePos = toolAreaPos + new Vector3(-90, 0, 0);
			var forestEdgePos = new Vector3(FarmWallCenter.X - FarmWallHalfSize, 0, FarmWallCenter.Z);
			var fieldCenterPos = FarmOrigin + new Vector3((FarmGridW - 1) * FarmSpacing / 2f, 0, (FarmGridH - 1) * FarmSpacing / 2f);

			// ---- Jean: quan gia ----
			SafeBuildStep(() =>
			{
				var housePos = NextHousePos();
				AddDecor(_smallBarnScene, housePos, 12f, 0f, SmallBarnFootprint);
				var interior = AddBuildingEntrance(housePos, 0f, 80f, 50f, RoomKind.Village);
				AddBuildingLabelZone(housePos, 100f, "label.steward_house");

				var jean = _farmStewardScene.Instantiate<FarmStewardNpc>();
				jean.NpcId = "jean_steward";
				jean.NpcName = "Jean";
				jean.DialogueLow = new[]
				{
					"Ta là Jean, quản gia trang trại này. Mọi thứ ở đây đều cần có trật tự.",
					"Cứ tự tin làm việc, ta sẽ để mắt tới mọi ngóc ngách của trang trại.",
				};
				jean.DialogueMid = new[]
				{
					"Trang trại dạo này ổn định, cứ giữ đúng nhịp là được.",
					"Ta không thích lãng phí - đồ dùng, công sức, hay thời gian đều vậy.",
					"Anh làm việc khá đấy, nhưng nhớ dọn dẹp gọn gàng sau khi xong nhé.",
				};
				jean.DialogueHigh = new[]
				{
					"Ta có kinh nghiệm quản lý trang trại lâu năm rồi, cứ yên tâm.",
					"Thực ra... anh hay bỏ đồ lại linh tinh lắm. Lần sau nhớ để đúng chỗ.",
					"Nếu anh bớt lãng phí hạt giống một chút, vụ sau sẽ lời nhiều hơn đấy.",
					"Trang trại này là tâm huyết cả đời ta - ta sẽ không để nó xuống cấp đâu.",
				};
				jean.DialogueLowEn = new[]
				{
					"I'm Jean, steward of this farm. Everything here needs to be kept in order.",
					"Work with confidence, I'll keep an eye on every corner of the farm.",
				};
				jean.DialogueMidEn = new[]
				{
					"The farm's been steady lately, just keep up the good rhythm.",
					"I dislike waste - of supplies, effort, or time, all the same.",
					"You're doing well, but remember to tidy up properly once you're done.",
				};
				jean.DialogueHighEn = new[]
				{
					"I've managed farms for many years now, so rest easy.",
					"Truth be told... you do leave things lying about. Put them back where they belong next time.",
					"If you waste fewer seeds, next season's profit will be much better.",
					"This farm is my life's work - I won't let it fall into disrepair.",
				};
				jean.HomePos = housePos + new Vector3(0, 0, 55);
				jean.InteriorHomePos = interior;
				jean.PatrolPoints = new[] { FarmhousePos, BarnPos2Vec(), CowPastureCenter, fieldCenterPos };
				_world.AddChild(jean);
			}, "BuildFarmStaff[Jean]");

			// ---- Marcel: tho sua chua ----
			SafeBuildStep(() =>
			{
				var housePos = NextHousePos();
				AddDecor(_smallBarnScene, housePos, 12f, 0f, SmallBarnFootprint);
				var interior = AddBuildingEntrance(housePos, 0f, 80f, 50f, RoomKind.Village);
				AddBuildingLabelZone(housePos, 100f, "label.repairman_house");

				var marcel = _repairmanScene.Instantiate<RepairmanNpc>();
				marcel.NpcId = "marcel_repairman";
				marcel.NpcName = "Marcel";
				marcel.DialogueLow = new[] { "Ta là Marcel, thợ sửa chữa ở đây. Hàng rào hư là ta biết ngay." };
				marcel.DialogueMid = new[] { "Vừa sửa xong 1 đoạn hàng rào, giờ mới chắc chắn hơn nhiều." };
				marcel.DialogueHigh = new[] { "Cứ để ý hàng rào cho ta, anh lo mấy vụ khác đi." };
				marcel.DialogueLowEn = new[] { "I'm Marcel, the repairman here. A broken fence, I spot it right away." };
				marcel.DialogueMidEn = new[] { "Just finished fixing a stretch of fence, much sturdier now." };
				marcel.DialogueHighEn = new[] { "Leave the fences to me, you focus on the other work." };
				marcel.HomePos = BarnPos2Vec() + new Vector3(0, 0, 60);
				marcel.InteriorHomePos = interior;
				marcel.WoodpilePos = woodpilePos;
				marcel.ToolAreaPos = toolAreaPos;
				_world.AddChild(marcel);
			}, "BuildFarmStaff[Marcel]");

			// ---- Antoine: quan ly kho ----
			SafeBuildStep(() =>
			{
				var housePos = NextHousePos();
				AddDecor(_smallBarnScene, housePos, 12f, 0f, SmallBarnFootprint);
				var interior = AddBuildingEntrance(housePos, 0f, 80f, 50f, RoomKind.Village);
				AddBuildingLabelZone(housePos, 100f, "label.warehouse_manager_house");

				var antoine = _warehouseManagerScene.Instantiate<WarehouseManagerNpc>();
				antoine.NpcId = "antoine_warehouse";
				antoine.NpcName = "Antoine";
				antoine.HomePos = BarnPos2Vec() + new Vector3(-60, 0, 40);
				antoine.InteriorHomePos = interior;
				_world.AddChild(antoine);
			}, "BuildFarmStaff[Antoine]");

			// ---- Henri: bao ve ----
			SafeBuildStep(() =>
			{
				var henri = _guardNpcScene.Instantiate<GuardNpc>();
				henri.NpcId = "henri_guard";
				henri.NpcName = "Henri";
				henri.DialogueLow = new[] { "Ta là Henri, bảo vệ trang trại này. Cứ yên tâm mà làm việc." };
				henri.DialogueMid = new[] { "Cổng và hàng rào ta kiểm tra đều đặn, chưa thấy gì bất thường." };
				henri.DialogueHigh = new[] { "Có gì khả nghi ta sẽ báo anh ngay, cứ tin ta." };
				henri.DialogueRain = new[]
				{
					"Trời mưa thì không tưới cây được, ta tranh thủ sửa sang công cụ, chuồng trại.",
					"Mưa thế này ta ở quanh đây dồn kho, đợi tạnh rồi tuần tra tiếp.",
				};
				henri.DialogueNight = new[]
				{
					"Đêm hôm ta đi tuần đúng 1 vòng: cổng - kho - đồng - bìa rừng - nhà chính.",
					"Ban đêm phải canh kỹ hơn, thú dữ gì cũng có thể thơ thẩn.",
				};
				henri.DialogueLowEn = new[] { "I'm Henri, guard of this farm. Work easy, I've got things covered." };
				henri.DialogueMidEn = new[] { "I check the gate and fences regularly, nothing out of the ordinary so far." };
				henri.DialogueHighEn = new[] { "Anything suspicious, I'll report it to you right away, trust me on that." };
				henri.DialogueRainEn = new[]
				{
					"Can't water the crops in this rain, so I'm using the time to fix up tools and pens.",
					"With weather like this I stay close by and tidy the storehouse, patrol again once it clears.",
				};
				henri.DialogueNightEn = new[]
				{
					"At night I make one full round: gate - storehouse - fields - forest edge - main house.",
					"Gotta keep a sharper watch at night, who knows what wild beast might be prowling about.",
				};
				henri.HomePos = FarmGatePos;
				henri.DayCheckpoints = new[] { FarmGatePos, forestEdgePos };
				henri.NightPatrolPoints = new[] { FarmGatePos, BarnPos2Vec(), fieldCenterPos, forestEdgePos, FarmhousePos };
				henri.RainHelpPos = toolAreaPos;
				_world.AddChild(henri);
			}, "BuildFarmStaff[Henri]");

			// ---- 5 FenceMarker: "cam bien" de Marcel biet cho nao can sua ----
			AddFenceMarker("Hàng rào ruộng", FarmGatePos);
			AddFenceMarker("Chuồng bò", CowPastureCenter);
			AddFenceMarker("Chuồng ngựa", HorseStableCenter);
			AddFenceMarker("Chuồng gà", ChickenCoopCenter);
			AddFenceMarker("Chuồng cừu heo", SheepPigPastureCenter);
		}

		private void AddFenceMarker(string name, Vector3 pos)
		{
			if (_fenceMarkerScene == null) return;
			var marker = _fenceMarkerScene.Instantiate<FenceMarker>();
			marker.FenceName = name;
			marker.Position = pos;
			_world.AddChild(marker);
		}

		// Doi Cam Ve bao ve nong trai (100 NPC, theo yeu cau) - 1 doanh trai rieng de nghi ngoi,
		// chia 2 CA DOI LAP HOAN TOAN (6h sang-6h toi / 6h toi-6h sang) nen LUON co dung 50 nguoi
		// dang tuan tra bat ke gio nao, 50 nguoi con lai dang ngu trong doanh trai. Vi 2 ca khong
		// bao gio o trong doanh trai CUNG LUC, chi can 1 bo 50 CHO NGU chung (10x5 luoi) - ca ngay
		// va ca dem dung LAI CUNG 50 vi tri do vao thoi diem khac nhau, khong can 100 cho rieng.
		private void BuildPalaceGuardBarracks()
		{
			if (_palaceGuardScene == null) return;

			var avoid = KnownOccupiedZonesExcluding(HousingZoneAnchor);
			var rng = new RandomNumberGenerator { Seed = 10700 };
			// Quy hoach lai: doanh trai Cam Ve gio thuoc Khu Nha O NPC (gom TAT CA NPC, ke ca linh
			// gac) - tim gan HousingZoneAnchor thay vi ngau nhien khap tuong da.
			var barracksPos = FindOpenSpot(avoid, 130f, rng, searchCenter: HousingZoneAnchor, searchRadius: 900f);
			_extraPenZones.Add((barracksPos, 130f));

			AddDecor(_farmhouseScene, barracksPos, 60f, 90f, FarmhouseFootprint);
			var interior = AddBuildingEntrance(barracksPos, 90f, 110f, 80f, RoomKind.Village);
			AddBuildingLabelZone(barracksPos, 130f, "label.palace_guard_barracks");

			string[] dialogueLow = { "Cấm Vệ Quân xin chào. Chúng tôi tuần tra khắp trang trại ngày đêm." };
			string[] dialogueMid = { "Trang trại được canh gác cẩn thận, anh cứ yên tâm làm việc." };
			string[] dialogueHigh = { "Có chuyện gì bất thường, anh cứ báo chúng tôi ngay." };
			string[] dialogueLowEn = { "Greetings, we're the Royal Guard. We patrol the whole farm, day and night." };
			string[] dialogueMidEn = { "The farm is being watched over carefully, work easy." };
			string[] dialogueHighEn = { "If anything's out of the ordinary, report it to us right away." };

			const int totalGuards = 100;
			const int dayShift = 50;
			var homeFront = barracksPos + new Vector3(0, 0, 90);

			for (int i = 0; i < totalGuards; i++)
			{
				int idx = i;
				SafeBuildStep(() =>
				{
					var guard = _palaceGuardScene.Instantiate<PalaceGuardNpc>();
					guard.NpcId = $"palace_guard_{idx}";
					guard.NpcName = $"{PickStaffName(idx)} (Cấm Vệ #{idx + 1})";
					guard.DialogueLow = dialogueLow;
					guard.DialogueMid = dialogueMid;
					guard.DialogueHigh = dialogueHigh;
					guard.DialogueLowEn = dialogueLowEn;
					guard.DialogueMidEn = dialogueMidEn;
					guard.DialogueHighEn = dialogueHighEn;
					bool isDayShift = idx < dayShift;
					guard.WorkStartHour = isDayShift ? 6 : 18;
					guard.WorkEndHour = isDayShift ? 18 : 6;
					guard.PatrolCenter = FarmWallCenter;
					guard.PatrolRadius = FarmWallHalfSize * 0.85f;
					guard.HomePos = homeFront;

					int slot = idx % dayShift; // 2 ca dung CHUNG 50 vi tri ngu (xem ghi chu tren)
					guard.InteriorHomePos = interior + new Vector3((slot % 10 - 4.5f) * 22f, 0, (slot / 10 - 2f) * 22f);
					_world.AddChild(guard);
				}, $"BuildPalaceGuardBarracks[{idx}]");
			}
		}

		// Vuon nho lon (~1.1 hecta, 1980 goc nho + 720 cot go) - dat o khu dat trong phia NAM
		// hang rao ruong (chua co gi khac o do), vi day la khu vuc RONG duy nhat con lai trong
		// pham vi tuong da 10 hecta khong dung cham voi bat ky cong trinh/khu vuc nao da xay
		// (nong trai, chuong trai, vuon nho nho/vuon cay cu, canh dong huong duong, duong lang).
		// Dung MultiMeshInstance3D (1 draw call cho toan bo hang ngan ban sao) THAY VI tao rieng
		// tung Node3D - dung theo yeu cau "khong spawn tat ca thanh Actor/GameObject rieng".
		private static readonly Vector3 BigVineyardCenter = new(-400, 0, 2132);
		private const int VineyardRows = 60;
		private const int VineyardVinesPerRow = 33;
		private const float VineyardRowSpacing = 45f;
		private const float VineyardVineSpacing = 50f;
		private const int VineyardPostsPerRow = 12;

		private void BuildBigVineyard()
		{
			AddBuildingLabelZone(BigVineyardCenter, 700f, "label.great_vineyard");
			var vineMesh = ExtractMesh(_grapesScene);
			var postMesh = new CylinderMesh { TopRadius = 2f, BottomRadius = 2.4f, Height = 26f };
			var postMat = GetCachedMaterial(new Color(0.3f, 0.2f, 0.1f), 1f);

			var rng = new RandomNumberGenerator { Seed = 9500 };
			float halfRowSpan = (VineyardRows - 1) * VineyardRowSpacing / 2f;
			float halfVineSpan = (VineyardVinesPerRow - 1) * VineyardVineSpacing / 2f;

			var vineTransforms = new List<Transform3D>(VineyardRows * VineyardVinesPerRow);
			var postTransforms = new List<Transform3D>(VineyardRows * VineyardPostsPerRow);

			for (int row = 0; row < VineyardRows; row++)
			{
				float z = row * VineyardRowSpacing - halfRowSpan;
				for (int v = 0; v < VineyardVinesPerRow; v++)
				{
					float x = v * VineyardVineSpacing - halfVineSpan;
					var pos = BigVineyardCenter + new Vector3(x, 16f, z);
					var basis = Basis.Identity.Scaled(Vector3.One * rng.RandfRange(4.5f, 6f))
						.Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
					vineTransforms.Add(new Transform3D(basis, pos));
				}
				for (int p = 0; p < VineyardPostsPerRow; p++)
				{
					float x = p * (VineyardVinesPerRow * VineyardVineSpacing / VineyardPostsPerRow) - halfVineSpan;
					var pos = BigVineyardCenter + new Vector3(x, 13f, z);
					postTransforms.Add(new Transform3D(Basis.Identity, pos));
				}
			}

			AddMultiMeshScatter(vineMesh, vineTransforms);
			AddMultiMeshScatter(postMesh, postTransforms, postMat);

			// Va cham dac 1 khoi hop bao quanh CA khu vuon nho (khong va cham tung goc/cot rieng
			// - MultiMesh khong ho tro va cham rieng le) de nguoi choi khong "lot" xuyen qua toan
			// bo khu vuon nhu khong co gi, nhung van co the di lai giua cac hang (chi chan RIA
			// ngoai cung).
			var edgeThickness = 20f;
			float halfX = halfVineSpan + 30f, halfZ = halfRowSpan + 20f;
			AddVineyardBoundaryWall(BigVineyardCenter + new Vector3(0, 0, -halfZ), new Vector3(halfX * 2, 24f, edgeThickness));
			AddVineyardBoundaryWall(BigVineyardCenter + new Vector3(0, 0, halfZ), new Vector3(halfX * 2, 24f, edgeThickness));
			AddVineyardBoundaryWall(BigVineyardCenter + new Vector3(-halfX, 0, 0), new Vector3(edgeThickness, 24f, halfZ * 2));
			AddVineyardBoundaryWall(BigVineyardCenter + new Vector3(halfX, 0, 0), new Vector3(edgeThickness, 24f, halfZ * 2));
		}

		private void AddVineyardBoundaryWall(Vector3 pos, Vector3 size)
		{
			var body = new StaticBody3D { Position = pos + Vector3.Up * (size.Y / 2f) };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
			_world.AddChild(body);
		}

		// Cay canh + cay ven duong xung quanh trang trai (80-120 cay lon, con lai qua
		// MultiMesh): rai ngau nhien trong pham vi tuong da 10 hecta, TRANH cac vung da xay
		// (nong trai/chuong trai/vuon nho/canh dong huong duong) qua danh sach vung tron loai
		// tru. Cay lon (so luong vua phai) van la Node3D rieng (can hinh dang/bong do khac
		// nhau ro net khi lai gan); bui/hoa/co (so luong RAT lon) dung MultiMesh - dung theo
		// yeu cau "khong spawn tat ca thanh Actor/GameObject rieng".
		private void BuildEstateLandscaping()
		{
			(Vector3 center, float radius)[] avoidZones =
			{
				(new Vector3(202, 0, 390), 750f),      // nong trai + ruong + nha kho + gieng/ao
				(LivestockZoneOrigin, 2100f),          // Khu Chan Nuoi
				(HousingZoneAnchor, 900f),             // Khu Nha O NPC
				(StorageZoneAnchor, 300f),              // Khu Nha Kho
				(CropsExtensionAnchor, 350f),           // Khu Trong Trot
				(OutbuildingsAnchor, 450f),             // Khu San Xuat
				(new Vector3(-2552, 0, 390), 420f),    // canh dong huong duong
				(BigVineyardCenter, 1650f),            // vuon nho lon moi
			};
			bool IsAvoided(Vector3 p)
			{
				foreach (var (c, r) in avoidZones)
					if (new Vector2(p.X - c.X, p.Z - c.Z).LengthSquared() < r * r) return true;
				return false;
			}

			float minX = FarmWallCenter.X - FarmWallHalfSize + 80f, maxX = FarmWallCenter.X + FarmWallHalfSize - 80f;
			float minZ = FarmWallCenter.Z - FarmWallHalfSize + 80f, maxZ = FarmWallCenter.Z + FarmWallHalfSize - 80f;
			var rng = new RandomNumberGenerator { Seed = 9600 };

			Vector3 RandomOpenSpot(RandomNumberGenerator r)
			{
				for (int tries = 0; tries < 20; tries++)
				{
					var p = new Vector3(r.RandfRange(minX, maxX), 0, r.RandfRange(minZ, maxZ));
					if (!IsAvoided(p)) return p;
				}
				return new Vector3(minX, 0, minZ); // du phong (hiem khi toi day)
			}

			// Cay lon (100 cay, tai su dung 2 model cay co san - maple/birch)
			for (int i = 0; i < 100; i++)
			{
				var pos = RandomOpenSpot(rng);
				var scene = rng.Randf() < 0.5f ? _treeScene : _treeScene2;
				if (scene == null) continue;
				var inst = scene.Instantiate<Node3D>();
				inst.Position = pos;
				inst.RotateY(rng.RandfRange(0f, Mathf.Tau));
				inst.Scale = Vector3.One * rng.RandfRange(34f, 42f);
				_world.AddChild(inst);
			}

			// Cay nho, bui, hoa, co - MultiMesh (khong tao Node/collision rieng cho tung cai)
			var smallTreeMesh = ExtractMesh(_treeScene2 ?? _treeScene);
			var bushMesh = ExtractMesh(_herbBushScene);
			var flowerMesh = ExtractMesh(_flowerScene);
			var grassMesh = ExtractMesh(_grassClumpScene);

			List<Transform3D> ScatterTransforms(int count, float minScale, float maxScale)
			{
				var list = new List<Transform3D>(count);
				for (int i = 0; i < count; i++)
				{
					var pos = RandomOpenSpot(rng);
					var basis = Basis.Identity.Scaled(Vector3.One * rng.RandfRange(minScale, maxScale))
						.Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
					list.Add(new Transform3D(basis, pos));
				}
				return list;
			}

			AddMultiMeshScatter(smallTreeMesh, ScatterTransforms(200, 14f, 22f));
			AddMultiMeshScatter(bushMesh, ScatterTransforms(400, 12f, 17f));
			AddMultiMeshScatter(flowerMesh, ScatterTransforms(250, 7f, 10f));
			AddMultiMeshScatter(grassMesh, ScatterTransforms(750, 10f, 15f));
		}

		// Cac cong trinh phu con lai theo bang yeu cau (nha phu, barn lon #2, kho lua, kho dung
		// cu, workshop, 2 nha kho nho, lo ren nho, nha may ep nho, ham ruou, 2 nha kinh, 3 choi
		// nghi, 2 nha ve sinh, gieng #2, 2 be nuoc). Da co san: Nha chinh(1)/Chuong ngua(1)/
		// Gieng(1)/Barn(1) tu truoc. PHAN LON la cong trinh CHUC NANG/trang tri KHONG can noi
		// that day du (tranh cong them hang chuc cap phong noi that nua len tren rat nhieu da
		// dung hom nay) - chi "Nha phu" va "Ham ruou" co phong that su. Dat trong dai dat trong
		// phia dong hang rao ruong (giua hang rao va cac cao nguyen), chua dung cham gi truoc do.
		private static readonly Vector3 OutbuildingsAnchor = new(850, 0, 280);

		private void BuildFarmOutbuildings()
		{
			var a = OutbuildingsAnchor; // = diem neo Khu San Xuat (gia tri khong doi)
			var s = StorageZoneAnchor;  // diem neo Khu Nha Kho (tach rieng khoi San Xuat)
			NpcEconomy.RestockPos = s;  // diem "nhap hang" cho GOAP cua NPC (xem NpcEconomy.cs)

			// Nha phu (co phong noi that that su) - nha o nho thu 2 canh nha chinh.
			var auxHousePos = a + new Vector3(-60, 0, -80);
			AddDecor(_smallBarnScene, auxHousePos, 12f, 0f, SmallBarnFootprint);
			AddBuildingEntrance(auxHousePos, 0f, 80f, 50f, RoomKind.Village);

			// ---- Khu Nha Kho (chua do): Barn lon #2 + kho lua + kho dung cu + 2 nha kho nho ----
			// Barn lon #2 - tai su dung model Barn, KHONG lam noi that 1000 mon nhu barn dau
			// tien (chi la kho chua ngoai, du 1 barn "sieu thi" la qua du roi).
			AddDecor(_barnScene, s + new Vector3(140, 0, -60), 22f, 90f, BarnFootprint);

			// Kho lua (grain silo) - thap tron cao, dung primitive (khong co model CC0 phu hop).
			AddSiloPrimitive(s + new Vector3(-160, 0, 20));

			AddDecor(_smallBarnScene, s + new Vector3(-20, 0, 100), 10f, 90f, SmallBarnFootprint);   // kho dung cu
			AddDecor(_smallBarnScene, s + new Vector3(160, 0, 80), 9f, 90f, SmallBarnFootprint);     // nha kho nho 1
			AddDecor(_smallBarnScene, s + new Vector3(-180, 0, -100), 9f, 90f, SmallBarnFootprint);  // nha kho nho 2
			AddBuildingLabelZone(s, 260f, "label.warehouse_district");

			// ---- Khu San Xuat (che bien): xuong nho + lo ren + nha kinh (Forge/greenhouse ben
			// duoi) - workshop giu tai day (khac Kho, day la noi GIA CONG chu khong phai CHUA DO).
			AddDecor(_smallBarnScene, a + new Vector3(60, 0, 90), 11f, 0f, SmallBarnFootprint);     // workshop
			AddBuildingLabelZone(a, 400f, "label.production_district");

			// Lo ren nho - khoi da/gach + ong khoi, dung primitive.
			AddForgePrimitive(a + new Vector3(140, 0, 130));

			// Nha may ep nho + ham ruou (co phong noi that - "hầm rượu" that su co the vao) - dat
			// gan vuon nho lon o phia nam.
			var wineryPos = BigVineyardCenter + new Vector3(0, 0, -1500);
			AddDecor(_smallBarnScene, wineryPos, 14f, 0f, SmallBarnFootprint);
			AddWinePressPrimitive(wineryPos + new Vector3(60, 0, 0));
			var cellarPos = wineryPos + new Vector3(-140, 0, 0);
			AddDecor(_smallBarnScene, cellarPos, 12f, 0f, SmallBarnFootprint);
			var cellarInterior = AddBuildingEntrance(cellarPos, 0f, 80f, 50f, RoomKind.Village);
			// Chat vai thung ruou trong ham (theo dung y "ham ruou chua thung ruou").
			for (int i = 0; i < 6; i++)
				AddDecor(_barrelScene, cellarInterior + new Vector3((i % 3 - 1) * 40f, 0, (i / 3) * 40f - 20f), 90f, i * 37f);

			// Nha kinh x2 - khung + kinh mau xanh nhat trong suot, dung primitive (khong co model
			// CC0 phu hop).
			AddGreenhousePrimitive(a + new Vector3(-260, 0, 100));
			AddGreenhousePrimitive(a + new Vector3(-260, 0, 200));

			// Choi nghi x3 - 4 cot + mai, dung primitive don gian.
			AddRestHutPrimitive(a + new Vector3(300, 0, -40));
			AddRestHutPrimitive(a + new Vector3(-320, 0, -20));
			AddRestHutPrimitive(BigVineyardCenter + new Vector3(0, 0, -300));

			// Nha ve sinh x2 - khoi hop nho don gian.
			AddOuthousePrimitive(a + new Vector3(300, 0, 80));
			AddOuthousePrimitive(a + new Vector3(-60, 0, 180));

			// Gieng #2 (da co 1 gan nha chinh).
			if (_wellScene != null)
			{
				var well2 = _wellScene.Instantiate<Node3D>();
				well2.Position = a + new Vector3(180, 0, -10);
				well2.Scale = Vector3.One * 26f;
				_world.AddChild(well2);
			}

			// Be nuoc x2 - tru tru kim loai don gian, dung primitive.
			AddWaterTankPrimitive(a + new Vector3(100, 0, 190));
			AddWaterTankPrimitive(a + new Vector3(-140, 0, -110));
		}

		private void AddSiloPrimitive(Vector3 pos)
		{
			var mat = GetCachedMaterial(new Color(0.75f, 0.72f, 0.68f), 0.6f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 30f, BottomRadius = 30f, Height = 110f },
				Position = pos + Vector3.Up * 55f,
				MaterialOverride = mat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 30f, Height = 30f },
				Position = pos + Vector3.Up * 110f,
				MaterialOverride = mat
			});
			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 30f, Height = 110f }, Position = Vector3.Up * 55f });
			_world.AddChild(body);
		}

		private void AddForgePrimitive(Vector3 pos)
		{
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(46f, 34f, 40f) },
				Position = pos + Vector3.Up * 17f,
				MaterialOverride = GetCachedMaterial(new Color(0.35f, 0.32f, 0.3f), 1f)
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 6f, BottomRadius = 8f, Height = 50f },
				Position = pos + Vector3.Up * 60f + Vector3.Back * 12f,
				MaterialOverride = GetCachedMaterial(new Color(0.3f, 0.15f, 0.1f), 1f)
			});
			var body = new StaticBody3D { Position = pos + Vector3.Up * 17f };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(46f, 34f, 40f) } });
			_world.AddChild(body);
		}

		private void AddWinePressPrimitive(Vector3 pos)
		{
			var woodMat = GetCachedMaterial(new Color(0.42f, 0.28f, 0.15f), 0.9f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 24f, BottomRadius = 26f, Height = 20f },
				Position = pos + Vector3.Up * 10f,
				MaterialOverride = woodMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 4f, BottomRadius = 4f, Height = 40f },
				Position = pos + Vector3.Up * 40f,
				MaterialOverride = woodMat
			});
			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 26f, Height = 20f }, Position = Vector3.Up * 10f });
			_world.AddChild(body);
		}

		private void AddGreenhousePrimitive(Vector3 pos)
		{
			var glassMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.7f, 0.9f, 0.85f, 0.35f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				Roughness = 0.1f
			};
			var frameMat = GetCachedMaterial(new Color(0.9f, 0.9f, 0.9f), 0.5f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(70f, 40f, 90f) },
				Position = pos + Vector3.Up * 20f,
				MaterialOverride = glassMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(70f, 20f, 90f) },
				Position = pos + Vector3.Up * 50f,
				MaterialOverride = glassMat
			});
			for (int i = -1; i <= 1; i++)
			{
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = new Vector3(2f, 60f, 2f) },
					Position = pos + new Vector3(i * 35f, 30f, 45f),
					MaterialOverride = frameMat
				});
			}
			var body = new StaticBody3D { Position = pos + Vector3.Up * 20f };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(70f, 40f, 90f) } });
			_world.AddChild(body);
		}

		private void AddRestHutPrimitive(Vector3 pos)
		{
			var postMat = GetCachedMaterial(new Color(0.35f, 0.24f, 0.14f), 0.9f);
			var roofMat = GetCachedMaterial(new Color(0.45f, 0.22f, 0.15f), 0.9f);
			foreach (var offset in new[] { new Vector3(-20, 0, -20), new Vector3(20, 0, -20), new Vector3(-20, 0, 20), new Vector3(20, 0, 20) })
			{
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new CylinderMesh { TopRadius = 3f, BottomRadius = 3.5f, Height = 34f },
					Position = pos + offset + Vector3.Up * 17f,
					MaterialOverride = postMat
				});
			}
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(56f, 16f, 56f) },
				Position = pos + Vector3.Up * 42f,
				MaterialOverride = roofMat
			});
			if (_benchScene != null) AddDecor(_benchScene, pos, 20f, 0f);
		}

		private void AddOuthousePrimitive(Vector3 pos)
		{
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(22f, 30f, 22f) },
				Position = pos + Vector3.Up * 15f,
				MaterialOverride = GetCachedMaterial(new Color(0.55f, 0.4f, 0.25f), 0.9f)
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(26f, 8f, 26f) },
				Position = pos + Vector3.Up * 34f,
				MaterialOverride = GetCachedMaterial(new Color(0.35f, 0.2f, 0.15f), 0.9f)
			});
			var body = new StaticBody3D { Position = pos + Vector3.Up * 15f };
			body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(22f, 30f, 22f) } });
			_world.AddChild(body);
		}

		private void AddWaterTankPrimitive(Vector3 pos)
		{
			var mat = GetCachedMaterial(new Color(0.55f, 0.58f, 0.6f), 0.4f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 22f, BottomRadius = 22f, Height = 60f },
				Position = pos + Vector3.Up * 30f,
				MaterialOverride = mat
			});
			foreach (var offset in new[] { new Vector3(-18, 0, -18), new Vector3(18, 0, -18), new Vector3(-18, 0, 18), new Vector3(18, 0, 18) })
			{
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new CylinderMesh { TopRadius = 2f, BottomRadius = 2f, Height = 20f },
					Position = pos + offset + Vector3.Up * 10f,
					MaterialOverride = GetCachedMaterial(new Color(0.3f, 0.3f, 0.3f), 0.7f)
				});
			}
			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 22f, Height = 60f }, Position = Vector3.Up * 30f });
			_world.AddChild(body);
		}

		// Tim 1 vi tri TRONG (khong dung cham vung nao da dung) trong pham vi tuong da 10 hecta -
		// dung cho viec them nhieu chuong moi ma khong the dat toa do tay chinh xac an toan (ban
		// do da qua chat). "avoid" LON DAN theo tung lan goi thanh cong (tham so ByRef qua List)
		// de cac chuong MOI cung khong dung cham LAN NHAU.
		// boundsFraction: gioi han vung tim kiem con (FarmWallHalfSize * boundsFraction) thay vi
		// dung toan bo pham vi tuong (mac dinh 1f = nhu truoc). Cac ham dat CHUONG DONG VAT truyen
		// gia tri nho hon (vd 0.6f) de CHUONG LUON nam GAN TRUNG TAM nong trai, khong bao gio ra
		// sat tuong nua - theo dung yeu cau "sap xep cac chuong gan tuong lai gan trung tam".
		// searchCenter/searchRadius: gioi han vung tim quanh 1 TAM RIENG (vd tam 1 khu vuc quy
		// hoach) thay vi tam tuong da mac dinh - dung de cac chuong/nha "thuoc ve" 1 khu vuc (vd
		// Khu Chan Nuoi/Khu Nha O) LUON duoc dat GAN diem neo cua khu do, khong bao gio bi
		// FindOpenSpot day ra 1 noi khac hoan toan trong tuong (xem cac Khu vuc trong
		// BuildAnimalPenDistrict/BuildHousingDistrict).
		private Vector3 FindOpenSpot(List<(Vector3 c, float r)> avoid, float neededRadius, RandomNumberGenerator rng,
			float boundsFraction = 1f, Vector3? searchCenter = null, float? searchRadius = null)
		{
			float margin = neededRadius + 60f;
			var center = searchCenter ?? FarmWallCenter;
			float effectiveHalf = searchRadius ?? (FarmWallHalfSize * boundsFraction);
			float minX = center.X - effectiveHalf + margin, maxX = center.X + effectiveHalf - margin;
			float minZ = center.Z - effectiveHalf + margin, maxZ = center.Z + effectiveHalf - margin;

			// KEP CUNG trong pham vi tuong da trang trai - mot searchCenter/searchRadius tuy y
			// (vd LivestockZoneOrigin + 2100) co the vuon RA NGOAI tuong neu diem neo qua gan mep
			// tuong (khoang cach toi mep gan nhat < searchRadius yeu cau), vi cong thuc tren CHI
			// biet ve searchCenter/searchRadius rieng, khong he biet gi ve bien tuong that su - da
			// tung khien Khu Chan Nuoi dat vai chuong (vd Chuong Ngua) LO RA NGOAI tuong ~590 don
			// vi. Giao (intersect) voi bien trong cua tuong dam bao KHONG BAO GIO co ket qua nam
			// ngoai tuong, du searchCenter/searchRadius truyen vao la gi.
			float wallMinX = FarmWallCenter.X - FarmWallHalfSize + margin, wallMaxX = FarmWallCenter.X + FarmWallHalfSize - margin;
			float wallMinZ = FarmWallCenter.Z - FarmWallHalfSize + margin, wallMaxZ = FarmWallCenter.Z + FarmWallHalfSize - margin;
			minX = Mathf.Max(minX, wallMinX); maxX = Mathf.Min(maxX, wallMaxX);
			minZ = Mathf.Max(minZ, wallMinZ); maxZ = Mathf.Min(maxZ, wallMaxZ);

			// Theo doi UNG VIEN TOT NHAT tung thay (it cham vao vung khac nhat) trong luc thu,
			// de dung lam du phong NEU khong co diem nao hoan toan trong sau 600 lan - truoc day
			// du phong la 1 GOC CO DINH (minX, minZ, gan sat tuong) KHONG HE kiem tra avoid, nen
			// khi nhieu lan goi FindOpenSpot LIEN TIEP deu roi vao truong hop nay (ban do cang
			// ngay cang chat - da co toi ~40 chuong/cong trinh dung FindOpenSpot), tat ca deu bi
			// day ve DUNG 1 GOC gan tuong giong het nhau, chong chat len nhau - day rat co the la
			// nguyen nhan that su cua bao cao "chuong gan tuong khong co dong vat/NPC".
			Vector3 bestPos = new(minX, 0, minZ);
			float bestClearance = float.NegativeInfinity;

			for (int tries = 0; tries < 3000; tries++)
			{
				var p = new Vector3(rng.RandfRange(minX, maxX), 0, rng.RandfRange(minZ, maxZ));
				float minClearance = float.PositiveInfinity;
				foreach (var (c, r) in avoid)
				{
					float clearance = new Vector2(p.X - c.X, p.Z - c.Z).Length() - (r + neededRadius);
					if (clearance < minClearance) minClearance = clearance;
				}
				if (minClearance >= 0f) return p; // hoan toan trong - dung ngay, khong can thu them
				if (minClearance > bestClearance) { bestClearance = minClearance; bestPos = p; }
			}
			// Khong tim duoc diem hoan toan trong sau 3000 lan - dung UNG VIEN IT CHAM NHAT da
			// thay (van co the hoi cham nhe vao 1 vung nao do, nhung KHONG con la 1 goc co dinh
			// co the trung lap voi nhieu lan goi khac). "cham" o day la cham vao VUNG DEM du phong
			// (neededRadius thuong da duoc goi PADDING san, vd zoneR=half*1.5 o BuildAnimalPenDistrict/
			// BuildGoatPen) - da xac nhan bang tinh toan: voi he so padding 1.5x, "do cham" phai
			// vuot qua 1/3 tong 2 ban kinh moi that su khien 2 hang rao/mo hinh THAT cham nhau; cac
			// truong hop do cham nho (nhu da gap trong Khu Chan Nuoi) chi la vung dem hoi bi eo hep,
			// KHONG phai 2 cong trinh chong len nhau that su. Dung GD.Print (khong phai PushWarning)
			// de khong hien nhu 1 loi/canh bao that trong Debugger cua Godot khi day chi la truong
			// hop du phong da luong truoc, van an toan ve mat hinh anh.
			if (bestClearance < 0f)
				GD.Print($"FindOpenSpot: khong tim duoc cho hoan toan trong sau 3000 lan (neededRadius={neededRadius}, do cham vung dem={-bestClearance:F0}), dung ung vien it cham nhat tai {bestPos}.");
			return bestPos;
		}

		// Tim 1 vi tri nha o CA NHAN (SmallBarn nho, ban kinh 90) GAN HousingZoneAnchor - dung
		// chung boi 5 NPC co ten rieng (Etienne/Baptiste/Severin/Theodore/Augustin, xem
		// BuildCowherd/BuildStablehand/BuildPoultryKeeper/BuildFarmWorker/BuildEstateWorker) de
		// nha cua ho nam trong Khu Nha O thay vi rai rac canh tung chuong/khu vuc lam viec nhu
		// truoc (quy hoach lai theo yeu cau "nha o npc quy hoach 1 cho rieng").
		private Vector3 NextHousingCottagePos(int seed)
		{
			var avoid = KnownOccupiedZonesExcluding(HousingZoneAnchor);
			var rng = new RandomNumberGenerator { Seed = (ulong)seed };
			var pos = FindOpenSpot(avoid, 90f, rng, searchCenter: HousingZoneAnchor, searchRadius: 900f);
			_extraPenZones.Add((pos, 90f));
			return pos;
		}

		// Vi tri+ban kinh TAT CA cac "chuong them" da dat qua NHIEU LAN goi (BuildExtraPastures/
		// BuildExtraSheepPens/BuildExtraCowPensRound2/...) - PHAI dung 1 danh sach CHUNG cap
		// class nay (khong phai bien cuc bo rieng tung ham) de CAC LAN GOI SAU biet ve chuong
		// CAC LAN GOI TRUOC da dat, tranh chuong moi de len/chong lan chuong cu (day chinh la
		// nguyen nhan cac lan bao cao "chuong van con trong" truoc do - 2 chuong chong len nhau
		// khien nguoi choi dung trong 1 chuong RONG ma tuong nham la chuong da co vat nuoi).
		private readonly List<(Vector3 c, float r)> _extraPenZones = new();

		// Vi tri THAT SU (tinh ngau nhien qua FindOpenSpot luc chay) cua khu chuong trai tap
		// trung va doanh trai nguoi cham soc - luu lai de BuildFarmPaths (chay SAU) noi duong tu
		// day toi cac khu vuc khac, vi 2 vi tri nay KHONG PHAI hang so co dinh nhu cac khu cu.
		private Vector3 _animalDistrictOrigin;
		private Vector3 _caretakerDormPos;

		// Vung da dung san TRONG pham vi tuong da 10 hecta (danh sach tron bao gom moi khu da
		// xay tu truoc den gio) - dung lam "avoid" ban dau cho FindOpenSpot khi them chuong moi.
		// LUON cong them _extraPenZones (cac chuong them da dat o CAC LAN GOI TRUOC).
		private List<(Vector3 c, float r)> KnownOccupiedZones()
		{
			var zones = new List<(Vector3 c, float r)>
			{
				(new Vector3(202, 0, 390), 780f),          // nong trai + ruong + nha kho + gieng/ao
				(LivestockZoneOrigin, 2100f),              // Khu Chan Nuoi (28 chuong + 4 chuong cu + De)
				(HousingZoneAnchor, 900f),                 // Khu Nha O NPC (gom tat ca NPC)
				(StorageZoneAnchor, 300f),                 // Khu Nha Kho
				(CropsExtensionAnchor, 350f),              // Khu Trong Trot (vuon cay + vuon nho nho)
				(new Vector3(-2552, 0, 390), 460f),        // canh dong huong duong
				(BigVineyardCenter, 1650f),                // vuon nho lon
				(OutbuildingsAnchor, 450f),                // Khu San Xuat (tang tu 320f de bao ca coi xay gio)
				(new Vector3(1600, 0, -350), 720f),        // cao nguyen 1
				(new Vector3(2650, 0, 750), 660f),         // cao nguyen 2
				(new Vector3(1750, 0, 1950), 780f),        // cao nguyen 3
			};
			zones.AddRange(_extraPenZones);
			return zones;
		}

		// Nhu KnownOccupiedZones() nhung LOAI BO zone co tam == excludeCenter - BAT BUOC dung
		// thay vi goi KnownOccupiedZones() truc tiep khi mot he thong dat vi tri BEN TRONG chinh
		// vung dat rieng cua no (vd BuildAnimalPenDistrict dat chuong GAN LivestockZoneOrigin,
		// trong khi KnownOccupiedZones() cung co san entry (LivestockZoneOrigin, 2100f) - entry
		// do CHI danh cho HE THONG KHAC biet ma tranh xa ca khu). Neu khong loai bo, MOI vi tri
		// ung vien deu tu bi tinh la "cham" chinh vung dat rieng cua minh (khoang cach toi tam
		// luon nho hon ban kinh du phong+can thiet, khong bao gio thoat duoc ve mat toan hoc),
		// khien FindOpenSpot luon that bai sau 600 lan thu va phai dung ung vien du phong chat
		// luong kem (xem canh bao "khong tim duoc cho hoan toan trong sau 600 lan").
		private List<(Vector3 c, float r)> KnownOccupiedZonesExcluding(Vector3 excludeCenter)
		{
			var zones = new List<(Vector3 c, float r)>();
			foreach (var z in KnownOccupiedZones())
				if (z.c != excludeCenter) zones.Add(z);
			return zones;
		}

		// Mang luoi duong mon noi CAC KHU VUC CHINH cua nong trai voi nhau (theo yeu cau "co
		// duong di cho NPC va player di xung quanh trang trai") - dung AddPath (duong dat mon
		// don gian, KHONG dung AddRoad vi ham do gan them 1 cai cau go moi doan, hop cho duong
		// lang chinh nhung sai neu rai khap noi trong nong trai). PHAI chay SAU
		// BuildAnimalPenDistrict/BuildPenCaretakerDorm (can _animalDistrictOrigin/
		// _caretakerDormPos da duoc tinh xong - 2 vi tri nay chi xac dinh luc chay, khong phai
		// hang so co dinh nhu cac khu cu).
		// Quy hoach lai thanh 5 khu vuc: mang duong gio noi CA 5 diem neo khu vuc (Khu Chan
		// Nuoi/Khu Nha O/Khu Trong Trot/Khu Nha Kho/Khu San Xuat) toi loi (nha chinh/Barn/ruong),
		// thay vi chi 8 doan rai rac cu (truoc day KHONG toi duoc FarmWorkerHousePos/
		// EstateWorkerHousePos/2 doanh trai cong nhan/BigVineyard/doanh trai Cam Ve/chuong De/
		// coi xay gio/OutbuildingsAnchor).
		private void BuildFarmPaths()
		{
			const float pathWidth = 55f;
			var fieldCenter = FarmOrigin + new Vector3((FarmGridW - 1) * FarmSpacing / 2f, 0, (FarmGridH - 1) * FarmSpacing / 2f);
			var barnPos = BarnPos2Vec();

			// Cum nha chinh <-> cong ruong <-> Barn.
			AddPath(FarmhousePos, FarmGatePos, pathWidth);
			AddPath(FarmhousePos, barnPos, pathWidth);
			AddPath(barnPos, fieldCenter, pathWidth);

			// Barn <-> Khu Nha Kho <-> Khu San Xuat (day Dong).
			AddPath(barnPos, StorageZoneAnchor, pathWidth);
			AddPath(StorageZoneAnchor, OutbuildingsAnchor, pathWidth);

			// Ruong <-> Khu Trong Trot mo rong <-> vuon nho lon (day Nam).
			AddPath(fieldCenter, CropsExtensionAnchor, pathWidth);
			AddPath(CropsExtensionAnchor, BigVineyardCenter, pathWidth);
			AddPath(OutbuildingsAnchor, CropsExtensionAnchor, pathWidth); // noi xuong ruou/nha kinh gan do

			// Barn <-> Khu Chan Nuoi (28 chuong + 4 chuong cu + De, day Tay) <-> doanh trai nguoi
			// cham soc (van dung _animalDistrictOrigin/_caretakerDormPos - vi tri THAT SU sau khi
			// BuildAnimalPenDistrict chay xong, gio la LivestockZoneOrigin + vi tri doanh trai).
			AddPath(barnPos, _animalDistrictOrigin, pathWidth);
			AddPath(_animalDistrictOrigin, _caretakerDormPos, pathWidth);

			// Khu Chan Nuoi <-> Khu Nha O NPC (day Bac) <-> Barn (duong tat truc tiep).
			AddPath(_animalDistrictOrigin, HousingZoneAnchor, pathWidth);
			AddPath(barnPos, HousingZoneAnchor, pathWidth);
		}

		// Navmesh THAT SU cho toan bo pham vi tuong da (NavigationRegion3D + NavigationMesh cua
		// Godot) - thay vi chi "di thang toi muc tieu roi lach neu bi ket" (SteeringUtil.
		// StuckDetector, van con giu lam luoi an toan du phong), NPC dung NavigationAgent3D se
		// TU TINH DUONG DI THAT SU vong qua hang rao/nha/thap canh/coi xay gio (xem
		// SteeringUtil.NavSteering + cach dung trong FarmhandNpc/StablehandNpc/PoultryKeeperNpc/
		// GuardNpc/FarmStewardNpc/PalaceGuardNpc).
		//
		// GeometryParsedGeometryType=StaticColliders: Godot TU DONG do toan bo StaticBody3D +
		// CollisionShape3D da co san trong pham vi FilterBakingAabb (khong can danh dau tay tung
		// vat can) de "khoet" thanh vung khong di duoc.
		//
		// AgentRadius=15 (NPC capsule radius that su = 12, chua them 3 don vi du phong) - PHAI
		// NHO HON nua chieu rong khe ho cong chuong (44 don vi, xem BuildSimplePasture) de NPC
		// van di qua cong duoc, khong bi ket tai ngay lo vao.
		//
		// CellSize=12 (kha nho so voi ty le nay - hang rao/cong chi rong ~44-440 don vi) de
		// navmesh nhan dung cac khe cong hep, doi lai bake se mat vai giay luc khoi dong (chap
		// nhan duoc, chi bake 1 LAN duy nhat luc _Ready(), dong bo/blocking de dam bao navmesh da
		// san sang truoc khi bat ky NPC nao bat dau di chuyen).
		private void BuildFarmNavigation()
		{
			// QUAN TRONG: map dieu huong MAC DINH cua the gioi dung CellSize/CellHeight rieng
			// (Godot mac dinh 0.25/0.25, qua nho so voi ty le the gioi nay) - phai KHOP voi
			// CellSize/CellHeight cua NavigationMesh ben duoi (12/8), neu khong Godot bao loi
			// "cell_size cua region khong khop cell_size cua map" va navmesh KHONG hoat dong
			// dung (NPC di xuyen tuong). Phai dat TRUOC khi tao/gan NavigationMesh cho region.
			var navMap = GetWorld3D().NavigationMap;
			NavigationServer3D.MapSetCellSize(navMap, 12f);
			NavigationServer3D.MapSetCellHeight(navMap, 8f);

			var navMesh = new NavigationMesh
			{
				GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
				AgentRadius = 15f,
				AgentHeight = 45f,
				AgentMaxClimb = 20f,
				AgentMaxSlope = 50f,
				CellSize = 12f,
				CellHeight = 8f,
				FilterBakingAabb = new Aabb(
					new Vector3(FarmWallCenter.X - FarmWallHalfSize - 150f, -100f, FarmWallCenter.Z - FarmWallHalfSize - 150f),
					new Vector3(FarmWallHalfSize * 2f + 300f, 300f, FarmWallHalfSize * 2f + 300f)),
			};

			var region = new NavigationRegion3D { NavigationMesh = navMesh };
			_world.AddChild(region);
			// onThread:false - bake DONG BO ngay tai day, dam bao navmesh san sang TRUOC KHI
			// frame dau tien cua bat ky NPC nao chay (moi buoc dung san khac trong _Ready() cung
			// dang chay dong bo nhu vay, nhat quan voi phan con lai).
			region.BakeNavigationMesh(false);
		}

		// QUY HOACH LAI theo yeu cau: TRUOC DAY 28 chuong ve tinh nam rai rac trong 6 ham rieng
		// (moi ham tu FindOpenSpot ngau nhien mot minh), nha o cua NPC cham soc dat NGAY SAT tung
		// chuong. GIO GOP LAI thanh 1 khu quy hoach DUY NHAT:
		//   - Ca 28 chuong xep THANG HANG theo 1 LUOI (7 cot x 4 hang, cung 1 khoang cach deu
		//     nhau) trong CUNG 1 vung rieng - "chuong dong vat quy hoach 1 cho rieng".
		//   - Nha o cua TAT CA 28 NPC cham soc GOM VE 1 DOANH TRAI DUY NHAT, TACH HAN khoi khu
		//     chuong (xem BuildPenCaretakerDorm ben duoi) - "nha o npc quy hoach 1 cho rieng". NPC
		//     di lam moi ngay tu doanh trai toi dung chuong minh phu trach (dung lai WorkPos/
		//     HomePos da co san trong FarmhandNpc/StablehandNpc/PoultryKeeperNpc).
		// Neu 1 o luoi vo tinh trung vao 1 vung da chiem (hiem, vi luoi nam trong 1 khu da duoc
		// FindOpenSpot chon rieng truoc), tu dong roi sang tim cho gan nhat con trong thay vi bo
		// qua - dam bao KHONG BAO GIO mat mot chuong nao ca.
		private void BuildAnimalPenDistrict()
		{
			// Loai bo tu-tham-chieu (xem KnownOccupiedZonesExcluding) - khu nay dat chuong GAN
			// LivestockZoneOrigin, khong the tranh chinh vung dat rieng cua no.
			var avoid = KnownOccupiedZonesExcluding(LivestockZoneOrigin);
			var rng = new RandomNumberGenerator { Seed = 11000 };

			// (tag, half, cow, sheep, pig, horse, chicken) - gop CHINH XAC so luong/loai vat nuoi
			// tu 6 ham cu (khong doi tong dan so vat nuoi, chi doi CACH SAP XEP vi tri).
			var specs = new (string tag, float half, int cow, int sheep, int pig, int horse, int chicken)[]
			{
				("bo_1", 168f, 16,0,0,0,0), ("bo_2", 168f, 16,0,0,0,0), ("bo_3", 168f, 16,0,0,0,0),
				("ngua_1", 168f, 0,0,0,14,0), ("ngua_2", 168f, 0,0,0,14,0), ("ngua_3", 168f, 0,0,0,14,0),
				("ga_1", 144f, 0,0,0,0,20), ("ga_2", 144f, 0,0,0,0,20), ("ga_3", 144f, 0,0,0,0,20),
				("cuuheo_1", 156f, 0,12,8,0,0), ("cuuheo_2", 156f, 0,12,8,0,0), ("cuuheo_3", 156f, 0,12,8,0,0),
				("cuurieng_1", 140f, 0,20,0,0,0), ("cuurieng_2", 140f, 0,20,0,0,0),
				("bo2_1", 150f, 16,0,0,0,0), ("bo2_2", 150f, 16,0,0,0,0), ("bo2_3", 150f, 16,0,0,0,0),
				("bosua_1", 150f, 16,0,0,0,0), ("bosua_2", 150f, 16,0,0,0,0), ("bosua_3", 150f, 16,0,0,0,0), ("bosua_4", 150f, 16,0,0,0,0),
				("cuumoi_1", 140f, 0,8,0,0,0), ("cuumoi_2", 140f, 0,8,0,0,0), ("cuumoi_3", 140f, 0,8,0,0,0),
				("vanhbo", 150f, 16,0,0,0,0), ("vanhcuu", 140f, 0,18,0,0,0), ("vanhngua", 150f, 0,0,0,14,0), ("vanhga", 140f, 0,0,0,0,22),
			};

			const int cols = 7;
			// 450 TRUOC DAY qua nho: hang 0 co 6 chuong LIEN TIEP ban kinh 252 (bo_1..3/ngua_1..3,
			// half=168*1.5) - 2 chuong 252 canh nhau can cach nhau >= 504 moi khong tu cham nhau,
			// 450 < 504 nen CHINH LUOI (khong phai ngoai canh) da tu gay cham lien tuc suot hang
			// do, don avoid[] day nhanh khien FindOpenSpot fallback het cho trong ban kinh 2100
			// (xem canh bao "khong tim duoc cho hoan toan trong sau 600 lan"). 520 dam bao 2
			// chuong 252 canh nhau luon co it nhat 16 don vi ho, ma goc luoi van nam trong ban
			// kinh 2100 dang danh rieng cho khu nay (khong can doi gi khac).
			const float cellSpacing = 520f;
			// Quy hoach lai: diem neo Khu Chan Nuoi gio la 1 HANG SO CO DINH (LivestockZoneOrigin)
			// thay vi FindOpenSpot ngau nhien - de toan bo khu (28 chuong + 4 chuong cu gop vao +
			// chuong De) luon nam DUNG cho trong ban do, khong doi vi tri moi lan chay.
			var districtOrigin = LivestockZoneOrigin;
			_animalDistrictOrigin = districtOrigin;
			// Giu truoc 1 vung uoc luong cho CA khu (ban kinh tho, dam bao khu nha o NPC dat sau
			// nay khong de len tren luoi chuong) - CHI them vao _extraPenZones (danh cho CAC HE
			// THONG KHAC doc qua KnownOccupiedZones() sau nay), TUYET DOI KHONG them vao avoid[]
			// cuc bo o day: avoid[] con duoc dung de dat CHINH cac chuong BEN TRONG vung nay, neu
			// them ca vao day se tai lap dung y het loi tu-tham-chieu vua sua o KnownOccupiedZonesExcluding
			// (moi chuong lai tu coi minh la "cham" chinh vung dat rieng cua minh).
			_extraPenZones.Add((districtOrigin, 2100f));

			// Chan cay/da hoang da (WorldStreamer) khoi moc chen vao chuong trong CA khu chan
			// nuoi - vung nay nam GAN RIA vung loai tru chung quanh tuong da (FarmWallHalfSize*1.45),
			// vai chuong (vd Chuong Ngua) co the tho ra NGOAI vung loai tru chung do (da do dac
			// kiem chung: canh xa nhat cua 1 chuong tung vuot ra ngoai ~590 don vi). Dang ky rieng
			// 1 vung loai tru DU LON cho CA khu (2100 = ban kinh tim cho + du phong cho chuong lon
			// nhat + bien do vuong cua FindOpenSpot) de dam bao KHONG chuong nao con bi cay/da moc
			// chen vao, du no nam o goc/ria nao cua khu.
			WorldStreamer.ExclusionZones.Add((districtOrigin, 2900f));

			var built = new List<(Vector3 pos, (string tag, float half, int cow, int sheep, int pig, int horse, int chicken) spec)>();

			for (int i = 0; i < specs.Length; i++)
			{
				int idx = i;
				SafeBuildStep(() =>
				{
					var spec = specs[idx];
					int col = idx % cols, row = idx / cols;
					var gridPos = districtOrigin + new Vector3((col - (cols - 1) / 2f) * cellSpacing, 0, (row - 1.5f) * cellSpacing);

					float zoneR = spec.half * 1.5f;
					// O luoi PHAI nam TRON trong tuong da (khong chi khong-cham avoid[]) - luoi 7
					// cot rong toi ~3120 don vi tinh tu districtOrigin, co the vuon ra ngoai tuong
					// o cot ngoai cung neu districtOrigin khong du xa tam tuong; kiem tra rieng,
					// neu vuon ra ngoai thi coi nhu "khong free" de roi sang FindOpenSpot (ham nay
					// da tu kep trong tuong).
					bool gridSlotFree = Mathf.Abs(gridPos.X - FarmWallCenter.X) + zoneR <= FarmWallHalfSize
						&& Mathf.Abs(gridPos.Z - FarmWallCenter.Z) + zoneR <= FarmWallHalfSize;
					if (gridSlotFree)
						foreach (var (c, r) in avoid)
							if (new Vector2(gridPos.X - c.X, gridPos.Z - c.Z).Length() < r + zoneR) { gridSlotFree = false; break; }
					var pos = gridSlotFree ? gridPos : FindOpenSpot(avoid, zoneR, rng, searchCenter: districtOrigin, searchRadius: 2100f);

					avoid.Add((pos, zoneR));
					_extraPenZones.Add((pos, zoneR));
					built.Add((pos, spec));

					BuildSimplePasture(pos, spec.half, spec.tag, (c, half) =>
					{
						for (int k = 0; k < spec.cow; k++)
						{
							float angle = Mathf.Tau * k / spec.cow;
							float radius = rng.RandfRange(30f, half - 35f);
							SpawnCow(c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), isAdult: true, homeCenterOverride: c, pastureHalfOverride: half);
						}
						for (int k = 0; k < spec.sheep; k++)
						{
							float angle = Mathf.Tau * k / spec.sheep;
							float radius = rng.RandfRange(30f, half - 35f);
							var sheep = _sheepScene.Instantiate<Sheep>();
							sheep.Position = c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
							sheep.TroughPosition = c; sheep.HomeCenter = c; sheep.PastureHalfExtent = half - 35f;
							_world.AddChild(sheep);
						}
						for (int k = 0; k < spec.pig; k++)
						{
							float angle = Mathf.Tau * k / spec.pig + 0.4f;
							float radius = rng.RandfRange(20f, half - 45f);
							var pig = _pigScene.Instantiate<Pig>();
							pig.Position = c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
							pig.TroughPosition = c; pig.HomeCenter = c; pig.PastureHalfExtent = half - 35f;
							_world.AddChild(pig);
						}
						for (int k = 0; k < spec.horse; k++)
						{
							float angle = Mathf.Tau * k / spec.horse;
							float radius = rng.RandfRange(30f, half - 35f);
							var horse = _horseScene.Instantiate<Horse>();
							horse.Position = c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
							horse.TroughPosition = c; horse.HomeCenter = c; horse.PastureHalfExtent = half - 35f;
							_world.AddChild(horse);
						}
						for (int k = 0; k < spec.chicken; k++)
						{
							float angle = Mathf.Tau * k / spec.chicken;
							float radius = rng.RandfRange(20f, half - 25f);
							SpawnChicken(c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), c, homeCenterOverride: c, pastureHalfOverride: half);
						}
					});
				}, $"BuildAnimalPenDistrict[{specs[idx].tag}]");
			}

			// Gop 4 chuong "cu" (truoc day rai rac o vi tri rieng, moi NPC dat TEN o canh chuong
			// cua minh) vao CHUNG Khu Chan Nuoi - chi doi VI TRI (tim gan districtOrigin qua
			// searchCenter), KHONG doi so luong dong vat hay logic xay chuong (BuildCowPasture/
			// BuildHorseStable/BuildChickenCoop/BuildSheepPigPasture giu nguyen, chi doc field vi
			// tri MOI duoc gan o day thay vi hang so cu).
			SafeBuildStep(() =>
			{
				CowPastureCenter = FindOpenSpot(avoid, CowPastureHalf * 1.5f, rng, searchCenter: districtOrigin, searchRadius: 2100f);
				avoid.Add((CowPastureCenter, CowPastureHalf * 1.5f));
				_extraPenZones.Add((CowPastureCenter, CowPastureHalf * 1.5f));
				BuildCowPasture();
			}, "BuildAnimalPenDistrict[bo_cu]");

			SafeBuildStep(() =>
			{
				HorseStableCenter = FindOpenSpot(avoid, HorseStableHalf * 1.5f, rng, searchCenter: districtOrigin, searchRadius: 2100f);
				avoid.Add((HorseStableCenter, HorseStableHalf * 1.5f));
				_extraPenZones.Add((HorseStableCenter, HorseStableHalf * 1.5f));
				BuildHorseStable();
			}, "BuildAnimalPenDistrict[ngua_cu]");

			SafeBuildStep(() =>
			{
				ChickenCoopCenter = FindOpenSpot(avoid, ChickenCoopHalf * 1.5f, rng, searchCenter: districtOrigin, searchRadius: 2100f);
				avoid.Add((ChickenCoopCenter, ChickenCoopHalf * 1.5f));
				_extraPenZones.Add((ChickenCoopCenter, ChickenCoopHalf * 1.5f));
				BuildChickenCoop();
			}, "BuildAnimalPenDistrict[ga_cu]");

			// Chuong Cuu/Heo cu TRUOC DAY khong co NPC cham soc rieng (khac 3 chuong tren) - nen
			// gop vao "built" de nhan 1 nguoi cham soc VO DANH tu BuildPenCaretakerDorm giong 28
			// chuong khac, thay vi bi bo trong.
			SafeBuildStep(() =>
			{
				SheepPigPastureCenter = FindOpenSpot(avoid, SheepPigPastureHalf * 1.5f, rng, searchCenter: districtOrigin, searchRadius: 2100f);
				avoid.Add((SheepPigPastureCenter, SheepPigPastureHalf * 1.5f));
				_extraPenZones.Add((SheepPigPastureCenter, SheepPigPastureHalf * 1.5f));
				BuildSheepPigPasture();
				built.Add((SheepPigPastureCenter, ("cuuheo_cu", SheepPigPastureHalf, 0, 20, 10, 0, 0)));
			}, "BuildAnimalPenDistrict[cuuheo_cu]");

			BuildPenCaretakerDorm(avoid, rng, built);
		}

		// 1 doanh trai DUY NHAT cho TAT CA NPC cham soc chuong (xem BuildAnimalPenDistrict) -
		// TACH HAN khoi khu chuong (khac han truoc day, moi NPC "o" ngay sau chuong minh phu
		// trach). Dung 1 luoi cho ngu trong noi that (10 cot) giong mau BuildPalaceGuardBarracks/
		// BuildWorkerDormsAndStaff - moi NPC van di lam moi ngay toi DUNG chuong cua minh qua
		// WorkPos (chi la noi "o" duoc gom lai 1 cho, khong phai noi "lam viec").
		private void BuildPenCaretakerDorm(List<(Vector3 c, float r)> avoid, RandomNumberGenerator rng,
			List<(Vector3 pos, (string tag, float half, int cow, int sheep, int pig, int horse, int chicken) spec)> pens)
		{
			// avoid[] duoc TRUYEN VAO tu BuildAnimalPenDistrict (chi loai LivestockZoneOrigin) -
			// van con nguyen entry (HousingZoneAnchor, 900f), trong khi ham nay tim cho QUANH
			// CHINH HousingZoneAnchor -> cung loi tu-tham-chieu da sua o cac ham khac (xem
			// KnownOccupiedZonesExcluding), phai loc rieng o day vi avoid[] la tham so nhan vao
			// chu khong tu goi KnownOccupiedZones().
			var dormAvoid = new List<(Vector3 c, float r)>();
			foreach (var z in avoid) if (z.c != HousingZoneAnchor) dormAvoid.Add(z);
			var dormPos = FindOpenSpot(dormAvoid, 200f, rng, searchCenter: HousingZoneAnchor, searchRadius: 900f);
			_caretakerDormPos = dormPos;
			AddDecor(_farmhouseScene, dormPos, 60f, 90f, FarmhouseFootprint);
			var interior = AddBuildingEntrance(dormPos, 90f, 110f, 80f, RoomKind.Village);
			AddBuildingLabelZone(dormPos, 130f, "label.caretaker_dormitory");

			var homeFront = dormPos + new Vector3(0, 0, 90);
			const int cols = 10;

			for (int i = 0; i < pens.Count; i++)
			{
				int idx = i;
				SafeBuildStep(() =>
				{
					var (penPos, spec) = pens[idx];
					var interiorSlot = interior + new Vector3((idx % cols - 4.5f) * 20f, 0, (idx / cols - 1.5f) * 20f);

					if (spec.horse > 0)
					{
						var stablehand = _stablehandScene.Instantiate<StablehandNpc>();
						stablehand.NpcId = $"district_stablehand_{idx}";
						stablehand.NpcName = PickStaffName(idx + 5); // +5: tranh trung ten voi 5 vai tro co dinh (Etienne...Augustin)
						stablehand.DialogueLow = new[] { "Tôi phụ trách một chuồng ngựa trong khu chăn nuôi." };
						stablehand.DialogueMid = new[] { "Mỗi ngày tôi đi từ đây đến chuồng để chăm ngựa." };
						stablehand.DialogueHigh = new[] { "Đàn ngựa tôi chăm đều khỏe mạnh cả." };
						stablehand.DialogueLowEn = new[] { "I look after a horse stable in the livestock district." };
						stablehand.DialogueMidEn = new[] { "Every day I come from here to the stable to tend the horses." };
						stablehand.DialogueHighEn = new[] { "Every horse I care for is healthy and strong." };
						stablehand.WorkPos = penPos + new Vector3(0, 0, -40);
						stablehand.HomePos = homeFront;
						stablehand.InteriorHomePos = interiorSlot;
						_world.AddChild(stablehand);
					}
					else if (spec.chicken > 0)
					{
						var keeper = _poultryKeeperScene.Instantiate<PoultryKeeperNpc>();
						keeper.NpcId = $"district_poultrykeeper_{idx}";
						keeper.NpcName = PickStaffName(idx + 5); // +5: tranh trung ten voi 5 vai tro co dinh (Etienne...Augustin)
						keeper.DialogueLow = new[] { "Tôi phụ trách một chuồng gà trong khu chăn nuôi." };
						keeper.DialogueMid = new[] { "Mỗi ngày tôi đi từ đây đến chuồng để nhặt trứng." };
						keeper.DialogueHigh = new[] { "Trứng mới đẻ còn ấm, anh lấy ngay đi." };
						keeper.DialogueLowEn = new[] { "I look after a chicken coop in the livestock district." };
						keeper.DialogueMidEn = new[] { "Every day I come from here to the coop to collect eggs." };
						keeper.DialogueHighEn = new[] { "Fresh eggs, still warm - go on and take them." };
						keeper.WorkPos = penPos + new Vector3(0, 0, -30);
						keeper.HomePos = homeFront;
						keeper.InteriorHomePos = interiorSlot;
						_world.AddChild(keeper);
					}
					else
					{
						var caretaker = _farmhandScene.Instantiate<FarmhandNpc>();
						caretaker.NpcId = $"district_caretaker_{idx}";
						caretaker.NpcName = PickStaffName(idx + 5); // +5: tranh trung ten voi 5 vai tro co dinh (Etienne...Augustin)
						caretaker.DialogueLow = new[] { "Tôi phụ trách một chuồng trong khu chăn nuôi." };
						caretaker.DialogueMid = new[] { "Mỗi ngày tôi đi từ đây đến chuồng để chăm sóc." };
						caretaker.DialogueHigh = new[] { "Vật nuôi tôi chăm đều khỏe mạnh cả." };
						caretaker.DialogueLowEn = new[] { "I look after a pen in the livestock district." };
						caretaker.DialogueMidEn = new[] { "Every day I come from here to the pen to take care of the animals." };
						caretaker.DialogueHighEn = new[] { "Every animal I care for is healthy and strong." };
						if (spec.sheep > 0 || spec.pig > 0) caretaker.ProduceItemId = "wool";
						caretaker.WorkPos = penPos + new Vector3(0, 0, -40);
						caretaker.TroughPos = penPos;
						caretaker.HomePos = homeFront;
						caretaker.InteriorHomePos = interiorSlot;
						_world.AddChild(caretaker);
					}
				}, $"BuildPenCaretakerDorm[{idx}]");
			}
		}

		// Chuong De rieng (loai vat nuoi moi, xem Goat.cs) - dung LAI truc tiep BuildSimplePasture
		// nhu cac chuong khac thay vi mo rong bang "specs" cua BuildAnimalPenDistrict (tuple 7
		// truong da dung o nhieu noi, them cot "goat" se phai sua ca BuildPenCaretakerDorm va moi
		// ham doc built-list - khong dang voi 1 chuong duy nhat). Goi SAU BuildAnimalPenDistrict
		// nen KnownOccupiedZones() da bao gom ca khu chuong + doanh trai cham soc. searchCenter=
		// LivestockZoneOrigin de chuong De nam GAN Khu Chan Nuoi thay vi ngau nhien khap tuong da.
		private void BuildGoatPen()
		{
			var avoid = KnownOccupiedZonesExcluding(LivestockZoneOrigin);
			var rng = new RandomNumberGenerator { Seed = 11500 };
			const float half = 140f;
			var pos = FindOpenSpot(avoid, half * 1.5f, rng, searchCenter: LivestockZoneOrigin, searchRadius: 2100f);
			_extraPenZones.Add((pos, half * 1.5f));

			BuildSimplePasture(pos, half, "de_1", (c, h) =>
			{
				const int count = 10;
				for (int k = 0; k < count; k++)
				{
					float angle = Mathf.Tau * k / count;
					float radius = rng.RandfRange(30f, h - 35f);
					SpawnGoat(c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), isAdult: true, homeCenter: c, pastureHalf: h);
				}
			});

			var caretaker = _farmhandScene.Instantiate<FarmhandNpc>();
			caretaker.NpcId = "goat_caretaker_1";
			// 34: doanh trai cham chuong gio co 29 nguoi (28 cu + "cuuheo_cu" moi gop vao, xem
			// BuildAnimalPenDistrict), chiem het chi so 5-33 (idx+5) - De dung 34 de khong trung.
			caretaker.NpcName = PickStaffName(34);
			caretaker.DialogueLow = new[] { "Tôi phụ trách chuồng Dê của trang trại." };
			caretaker.DialogueMid = new[] { "Dê cần ăn cỏ tươi mỗi ngày, không được lơ là." };
			caretaker.DialogueHigh = new[] { "Đàn Dê tôi chăm cho sữa rất tốt." };
			caretaker.DialogueLowEn = new[] { "I look after the farm's goat pen." };
			caretaker.DialogueMidEn = new[] { "Goats need fresh grass every day, can't slack on that." };
			caretaker.DialogueHighEn = new[] { "The goats I raise give excellent milk." };
			caretaker.ProduceItemId = "wool";
			caretaker.WorkPos = pos + new Vector3(0, 0, -40);
			caretaker.TroughPos = pos;
			// Quy hoach lai: nguoi cham De NGU trong Khu Nha O (tach khoi chuong), chi con di lam
			// moi ngay toi chuong (WorkPos/TroughPos van tai vi tri chuong nhu cu).
			var goatHomeAvoid = KnownOccupiedZonesExcluding(HousingZoneAnchor);
			var goatHomeRng = new RandomNumberGenerator { Seed = 11600 };
			var goatHomePos = FindOpenSpot(goatHomeAvoid, 60f, goatHomeRng, searchCenter: HousingZoneAnchor, searchRadius: 900f);
			_extraPenZones.Add((goatHomePos, 60f));
			caretaker.HomePos = goatHomePos;
			caretaker.InteriorHomePos = goatHomePos;
			_world.AddChild(caretaker);
		}

		// Nha Kinh - trong duoc QUANH NAM (bo qua ValidSeasons) + tu dong tuoi moi ngay (xem
		// FarmPlot.IsGreenhouse). Xay NGOAI TROI (khong phai phong noi that day chuyen tele port -
		// don gian hoa, tranh rui ro noi day AddBuildingEntrance/BuildRoom sai) - 1 khung "kinh"
		// (tuong dac mau sang, GetCachedMaterial khong ho tro alpha that) + mai phang + Cong khoa
		// (xem GreenhouseGate.cs) chan loi vao cho toi khi tra vang mo khoa 1 lan.
		private static readonly Vector3 GreenhouseAnchor = CropsExtensionAnchor + new Vector3(430, 0, -260);

		private void BuildGreenhouse()
		{
			const int gridSize = 4;
			const float spacing = 100f;
			float half = (gridSize - 1) * spacing / 2f + 60f;
			const float doorGap = 60f;
			float segLen = half - doorGap / 2f;

			_extraPenZones.Add((GreenhouseAnchor, half + 60f));

			var wallMat = GetCachedMaterial(new Color(0.78f, 0.88f, 0.86f), 0.2f);
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(half * 2f, 70f, 6f) }, Position = GreenhouseAnchor + new Vector3(0, 35f, -half), MaterialOverride = wallMat });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(6f, 70f, half * 2f) }, Position = GreenhouseAnchor + new Vector3(-half, 35f, 0), MaterialOverride = wallMat });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(6f, 70f, half * 2f) }, Position = GreenhouseAnchor + new Vector3(half, 35f, 0), MaterialOverride = wallMat });
			// Tuong Nam chia 2 doan, chua khoang trong o giua lam loi vao (Cong khoa dat dung do).
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(segLen, 70f, 6f) }, Position = GreenhouseAnchor + new Vector3(-(doorGap / 2f + segLen / 2f), 35f, half), MaterialOverride = wallMat });
			_world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(segLen, 70f, 6f) }, Position = GreenhouseAnchor + new Vector3(doorGap / 2f + segLen / 2f, 35f, half), MaterialOverride = wallMat });
			_world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(half * 2f + 10f, half * 2f + 10f) }, Position = GreenhouseAnchor + Vector3.Up * 72f, MaterialOverride = GetCachedMaterial(new Color(0.72f, 0.86f, 0.86f), 0.15f) });

			if (_greenhouseGateScene != null)
			{
				var gate = _greenhouseGateScene.Instantiate<GreenhouseGate>();
				gate.Position = GreenhouseAnchor + new Vector3(0, 0, half);
				_world.AddChild(gate);
			}

			for (int gx = 0; gx < gridSize; gx++)
			{
				for (int gz = 0; gz < gridSize; gz++)
				{
					var plot = _farmScene.Instantiate<FarmPlot>();
					// GridX/GridY 300+ - nam NGOAI pham vi luoi that (0-11, BuildFarm) va sentinel
					// tu do (-1) - tranh trung khoa luu voi FarmTileState cua he thong khac.
					plot.GridX = 300 + gx;
					plot.GridY = 300 + gz;
					plot.IsGreenhouse = true;
					plot.Soil = SoilType.Fertile;
					plot.Position = GreenhouseAnchor + new Vector3((gx - (gridSize - 1) / 2f) * spacing, 0, (gz - (gridSize - 1) / 2f) * spacing);
					_world.AddChild(plot);
				}
			}

			AddBuildingLabelZone(GreenhouseAnchor, half, "label.greenhouse");

			// 1 NPC nu rieng quan ly/cham soc Nha Kinh (theo dung yeu cau) - GreenhouseOnly=true
			// nen CHI cham soc 16 o trong Nha Kinh, khong bao gio lan sang ruong chinh. Truoc khi
			// nguoi choi mo khoa Cong Nha Kinh (xem GreenhouseGate.cs), toan bo o van rong
			// (IsEmpty=true) nen co nay chi Ngu/Di dao gan do cho toi khi co cay de cham (khong bi
			// ket o cong khoa vi khong co gi de tim duong toi ben trong ca).
			if (_farmWorkerScene != null)
			{
				var greenhouseHousePos = NextHousingCottagePos(10809);
				AddDecor(_smallBarnScene, greenhouseHousePos, 12f, 0f, SmallBarnFootprint);
				var ghInterior = AddBuildingEntrance(greenhouseHousePos, 0f, 80f, 50f, RoomKind.Village);
				AddBuildingLabelZone(greenhouseHousePos, 100f, "label.farm_worker_house");

				var keeper = _farmWorkerScene.Instantiate<FarmWorkerNpc>();
				keeper.NpcId = "greenhouse_keeper";
				keeper.NpcName = "Adeline";
				keeper.ModelScale = 20f;
				keeper.GreenhouseOnly = true;
				keeper.DialogueLow = new[] { "Chào anh, tôi phụ trách chăm sóc Nhà Kính của trang trại." };
				keeper.DialogueMid = new[] { "Trồng trong Nhà Kính thích lắm, quanh năm mùa nào cũng trồng được." };
				keeper.DialogueHigh = new[] { "Anh cứ tin tưởng, cây trong Nhà Kính lúc nào tôi cũng chăm kỹ từng chút một." };
				keeper.DialogueLowEn = new[] { "Hello, I'm in charge of taking care of the farm's Greenhouse." };
				keeper.DialogueMidEn = new[] { "I love growing things in the Greenhouse - you can plant year-round, any season." };
				keeper.DialogueHighEn = new[] { "You can trust me, I look after every plant in the Greenhouse down to the last detail." };
				keeper.HomePos = greenhouseHousePos + new Vector3(0, 0, 55);
				keeper.InteriorHomePos = ghInterior;
				keeper.WorkPos = GreenhouseAnchor;
				_world.AddChild(keeper);
			}
		}

		// May che bien nong san (xem ProcessingMachine.cs) - 5 may CANH NHAU gan Khu Nha Kho:
		// Lo Say/May Ep (nhan BAT KY nong san), May Lam Pho Mai/May Mayonnaise/May Det (nhan
		// DUNG 1 loai san pham chan nuoi).
		private static readonly Vector3 ProcessingAnchor = StorageZoneAnchor + new Vector3(400, 0, 220);

		private void BuildProcessingArea()
		{
			if (_processingMachineScene == null) return;
			_extraPenZones.Add((ProcessingAnchor, 220f));

			(string name, bool anyCrop, string fixedIn, string prefix, string fixedOut, int days)[] machines =
			{
				("Lò Sấy", true, "", "mut_", "", 2),
				("Máy Ép", true, "", "ruou_", "", 3),
				("Máy Làm Phô Mai", false, "milk", "", "pho_mai", 1),
				("Máy Mayonnaise", false, "egg", "", "mayonnaise", 1),
				("Máy Dệt", false, "wool", "", "vai", 1),
			};
			// Khoa Loc.cs tuong ung tung may, CUNG THU TU voi mang machines o tren (dung de hien
			// bang ten cong trinh - xem HUD.RefreshBuildingLabel - KHONG dung cho MachineName vi
			// MachineName van giu tieng Viet cho cac GD.Print debug o ProcessingMachine.cs).
			string[] machineLabelKeys = { "label.dryer", "label.press", "label.cheese_machine", "label.mayo_machine", "label.loom" };

			for (int i = 0; i < machines.Length; i++)
			{
				var (name, anyCrop, fixedIn, prefix, fixedOut, days) = machines[i];
				var machine = _processingMachineScene.Instantiate<ProcessingMachine>();
				machine.MachineName = name;
				machine.AcceptsAnyCrop = anyCrop;
				machine.FixedInputId = fixedIn;
				machine.OutputPrefix = prefix;
				machine.FixedOutputId = fixedOut;
				machine.ProcessDays = days;
				machine.Position = ProcessingAnchor + new Vector3((i - (machines.Length - 1) / 2f) * 70f, 0, 0);
				_world.AddChild(machine);
				AddBuildingLabelZone(machine.Position, 30f, machineLabelKeys[i]);
			}
		}

		// Bep nau an (xem CookingStation.cs/CookingUI.cs) - canh khu che bien.
		private void BuildCookingStation()
		{
			if (_cookingStationScene == null) return;
			var station = _cookingStationScene.Instantiate<CookingStation>();
			station.Position = ProcessingAnchor + new Vector3(0, 0, -150);
			_world.AddChild(station);
			AddBuildingLabelZone(station.Position, 30f, "label.kitchen");
		}

		// Chuong ong (xem AddBeehive) - san xuat mat ong THU DONG (khong can cham soc hang ngay,
		// tai su dung chu ky chin/hai/moc lai cua FruitTree.cs giong sen/rong o ho).
		private void BuildBeehives()
		{
			var rng = new RandomNumberGenerator { Seed = 13000 };
			for (int i = 0; i < 3; i++)
			{
				var pos = CropsExtensionAnchor + new Vector3(-250 + i * 90f, 0, 300f);
				AddBeehive(pos, rng);
			}
		}

		private void AddBeehive(Vector3 pos, RandomNumberGenerator rng)
		{
			var hiveMat = GetCachedMaterial(new Color(0.78f, 0.6f, 0.32f), 0.6f);
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 10f, BottomRadius = 14f, Height = 26f },
				Position = pos + Vector3.Up * 13f,
				MaterialOverride = hiveMat,
			});

			// "Mat ong" (san pham) - AN khi chua chin, HIEN khi chin (xem FruitTree.Init).
			var honeyVisual = new Node3D { Position = pos + Vector3.Up * 28f };
			_world.AddChild(honeyVisual);
			honeyVisual.AddChild(new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 4f, Height = 7f },
				MaterialOverride = GetCachedMaterial(new Color(0.9f, 0.7f, 0.15f), 0.4f),
			});

			var hive = new FruitTree { Position = pos, RipenDays = 4, FruitItemId = "mat_ong" };
			hive.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 12f, Height = 26f } });
			_world.AddChild(hive);
			hive.Init(honeyVisual);
		}

		// Chuong don gian dung chung: hang rao vuong + 1 cong o giua canh Nam + mang an + 2 cot
		// den, sau do goi lai "spawnAnimals" de dat vat nuoi rieng cho tung loai.
		private void BuildSimplePasture(Vector3 center, float half, string tag, System.Action<Vector3, float> spawnAnimals)
		{
			float minX = center.X - half, maxX = center.X + half;
			float minZ = center.Z - half, maxZ = center.Z + half;
			float gateX = center.X;

			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), _fenceScene);
			AddFenceLine(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(minX, 0, maxZ), new Vector3(gateX - 22f, 0, maxZ), _fenceScene);
			AddFenceLine(new Vector3(gateX + 22f, 0, maxZ), new Vector3(maxX, 0, maxZ), _fenceScene);
			AddFencePost(new Vector3(minX, 0, minZ));
			AddFencePost(new Vector3(maxX, 0, minZ));
			AddFencePost(new Vector3(minX, 0, maxZ));
			AddFencePost(new Vector3(maxX, 0, maxZ));

			AddFeedTrough(center);
			AddStreetLamp(new Vector3(gateX - 30, 0, maxZ), 90f);
			AddStreetLamp(new Vector3(gateX + 30, 0, maxZ), -90f);
			AddPenCenterLight(center, half);

			spawnAnimals(center, half);
		}

		// homeCenterOverride/pastureHalfOverride: CHO PHEP goi ham nay cho CAC CHUONG BO KHAC
		// (xem BuildExtraPastures) - neu KHONG truyen, mac dinh dung dung CowPastureCenter/
		// CowPastureHalf nhu truoc (khong doi hanh vi cho BuildCowPasture goc). TRUOC DAY luon
		// hard-code CowPastureCenter bat ke "pos" o dau, khien bo sinh o CAC CHUONG MOI (khac vi
		// tri) tuong nham "nha" cua no la chuong bo GOC o xa, roi bo di ve huong do (dung sat
		// hang rao chuong MOI, nhin nhu "chuong bo trong khong co con nao") - day chinh la loi
		// gay ra bao cao "cac chuong khac van con bo trong".
		private void SpawnCow(Vector3 pos, bool isAdult, Vector3? homeCenterOverride = null, float? pastureHalfOverride = null)
		{
			var cow = _cowScene.Instantiate<Cow>();
			cow.Position = pos;
			var home = homeCenterOverride ?? CowPastureCenter;
			var half = pastureHalfOverride ?? CowPastureHalf;
			cow.TroughPosition = home;
			cow.IsAdult = isAdult;
			// Tam wander PHAI la tam that cua hang rao (khong phai vi tri spawn rieng cua tung
			// con) + gioi han ban kinh nho hon nua be rong hang rao that mot khoang an toan, de
			// bo khong bao gio wander ra ngoai hang rao.
			cow.HomeCenter = home;
			cow.PastureHalfExtent = half - 35f;
			_world.AddChild(cow);
		}

		private void SpawnGoat(Vector3 pos, bool isAdult, Vector3 homeCenter, float pastureHalf)
		{
			var goat = _goatScene.Instantiate<Goat>();
			goat.Position = pos;
			goat.TroughPosition = homeCenter;
			goat.HomeCenter = homeCenter;
			goat.PastureHalfExtent = pastureHalf - 35f;
			goat.IsAdult = isAdult;
			_world.AddChild(goat);
		}

		// Gioi han so con MOI CHUONG (khong phai toan nong trai) - xem ghi chu tren TryBreedCows.
		private const int MaxAnimalsPerPen = 20;

		// Moi ngay THAT: neu 1 CHUONG (nhom theo HomeCenter - vi tri tam that cua hang rao, KHONG
		// phai vi tri spawn rieng tung con) co it nhat 2 con TRUONG THANH va CHUONG DO chua vuot
		// MaxAnimalsPerPen, co 1 co hoi ngau nhien sinh be con moi trong DUNG chuong do.
		//
		// LOI CU (da sua): gioi han truoc day la MaxCows=10 kiem tra TOAN CUC tren group "cows",
		// nhung nong trai hien co HANG TRAM con bo trai khap 32+ chuong (tu nhieu lan mo rong
		// trong phien nay) - dieu kien "cows.Count >= MaxCows" LUON DUNG ngay tu dau, nen sinh san
		// KHONG BAO GIO kich hoat duoc (dead code). Nhom theo TUNG CHUONG rieng biet moi sua dung
		// goc, dong thoi be con gio sinh DUNG trong chuong cua bo me (truoc day luon hard-code ve
		// CowPastureCenter o xa bat ke bo me dang o chuong nao).
		private void TryBreedCows()
		{
			var groups = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<Cow>>();
			foreach (var node in GetTree().GetNodesInGroup("cows"))
			{
				if (node is not Cow c || !IsInstanceValid(c)) continue;
				if (!groups.TryGetValue(c.HomeCenter, out var list)) { list = new(); groups[c.HomeCenter] = list; }
				list.Add(c);
			}

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var (home, list) in groups)
			{
				if (list.Count >= MaxAnimalsPerPen) continue;
				int adultCount = 0;
				Vector3 lastAdultPos = home;
				float half = 150f;
				foreach (var c in list)
					if (c.IsAdult) { adultCount++; lastAdultPos = c.GlobalPosition; half = c.PastureHalfExtent; }
				if (adultCount < 2) continue;
				if (rng.Randf() > 0.5f) continue; // ~50% co hoi moi ngay/moi chuong

				var calfPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
				SpawnCow(calfPos, isAdult: false, homeCenterOverride: home, pastureHalfOverride: half);
			}
		}

		// 3 ham sau day dung CHUNG 1 khuon mau nhom-theo-chuong nhu TryBreedCows o tren, ap dung
		// cho Cuu/Heo/Ngua (Sheep.cs/Pig.cs/Horse.cs vua duoc them IsAdult/GrowthDaysNeeded... -
		// xem ghi chu trong tung file do).
		private void TryBreedSheep()
		{
			var groups = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<Sheep>>();
			foreach (var node in GetTree().GetNodesInGroup("sheep"))
			{
				if (node is not Sheep s || !IsInstanceValid(s)) continue;
				if (!groups.TryGetValue(s.HomeCenter, out var list)) { list = new(); groups[s.HomeCenter] = list; }
				list.Add(s);
			}

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var (home, list) in groups)
			{
				if (list.Count >= MaxAnimalsPerPen) continue;
				int adultCount = 0;
				Vector3 lastAdultPos = home;
				float half = 130f;
				Vector3 trough = home;
				foreach (var s in list)
					if (s.IsAdult) { adultCount++; lastAdultPos = s.GlobalPosition; half = s.PastureHalfExtent; trough = s.TroughPosition; }
				if (adultCount < 2) continue;
				if (rng.Randf() > 0.5f) continue;

				var lambPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
				var lamb = _sheepScene.Instantiate<Sheep>();
				lamb.Position = lambPos;
				lamb.TroughPosition = trough;
				lamb.HomeCenter = home;
				lamb.PastureHalfExtent = half;
				lamb.IsAdult = false;
				_world.AddChild(lamb);
			}
		}

		private void TryBreedPigs()
		{
			var groups = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<Pig>>();
			foreach (var node in GetTree().GetNodesInGroup("pigs"))
			{
				if (node is not Pig p || !IsInstanceValid(p)) continue;
				if (!groups.TryGetValue(p.HomeCenter, out var list)) { list = new(); groups[p.HomeCenter] = list; }
				list.Add(p);
			}

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var (home, list) in groups)
			{
				if (list.Count >= MaxAnimalsPerPen) continue;
				int adultCount = 0;
				Vector3 lastAdultPos = home;
				float half = 120f;
				Vector3 trough = home;
				foreach (var p in list)
					if (p.IsAdult) { adultCount++; lastAdultPos = p.GlobalPosition; half = p.PastureHalfExtent; trough = p.TroughPosition; }
				if (adultCount < 2) continue;
				if (rng.Randf() > 0.5f) continue;

				var pigletPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
				var piglet = _pigScene.Instantiate<Pig>();
				piglet.Position = pigletPos;
				piglet.TroughPosition = trough;
				piglet.HomeCenter = home;
				piglet.PastureHalfExtent = half;
				piglet.IsAdult = false;
				_world.AddChild(piglet);
			}
		}

		private void TryBreedHorses()
		{
			var groups = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<Horse>>();
			foreach (var node in GetTree().GetNodesInGroup("horses"))
			{
				if (node is not Horse h || !IsInstanceValid(h)) continue;
				if (!groups.TryGetValue(h.HomeCenter, out var list)) { list = new(); groups[h.HomeCenter] = list; }
				list.Add(h);
			}

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var (home, list) in groups)
			{
				if (list.Count >= MaxAnimalsPerPen) continue;
				int adultCount = 0;
				Vector3 lastAdultPos = home;
				float half = 150f;
				Vector3 trough = home;
				foreach (var h in list)
					if (h.IsAdult) { adultCount++; lastAdultPos = h.GlobalPosition; half = h.PastureHalfExtent; trough = h.TroughPosition; }
				if (adultCount < 2) continue;
				if (rng.Randf() > 0.5f) continue;

				var foalPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
				var foal = _horseScene.Instantiate<Horse>();
				foal.Position = foalPos;
				foal.TroughPosition = trough;
				foal.HomeCenter = home;
				foal.PastureHalfExtent = half;
				foal.IsAdult = false;
				_world.AddChild(foal);
			}
		}

		private void TryBreedGoats()
		{
			var groups = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<Goat>>();
			foreach (var node in GetTree().GetNodesInGroup("goats"))
			{
				if (node is not Goat g || !IsInstanceValid(g)) continue;
				if (!groups.TryGetValue(g.HomeCenter, out var list)) { list = new(); groups[g.HomeCenter] = list; }
				list.Add(g);
			}

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			foreach (var (home, list) in groups)
			{
				if (list.Count >= MaxAnimalsPerPen) continue;
				int adultCount = 0;
				Vector3 lastAdultPos = home;
				float half = 130f;
				foreach (var g in list)
					if (g.IsAdult) { adultCount++; lastAdultPos = g.GlobalPosition; half = g.PastureHalfExtent; }
				if (adultCount < 2) continue;
				if (rng.Randf() > 0.5f) continue;

				var kidPos = lastAdultPos + new Vector3(rng.RandfRange(-25, 25), 0, rng.RandfRange(-25, 25));
				SpawnGoat(kidPos, isAdult: false, homeCenter: home, pastureHalf: half + 35f);
			}
		}

		// Mang thuc an cho bo: khong tim duoc model CC0 phu hop rieng cho hinh dang nay, nen
		// dung go primitive (giong cach lam cot hang rao/mat troi truoc do) - 1 khay go ho mo
		// chat day thuc an mau vang, du don gian de khong can 1 model rieng.
		private void AddFeedTrough(Vector3 pos)
		{
			var woodMat = GetCachedMaterial(new Color(0.4f, 0.26f, 0.14f), 0.9f);
			var foodMat = GetCachedMaterial(new Color(0.85f, 0.7f, 0.3f), 1f);

			void AddBox(Vector3 offset, Vector3 size, StandardMaterial3D mat)
			{
				_world.AddChild(new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = size },
					Position = pos + offset,
					MaterialOverride = mat
				});
			}

			AddBox(new Vector3(0, 4, 0), new Vector3(70, 8, 30), woodMat);
			AddBox(new Vector3(-33, 12, 0), new Vector3(4, 16, 30), woodMat);
			AddBox(new Vector3(33, 12, 0), new Vector3(4, 16, 30), woodMat);
			AddBox(new Vector3(0, 12, -13), new Vector3(70, 16, 4), woodMat);
			AddBox(new Vector3(0, 12, 13), new Vector3(70, 16, 4), woodMat);
			AddBox(new Vector3(0, 9, 0), new Vector3(60, 6, 22), foodMat);

			var body = new StaticBody3D { Position = pos };
			body.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(74, 20, 34) },
				Position = Vector3.Up * 10f
			});
			_world.AddChild(body);
		}

		// Chuong cho: khong tim duoc model CC0 phu hop rieng cho hinh dang nay sau nhieu lan
		// tim (giong cach lam mang thuc an/cot hang rao truoc do) - dung go primitive: than
		// hop go + mai chop (PrismMesh) + cua vao toi mau.
		private void AddDogHouse(Vector3 pos, float rotationYDegrees)
		{
			var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));
			var rot = new Vector3(0, rotationYDegrees, 0);
			var woodMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.32f, 0.18f), Roughness = 0.9f };
			var roofMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.16f, 0.09f), Roughness = 0.9f };
			var doorMat = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.03f, 0.03f), Roughness = 1f };

			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(34, 28, 34) },
				Position = pos + basis * new Vector3(0, 14, 0),
				RotationDegrees = rot,
				MaterialOverride = woodMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(13, 18, 2) },
				Position = pos + basis * new Vector3(0, 9, 17.5f),
				RotationDegrees = rot,
				MaterialOverride = doorMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(40, 16, 40) },
				Position = pos + basis * new Vector3(0, 28, 0),
				RotationDegrees = rot,
				MaterialOverride = roofMat
			});

			var body = new StaticBody3D { Position = pos, RotationDegrees = rot };
			body.AddChild(new CollisionShape3D
			{
				Shape = new BoxShape3D { Size = new Vector3(34, 30, 34) },
				Position = Vector3.Up * 15f
			});
			_world.AddChild(body);
		}

		// Chuong meo: khong tim duoc model CC0 phu hop (2 lua chon tim duoc deu CC-BY) - dung go
		// primitive nhu chuong cho nhung nho hon va mau mieng/lieu gai de khac biet.
		private void AddCatHouse(Vector3 pos, float rotationYDegrees)
		{
			var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(rotationYDegrees));
			var rot = new Vector3(0, rotationYDegrees, 0);
			var wickerMat = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.55f, 0.32f), Roughness = 1f };
			var roofMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.38f, 0.2f), Roughness = 0.9f };
			var doorMat = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.03f, 0.03f), Roughness = 1f };

			_world.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 13f, BottomRadius = 14f, Height = 16f },
				Position = pos + basis * new Vector3(0, 8, 0),
				RotationDegrees = rot,
				MaterialOverride = wickerMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(8, 10, 2) },
				Position = pos + basis * new Vector3(0, 6, 12.5f),
				RotationDegrees = rot,
				MaterialOverride = doorMat
			});
			_world.AddChild(new MeshInstance3D
			{
				Mesh = new PrismMesh { Size = new Vector3(20, 8, 20) },
				Position = pos + basis * new Vector3(0, 16, 0),
				RotationDegrees = rot,
				MaterialOverride = roofMat
			});

			var body = new StaticBody3D { Position = pos, RotationDegrees = rot };
			body.AddChild(new CollisionShape3D
			{
				Shape = new CylinderShape3D { Radius = 14f, Height = 18f },
				Position = Vector3.Up * 9f
			});
			_world.AddChild(body);
		}

		private const int MaxHandPlacedEnemies = 8;

		private void RespawnSomeEnemies()
		{
			// Moi ngay sinh them vai quai bun de co gi de danh, nhung gioi han tong so de
			// khong don cuc thanh mot dong quai chong len nhau qua nhieu ngay khong ai danh.
			if (GetTree().GetNodesInGroup("enemies").Count >= MaxHandPlacedEnemies) return;

			var rng = new RandomNumberGenerator();
			rng.Randomize();
			// Cung nam NGOAI tuong da 10 hecta (xem SpawnEnemies) - giong ly do o tren.
			for (int i = 0; i < 2; i++)
				SpawnEnemy("mud_monster", new Vector3(rng.RandfRange(-150, 250), 0, rng.RandfRange(-3050, -2850)));
		}

		private void GiveStartingItems()
		{
			// Luon cap do khoi dau truoc - neu nguoi choi co ban luu tren server, FetchAndApplySave
			// (cuoi _Ready, xem duoi day) se GHI DE len ngay sau do (Inventory.Clear() + nap lai
			// tu save that). Truoc day co the kiem tra HasSave() dong bo qua file - nay la goi
			// MANG bat dong bo nen khong the biet truoc luc nay, phai luon cap do mac dinh truoc.
			Inventory.Instance.AddItem("pumpkin_seed", 3);
			Inventory.Instance.AddItem("tomato_seed", 2);
			Inventory.Instance.AddItem("potion", 2);
			Inventory.Instance.AddItem("sword", 1);
			Inventory.Instance.AddItem("hoe", 1);
			Inventory.Instance.Equip("sword");
		}
	}
}
