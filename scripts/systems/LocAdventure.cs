using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Ban dich UI cho phieu luu (kham pha/ruong/cot da/trum/ham nguc/bao tang), le hoi, nha & vat
    // nuoi, qua/cay khong lo - Loc.T tra cuu bang nay sau Loc.Strings va LocFarm.Strings.
    internal static class LocAdventure
    {
        public static readonly Dictionary<string, (string vi, string en)> Strings = new()
        {
            // ---- Kham pha / ruong / cot da / trum ----
            ["adv.discovered_fmt"] = ("Khám phá vùng mới: {0}! (+60 EXP)", "New area discovered: {0}! (+60 EXP)"),
            ["adv.chest_empty"] = ("Rương này đã trống.", "This chest is empty."),
            ["adv.chest_opened_fmt"] = ("Mở rương báu: +{0} vàng, {1}", "Treasure chest: +{0} gold, {1}"),
            ["adv.waystone_activated"] = ("Đã kích hoạt Cột Đá Dịch Chuyển! Bấm [E] ở bất kỳ cột nào để đi nhanh.", "Waystone activated! Press [E] at any waystone to fast travel."),
            ["adv.boss_defeated_fmt"] = ("Đã hạ {0}! Trùm sẽ trở lại sau {1} ngày.", "Defeated {0}! The boss returns in {1} days."),
            ["map.boss_respawn_fmt"] = ("{0} ngày", "{0}d"),
            ["waystone.title"] = ("CỘT ĐÁ DỊCH CHUYỂN", "WAYSTONES"),
            ["waystone.none"] = ("Chưa kích hoạt cột đá nào khác - hãy khám phá thêm!", "No other waystones activated yet - explore more!"),
            ["boss_label.boss_forest"] = ("Hang Vua Nấm", "Mushroom King's Lair"),
            ["boss_label.boss_mountain"] = ("Hang Yeti", "Yeti's Lair"),
            ["boss_label.boss_swamp"] = ("Hang Bạo Chúa Đầm Lầy", "Swamp Tyrant's Lair"),
            ["boss_label.boss_ruins"] = ("Tế Đàn Quỷ Vương", "Demon Lord's Altar"),
            ["boss_label.boss_cemetery"] = ("Mộ Chúa Tể", "Graveyard Lord's Tomb"),
            ["boss_label.boss_cave"] = ("Hang Rồng", "Dragon's Lair"),

            // ---- Ham nguc ----
            ["dungeon.floor_fmt"] = ("Hầm Ngục Cổ - Tầng {0}/{1}", "Ancient Dungeon - Floor {0}/{1}"),
            ["dungeon.boss_awake"] = ("Rồng Cổ Đại đã thức giấc!", "The Ancient Dragon awakens!"),

            // ---- Bao tang ----
            ["museum.title"] = ("BẢO TÀNG LÀNG", "VILLAGE MUSEUM"),
            ["museum.progress_fmt"] = ("Đã trưng bày {0}/{1} hiện vật.", "{0}/{1} items on display."),
            ["museum.next_reward_fmt"] = ("Mốc thưởng tiếp theo: {0} hiện vật.", "Next reward at {0} items."),
            ["museum.donate_btn"] = ("Quyên góp", "Donate"),
            ["museum.on_display"] = ("Đang trưng bày", "On display"),
            ["museum.donated_fmt"] = ("Bảo tàng đã nhận {0}. Cảm ơn bạn!", "The museum received {0}. Thank you!"),
            ["museum.reward_title"] = ("Quà cảm ơn từ Bảo Tàng", "A thank-you from the Museum"),
            ["museum.reward_body_fmt"] = ("Bộ sưu tập đã có {0} hiện vật nhờ bạn! Xin nhận chút quà cảm ơn.\n- Học Giả", "Thanks to you the collection now has {0} items! Please accept this gift.\n- The Scholar"),

            // ---- Nong trai mo rong ----
            ["farm.scarecrow_placed"] = ("Đã cắm Bù Nhìn - quạ sẽ tránh xa ruộng quanh đây.", "Scarecrow placed - crows will stay away from nearby fields."),
            ["farm.crows_fmt"] = ("Quạ đã ăn mất {0} cây trồng! Hãy cắm Bù Nhìn để bảo vệ ruộng.", "Crows ate {0} crop(s)! Place a Scarecrow to protect your fields."),
            ["farm.giant_fmt"] = ("Kỳ diệu! {0} cây đã mọc thành CÂY KHỔNG LỒ!", "Amazing! {0} crop(s) grew into GIANT crops!"),
            ["farm.season_wither_fmt"] = ("Hết mùa - {0} cây trái mùa đã tàn.", "The season ended - {0} out-of-season crop(s) withered."),
            ["animal.happy_pop"] = ("Vui quá!", "Happy!"),

            // ---- Nha ----
            ["home.title"] = ("NÂNG CẤP NHÀ", "HOUSE UPGRADE"),
            ["home.level_fmt"] = ("Nhà cấp {0}/{1} - thể lực tối đa +{2}, rương chứa {3} ô.", "House level {0}/{1} - max stamina +{2}, chest {3} slots."),
            ["home.herd_fmt"] = ("Độ vui đàn vật nuôi: {0}/100 ({1}) - vuốt ve [E] và giữ kho có thức ăn để chúng vui.", "Herd happiness: {0}/100 ({1}) - pet animals [E] and keep feed in storage."),
            ["home.next_fmt"] = ("Nâng lên cấp {0} cần:", "Upgrade to level {0} needs:"),
            ["home.benefit_fmt"] = ("Lợi ích: +{0} thể lực tối đa, +{1} ô rương.", "Benefit: +{0} max stamina, +{1} chest slots."),
            ["home.upgrade_btn"] = ("Nâng cấp", "Upgrade"),
            ["home.max_level"] = ("Nhà đã đạt cấp tối đa!", "Your house is fully upgraded!"),
            ["home.missing_fmt"] = ("Chưa đủ: {0}", "Missing: {0}"),
            ["home.upgraded_fmt"] = ("Nhà đã lên cấp {0}! +{1} thể lực tối đa, rương {2} ô.", "House upgraded to level {0}! +{1} max stamina, chest {2} slots."),
            ["home.chest_title"] = ("RƯƠNG CHỨA ĐỒ", "STORAGE CHEST"),
            ["home.chest_info_fmt"] = ("Trái: túi đồ - Phải: rương ({0}/{1} loại).", "Left: inventory - Right: chest ({0}/{1} kinds)."),
            ["home.store_btn"] = ("Cất", "Store"),
            ["home.take_btn"] = ("Lấy", "Take"),
            ["home.chest_full"] = ("Rương đã đầy - nâng cấp nhà để thêm chỗ.", "Chest is full - upgrade your house for more room."),
            ["home.mood.great"] = ("Rất vui", "Very happy"),
            ["home.mood.good"] = ("Vui", "Happy"),
            ["home.mood.ok"] = ("Bình thường", "Okay"),
            ["home.mood.sad"] = ("Buồn", "Sad"),
            ["journal.home_fmt"] = ("Nhà cấp {0}/{1} - Đàn vật nuôi: {2}/100 ({3})", "House level {0}/{1} - Herd: {2}/100 ({3})"),
            ["journal.adventure_fmt"] = ("Đã khám phá {0}/{1} vùng - Mở {2} rương - Hầm ngục sâu nhất: tầng {3} - Bảo tàng: {4} hiện vật",
                "Explored {0}/{1} areas - {2} chests opened - Deepest dungeon floor: {3} - Museum: {4} items"),

            // ---- Le hoi ----
            ["fest.board_title"] = ("BẢNG LỄ HỘI", "FESTIVAL BOARD"),
            ["fest.name.EggHunt"] = ("Lễ Hội Săn Trứng", "Egg Hunt Festival"),
            ["fest.name.FishingContest"] = ("Hội Thi Câu Cá", "Fishing Contest"),
            ["fest.name.HarvestFair"] = ("Hội Chợ Nông Sản", "Harvest Fair"),
            ["fest.name.StarNight"] = ("Lễ Hội Ánh Sao", "Night of Stars"),
            ["fest.desc.EggHunt"] = ("Trứng sơn màu được giấu quanh quảng trường làng. Bạn có 2 phút để nhặt càng nhiều càng tốt!", "Painted eggs are hidden around the village square. You have 2 minutes to collect as many as you can!"),
            ["fest.desc.FishingContest"] = ("Câu được càng nhiều cá trong 5 phút càng tốt - hồ, sông đều được tính!", "Catch as many fish as you can in 5 minutes - any lake or river counts!"),
            ["fest.desc.HarvestFair"] = ("Ban giám khảo chấm 3 món nông sản/sản phẩm giá trị nhất trong túi đồ của bạn (không lấy mất đồ).", "The judges score the 3 most valuable produce/products in your inventory (you keep them)."),
            ["fest.desc.StarNight"] = ("Mỗi người được bốc thăm một \"bạn bí mật\" - hãy tặng quà cho người đó hôm nay!", "Everyone draws a \"secret friend\" - give them a gift today!"),
            ["fest.mail_title_fmt"] = ("Hôm nay: {0}!", "Today: {0}!"),
            ["fest.mail_where"] = ("Hãy tới Bảng Lễ Hội ở quảng trường thị trấn và bấm [E] để tham gia.", "Go to the Festival Board in the town square and press [E] to join."),
            ["fest.next_fmt"] = ("Lễ hội tiếp theo: {0} - ngày {1} mùa {2} (còn {3} ngày).", "Next festival: {0} - {2} {1} (in {3} days)."),
            ["fest.already_done"] = ("Bạn đã tham gia lễ hội này năm nay rồi. Hẹn năm sau!", "You already took part this year. See you next year!"),
            ["fest.running_fmt"] = ("Đang diễn ra - còn {0} giây!", "In progress - {0} seconds left!"),
            ["fest.time_left_fmt"] = ("Lễ hội: còn {0} giây!", "Festival: {0} seconds left!"),
            ["fest.egg_start"] = ("BẮT ĐẦU! Chạy quanh quảng trường nhặt trứng!", "GO! Run around the square collecting eggs!"),
            ["fest.egg_result_fmt"] = ("Hết giờ! Bạn nhặt được {0} quả trứng.", "Time's up! You found {0} eggs."),
            ["fest.fish_start"] = ("BẮT ĐẦU! Ra hồ hoặc sông câu cá ngay!", "GO! Head to a lake or river and fish!"),
            ["fest.fish_result_fmt"] = ("Hết giờ thi! Bạn câu được {0} con cá.", "Contest over! You caught {0} fish."),
            ["fest.fair_need"] = ("Bạn chưa có nông sản/sản phẩm nào trong túi để dự thi.", "You have no produce or products in your inventory to enter."),
            ["fest.fair_result_fmt"] = ("Giám khảo chấm: {0}\nTổng điểm: {1} - {2}", "Judged: {0}\nTotal score: {1} - {2}"),
            ["fest.place1"] = ("GIẢI NHẤT! (+1500 vàng + Cúp Hội Chợ)", "FIRST PLACE! (+1500 gold + Fair Trophy)"),
            ["fest.place2"] = ("Giải nhì (+600 vàng)", "Second place (+600 gold)"),
            ["fest.place3"] = ("Giải ba (+200 vàng)", "Third place (+200 gold)"),
            ["fest.star_assigned_fmt"] = ("Bạn bí mật của bạn là: {0}. Hãy tặng quà [G] cho họ hôm nay rồi quay lại đây!", "Your secret friend is: {0}. Give them a gift [G] today, then come back!"),
            ["fest.star_remind_fmt"] = ("Bạn chưa tặng quà cho {0} hôm nay.", "You haven't given {0} a gift today."),
            ["fest.star_done_fmt"] = ("{0} rất cảm động vì món quà! Bạn nhận 800 vàng và Quà Đông Chí.", "{0} was touched by your gift! You receive 800 gold and a Solstice Gift."),
            ["fest.prize_title"] = ("Giải thưởng lễ hội", "Festival prize"),
            ["fest.prize_body"] = ("Túi đồ của bạn đầy nên ban tổ chức gửi giải thưởng qua thư.", "Your inventory was full, so the organizers mailed your prize."),

            // ---- Ham mo ----
            ["label.mine_coal"] = ("Mỏ Than", "Coal Mine"),
            ["label.mine_crystal"] = ("Hang Pha Lê", "Crystal Cave"),
            ["label.mine_gold"] = ("Mỏ Vàng Cổ", "Ancient Gold Mine"),
            ["label.mine_forest"] = ("Mỏ Đồng Rừng Sâu", "Deep Forest Copper Mine"),
            ["label.mine_swamp"] = ("Mỏ Than Bùn", "Peat Coal Mine"),
            ["label.mine_river"] = ("Mỏ Bạc Ven Sông", "Riverside Silver Mine"),
            ["label.mine_lake"] = ("Hang Ngọc Hồ", "Lake Gem Grotto"),
            ["label.mine_cemetery"] = ("Mỏ Xương Cổ", "Ancient Bone Mine"),
            ["label.mine_field"] = ("Mỏ Đá Đồng Bằng", "Plains Quarry"),
            ["mine.floor_fmt"] = ("{0} - Tầng {1}/{2}: {3}", "{0} - Floor {1}/{2}: {3}"),
            ["mine.need_pick_fmt"] = ("cần {0}", "needs {0}"),
            ["mine.too_weak_fmt"] = ("Cuốc chim quá yếu để đào {0} - cần {1} trở lên.", "Your pickaxe is too weak for {0} - needs {1} or better."),

            // ---- Bang ten ----
            ["label.dungeon"] = ("Hầm Ngục Cổ", "Ancient Dungeon"),
            ["label.museum"] = ("Bảo Tàng", "Museum"),
            ["label.festival_board"] = ("Bảng Lễ Hội", "Festival Board"),
            ["label.house_board"] = ("Nâng Cấp Nhà", "House Upgrade"),
            ["label.ws_farm"] = ("Nông Trại", "Farm"),
        };
    }
}
