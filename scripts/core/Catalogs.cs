using System.Collections.Generic;

namespace HiepSiVeVuon.Core
{
    // Do trang tri: id vat pham "decor_<key>" -> model 3D (CC0) + chieu cao dat trong the gioi.
    public static class DecorCatalog
    {
        private const string F = "res://assets3d/features/";
        private const string Q = "res://assets3d/quaternius/furniture/";
        private const string I = "res://assets3d/items3d/";

        public static readonly Dictionary<string, (string model, float height)> Models = new()
        {
            ["decor_sofa"] = (F + "sofa.glb", 30f),
            ["decor_couch_small"] = (F + "couch_small.glb", 28f),
            ["decor_lamp_floor"] = (F + "lamp_floor.glb", 55f),
            ["decor_lamp_table"] = (F + "lamp_table.glb", 22f),
            ["decor_plant_a"] = (F + "plant_a.glb", 22f),
            ["decor_plant_b"] = (F + "plant_b.glb", 40f),
            ["decor_fountain"] = (F + "fountain.glb", 60f),
            ["decor_painting"] = (F + "painting.glb", 30f),
            ["decor_clock"] = (F + "clock.glb", 70f),
            ["decor_bench"] = (F + "bench.glb", 24f),
            ["decor_lantern"] = (F + "lantern.glb", 60f),
            ["decor_fox_statue"] = (I + "fox_statue.glb", 40f),
            ["decor_horse_statue"] = (F + "horse_statue.glb", 60f),
            ["decor_stag_statue"] = (F + "stag_statue.glb", 60f),
            ["decor_bookshelf"] = (Q + "bookshelf.glb", 60f),
            ["decor_wardrobe"] = (Q + "wardrobe.glb", 65f),
            ["decor_table"] = (Q + "table_round.glb", 24f),
            ["decor_chair"] = (Q + "chair.glb", 28f),
            ["decor_barrel"] = (Q + "barrel.glb", 26f),
            ["decor_rug"] = (Q + "rug.glb", 2f),
            ["decor_chandelier"] = (Q + "chandelier.glb", 30f),
        };
    }

    // Thu cung: id vat pham "pet_<key>" -> model dong vat (co hoat canh) + ti le + bay hay di bo.
    public static class PetCatalog
    {
        public record PetDef(string Key, string Model, float Height, bool Flying, int BaseDamage, string NameVi, string NameEn);

        public static readonly Dictionary<string, PetDef> Pets = new()
        {
            ["pet_cho"] = new("cho", "res://assets3d/quaternius/animals/dog.glb", 18f, false, 6, "Chó Shiba", "Shiba Dog"),
            ["pet_meo"] = new("meo", "res://assets3d/quaternius/animals/cat.glb", 14f, false, 5, "Mèo", "Cat"),
            ["pet_husky"] = new("husky", "res://assets3d/quaternius/animals/husky.glb", 22f, false, 9, "Husky", "Husky"),
            ["pet_soi"] = new("soi", "res://assets3d/quaternius/animals/wolf.glb", 24f, false, 13, "Sói Xám", "Grey Wolf"),
            ["pet_ong"] = new("ong", "res://assets3d/features/bee.glb", 12f, true, 10, "Ong Vàng", "Golden Bee"),
            ["pet_rong"] = new("rong", "res://assets3d/quaternius/monsters/dragon.glb", 20f, true, 20, "Rồng Con", "Baby Dragon"),
        };
    }
}
