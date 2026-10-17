using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Noi dung man hinh HUONG DAN CHOI ([F1], xem GuideUI) + cac buoc lam quen cho nguoi moi (GuideTracker).
    // Than bai dung BBCode cua RichTextLabel ([b], [color]).
    internal static class LocGuide
    {
        // Thu tu chu de hien trong danh sach ben trai.
        public static readonly string[] Topics =
        {
            "start", "controls", "farm", "animals", "survival", "craft", "explore", "combat",
            "pvp", "base", "bounty", "social", "events", "dungeon", "taming", "trade",
        };

        public static readonly Dictionary<string, (string vi, string en)> Strings = new()
        {
            ["guide.title"] = ("HƯỚNG DẪN CHƠI", "HOW TO PLAY"),
            ["guide.hint"] = ("[F1] mở / đóng hướng dẫn bất cứ lúc nào", "[F1] open / close this guide anytime"),
            ["guide.close"] = ("Đóng", "Close"),
            ["guide.reset_steps"] = ("Hiện lại các bước cho người mới", "Show beginner steps again"),
            ["settings.ctrl.guide"] = ("Hướng dẫn chơi (từng chủ đề)", "How to play (by topic)"),

            ["guide.t.start"] = ("Bắt đầu", "Getting started"),
            ["guide.b.start"] = (
                "[b]Hiệp Sĩ Về Vườn[/b] là game [b]sinh tồn - nông trại - khám phá thế giới mở[/b] online.\n\n" +
                "• Bạn có [b]1 nông trại riêng[/b] ở toạ độ cố định (cách người khác ≥ 5 km). Mỗi loài vật nuôi có 2 con.\n" +
                "• Thế giới [b]không giới hạn[/b]: đi càng xa nông trại càng nguy hiểm, quái mạnh hơn nhưng đồ xịn hơn.\n" +
                "• Người chơi khác có thể [b]giúp đỡ[/b] hoặc [b]tấn công cướp đồ[/b] bạn ở mọi nơi trừ khu chợ.\n\n" +
                "[color=#e8c060]Gợi ý ngày đầu:[/color]\n" +
                "1. Cuốc đất, gieo hạt, tưới nước (xem mục Nông trại).\n" +
                "2. Bỏ nông sản vào [b]Thùng Giao Hàng[/b] trước nhà để bán lấy vàng.\n" +
                "3. Giữ no và đủ nước (thanh No/Nước trên màn hình).\n" +
                "4. Mở bản đồ [M], kích hoạt Cột Đá dịch chuyển để đi lại nhanh.\n" +
                "5. Làm Bàn Chế Tác, chế công cụ tốt hơn, rồi đi khám phá.\n\n" +
                "Game tự lưu khi ngủ trên giường và khi thoát; bấm [F5] để lưu tay.",
                "[b]Hiep Si Ve Vuon[/b] is an online [b]survival - farming - open world[/b] game.\n\n" +
                "• You own [b]a private farm[/b] at fixed coordinates (≥ 5 km from others). Each livestock species has 2 animals.\n" +
                "• The world is [b]endless[/b]: the farther from farms, the more dangerous - tougher monsters, better loot.\n" +
                "• Other players can [b]help[/b] you or [b]attack and rob[/b] you anywhere except markets.\n\n" +
                "[color=#e8c060]First-day tips:[/color]\n" +
                "1. Till, plant, water (see Farming).\n" +
                "2. Put crops in the [b]Shipping Bin[/b] in front of your house to sell them.\n" +
                "3. Stay fed and hydrated (Food/Water on screen).\n" +
                "4. Open the map [M] and activate Waystones for fast travel.\n" +
                "5. Build a Workbench, craft better tools, then go exploring.\n\n" +
                "The game saves when you sleep in a bed and when you quit; press [F5] to save manually."),

            ["guide.t.controls"] = ("Điều khiển", "Controls"),
            ["guide.b.controls"] = (
                "[b]Di chuyển[/b]: W A S D (theo hướng camera)\n" +
                "[b]Nhìn[/b]: chuột (xoay tự do, tâm ngắm giữa màn hình)\n" +
                "[b][V][/b]: đổi góc qua vai ↔ góc nhìn thứ nhất · lăn chuột: phóng to / thu nhỏ\n" +
                "[b]Giữ [Alt] hoặc bấm [Esc][/b]: hiện con trỏ chuột (bấm vào game để khoá lại)\n\n" +
                "[b]Chuột trái / J[/b]: đánh · [b]Space / chuột phải[/b]: dùng công cụ đang cầm\n" +
                "[b]E[/b]: tương tác (nói chuyện, mở rương, cửa, nhặt đồ, cho thú ăn...)\n" +
                "[b]R[/b]: cưỡi / xuống ngựa, thuyền · Space trên ngựa: phi nước đại\n" +
                "[b]1 / 2 / 3[/b]: kỹ năng Lướt / Chém Xoáy / Hồi Phục\n\n" +
                "[b]I[/b] túi đồ · [b]B[/b] ba lô · [b]M[/b] bản đồ · [b]N[/b] xây dựng · [b]L[/b] trang trí\n" +
                "[b]K[/b] nhật ký · [b]O[/b] xã hội · [b]C[/b] tủ đồ · [b]G[/b] tặng quà\n" +
                "[b]H[/b] cài đặt · [b]F1[/b] hướng dẫn · [b]F5[/b] lưu game\n" +
                "[b]Enter[/b] chat (/w Tên: nhắn riêng · /toado · /den x z · /cho) · [b]T[/b] nói chuyện bằng giọng",
                "[b]Move[/b]: W A S D (relative to camera)\n" +
                "[b]Look[/b]: mouse (free look, crosshair at screen center)\n" +
                "[b][V][/b]: shoulder ↔ first-person view · wheel: zoom\n" +
                "[b]Hold [Alt] or press [Esc][/b]: show the cursor (click the game to lock again)\n\n" +
                "[b]Left mouse / J[/b]: attack · [b]Space / right mouse[/b]: use held tool\n" +
                "[b]E[/b]: interact (talk, open chests, doors, pick up, feed animals...)\n" +
                "[b]R[/b]: mount / dismount horse, boat · Space on horseback: gallop\n" +
                "[b]1 / 2 / 3[/b]: Dash / Whirlwind / Heal\n\n" +
                "[b]I[/b] inventory · [b]B[/b] backpack · [b]M[/b] map · [b]N[/b] build · [b]L[/b] decor\n" +
                "[b]K[/b] journal · [b]O[/b] social · [b]C[/b] wardrobe · [b]G[/b] give gift\n" +
                "[b]H[/b] settings · [b]F1[/b] guide · [b]F5[/b] save\n" +
                "[b]Enter[/b] chat (/w Name: whisper · /pos · /goto x z · /market) · [b]T[/b] voice chat"),

            ["guide.t.farm"] = ("Nông trại", "Farming"),
            ["guide.b.farm"] = (
                "• Cầm [b]Cuốc[/b], bấm [b]Space[/b] vào đất trống để cuốc (ô ruộng có sẵn hoặc bất cứ đâu trên đất).\n" +
                "• Bấm [b]Space[/b] vào ô đã cuốc để [b]chọn hạt giống[/b] và gieo (mua hạt ở cửa hàng thị trấn).\n" +
                "• Cầm [b]Bình Tưới[/b] tưới mỗi ngày - không tưới cây lớn chậm. Mỗi loại cây chỉ mọc đúng mùa (trừ Nhà Kính).\n" +
                "• Cây chín → bấm Space để thu hoạch. Bù Nhìn đuổi quạ, Máy Tưới tự tưới ruộng xung quanh.\n" +
                "• Bán: bỏ vào [b]Thùng Giao Hàng[/b] (tiền về sáng hôm sau) hoặc bán ở chợ / cửa hàng.\n" +
                "• [color=#ff9070]Nông sản có hạn sử dụng[/color]: để lâu sẽ thối và không dùng được - bán hoặc chế biến sớm.\n" +
                "• Bạn bè ghé nông trại có thể tưới giúp bạn. Nâng cấp nhà ở Bảng Nâng Cấp trước nhà.",
                "• Hold a [b]Hoe[/b] and press [b]Space[/b] on open ground to till (existing plots or anywhere on soil).\n" +
                "• Press [b]Space[/b] on tilled soil to [b]pick a seed[/b] and plant (buy seeds at the town shop).\n" +
                "• Use the [b]Watering Can[/b] every day - dry crops grow slowly. Crops only grow in their season (except the Greenhouse).\n" +
                "• Ripe crops → Space to harvest. Scarecrows keep crows away, Sprinklers water nearby plots.\n" +
                "• Sell: drop items in the [b]Shipping Bin[/b] (paid next morning) or sell at markets / shops.\n" +
                "• [color=#ff9070]Produce spoils[/color]: left too long it rots and becomes useless - sell or process early.\n" +
                "• Friends visiting your farm can water for you. Upgrade your house at the board in front of it."),

            ["guide.t.animals"] = ("Chăn nuôi & săn bắn", "Animals & hunting"),
            ["guide.b.animals"] = (
                "• Nông trại có chuồng bò, ngựa, gà, cừu + heo, dê - [b]mỗi loài 2 con[/b]; còn 1 con thì nó tự sinh bù.\n" +
                "• Đổ thức ăn vào máng, vuốt ve ([E]) để thú vui và cho sữa / trứng / lông nhiều hơn. Nhân công chăm hộ ban ngày.\n" +
                "• Có thể [b]giết thịt[/b] vật nuôi (đánh) để lấy thịt, da, lông - nhưng đàn sẽ ít đi.\n" +
                "• Ngoài hoang dã: săn hươu, thỏ, cáo, vịt, sói lấy [b]thịt rừng, da thú, lông thú[/b] (đồ đánh rơi, bấm E nhặt).\n" +
                "• [color=#ff9070]Sói[/color] tấn công vào ban đêm, đêm Trăng Máu hoặc khi bị đánh trước - nghe tiếng gầm gừ là phải cẩn thận.\n" +
                "• Thịt sống mau hỏng - nấu chín ở bếp để ăn lâu hơn và hồi nhiều hơn.",
                "• Your farm has pens for cows, horses, chickens, sheep + pigs, goats - [b]2 of each[/b]; one left breeds a replacement.\n" +
                "• Fill troughs, pet them ([E]) for happier animals and more milk / eggs / wool. Farmhands help during the day.\n" +
                "• You can [b]slaughter[/b] livestock (attack) for meat, hide, fur - but your herd shrinks.\n" +
                "• In the wild: hunt deer, rabbits, foxes, ducks, wolves for [b]game meat, hides, fur[/b] (drops - press E to pick up).\n" +
                "• [color=#ff9070]Wolves[/color] attack at night, during a Blood Moon or when provoked - a growl means trouble.\n" +
                "• Raw meat spoils fast - cook it at a kitchen to keep it longer and heal more."),

            ["guide.t.survival"] = ("Sinh tồn", "Survival"),
            ["guide.b.survival"] = (
                "Thanh [b]No · Nước · Nhiệt độ[/b] ở trên màn hình:\n" +
                "• [b]Đói[/b]: ăn nông sản, món nấu (nấu ăn hồi nhiều hơn). Đói lả thì mất máu.\n" +
                "• [b]Khát[/b]: đứng ở hồ / sông bấm E để uống, hoặc dùng [b]Nước Sạch[/b] (lấy ở giếng, hồ). Mất nước thì mất máu.\n" +
                "• [b]Lạnh[/b] (ban đêm, mùa đông, núi tuyết): mặc áo lông / giáp, đứng gần lửa trại hoặc vào nhà.\n" +
                "• [b]Nóng[/b] (hoang mạc): khát nhanh gấp đôi - mang theo nhiều nước.\n" +
                "• [b]Ngủ[/b] trên giường: hồi đầy máu + thể lực và lưu game. Giường ở căn cứ là [b]điểm hồi sinh[/b].\n" +
                "• [b]Ẩn nấp[/b]: đứng trong bụi cỏ cao hoặc trong lều - người chơi khác không thấy bạn (cho tới khi bạn tấn công).",
                "The [b]Food · Water · Temperature[/b] bar is on screen:\n" +
                "• [b]Hunger[/b]: eat crops or cooked dishes (cooking heals more). Starving drains health.\n" +
                "• [b]Thirst[/b]: stand at a lake / river and press E to drink, or use [b]Clean Water[/b] (from wells, lakes). Dehydration drains health.\n" +
                "• [b]Cold[/b] (night, winter, snowy mountains): wear fur / armor, stay near a campfire or go indoors.\n" +
                "• [b]Heat[/b] (deserts): thirst doubles - bring plenty of water.\n" +
                "• [b]Sleep[/b] in a bed: full health + stamina and the game saves. A base bed is your [b]respawn point[/b].\n" +
                "• [b]Hide[/b]: stand in tall grass or inside a tent - other players can't see you (until you attack)."),

            ["guide.t.craft"] = ("Chế tạo & độ bền", "Crafting & durability"),
            ["guide.b.craft"] = (
                "• Chế tạo theo [b]cấp trạm[/b]: Tay không → Bàn Chế Tác → Bàn Mộc → Lò Rèn → Xưởng Chế Tạo. Trạm cao làm được cả công thức cấp thấp.\n" +
                "• Đặt trạm ở căn cứ (bộ xây dựng), đứng gần bấm [E] → Dùng trạm.\n" +
                "• Mũi tên: 1 Gỗ + 1 Đá = 8 mũi. Cung bắn theo tâm ngắm, cần mũi tên trong túi.\n" +
                "• [color=#ff9070]Mọi công cụ, vũ khí, giáp đều có độ bền[/color]. Dưới [b]40%[/b] là không dùng được nữa → sửa ở Lò Rèn hoặc làm cái mới.\n" +
                "• Cường hoá trang bị bằng vàng + nguyên liệu (thành công có tỉ lệ).",
                "• Crafting uses [b]station tiers[/b]: By hand → Workbench → Carpentry → Forge → Workshop. Higher stations also make lower recipes.\n" +
                "• Place stations at your base (building kits), stand close and press [E] → Use station.\n" +
                "• Arrows: 1 Wood + 1 Stone = 8 arrows. Bows fire toward the crosshair and need arrows in your bag.\n" +
                "• [color=#ff9070]All tools, weapons and armor wear out[/color]. Below [b]40%[/b] they stop working → repair at a Forge or craft new.\n" +
                "• Enhance gear with gold + materials (success is a chance)."),

            ["guide.t.explore"] = ("Khám phá & bản đồ", "Exploring & map"),
            ["guide.b.explore"] = (
                "• Bản đồ [b][M][/b] ghi lại vùng bạn đã đi qua, có [b]toạ độ (mét)[/b] - bấm chuột trái để đặt điểm đến, mũi tên trên màn hình chỉ đường.\n" +
                "• Thế giới chia sinh cảnh: đồng cỏ, rừng rậm, cao nguyên đá, đầm lầy, hoang mạc... [b]Hệ số nguy hiểm[/b] tăng dần khi xa nông trại / thị trấn.\n" +
                "• [b]Cột Đá[/b]: chạm để kích hoạt, sau đó dịch chuyển nhanh giữa các cột đã mở (thị trấn, mỏ, các khu vực).\n" +
                "• Khu vực có rương báu, hang trùm (hồi sinh mỗi 7 ngày), mỏ quặng nhiều tầng, Hầm Ngục Cổ dưới Phế Tích.\n" +
                "• Gặp người chơi khác: gõ /toado trong chat để gửi vị trí cho nhau.",
                "• The map [b][M][/b] records explored land with [b]coordinates (meters)[/b] - left click sets a waypoint, an on-screen arrow guides you.\n" +
                "• The world has biomes: meadows, deep forests, rocky highlands, swamps, deserts... [b]Danger[/b] rises the farther you are from farms / town.\n" +
                "• [b]Waystones[/b]: touch to activate, then fast-travel between unlocked stones (town, mines, regions).\n" +
                "• Regions hold treasure chests, boss lairs (respawn every 7 days), multi-level mines and the Ancient Dungeon under the Ruins.\n" +
                "• Meeting others: type /pos in chat to share your location."),

            ["guide.t.combat"] = ("Chiến đấu", "Combat"),
            ["guide.b.combat"] = (
                "• [b]Chuột trái[/b]: đánh theo hướng tâm ngắm. Giữ để đánh liên tục. Kỹ năng [b]1/2/3[/b] mở theo cấp.\n" +
                "• [b]Lướt (1)[/b] né đòn, [b]Chém Xoáy (2)[/b] đánh quanh người, [b]Hồi Phục (3)[/b] hồi máu.\n" +
                "• Trùm có vòng đỏ dưới chân trước khi đập đất - chạy ra khỏi vòng để tránh.\n" +
                "• Giáp giảm sát thương. Mùa đông và đêm Trăng Máu quái mạnh hơn nhiều.\n" +
                "• Trên ngựa: chém quét 2 bên; đang phi nước đại mà chém trúng người cưỡi ngựa khác có thể hất họ xuống.",
                "• [b]Left mouse[/b]: attack toward the crosshair. Hold to keep attacking. Skills [b]1/2/3[/b] unlock with levels.\n" +
                "• [b]Dash (1)[/b] dodges, [b]Whirlwind (2)[/b] hits all around, [b]Heal (3)[/b] restores health.\n" +
                "• Bosses show a red circle before a ground slam - step out of it.\n" +
                "• Armor reduces damage. Monsters are much stronger in winter and during a Blood Moon.\n" +
                "• On horseback: sweeping slashes to both sides; hitting another rider while galloping can knock them off."),

            ["guide.t.pvp"] = ("PvP & cướp đồ", "PvP & looting"),
            ["guide.b.pvp"] = (
                "• Người chơi có thể đánh nhau [b]ở mọi nơi trừ khu chợ[/b] (vùng an toàn, có vòng xanh trên bản đồ).\n" +
                "• Bị hạ gục: [color=#ff9070]toàn bộ túi chính rơi tại chỗ[/color] thành túi đồ - người hạ bạn được ưu tiên nhặt 1 phút, sau đó ai cũng nhặt được.\n" +
                "• Ba lô và rương căn cứ [b]không[/b] rơi - cất đồ quý vào đó trước khi đi xa.\n" +
                "• Hồi sinh ở giường căn cứ gần nhất; mất giường thì phải chờ 1 giờ mới hồi sinh ở nông trại.\n" +
                "• Tổ đội và bang hội không đánh được nhau.",
                "• Players can fight [b]anywhere except markets[/b] (safe zones, green rings on the map).\n" +
                "• When downed: [color=#ff9070]your whole main bag drops[/color] as a loot bag - your killer has 1 minute of priority, then anyone can loot it.\n" +
                "• Your backpack and base chests do [b]not[/b] drop - stash valuables before long trips.\n" +
                "• You respawn at your nearest base bed; lose your bed and you wait 1 hour to respawn at your farm.\n" +
                "• Party and guild members can't hurt each other."),

            ["guide.t.base"] = ("Căn cứ", "Bases"),
            ["guide.b.base"] = (
                "• Chế bộ xây dựng (tường, cửa, giường, rương, lửa trại, lều, trạm chế tạo...), cầm bộ đó rồi bấm [b]Space[/b] để đặt ở thế giới mở.\n" +
                "• Bấm [E] vào công trình: Sửa (tốn gỗ), Mở rương, Dùng trạm, Gỡ bỏ (hoàn lại bộ nếu còn ≥ 50% máu).\n" +
                "• [color=#ff9070]Người khác có thể đập phá căn cứ của bạn[/color] - công trình bị phá [b]không tự hồi lại[/b], đồ trong rương bị phá là mất. Bạn được báo khi căn cứ bị tấn công.\n" +
                "• Tổ đội và bang hội được sửa / mở rương của nhau. Không xây được trong khu chợ.\n" +
                "• Dựng [b]Giường[/b] để có điểm hồi sinh gần vùng đang khám phá.",
                "• Craft building kits (walls, doors, beds, chests, campfires, tents, stations...), hold one and press [b]Space[/b] to place it in the open world.\n" +
                "• Press [E] on a structure: Repair (costs wood), Open chest, Use station, Dismantle (kit refunded at ≥ 50% HP).\n" +
                "• [color=#ff9070]Others can smash your base[/color] - destroyed pieces [b]don't come back[/b], a destroyed chest loses its contents. You're notified when attacked.\n" +
                "• Party and guild members can repair / open each other's chests. No building inside markets.\n" +
                "• Build a [b]Bed[/b] to respawn near the area you're exploring."),

            ["guide.t.bounty"] = ("Truy nã", "Bounties"),
            ["guide.b.bounty"] = (
                "• Hạ người chơi vô tội → bạn bị [color=#ff6050]TRUY NÃ[/color]: hệ thống treo thưởng lên đầu bạn, mỗi lần hạ người lại tăng thêm.\n" +
                "• Người bị truy nã có dòng đỏ [b]☠ TRUY NÃ ...g[/b] trên đầu. Ai hạ được họ sẽ [b]lãnh toàn bộ tiền thưởng[/b].\n" +
                "• Hạ kẻ bị truy nã không làm bạn bị truy nã.\n" +
                "• Tab [b]Truy nã[/b] trong Xã hội [O]: xem bảng truy nã, góp vàng của bạn để treo thưởng thêm lên đầu ai đó.",
                "• Kill an innocent player → you become [color=#ff6050]WANTED[/color]: a bounty is placed on you and grows with each kill.\n" +
                "• Wanted players show a red [b]☠ WANTED ...g[/b] tag. Whoever kills them [b]claims the whole bounty[/b].\n" +
                "• Killing a wanted player doesn't make you wanted.\n" +
                "• The [b]Wanted[/b] tab in Social [O]: see the board and add your own gold to anyone's bounty."),

            ["guide.t.social"] = ("Xã hội & nhiệm vụ", "Social & quests"),
            ["guide.b.social"] = (
                "• [b]Xã hội [O][/b]: kết bạn, lập / vào bang hội (kho chung), điểm danh hằng ngày, [b]nhiệm vụ ngày & tuần[/b], bảng xếp hạng.\n" +
                "• Nhiệm vụ do server đếm (thu hoạch, câu cá, đào quặng, săn bắn, hạ trùm, hầm ngục, truy nã...) - xong bấm Nhận thưởng.\n" +
                "• [b]Tổ đội[/b]: đứng gần người chơi bấm [E] → mời vào tổ (tối đa nhiều người, thấy vị trí nhau trên bản đồ, không đánh nhầm nhau).\n" +
                "• Đến gần người khác sẽ nghe họ nói (voice chat theo khoảng cách) - bấm [T] để nói.\n" +
                "• Đi tới nông trại người khác để xem ruộng của họ; bạn bè tưới giúp được.",
                "• [b]Social [O][/b]: friends, guilds (shared vault), daily check-in, [b]daily & weekly quests[/b], rankings.\n" +
                "• Quests are counted by the server (harvest, fishing, mining, hunting, bosses, dungeon, bounties...) - claim when done.\n" +
                "• [b]Party[/b]: stand near a player, press [E] → invite (see each other on the map, no friendly fire).\n" +
                "• Get close to others to hear them (proximity voice chat) - press [T] to talk.\n" +
                "• Walk to another player's farm to see their fields; friends can water for each other."),

            ["guide.t.events"] = ("Sự kiện & Trùm Thế Giới", "Events & World Bosses"),
            ["guide.b.events"] = (
                "• [b]Rương tiếp tế[/b] rơi từ trời (báo trước 2 phút kèm toạ độ) - ai tới trước bấm E mở được, đồ càng xa càng xịn.\n" +
                "• [b]Đoàn thương nhân[/b] dừng 15 phút, bán đồ hiếm số lượng có hạn.\n" +
                "• [b]Đêm Trăng Máu[/b]: trời đỏ, quái mạnh và đông hơn, sói hung dữ.\n" +
                "• [color=#ff6050]TRÙM THẾ GIỚI[/color] xuất hiện mỗi 1-1,5 giờ quanh thị trấn (báo toạ độ trước 3 phút, dấu đỏ trên bản đồ). [b]Máu chung[/b] - mọi người cùng đánh, thưởng vàng + đồ chia theo % sát thương (cần ≥ 1%), top 3 có kim cương.",
                "• [b]Supply drops[/b] fall from the sky (2-minute warning with coordinates) - first to press E gets it; farther drops are richer.\n" +
                "• A [b]traveling caravan[/b] stays 15 minutes selling rare items in limited stock.\n" +
                "• [b]Blood Moon[/b]: red sky, stronger and more numerous monsters, aggressive wolves.\n" +
                "• [color=#ff6050]WORLD BOSSES[/color] appear every 1-1.5 hours around town (3-minute warning, red marker on the map). [b]Shared health[/b] - everyone fights together, gold + loot split by % damage (≥ 1% needed), top 3 get diamonds."),

            ["guide.t.dungeon"] = ("Hầm ngục tổ đội", "Party dungeon"),
            ["guide.b.dungeon"] = (
                "• [b]Hầm Ngục Cổ[/b] nằm dưới Phế Tích: 5 tầng, quái mạnh dần theo tầng.\n" +
                "• Tầng 5 có [b]Rồng Cổ Đại[/b]. Khi online, cả [b]tổ đội dùng chung 1 con rồng[/b] - máu tăng theo số thành viên, nhưng thưởng cũng tăng 15% cho mỗi người thêm.\n" +
                "• Mỗi người nhận thưởng rồng [b]1 lần/ngày[/b]; ngoài ra có rương báu tuần ở tầng cuối.\n" +
                "• Rủ tổ đội cùng xuống: mời tổ đội trước, rồi cùng vào phòng rồng.",
                "• The [b]Ancient Dungeon[/b] lies under the Ruins: 5 floors, monsters get stronger per floor.\n" +
                "• Floor 5 holds the [b]Ancient Dragon[/b]. Online, the [b]whole party shares one dragon[/b] - more health per member, but +15% reward per extra member.\n" +
                "• Each player can claim the dragon reward [b]once per day[/b]; there's also a weekly chest on the last floor.\n" +
                "• Go with your party: invite them first, then enter the dragon's room together."),

            ["guide.t.taming"] = ("Thuần hoá & cưỡi ngựa", "Taming & riding"),
            ["guide.b.taming"] = (
                "• [b]Ngựa hoang[/b]: đi chậm lại gần, bấm [E] cho ăn (Cỏ Khô tốt nhất; Cà Rốt, Táo, Lúa Mì cũng được) đến khi thuần hoá xong. Mỗi con có tốc độ riêng.\n" +
                "• [b]Sói hoang[/b]: cho ăn thịt sống - thành bạn đồng hành, đi theo và đánh quái cùng bạn.\n" +
                "• [b]R[/b]: lên / xuống ngựa. [b]Space[/b]: phi nước đại (có thời gian hồi). Ngựa đã thuần được lưu và về chuồng nông trại.\n" +
                "• Thuyền ở hồ: cũng bấm R để lên.",
                "• [b]Wild horses[/b]: approach slowly and press [E] to feed (Hay is best; Carrots, Apples, Wheat work) until tamed. Each has its own speed.\n" +
                "• [b]Wild wolves[/b]: feed raw meat - they become companions that follow you and fight monsters.\n" +
                "• [b]R[/b]: mount / dismount. [b]Space[/b]: gallop (with cooldown). Tamed horses are saved and return to your farm stable.\n" +
                "• Boats on lakes: also press R to board."),

            ["guide.t.trade"] = ("Chợ & giao dịch", "Markets & trading"),
            ["guide.b.trade"] = (
                "• [b]Khu chợ[/b] rải khắp thế giới (vòng xanh trên bản đồ, gõ /cho để tìm chợ gần nhất) - là [b]vùng an toàn[/b].\n" +
                "• [b]Chợ người chơi[/b]: đăng bán đồ với giá tự đặt, giữ 7 ngày, thuế 5%; tiền về khi có người mua (kể cả lúc bạn offline).\n" +
                "• [b]Giao dịch trực tiếp[/b]: đứng gần người chơi bấm [E] → Giao dịch, hai bên khoá rồi xác nhận.\n" +
                "• Cửa hàng ở thị trấn bán hạt giống, công cụ; Thùng Giao Hàng mua nông sản.",
                "• [b]Markets[/b] are scattered across the world (green rings on the map, /market finds the nearest) - they are [b]safe zones[/b].\n" +
                "• [b]Player market[/b]: list items at your price, held 7 days, 5% tax; you get paid when someone buys (even offline).\n" +
                "• [b]Direct trade[/b]: stand near a player, press [E] → Trade, both lock then confirm.\n" +
                "• Town shops sell seeds and tools; the Shipping Bin buys produce."),

            // ---- Cac buoc lam quen (GuideTracker) ----
            ["steps.title"] = ("NGƯỜI MỚI - VIỆC CẦN LÀM", "BEGINNER STEPS"),
            ["steps.move"] = ("Di chuyển bằng W A S D", "Move with W A S D"),
            ["steps.look"] = ("Xoay chuột để nhìn quanh ([V] đổi góc nhìn)", "Move the mouse to look around ([V] switch view)"),
            ["steps.interact"] = ("Bấm [E] để tương tác với thứ gì đó", "Press [E] to interact with something"),
            ["steps.tool"] = ("Dùng công cụ: cầm Cuốc bấm [Space] vào đất", "Use a tool: hold the Hoe and press [Space] on soil"),
            ["steps.bag"] = ("Mở túi đồ [I] hoặc ba lô [B]", "Open your inventory [I] or backpack [B]"),
            ["steps.map"] = ("Mở bản đồ [M]", "Open the map [M]"),
            ["steps.guide"] = ("Đọc hướng dẫn chơi [F1]", "Read the guide [F1]"),
            ["steps.done"] = ("Bạn đã nắm cơ bản! Xem thêm mọi lúc bằng [F1].", "You know the basics! Open the guide anytime with [F1]."),
            ["steps.skip"] = ("[F1] hướng dẫn · bỏ qua: tắt trong hướng dẫn", "[F1] guide · to hide: turn off in the guide"),
            ["guide.hide_steps"] = ("Ẩn bảng việc cho người mới", "Hide beginner steps"),
        };
    }
}
