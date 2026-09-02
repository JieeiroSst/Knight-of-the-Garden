using Godot;
using System;
using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // He thong dich thuat CHO GIAO DIEN (UI chrome: Settings/HUD/Tui do/Balo/Cua hang/Ban do/Xay
    // dung/Nau an/Dang nhap/Chon giong...) - KHONG bao gom ten/mo ta vat pham (items.json), loi
    // thoai NPC, hay noi dung nhiem vu (van la tieng Viet, pham vi qua lon de dich het trong 1
    // lan - xem ghi chu trong ke hoach). Toan bo UI trong game duoc XAY BANG CODE (khong dung
    // Control tinh trong .tscn + tr() tu dong cua Godot), nen can 1 he thong tra cuu THU CONG:
    // moi chuoi hien thi goi Loc.T("khoa") thay vi viet chuoi truc tiep, tra ve dung ngon ngu
    // dang chon. Static (khong phai autoload Node) - co the goi tu bat ky dau, ke ca truoc khi
    // scene dau tien (LoginScreen) da dung xong.
    public static class Loc
    {
        public enum Lang { VI, EN }

        // Nap ngon ngu da luu (neu co) NGAY LAN DAU class nay duoc dung toi (static constructor -
        // C# tu goi truoc bat ky truy cap nao khac) - dam bao man hinh DAU TIEN (dang nhap) da
        // hien dung ngon ngu nguoi choi chon lan truoc, khong phai doi Settings roi moi doi.
        static Loc()
        {
            LoadSaved();
        }

        public static Lang Current { get; private set; } = Lang.VI;

        // Cac UI dang mo dang ky vao day (xem LocalizedLabelSet.cs) de tu lam moi chu khi ngon
        // ngu doi NGAY LAP TUC (khong can khoi dong lai/mo lai man hinh).
        public static event Action LanguageChanged;

        private const string ConfigPath = "user://settings.cfg";

        public static void SetLanguage(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            SaveConfig();
            LanguageChanged?.Invoke();
        }

        // Tra ve chuoi dung ngon ngu dang chon - neu KHONG tim thay khoa (vd go nham/quen them
        // ban dich), tra ve CHINH cai khoa (de nhan ra NGAY co cho nao thieu ban dich thay vi
        // man hinh trong khong ro ly do).
        public static string T(string key) =>
            Strings.TryGetValue(key, out var pair) ? (Current == Lang.VI ? pair.vi : pair.en) : key;

        private static void LoadSaved()
        {
            var cfg = new ConfigFile();
            if (cfg.Load(ConfigPath) != Error.Ok) return;
            string lang = (string)cfg.GetValue("display", "language", "vi");
            Current = lang == "en" ? Lang.EN : Lang.VI;
        }

        private static void SaveConfig()
        {
            var cfg = new ConfigFile();
            cfg.Load(ConfigPath); // giu lai cac muc khac neu file da co (bo qua loi neu chua ton tai)
            cfg.SetValue("display", "language", Current == Lang.EN ? "en" : "vi");
            cfg.Save(ConfigPath);
        }

        // (vi, en) - to chuc theo tien to man hinh (settings./hud./shop du...) de de doi chieu.
        // Tieng Viet co dau day du (UTF-8) - Godot 4 doc/hien thi UTF-8 mac dinh, font mac dinh
        // (ThemeDB.FallbackFont) ho tro day du dau tieng Viet.
        private static readonly Dictionary<string, (string vi, string en)> Strings = new()
        {
            ["common.close"] = ("Đóng", "Close"),
            ["common.cancel"] = ("Hủy", "Cancel"),

            // ---- Settings ----
            ["settings.title"] = ("CÀI ĐẶT  /  HƯỚNG DẪN", "SETTINGS  /  HELP"),
            ["settings.language_header"] = ("Ngôn ngữ", "Language"),
            ["settings.shortcuts_header"] = ("Phím tắt", "Controls"),
            ["settings.close_btn"] = ("Đóng [H]", "Close [H]"),
            ["settings.ctrl.move"] = ("Di chuyển", "Move"),
            ["settings.ctrl.attack"] = ("Tấn công", "Attack"),
            ["settings.ctrl.tool"] = (
                "Dùng công cụ (cuốc đất mới/trồng/tưới/thu hoạch/cuốc quặng/câu cá/đặt máy tưới tự động - cuốc bạc/vàng tác động cả vùng)",
                "Use tool (till new soil/plant/water/harvest/mine ore/fish/place auto-sprinkler - silver/gold hoe affects an area)"),
            ["settings.ctrl.interact"] = (
                "Tương tác / Mở cửa / Cầu thang / Sửa tháp nước / Cho vịt ăn / Máy chế biến / Bếp / Cổng Nhà Kính",
                "Interact / Open door / Stairs / Repair water tower / Feed ducks / Processing machine / Kitchen / Greenhouse gate"),
            ["settings.ctrl.mount"] = ("Cưỡi / Xuống ngựa hoặc thuyền", "Mount / dismount horse or boat"),
            ["settings.ctrl.inventory"] = ("Túi đồ", "Inventory"),
            ["settings.ctrl.backpack"] = (
                "Balo (kho chứa thêm 50 ô - chuyển đồ qua lại với túi đồ)",
                "Backpack (50 extra slots - move items to/from inventory)"),
            ["settings.ctrl.build"] = (
                "Bảng Xây Dựng (xây nhà/chuồng/tháp canh... cần vật liệu gỗ/đá/sắt/đồng)",
                "Build menu (build houses/pens/watchtowers... needs wood/stone/iron/copper)"),
            ["settings.ctrl.map"] = ("Bản đồ thế giới", "World map"),
            ["settings.ctrl.settings"] = ("Cài đặt / Hướng dẫn (màn hình này)", "Settings / Help (this screen)"),
            ["settings.ctrl.save"] = ("Lưu game", "Save game"),

            // ---- HUD ----
            ["hud.quest_header"] = ("-- Nhiệm vụ --", "-- Quests --"),
            ["hud.quest_empty"] = ("(chưa có)", "(none yet)"),
            ["hud.hp_fmt"] = ("HP: {0}/{1}", "HP: {0}/{1}"),
            ["hud.level_fmt"] = ("Cấp {0}  (EXP {1}/{2})", "Level {0}  (EXP {1}/{2})"),
            ["hud.gold_fmt"] = ("Vàng: {0}", "Gold: {0}"),
            ["hud.day"] = ("Ngày", "Day"),
            ["hud.night"] = ("Đêm", "Night"),

            // ---- Inventory ----
            ["inventory.title"] = ("== TÚI ĐỒ ==  (bấm để trang bị / dùng)", "== INVENTORY ==  (click to equip / use)"),
            ["inventory.damage_fmt"] = ("Sát thương: {0}", "Damage: {0}"),
            ["inventory.defense_fmt"] = ("Phòng thủ: {0}", "Defense: {0}"),
            ["inventory.heal_fmt"] = ("Hồi máu: {0}", "Heal: {0}"),
            ["inventory.equipped_fmt"] = ("Đã trang bị: {0}", "Equipped: {0}"),
            ["inventory.used_fmt"] = ("Đã dùng: {0}", "Used: {0}"),
            ["inventory.cannot_use_fmt"] = ("{0}: không thể dùng trực tiếp.", "{0}: cannot be used directly."),

            // ---- Shop ----
            ["shop.title"] = ("== CỬA HÀNG ==", "== SHOP =="),
            ["shop.buy_header"] = ("-- Mua --", "-- Buy --"),
            ["shop.sell_header"] = ("-- Bán nông sản --", "-- Sell produce --"),
            ["shop.contract_header"] = ("-- Hợp đồng --", "-- Contracts --"),
            ["shop.gold_fmt"] = ("Vàng của bạn: {0}", "Your gold: {0}"),
            ["shop.buy_item_fmt"] = ("{0} - {1} vàng", "{0} - {1} gold"),
            ["shop.sell_item_fmt"] = ("Bán {0} x{1} ({2}/cái)", "Sell {0} x{1} ({2} each)"),
            ["shop.contract_sign_fmt"] = ("Ký: {0} ({1} {2}/{3} ngày x{4}, thưởng {5}v)", "Sign: {0} ({1} {2}/{3} days x{4}, reward {5}g)"),
            ["shop.contract_active_fmt"] = ("{0}: {1}/{2} lần - còn {3} ngày tới hạn giao", "{0}: {1}/{2} deliveries - {3} days until due"),
            ["shop.buy_success"] = ("Mua thành công!", "Purchased!"),
            ["shop.buy_fail"] = ("Không đủ vàng!", "Not enough gold!"),
            ["shop.sell_success"] = ("Bán thành công!", "Sold!"),

            // ---- Map ----
            ["map.title"] = ("B Ả N   Đ Ồ   T H Ế   G I Ớ I", "W O R L D   M A P"),
            ["map.hint"] = ("[M] đóng  -  bấm để đánh dấu điểm đến  -  vòng đỏ = khu có quái", "[M] close - click to mark a destination - red ring = monster zone"),
            ["map.player_label"] = ("Bạn", "You"),
            ["map.france_hint"] = ("Vùng quê Pháp (~10km)", "French countryside (~10km)"),
            ["map.compass_n"] = ("B", "N"),
            ["map.compass_s"] = ("N", "S"),
            ["map.compass_e"] = ("Đ", "E"),
            ["map.compass_w"] = ("T", "W"),
            ["map.landmark.farm"] = ("Nông Trại", "Farm"),
            ["map.landmark.storage"] = ("Nhà Kho", "Storage"),
            ["map.landmark.town"] = ("Thị Trấn", "Town"),
            ["map.landmark.plateau"] = ("Cao Nguyên", "Plateau"),
            ["map.landmark.sunflower_field"] = ("Đồng Hoa Hướng Dương", "Sunflower Field"),
            ["map.landmark.mine"] = ("Mỏ", "Mine"),
            ["map.landmark.mountain"] = ("Núi", "Mountain"),
            ["map.landmark.forest"] = ("Rừng", "Forest"),
            ["map.landmark.fields"] = ("Đồng Ruộng", "Fields"),
            ["map.landmark.lake"] = ("Hồ", "Lake"),
            ["map.landmark.river"] = ("Sông", "River"),
            ["map.landmark.village"] = ("Làng", "Village"),
            ["map.landmark.city"] = ("Thành Phố", "City"),
            ["map.landmark.ruins"] = ("Phế Tích", "Ruins"),
            ["map.landmark.cemetery"] = ("Nghĩa Địa", "Cemetery"),
            ["map.landmark.swamp"] = ("Đầm Lầy", "Swamp"),
            ["map.landmark.cave"] = ("Hang Động", "Cave"),

            // ---- Backpack ----
            ["backpack.title"] = ("== BALO ==", "== BACKPACK =="),
            ["backpack.inventory_header"] = ("Túi đồ (bấm để cất vào balo)", "Inventory (click to store in backpack)"),
            ["backpack.balo_header_fmt"] = ("Balo, {0} ô (bấm để lấy ra túi đồ)", "Backpack, {0} slots (click to withdraw to inventory)"),
            ["backpack.full"] = ("Balo đã đầy!", "Backpack is full!"),
            ["backpack.inventory_full"] = ("Túi đồ đã đầy!", "Inventory is full!"),
            ["backpack.use_button"] = ("Sử Dụng", "Use"),
            ["backpack.currently_used"] = ("Đang dùng", "In use"),

            // ---- Cooking ----
            ["cooking.title"] = ("BẾP - NẤU ĂN", "KITCHEN - COOKING"),
            ["cooking.need_fmt"] = ("Cần: {0}", "Needs: {0}"),
            ["cooking.dish_fmt"] = ("{0}  (hồi {1} sinh lực)", "{0}  (heals {1})"),
            ["cooking.cook_btn"] = ("Nấu", "Cook"),
            ["cooking.missing_ingredients"] = ("Thiếu nguyên liệu", "Missing ingredients"),
            ["cooking.cooked_fmt"] = ("Đã nấu: {0}.", "Cooked: {0}."),

            // ---- Build menu ----
            ["buildmenu.title"] = ("BẢNG XÂY DỰNG", "BUILD MENU"),
            ["buildmenu.subtitle"] = (
                "Chọn công trình - sẽ đặt ngay trước mặt bạn, cần đủ vật liệu trong túi đồ.",
                "Choose a structure - it will be placed right in front of you, you need enough materials in your inventory."),
            ["buildmenu.need_fmt"] = ("Cần: {0}", "Needs: {0}"),
            ["buildmenu.build_btn"] = ("Xây", "Build"),
            ["buildmenu.missing_materials"] = ("Thiếu vật liệu", "Missing materials"),
            ["buildmenu.close_btn"] = ("Đóng [N]", "Close [N]"),
            ["buildmenu.built_fmt"] = ("Đã xây: {0}.", "Built: {0}."),
            ["buildmenu.no_player"] = ("Không tìm thấy người chơi.", "Player not found."),
            ["building.hang_rao"] = ("Hàng Rào", "Fence"),
            ["building.kho_nho"] = ("Nhà Kho Nhỏ", "Small Storage Shed"),
            ["building.nha_go"] = ("Nhà Gỗ", "Wooden House"),
            ["building.nha_lang"] = ("Nhà Kiểu Làng", "Village House"),
            ["building.nha_lon"] = ("Nhà Lớn", "Large House"),
            ["building.thap_canh"] = ("Tháp Canh", "Watchtower"),

            // ---- Seed select ----
            ["seedselect.title"] = ("CHỌN GIỐNG ĐỂ TRỒNG", "CHOOSE A SEED TO PLANT"),
            ["seedselect.empty"] = ("Bạn không có hạt giống nào. Mua ở cửa hàng!", "You have no seeds. Buy some at the shop!"),
            ["seedselect.item_fmt"] = ("{0} (đang có {1})", "{0} (have {1})"),

            // ---- Login ----
            ["login.title"] = ("HIỆP SĨ VỆ VƯỜN", "GARDEN KNIGHT"),
            ["login.subtitle"] = ("Nông Trại  *  Phiêu Lưu  *  Chiến Đấu", "Farming  *  Adventure  *  Combat"),
            ["login.username_label"] = ("Tên đăng nhập", "Username"),
            ["login.username_placeholder"] = ("VD: nongdan01", "e.g. farmer01"),
            ["login.password_label"] = ("Mật khẩu", "Password"),
            ["login.password_placeholder"] = ("ít nhất 6 ký tự", "at least 6 characters"),
            ["login.btn_login"] = ("Đăng Nhập", "Log In"),
            ["login.btn_register"] = ("Đăng Ký Tài Khoản Mới", "Create New Account"),
            ["login.toggle_to_register"] = ("Chưa có tài khoản? Bấm để đăng ký", "No account yet? Click to register"),
            ["login.toggle_to_login"] = ("Đã có tài khoản? Bấm để đăng nhập", "Already have an account? Click to log in"),
            ["login.validation_error"] = (
                "Tên đăng nhập >= 3 ký tự, mật khẩu >= 6 ký tự.",
                "Username must be >= 3 characters, password >= 6 characters."),
            ["login.connecting"] = ("Đang kết nối server...", "Connecting to server..."),
            ["login.entering_world"] = ("Đang vào trang trại...", "Entering the farm..."),
            ["login.unknown_error"] = ("Lỗi không rõ.", "Unknown error."),
            ["login.footer"] = (
                "Gieo hạt, nuôi trồng, phiêu lưu - hành trình của bạn bắt đầu từ đây.",
                "Sow, raise, adventure - your journey starts here."),

            // ---- NPC interaction ----
            ["npc.new_quest_fmt"] = ("[Nhiệm vụ mới: {0}]", "[New quest: {0}]"),
            ["npc.shop_hint"] = ("(Nhấn cửa hàng: mua hạt giống & đồ dùng)", "(Press shop: buy seeds & supplies)"),
            ["dialogue.close_hint"] = ("[E] Đóng", "[E] Close"),

            // ---- Warehouse manager NPC (dynamic report dialogue) ----
            ["warehouse.report_header"] = ("Tôi quản lý kho nông sản chung của trang trại. Giá chợ hiện tại:\n", "I manage the farm's shared produce warehouse. Current market prices:\n"),
            ["warehouse.report_item_fmt"] = ("{0} {1} ({2}v)", "{0} {1} ({2}g)"),
            ["warehouse.almost_full_fmt"] = ("Kho gần đầy rồi: {0} chỉ còn {1}% chỗ trống.", "Storage is almost full: {0} only has {1}% space left."),
            ["warehouse.suggestion_fmt"] = ("Nên đưa bớt {0} ra chợ bán, để trong kho làm gì cho chật.", "You should sell some {0} at the market, no point hoarding it in storage."),
            ["warehouse.land_value_fmt"] = ("Giá trị đất trang trại ước tính: {0} vàng.", "Estimated farm land value: {0} gold."),

            // ---- Building/location labels (on-screen banner near landmarks, see BuildingLabelZone.cs) ----
            ["label.farmhouse"] = ("Nhà Nông Dân", "Farmer's House"),
            ["label.storage"] = ("Nhà Kho", "Storage"),
            ["label.town_hall"] = ("Tòa Thị Chính", "Town Hall"),
            ["label.police_post"] = ("Trụ Cảnh Sát", "Guard Post"),
            ["label.cow_pasture"] = ("Chuồng Bò", "Cow Pasture"),
            ["label.cowherd_house"] = ("Nhà Người Chăm Bò", "Cowherd's House"),
            ["label.horse_stable"] = ("Chuồng Ngựa", "Horse Stable"),
            ["label.stablehand_house"] = ("Nhà Người Chăm Ngựa", "Stablehand's House"),
            ["label.chicken_coop"] = ("Chuồng Gà", "Chicken Coop"),
            ["label.poultry_keeper_house"] = ("Nhà Người Chăm Gà", "Poultry Keeper's House"),
            ["label.mountain"] = ("Núi", "Mountain"),
            ["label.deep_forest"] = ("Rừng Sâu", "Deep Forest"),
            ["label.fields"] = ("Đồng Ruộng", "Fields"),
            ["label.water_tower"] = ("Tháp Nước", "Water Tower"),
            ["label.cemetery"] = ("Nghĩa Địa", "Cemetery"),
            ["label.cave"] = ("Hang Động", "Cave"),
            ["label.mine_shaft"] = ("Hầm Mỏ", "Mine Shaft"),
            ["label.ruins"] = ("Phế Tích", "Ruins"),
            ["label.sunflower_field"] = ("Cánh Đồng Hướng Dương", "Sunflower Field"),
            ["label.windmill"] = ("Cối Xay Gió", "Windmill"),
            ["label.watchtower"] = ("Tháp Canh", "Watchtower"),
            ["label.farm_worker_house"] = ("Nhà Người Làm Ruộng", "Field Worker's House"),
            ["label.sheep_pig_pen"] = ("Chuồng Cừu/Heo", "Sheep/Pig Pen"),
            ["label.orchard"] = ("Vườn Cây Ăn Quả", "Orchard"),
            ["label.vineyard"] = ("Vườn Nho", "Vineyard"),
            ["label.estate_worker_house"] = ("Nhà Người Làm Vườn", "Estate Worker's House"),
            ["label.steward_house"] = ("Nhà Quản Gia", "Steward's House"),
            ["label.repairman_house"] = ("Nhà Thợ Sửa Chữa", "Repairman's House"),
            ["label.warehouse_manager_house"] = ("Nhà Quản Lý Kho", "Warehouse Manager's House"),
            ["label.palace_guard_barracks"] = ("Doanh Trại Cấm Vệ", "Palace Guard Barracks"),
            ["label.great_vineyard"] = ("Vườn Nho Lớn", "Great Vineyard"),
            ["label.warehouse_district"] = ("Khu Nhà Kho", "Warehouse District"),
            ["label.production_district"] = ("Khu Sản Xuất", "Production District"),
            ["label.caretaker_dormitory"] = ("Nhà Ở Người Chăm Nuôi", "Caretaker Dormitory"),
            ["label.greenhouse"] = ("Nhà Kính", "Greenhouse"),
            ["label.kitchen"] = ("Bếp", "Kitchen"),
            ["label.lake"] = ("Hồ Nước", "Lake"),
            ["label.river"] = ("Sông", "River"),
            ["label.swamp"] = ("Đầm Lầy", "Swamp"),
            ["label.village"] = ("Làng", "Village"),
            ["label.city"] = ("Thành Phố", "City"),
            ["label.dryer"] = ("Lò Sấy", "Drying Kiln"),
            ["label.press"] = ("Máy Ép", "Press"),
            ["label.cheese_machine"] = ("Máy Làm Phô Mai", "Cheese Maker"),
            ["label.mayo_machine"] = ("Máy Mayonnaise", "Mayonnaise Machine"),
            ["label.loom"] = ("Máy Dệt", "Loom"),
            ["label.gate_north"] = ("CỔNG BẮC", "NORTH GATE"),
            ["label.gate_west"] = ("CỔNG TÂY", "WEST GATE"),
            ["label.gate_south"] = ("CỔNG NAM", "SOUTH GATE"),
            ["label.farm_welcome_sign"] = ("NÔNG TRẠI - CHÀO MỪNG", "THE FARM - WELCOME"),
        };
    }
}
