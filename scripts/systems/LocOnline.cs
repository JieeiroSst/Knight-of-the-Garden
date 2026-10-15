using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Ban dich UI cho phan ONLINE (tao nhan vat, chat, giao dich, cho, ket noi, dong bo kinh te/thoi gian).
    internal static class LocOnline
    {
        public static readonly Dictionary<string, (string vi, string en)> Strings = new()
        {
            // ---- Tao nhan vat ----
            ["cc.title"] = ("TẠO NHÂN VẬT", "CREATE YOUR CHARACTER"),
            ["cc.name"] = ("Tên nhân vật (người khác sẽ thấy)", "Character name (visible to others)"),
            ["cc.name_hint"] = ("3-20 ký tự", "3-20 characters"),
            ["cc.name_short"] = ("Tên phải có ít nhất 3 ký tự.", "Name must be at least 3 characters."),
            ["cc.gender"] = ("Giới tính", "Gender"),
            ["cc.male"] = ("Nam", "Male"),
            ["cc.female"] = ("Nữ", "Female"),
            ["cc.style"] = ("Kiểu nhân vật", "Style"),
            ["cc.skin"] = ("Màu da", "Skin"),
            ["cc.hair"] = ("Màu tóc", "Hair"),
            ["cc.outfit"] = ("Màu trang phục", "Outfit"),
            ["cc.save"] = ("Bắt đầu phiêu lưu", "Start adventure"),
            ["cc.saving"] = ("Đang lưu...", "Saving..."),
            ["login.loading_character"] = ("Đang tải nhân vật...", "Loading character..."),

            // ---- Ket noi / thoi gian / kinh te ----
            ["net.no_connection"] = ("Không kết nối được server.", "Can't reach the server."),
            ["net.server_error"] = ("Server báo lỗi, thử lại sau.", "Server error, please try again."),
            ["net.kicked_other_login"] = ("Tài khoản vừa đăng nhập ở nơi khác.", "Your account logged in elsewhere."),
            ["net.need_character"] = ("Cần tạo nhân vật trước khi vào thế giới chung.", "Create a character before joining the shared world."),
            ["net.joined_fmt"] = ("Đã vào {0} - kênh {1}.", "Joined {0} - channel {1}."),
            ["net.zone.farm"] = ("Nông trại (riêng tư)", "Farm (private)"),
            ["net.zone.wild"] = ("Vùng hoang dã", "Wilderness"),
            ["time.caught_up_fmt"] = ("Bạn vắng mặt {0} ngày - nhân công đã chăm nông trại giúp bạn.", "You were away {0} day(s) - the farmhands looked after your farm."),
            ["econ.rejected_fmt"] = ("Server từ chối giao dịch: {0}", "Server rejected the action: {0}"),
            ["mail.ach_body_paid_fmt"] = ("Chúc mừng bạn đạt thành tựu \"{0}\"! Đã cộng {1} vàng vào túi.", "Congratulations on \"{0}\"! {1} gold has been added."),

            // ---- Chat ----
            ["chat.placeholder"] = ("Nhập tin... (/w Tên nội dung = nhắn riêng, /kenh 2 = đổi kênh)", "Type... (/w Name text = whisper, /ch 2 = switch channel)"),
            ["chat.offline"] = ("Mất kết nối - đang thử lại...", "Disconnected - retrying..."),
            ["chat.private_farm"] = ("Nông trại riêng - [Enter] chat, ra thị trấn để gặp người chơi khác", "Private farm - [Enter] chat, visit town to meet others"),
            ["chat.status_fmt"] = ("{0} - kênh {1} - {2} người", "{0} - channel {1} - {2} players"),
            ["chat.whisper_usage"] = ("Cách dùng: /w Tên nội dung", "Usage: /w Name message"),
            ["chat.no_zone"] = ("Bạn đang ở nông trại riêng - chỉ nhắn riêng được (/w Tên nội dung).", "You're on your private farm - only whispers work (/w Name message)."),

            // ---- Nguoi choi khac / giao dich ----
            ["pa.trade"] = ("Giao dịch", "Trade"),
            ["pa.whisper"] = ("Nhắn riêng", "Whisper"),
            ["pa.whisper_hint_fmt"] = ("Bấm [Enter] rồi gõ: /w {0} nội dung", "Press [Enter] and type: /w {0} message"),
            ["trade.title_fmt"] = ("GIAO DỊCH VỚI {0}", "TRADING WITH {0}"),
            ["trade.invite_fmt"] = ("{0} muốn giao dịch với bạn.", "{0} wants to trade with you."),
            ["trade.invited_fmt"] = ("Đã gửi lời mời giao dịch tới {0}.", "Trade invite sent to {0}."),
            ["trade.accept"] = ("Đồng ý", "Accept"),
            ["trade.decline"] = ("Từ chối", "Decline"),
            ["trade.your_bag"] = ("Túi của bạn (bấm để thêm)", "Your bag (click to add)"),
            ["trade.you_offer"] = ("Bạn đưa (bấm để bớt)", "You give (click to remove)"),
            ["trade.they_offer"] = ("Họ đưa", "They give"),
            ["trade.gold"] = ("Vàng:", "Gold:"),
            ["trade.update_offer"] = ("Cập nhật đề nghị", "Update offer"),
            ["trade.lock"] = ("Khóa", "Lock"),
            ["trade.confirm"] = ("Xác nhận", "Confirm"),
            ["trade.cancel"] = ("Hủy giao dịch", "Cancel trade"),
            ["trade.waiting_accept"] = ("Đang chờ đối phương đồng ý...", "Waiting for the other player..."),
            ["trade.state_fmt"] = ("Bạn: {0} - Họ: {1} {2}  (cả 2 khóa rồi mới xác nhận được)", "You: {0} - Them: {1} {2}  (both must lock before confirming)"),
            ["trade.locked"] = ("đã khóa", "locked"),
            ["trade.editing"] = ("đang chọn", "editing"),
            ["trade.they_confirmed"] = ("- họ đã xác nhận", "- they confirmed"),
            ["trade.done"] = ("Giao dịch thành công!", "Trade complete!"),
            ["trade.cancelled"] = ("Giao dịch đã bị hủy.", "Trade cancelled."),

            // ---- Cho ----
            ["market.title"] = ("CHỢ NGƯỜI CHƠI", "PLAYER MARKET"),
            ["market.tab.Browse"] = ("Mua", "Buy"),
            ["market.tab.Sell"] = ("Đăng bán", "Sell"),
            ["market.tab.Mine"] = ("Tin của tôi", "My listings"),
            ["market.search_hint"] = ("Tìm vật phẩm (vd: cà chua, kim cương)", "Search items"),
            ["market.search_btn"] = ("Tìm", "Search"),
            ["market.empty"] = ("Chưa có tin rao phù hợp.", "No matching listings."),
            ["market.row_fmt"] = ("{0} x{1} - {2} vàng - {3}", "{0} x{1} - {2} gold - {3}"),
            ["market.buy_btn"] = ("Mua", "Buy"),
            ["market.bought_fmt"] = ("Đã mua {0} x{1}.", "Bought {0} x{1}."),
            ["market.sell_hint"] = ("Đồ đăng bán được giữ ở chợ tới khi có người mua (7 ngày). Thuế 5% trừ vào tiền bán.", "Listed items are held by the market until sold (7 days). 5% tax on sales."),
            ["market.post_btn"] = ("Đăng", "Post"),
            ["market.posted"] = ("Đã đăng bán.", "Listing posted."),
            ["market.no_listings"] = ("Bạn chưa đăng bán gì.", "You have no listings."),
            ["market.mine_row_fmt"] = ("{0} x{1} - {2} vàng - {3}", "{0} x{1} - {2} gold - {3}"),
            ["market.cancel_btn"] = ("Gỡ tin", "Cancel"),
            ["market.status.active"] = ("đang bán", "active"),
            ["market.status.sold"] = ("đã bán", "sold"),
            ["market.status.cancelled"] = ("đã gỡ", "cancelled"),
            ["market.status.expired"] = ("hết hạn", "expired"),
            ["market.sold_fmt"] = ("Chợ: đã bán {0} x{1}, nhận {2} vàng!", "Market: sold {0} x{1}, earned {2} gold!"),
            ["market.expired_fmt"] = ("Tin rao {0} x{1} đã hết hạn - đồ đã trả lại túi.", "Listing {0} x{1} expired - items returned."),
            ["label.market_board"] = ("Chợ Người Chơi", "Player Market"),
        };
    }
}
