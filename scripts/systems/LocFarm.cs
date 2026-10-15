using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Ban dich UI cho cac he thong nong trai mo rong (The luc/Ky nang/Cau ca/Che tao/Giao hang/
    // Tang qua/Thu/Thanh tuu/Hai luom/U phan) - tach rieng khoi Loc.cs cho gon, Loc.T tu tra cuu
    // them bang nay khi khong tim thay khoa trong bang chinh.
    internal static class LocFarm
    {
        public static readonly Dictionary<string, (string vi, string en)> Strings = new()
        {
            // ---- Mua / thoi tiet / HUD ----
            ["season.Spring"] = ("Xuân", "Spring"),
            ["season.Summer"] = ("Hạ", "Summer"),
            ["season.Fall"] = ("Thu", "Fall"),
            ["season.Winter"] = ("Đông", "Winter"),
            ["hud.stamina_fmt"] = ("Thể lực: {0}/{1}", "Stamina: {0}/{1}"),
            ["hud.exhausted"] = ("(Kiệt sức!)", "(Exhausted!)"),
            ["hud.season_fmt"] = ("Mùa {0} - ngày {1}/28 - {2}", "{0} - day {1}/28 - {2}"),
            ["hud.rain"] = ("Mưa", "Rain"),
            ["hud.sunny"] = ("Nắng", "Sunny"),
            ["hud.festival"] = ("[Lễ hội!]", "[Festival!]"),
            ["hud.mail_fmt"] = ("Có {0} thư mới - bấm [K]", "{0} new letter(s) - press [K]"),
            ["stamina.too_tired"] = ("Quá mệt! Hãy ăn gì đó hoặc về giường ngủ.", "Too tired! Eat something or go to bed."),
            ["pickup.got_fmt"] = ("Nhận được: {0} x{1}", "Got: {0} x{1}"),
            ["bed.slept"] = ("Bạn đã ngủ một giấc ngon - thể lực và máu hồi đầy. Đã lưu game.", "You slept well - stamina and health fully restored. Game saved."),

            // ---- Nong trai ----
            ["farm.harvested_fmt"] = ("Thu hoạch: {0} x{1}", "Harvested: {0} x{1}"),
            ["farm.tilled_fmt"] = ("Đã cuốc {0} ô đất mới.", "Tilled {0} new plot(s)."),
            ["farm.cannot_till"] = ("Không thể cuốc đất ở đây.", "Can't till here."),
            ["animal.petted"] = ("Bạn vuốt ve con vật - nó có vẻ rất vui!", "You pet the animal - it looks happy!"),
            ["animal.already_petted"] = ("Hôm nay bạn đã vuốt ve con vật này rồi.", "You already pet this animal today."),

            // ---- Ky nang ----
            ["skill.Farming"] = ("Trồng Trọt", "Farming"),
            ["skill.Animal"] = ("Chăn Nuôi", "Animal Care"),
            ["skill.Fishing"] = ("Câu Cá", "Fishing"),
            ["skill.Mining"] = ("Khai Khoáng", "Mining"),
            ["skill.Foraging"] = ("Hái Lượm", "Foraging"),
            ["skill.levelup_fmt"] = ("Kỹ năng {0} lên cấp {1}!", "{0} skill reached level {1}!"),
            ["skill.perk.Farming"] = ("Mỗi cấp: +2% cơ hội thu thêm nông sản, chất lượng cao hơn, nông sản bán +2%, ít tốn thể lực hơn.",
                "Per level: +2% extra crop chance, better quality, crops sell +2%, less stamina used."),
            ["skill.perk.Animal"] = ("Mỗi cấp: sản phẩm chăn nuôi bán +3%. Vuốt ve vật nuôi và nhặt trứng/sữa/len để lên cấp.",
                "Per level: animal products sell +3%. Pet animals and collect eggs/milk/wool to level up."),
            ["skill.perk.Fishing"] = ("Mỗi cấp: vùng bắt cá rộng hơn 4%, cá bán +3%, mở khóa loài cá hiếm.",
                "Per level: 4% wider catch zone, fish sell +3%, unlocks rarer fish."),
            ["skill.perk.Mining"] = ("Mỗi cấp: +5% cơ hội đào được gấp đôi quặng, ít tốn thể lực hơn.",
                "Per level: +5% chance of double ore, less stamina used."),
            ["skill.perk.Foraging"] = ("Mỗi cấp: +5% cơ hội nhặt gấp đôi đồ hái lượm/gỗ.",
                "Per level: +5% chance of double forage/wood."),

            // ---- Cau ca ----
            ["fish.need_water"] = ("Cần đứng gần hồ/sông để câu cá.", "Stand near a lake or river to fish."),
            ["fish.depleted"] = ("Khu vực này không còn cá để câu, chờ quần thể phục hồi.", "No fish left here - wait for the population to recover."),
            ["fish.waiting"] = ("Đang chờ cá cắn câu...", "Waiting for a bite..."),
            ["fish.bite"] = ("Cá cắn câu! Bấm [Space] khi vạch vàng nằm trong vùng xanh!", "A bite! Press [Space] when the marker is in the green zone!"),
            ["fish.bait_used"] = ("(dùng mồi)", "(bait used)"),
            ["fish.too_early"] = ("Giật cần quá sớm - cá sợ bỏ đi mất.", "Reeled too early - the fish swam away."),
            ["fish.escaped"] = ("Cá đã thoát mất!", "The fish got away!"),
            ["fish.moved_away"] = ("Bạn đã rời chỗ câu.", "You left your fishing spot."),
            ["fish.caught_fmt"] = ("Câu được: {0}!", "Caught: {0}!"),

            // ---- Che tao ----
            ["craft.title"] = ("BÀN CHẾ TẠO", "CRAFTING BENCH"),
            ["craft.craft_btn"] = ("Chế tạo", "Craft"),
            ["craft.locked_fmt"] = ("Khóa - cần {0} cấp {1}", "Locked - requires {0} level {1}"),
            ["craft.crafted_fmt"] = ("Đã chế tạo: {0} x{1}", "Crafted: {0} x{1}"),
            ["cooking.stamina_fmt"] = (" (+{0} thể lực)", " (+{0} stamina)"),

            // ---- Giao hang ----
            ["ship.title"] = ("THÙNG GIAO HÀNG - tự bán khi sang ngày mới", "SHIPPING BIN - sold automatically at the start of a new day"),
            ["ship.inventory_header"] = ("Túi đồ (giá bán hiện tại)", "Inventory (current price)"),
            ["ship.bin_header"] = ("Trong thùng", "In the bin"),
            ["ship.all_btn"] = ("Hết", "All"),
            ["ship.take_back_btn"] = ("Lấy lại", "Take back"),
            ["ship.all_crops_btn"] = ("Bỏ hết nông sản", "Ship all crops"),
            ["ship.total_fmt"] = ("Dự kiến thu: {0} vàng", "Expected income: {0} gold"),
            ["ship.paid_fmt"] = ("Thùng giao hàng đã bán được {0} vàng!", "The shipping bin earned {0} gold!"),

            // ---- Tang qua / than thiet ----
            ["gift.title_fmt"] = ("TẶNG QUÀ CHO {0}", "GIVE A GIFT TO {0}"),
            ["gift.hearts_fmt"] = ("{0}/10 tim", "{0}/10 hearts"),
            ["gift.bday_today"] = ("- HÔM NAY LÀ SINH NHẬT! (quà x5)", "- BIRTHDAY TODAY! (gift x5)"),
            ["gift.already_today"] = ("Hôm nay bạn đã tặng quà cho người này rồi.", "You already gave this person a gift today."),
            ["gift.give_btn"] = ("Tặng", "Give"),
            ["gift.no_npc"] = ("Hãy đứng gần một người dân để tặng quà [G].", "Stand next to a villager to give a gift [G]."),
            ["friend.bday_fmt"] = ("Sinh nhật: ngày {1} mùa {0}", "Birthday: {0} {1}"),
            ["friend.heart_up_fmt"] = ("Tình bạn với {0} đạt {1} tim!", "Friendship with {0} reached {1} hearts!"),
            ["friend.already_gifted_fmt"] = ("Hôm nay bạn đã tặng quà cho {0} rồi.", "You already gave {0} a gift today."),
            ["friend.cannot_gift"] = ("Không thể tặng món này.", "You can't give this."),
            ["friend.gift_given_fmt"] = ("{0} nhận {1}: {2}", "{0} received {1}: {2}"),
            ["friend.bday_bonus"] = ("(Quà sinh nhật!)", "(Birthday gift!)"),
            ["friend.taste.Loved"] = ("\"Ôi, ta thích món này nhất!\"", "\"Oh, I love this!\""),
            ["friend.taste.Liked"] = ("\"Cảm ơn nhé, ta thích lắm.\"", "\"Thanks, I like it.\""),
            ["friend.taste.Neutral"] = ("\"Cảm ơn.\"", "\"Thank you.\""),
            ["friend.taste.Disliked"] = ("\"Ờ... ta không thích cái này lắm.\"", "\"Uh... I don't really like this.\""),
            ["npc.gift_hint"] = ("[G] Tặng quà", "[G] Give a gift"),
            ["npc.bday_line"] = ("Hôm nay là sinh nhật ta đấy!", "It's my birthday today!"),

            // ---- Nhat ky ----
            ["journal.title"] = ("NHẬT KÝ", "JOURNAL"),
            ["journal.tab.Skills"] = ("Kỹ năng", "Skills"),
            ["journal.tab.Friends"] = ("Bạn bè", "Friends"),
            ["journal.tab.Achievements"] = ("Thành tựu", "Achievements"),
            ["journal.tab.Mail"] = ("Thư", "Mail"),
            ["journal.close_btn"] = ("Đóng [K]", "Close [K]"),
            ["journal.skill_fmt"] = ("{0} - cấp {1}/{2}", "{0} - level {1}/{2}"),
            ["journal.stamina_bonus_fmt"] = ("Thể lực tối đa +{0} nhờ cấp kỹ năng.", "Max stamina +{0} from skill levels."),
            ["journal.no_friends"] = ("Chưa quen ai - hãy trò chuyện với người dân [E].", "No friends yet - talk to villagers [E]."),
            ["journal.gifted_today"] = ("(đã tặng quà hôm nay)", "(gifted today)"),
            ["journal.ach_progress_fmt"] = ("Đã đạt {0}/{1} thành tựu", "{0}/{1} achievements unlocked"),
            ["journal.no_mail"] = ("Hộp thư trống.", "Your mailbox is empty."),
            ["journal.mail_day_fmt"] = ("ngày {0}", "day {0}"),
            ["journal.claim_fmt"] = ("Nhận: {0}", "Claim: {0}"),
            ["journal.back_btn"] = ("< Quay lại", "< Back"),

            // ---- Thu ----
            ["mail.new_fmt"] = ("Có thư mới: {0}", "New mail: {0}"),
            ["mail.inventory_full"] = ("Túi đồ đã đầy!", "Your inventory is full!"),
            ["mail.welcome_title"] = ("Chào mừng về vườn!", "Welcome to the farm!"),
            ["mail.welcome_body"] = (
                "Chào cháu,\n\nCả làng mừng vì cháu đã về tiếp quản khu vườn. Vài mẹo nhỏ:\n- Dùng công cụ tốn thể lực; ăn uống hoặc ngủ trên giường (tầng 2 nhà) để hồi.\n- Bỏ nông sản vào Thùng Giao Hàng cạnh nhà - sang ngày mới sẽ tự bán.\n- Bàn Chế Tạo ở trong nhà và cạnh Bếp.\n- Trò chuyện [E] và tặng quà [G] để kết thân với dân làng.\n- Mở Nhật Ký [K] để xem kỹ năng, bạn bè, thành tựu và thư.\n\nGửi kèm ít trà thảo mộc cho cháu lấy sức.\n- Ông Già Làng",
                "Dear child,\n\nThe whole village is glad you came back to take over the farm. A few tips:\n- Tools use stamina; eat or sleep in your bed (2nd floor) to recover.\n- Put produce in the Shipping Bin next to the house - it sells at the start of a new day.\n- There is a Crafting Bench inside the house and next to the Kitchen.\n- Talk [E] and give gifts [G] to befriend villagers.\n- Open the Journal [K] for skills, friends, achievements and mail.\n\nHere is some herbal tea to keep your strength up.\n- The Village Elder"),
            ["mail.season_title_fmt"] = ("Quà đầu mùa {0}", "{0} starter gift"),
            ["mail.season_body"] = ("Mùa mới đến rồi! Gửi cháu ít hạt giống hợp mùa để gieo trồng.\n- Ông Già Làng",
                "A new season is here! Some seasonal seeds for you to plant.\n- The Village Elder"),
            ["mail.ship_title_fmt"] = ("Báo cáo giao hàng: +{0} vàng", "Shipping report: +{0} gold"),
            ["mail.ship_body_fmt"] = ("Hàng đã bán hôm qua:\n{0}\n\nTổng cộng: {1} vàng (đã cộng vào túi).",
                "Items sold yesterday:\n{0}\n\nTotal: {1} gold (already added)."),
            ["mail.bday_title_fmt"] = ("Hôm nay sinh nhật {0}", "{0}'s birthday is today"),
            ["mail.bday_body_fmt"] = ("Đừng quên hôm nay là sinh nhật của {0}! Một món quà hợp ý sẽ được trân trọng gấp 5 lần.",
                "Don't forget it's {0}'s birthday today! A thoughtful gift is worth 5x as much."),
            ["mail.friend_gift_title_fmt"] = ("Quà từ {0}", "A gift from {0}"),
            ["mail.friend_gift_body_fmt"] = ("Cảm ơn vì đã luôn quan tâm. Tặng bạn chút quà nhỏ!\n- {0}",
                "Thanks for always being kind. Here's a little something!\n- {0}"),
            ["mail.ach_title"] = ("Phần thưởng thành tựu", "Achievement reward"),
            ["mail.ach_body_fmt"] = ("Chúc mừng bạn đạt thành tựu \"{0}\"! Phần thưởng: {1} vàng.",
                "Congratulations on unlocking \"{0}\"! Reward: {1} gold."),
            ["ach.unlocked_fmt"] = ("Thành tựu mới: {0}!", "Achievement unlocked: {0}!"),

            // ---- U phan ----
            ["compost.need_organic"] = ("Cần đồ hữu cơ (nông sản, đồ hái lượm, rong, thảo dược) để ủ phân.", "Needs organic matter (crops, forage, pondweed, herbs) to compost."),
            ["compost.added_fmt"] = ("Đã bỏ {0} món vào thùng ủ ({1}/{2}).", "Added {0} item(s) to the compost bin ({1}/{2})."),
            ["compost.started_fmt"] = ("Thùng đã đầy - bắt đầu ủ, {0} ngày nữa xong.", "Bin full - composting, ready in {0} day(s)."),
            ["compost.busy_fmt"] = ("Đang ủ phân, còn {0} ngày.", "Composting, {0} day(s) left."),
            ["compost.collected_fmt"] = ("Lấy ra {0} {1}.", "Collected {0} {1}."),

            // ---- Bang ten cong trinh ----
            ["label.shipping_bin"] = ("Thùng Giao Hàng", "Shipping Bin"),
            ["label.mailbox"] = ("Hộp Thư", "Mailbox"),
            ["label.compost_bin"] = ("Thùng Ủ Phân", "Compost Bin"),
            ["label.workbench"] = ("Bàn Chế Tạo", "Crafting Bench"),

            // ---- Phim tat (SettingsUI) ----
            ["settings.ctrl.gift"] = ("Tặng quà cho người dân đứng gần", "Give a gift to a nearby villager"),
            ["settings.ctrl.journal"] = ("Nhật Ký (kỹ năng / bạn bè / thành tựu / thư)", "Journal (skills / friends / achievements / mail)"),
        };
    }
}
