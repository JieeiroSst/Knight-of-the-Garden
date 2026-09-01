using Godot;
using System.Collections.Generic;
using System.Text.Json;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Systems
{
    // Database quan ly HANG HOA/SAN PHAM trong nha kho (vat dung trang tri nhu thung/bao/rom LAN
    // san pham thuc duoc theo doi so luong qua FarmStorage nhu trung ga) - nap tu
    // warehouse_products.json, giong cach ItemDatabase nap items.json. Nha kho (xem
    // Main.BuildRoomForKind - RoomKind.Barn) doc du lieu tu day de TU SAP XEP hang hoa (qua
    // GetScatterRecipe/GetGridRecipe) thay vi hard-code danh sach truc tiep trong code - muon
    // doi loai hang/so luong chi can sua JSON, khong dong vao Main.cs.
    public partial class WarehouseDatabase : Node
    {
        public static WarehouseDatabase Instance { get; private set; }

        private readonly List<WarehouseProductDef> _all = new();
        private readonly Dictionary<string, WarehouseProductDef> _byId = new();
        private readonly Dictionary<string, PackedScene> _sceneCache = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // WarehouseProductDef dung PUBLIC FIELD - can IncludeFields=true de System.Text.Json
            // thuc su doc duoc (xem giai thich chi tiet trong ItemDatabase.cs).
            IncludeFields = true
        };

        public override void _EnterTree()
        {
            Instance = this;
            Load();
        }

        private void Load()
        {
            const string path = "res://data/warehouse_products.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PushWarning($"Không tìm thấy file: {path}");
                return;
            }
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var list = JsonSerializer.Deserialize<List<WarehouseProductDef>>(f.GetAsText(), JsonOpts);
            foreach (var p in list)
            {
                _all.Add(p);
                _byId[p.Id] = p;
            }
            GD.Print($"Đã nạp {_all.Count} loại hàng hóa nhà kho.");
        }

        public WarehouseProductDef GetProduct(string id) => _byId.TryGetValue(id, out var v) ? v : null;

        // Ten hien thi theo ngon ngu dang chon (Loc.Current) - xem ItemDatabase.GetDisplayName,
        // cung mau: JSON van la tieng Viet goc, DataLoc.cs chua ban dich EN theo Id.
        public string GetDisplayName(string id)
        {
            var def = GetProduct(id);
            if (def == null) return id;
            return Loc.Current == Loc.Lang.EN && DataLoc.WarehouseProductNamesEn.TryGetValue(id, out var en) ? en : def.Name;
        }

        private PackedScene LoadScene(WarehouseProductDef def)
        {
            if (string.IsNullOrEmpty(def.ModelPath)) return null;
            if (!_sceneCache.TryGetValue(def.ModelPath, out var scene))
            {
                scene = GD.Load<PackedScene>(def.ModelPath);
                _sceneCache[def.ModelPath] = scene;
            }
            return scene;
        }

        // Cong thuc "rai ngau nhien" (xem Main.ScatterBarnStock) - moi hang co ScatterCount > 0,
        // giu dung THU TU khai bao trong JSON.
        public (PackedScene scene, float scale, int count)[] GetScatterRecipe()
        {
            var result = new List<(PackedScene, float, int)>();
            foreach (var p in _all)
                if (p.ScatterCount > 0)
                    result.Add((LoadScene(p), p.Scale, p.ScatterCount));
            return result.ToArray();
        }

        // Cong thuc "ke hang chinh" dang luoi (xem Main.BuildWarehouseGrid) - hang co
        // UseInGrid=true, giu dung THU TU khai bao trong JSON (anh huong toi cach xoay vong loai
        // hang giua cac cot).
        public (PackedScene scene, float scale)[] GetGridRecipe()
        {
            var result = new List<(PackedScene, float)>();
            foreach (var p in _all)
                if (p.UseInGrid)
                    result.Add((LoadScene(p), p.Scale));
            return result.ToArray();
        }
    }
}
