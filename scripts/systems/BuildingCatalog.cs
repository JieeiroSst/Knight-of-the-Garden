using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Danh sach cong trinh nguoi choi CO THE tu xay (xem BuildMenuUI.cs/PlacedBuilding.cs) - tai
    // su dung cac model GLB da co san trong game (cottage/village_house/nha lon/SmallBarn/
    // watchtower) thay vi tao model rieng, giong tinh than cac danh sach du lieu khac
    // (EnemyDef/WildSpeciesConfig...). Scale lay tu cac cho da dung CUNG model nay trong Main.cs
    // (vd BuildLightSettlement) de nhin dung ty le voi cac cong trinh co san khac.
    public class BuildingDef
    {
        public string Id;
        public string Name;
        public string ScenePath;
        public float ModelScale;
        public float FootprintRadius;
        public Dictionary<string, int> Cost;
    }

    public static class BuildingCatalog
    {
        public static readonly BuildingDef[] Entries =
        {
            new BuildingDef
            {
                Id = "hang_rao", Name = "Hang Rao", ScenePath = "res://scenes/FenceMarker.tscn",
                ModelScale = 1f, FootprintRadius = 15f,
                Cost = new Dictionary<string, int> { { "wood", 5 } },
            },
            new BuildingDef
            {
                Id = "kho_nho", Name = "Nha Kho Nho", ScenePath = "res://assets3d/quaternius/farm/SmallBarn.fbx",
                ModelScale = 11f, FootprintRadius = 55f,
                Cost = new Dictionary<string, int> { { "wood", 30 }, { "da", 15 } },
            },
            new BuildingDef
            {
                Id = "nha_go", Name = "Nha Go", ScenePath = "res://assets3d/quaternius/french_countryside/cottage.glb",
                ModelScale = 31f, FootprintRadius = 65f,
                Cost = new Dictionary<string, int> { { "wood", 50 }, { "da", 20 } },
            },
            new BuildingDef
            {
                Id = "nha_lang", Name = "Nha Kieu Lang", ScenePath = "res://assets3d/quaternius/french_countryside/village_house.glb",
                ModelScale = 40f, FootprintRadius = 70f,
                Cost = new Dictionary<string, int> { { "wood", 40 }, { "da", 25 }, { "sat_tho", 10 } },
            },
            new BuildingDef
            {
                Id = "nha_lon", Name = "Nha Lon", ScenePath = "res://assets3d/quaternius/buildings/house_v2.glb",
                ModelScale = 54f, FootprintRadius = 90f,
                Cost = new Dictionary<string, int> { { "wood", 80 }, { "da", 50 }, { "sat_tho", 20 }, { "dong_tho", 10 } },
            },
            new BuildingDef
            {
                Id = "thap_canh", Name = "Thap Canh", ScenePath = "res://assets3d/quaternius/watchtower/watch_tower.glb",
                ModelScale = 20f, FootprintRadius = 55f,
                Cost = new Dictionary<string, int> { { "wood", 60 }, { "da", 40 }, { "sat_tho", 15 } },
            },
        };

        public static BuildingDef Get(string id)
        {
            foreach (var e in Entries) if (e.Id == id) return e;
            return null;
        }
    }
}
