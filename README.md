# 🌾 Hiệp Sĩ Về Vườn — Godot 4 / C# (project mẫu)

Một game **top-down 2D** mẫu kết hợp **nông trại + khám phá + chiến đấu quái vật + nhặt vật phẩm**, dựng theo đúng cốt truyện và yêu cầu kỹ thuật bạn cung cấp. Code hoàn toàn bằng **C#**, kiến trúc chia hệ thống (data-driven, có save/load).

> ⚠️ **Về "3D":** toàn bộ art bạn gửi là **pixel-art 2D** và thiết kế theo phong cách Stardew Valley (top-down), nên project này làm **2D**. Kiến trúc code (các System tách rời, data-driven, save/load) giữ nguyên và có thể tái dùng cho bản 3D sau này — chỉ cần thay `CharacterBody2D` → `CharacterBody3D`, `Sprite2D` → `Sprite3D`/model, và camera.

---

## ▶️ Cách chạy

1. Cài **Godot 4.3 – phiên bản .NET/Mono** (bắt buộc bản .NET để chạy C#): https://godotengine.org/download
2. Cài **.NET SDK 8.0**: https://dotnet.microsoft.com/download
3. Mở Godot → **Import** → chọn file `project.godot` trong thư mục này.
4. Lần đầu mở, Godot sẽ tự **import các ảnh** (`.png`) và **build C#**. Đợi build xong.
5. Nhấn **F5** (hoặc nút ▶ Play) để chạy. Scene chính là `scenes/Main.tscn`.

Nếu C# chưa build: menu **Project → Tools → C# → Create C# solution**, rồi build lại.

---

## 🎮 Điều khiển

| Phím | Hành động |
|------|-----------|
| **WASD** / mũi tên | Di chuyển |
| **J** hoặc **Chuột trái** | Tấn công - bấm liên tiếp ra combo 3 đòn, giữ phím để đánh liên tục, tự xoay về quái gần nhất |
| **Space** | Dùng công cụ trên ô đất (trồng / tưới / thu hoạch) - giữ phím để cuốc/tưới/cắt cỏ liên tục; kết quả xảy ra đúng lúc lưỡi cuốc chạm đất |
| **E** | Tương tác (nói chuyện NPC, nhặt đồ) |
| **I** | Mở/đóng túi đồ (bấm item để trang bị/dùng) |
| **G** | Tặng quà cho người dân đứng gần |
| **K** | Nhật Ký: kỹ năng / bạn bè / thành tựu / thư |
| **Enter** | Chat (`/w Tên` nhắn riêng, `/kenh 2` đổi kênh) |
| **F5** | Lưu game |
| **1 / 2 / 3** | Kỹ năng chiến đấu: Lướt (cấp 1) / Chém Xoáy (cấp 3) / Hồi Phục (cấp 5) |
| **O** | Xã hội: bạn bè, bang hội, điểm danh, nhiệm vụ ngày/tuần, bảng xếp hạng |
| **V** | Trang trí: đặt đồ nội thất / sân vườn |
| **C** | Tủ đồ: thay trang phục nông trại / bộ giáp Trung cổ (may đồ ở Bàn May) |

Vòng lặp demo: trồng hạt → tưới → chờ sang ngày (30 giây = 1 ngày) → thu hoạch → bán cho thương nhân → mua đồ → đi đánh quái → nhặt loot → nhận nhiệm vụ từ NPC.

---

## 🗂️ Kiến trúc & phân bổ file

```
HiepSiVeVuon/
├── project.godot            # Cấu hình project + autoload + input map
├── HiepSiVeVuon.csproj      # Project C# (.NET 8)
├── HiepSiVeVuon.sln
├── icon.svg
│
├── data/                    # DATA-DRIVEN: định nghĩa nội dung bằng JSON
│   ├── items.json           #   vật phẩm, vũ khí, giáp, hạt giống, nông sản
│   ├── enemies.json         #   quái vật (máu, sát thương, loot)
│   └── quests.json          #   nhiệm vụ
│
├── assets/                  # Art (pixel-art từ ảnh bạn cung cấp)
│   ├── player/  enemies/  items/  crops/  tools/  npc/  scenery/
│
├── scenes/                  # Scene (.tscn)
│   ├── Main.tscn            #   scene gốc: controller + các lớp UI
│   ├── Player.tscn
│   ├── Enemy.tscn
│   ├── NPC.tscn
│   └── FarmPlot.tscn
│
└── scripts/                 # Toàn bộ code C#, chia theo tầng
    ├── core/
    │   ├── GameManager.cs    #   [autoload] chỉ số người chơi, vàng, ngày, EXP/level
    │   └── Main.cs           #   dựng thế giới, spawn entity, vòng ngày-đêm
    ├── systems/             #   các hệ thống độc lập ([autoload])
    │   ├── ItemDatabase.cs   #   nạp items/enemies/quests từ JSON
    │   ├── Inventory.cs      #   túi đồ, stack, trang bị
    │   ├── QuestSystem.cs    #   theo dõi & hoàn thành nhiệm vụ
    │   └── SaveSystem.cs     #   lưu/nạp JSON ra user://
    ├── entities/
    │   ├── Player.cs         #   di chuyển, tấn công, tương tác, dùng công cụ
    │   ├── Enemy.cs          #   AI máy trạng thái (Idle→Chase→Attack), loot
    │   ├── NPC.cs            #   hội thoại theo lòng tin, giao quest, cửa hàng
    │   ├── DroppedItem.cs    #   vật phẩm rơi/nhặt
    │   └── FarmPlot.cs       #   ô đất: trồng→tưới→lớn→thu hoạch
    ├── ui/
    │   ├── HUD.cs            #   HP, vàng, cấp, ngày, theo dõi nhiệm vụ
    │   ├── DialogueBox.cs    #   khung hội thoại
    │   ├── InventoryUI.cs    #   túi đồ dạng lưới
    │   └── ShopUI.cs         #   mua/bán
    └── data/
        └── GameData.cs       #   các class dữ liệu (ItemDef, EnemyDef, QuestDef...)
```

### Nguyên tắc thiết kế đã áp dụng (theo tài liệu yêu cầu)

- **Data-driven** — mọi nội dung (item, quái, quest) nằm trong JSON, không hard-code. Thêm nội dung = sửa JSON.
- **Hệ thống tách rời** — mỗi System là một autoload singleton, giao tiếp qua signal, không phụ thuộc chặt lẫn nhau.
- **Gameplay loop khép kín** — Nông trại ⇄ Kinh tế ⇄ Chiến đấu ⇄ Nhiệm vụ ⇄ Nâng cấp.
- **Enemy AI có perception + state machine** — Idle / Patrol / Chase / Attack.
- **NPC theo lòng tin** — lời thoại đổi theo mức thân thiết (cơ chế "hỏi đường" cốt lõi).
- **Save/Load** ổn định — lưu chỉ số, túi đồ, trang bị, quest, trạng thái nông trại.

---

## 🌱 Hệ thống nông trại mở rộng

| Hệ thống | Cách chơi | Code / dữ liệu |
|---|---|---|
| **Thể lực** | Cuốc/tưới/bón phân/câu cá/đào quặng/chặt cây tốn thể lực; tự hồi chậm, ăn món ăn hoặc ngủ trên giường (tầng 2 nhà) để hồi đầy. Kiệt sức (<10%) đi chậm hơn. Trồng & thu hoạch không tốn. | `GameManager` (Stamina), `Bed.cs`, `StaminaAmount` trong `items.json` |
| **Kỹ năng** | 5 kỹ năng Trồng Trọt / Chăn Nuôi / Câu Cá / Khai Khoáng / Hái Lượm, cấp 0–10; mỗi cấp tăng bonus + 3 thể lực tối đa + mở công thức chế tạo. | `SkillSystem.cs` |
| **Câu cá** | Space gần hồ → chờ cá cắn → bấm Space khi vạch vàng trong vùng xanh. 8 loài theo mùa/giờ/mưa, Mồi Câu nới vùng xanh. | `FishingUI.cs`, `data/fish.json` |
| **Chế tạo** | Bàn Chế Tạo trong nhà và cạnh Bếp: phân bón, thuốc sâu, mồi câu, trà/bánh năng lượng, máy tưới, nâng cấp cuốc/cuốc chim… | `CraftingUI.cs`, `data/recipes.json` |
| **Thùng giao hàng** | Bỏ hàng vào thùng cạnh nhà, sang ngày mới tự bán (theo giá mùa/lễ hội/kỹ năng) + thư báo cáo. | `ShippingSystem.cs`, `ShippingBin.cs` |
| **Tặng quà & thân thiết** | Trò chuyện [E] mỗi ngày, tặng quà [G] 1 lần/ngày; mỗi NPC có món Yêu/Thích/Ghét và sinh nhật (quà x5). 10 tim, được lưu. | `FriendshipSystem.cs`, `data/npc_gifts.json` |
| **Hái lượm** | Đồ hái lượm theo mùa mọc lại mỗi ngày trong rừng, cánh đồng và quanh nông trại. | `ForageSpawner.cs`, `data/forage.json` |
| **Thành tựu** | 18 thành tựu theo thống kê lối chơi, thưởng vàng gửi qua thư. | `AchievementSystem.cs`, `data/achievements.json` |
| **Thư** | Hộp thư trước nhà: thư chào mừng, quà đầu mùa, nhắc sinh nhật, quà từ bạn thân, báo cáo giao hàng, thưởng thành tựu. | `MailSystem.cs`, `Mailbox.cs`, `JournalUI.cs` |
| **Ủ phân** | Bỏ 5 đồ hữu cơ vào Thùng Ủ Phân cạnh ruộng → 2 ngày sau nhận 4 Phân Bón. | `CompostBin.cs` |

Ngoài ra: vuốt ve vật nuôi [E] (+EXP Chăn Nuôi, 1 lần/ngày/con), HUD hiển thị thể lực, mùa/thời tiết, thư mới và thông báo nhanh (`Notify.Show`). Toàn bộ trạng thái mới được lưu trong `SaveSystem` (bản lưu cũ vẫn nạp được).

---

## 🌐 Online nhiều người chơi

- **Thời gian thực, lịch chung**: 1 ngày game = 1 ngày thật, mọi người cùng mùa/ngày/thời tiết; đồng hồ lấy từ server
  (đổi giờ máy không có tác dụng). Offline nhiều ngày → lúc vào lại game tự "chạy bù" tối đa 28 ngày, nhân công chăm cây hộ.
- **Tạo nhân vật**: lần đầu đăng nhập chọn tên (duy nhất), giới tính Nam/Nữ, 6 kiểu nam + 4 kiểu nữ, màu da/tóc/trang phục,
  có xem trước 3D. Người chơi khác thấy đúng ngoại hình này.
- **Thế giới chung chia kênh**: nông trại là riêng tư; thị trấn, 11 vùng và vùng hoang dã là không gian chung, mỗi vùng chia
  kênh tối đa 80 người. Thấy người chơi khác di chuyển, có tên + bong bóng chat trên đầu.
- **Chat**: [Enter] chat kênh, `/w Tên nội dung` nhắn riêng, `/kenh 2` đổi kênh.
- **Giao dịch**: đứng gần người chơi khác bấm [E] → Giao dịch: hai bên đặt đồ/vàng → khóa → xác nhận.
- **Chợ người chơi**: Bảng Chợ ở thị trấn (cạnh Bảo Tàng) - đăng bán, tìm & mua, gỡ tin; thuế 5%.
- **Kinh tế do server quản lý**: vàng & vật phẩm chính thức nằm trên server, mọi thay đổi được server kiểm tra (giá,
  công thức, nguồn rơi đồ, giới hạn/giờ, phần thưởng 1 lần) - sửa file lưu để tự tăng đồ không có tác dụng.

Backend + kiến trúc mở rộng (nhiều API/gateway, Redis, k8s, kiểm tải): xem [`backend/README.md`](backend/README.md).

## ⚔️ Khám phá & phiêu lưu

| Tính năng | Cách chơi | Code / dữ liệu |
|---|---|---|
| **Quái vật 3D mới** | Mỗi loại quái có model riêng (Quaternius Ultimate Monsters, CC0): sói, golem đá, mực hồ, ma, đầu lâu ma, dơi... Có thanh máu, chớp sáng khi trúng đòn, bị đẩy lùi, ma trong suốt phát sáng, nổ bụi hạt khi chết. | `Enemy.cs`, `EnemyVisuals.cs`, `data/enemies.json` (`ModelPath`, `ModelScale`, `HoverHeight`, `Ghostly`, `GlowColor`) |
| **Trùm mỗi vùng** | 6 trùm (Rừng, Núi, Đầm Lầy, Phế Tích, Nghĩa Địa, Hang Động) ở hang có cột đầu lâu đỏ. Thanh máu trên HUD, đòn dậm đất có vòng đỏ cảnh báo (né ra khỏi vòng!). Rơi chiến lợi phẩm độc nhất, hồi sinh sau 7 ngày. | `BossLair.cs`, `AdventureSystem.cs` |
| **Rương báu & khám phá** | 22 rương báu rải khắp 11 vùng (mở 1 lần). Lần đầu vào mỗi vùng được +60 EXP; vùng chưa khám phá bị sương mù trên bản đồ [M]. | `TreasureChest.cs`, `AdventureTracker.cs`, `MapUI.cs` |
| **Hầm Ngục Cổ** | Lối vào ở Phế Tích. 5 tầng quái mạnh dần, tầng 5 có Rồng Cổ Đại + rương báu mỗi tuần. | `MainAdventure.cs` (`BuildDungeon`), `DungeonFloor.cs` |
| **Hầm mỏ khắp bản đồ** | **10 hầm mỏ** (mỗi vùng hoang dã đều có): Hầm Mỏ gốc, Mỏ Than (Núi), Hang Pha Lê (Hang Động), Mỏ Vàng Cổ (Phế Tích), Mỏ Đồng Rừng Sâu, Mỏ Than Bùn (Đầm Lầy), Mỏ Bạc Ven Sông, Hang Ngọc Hồ, Mỏ Xương Cổ (Nghĩa Địa), Mỏ Đá Đồng Bằng - mỗi mỏ 2-3 tầng, quặng quý cần cuốc chim tốt hơn. Thêm **bãi quặng lộ thiên sinh ngẫu nhiên khắp thế giới** (càng xa nông trại quặng càng quý), hiện trên bản đồ khi đi qua. Quặng, cửa mỏ, icon vật phẩm đều dùng **model 3D CC0** (Quaternius/Kenney/iPoly3D). Nung quặng + than thành thỏi; Cuốc Chim Kim Cương. | `MainMines.cs`, `OreFields.cs`, `ModelUtil.cs`, `IconRenderer.cs`, `OreRegistry.cs` |
| **Cột đá dịch chuyển** | 14 cột (nông trại, thị trấn, hầm mỏ, 11 vùng). [E] để kích hoạt, rồi [E] ở bất kỳ cột nào để đi nhanh. | `Waystone.cs`, `WaystoneUI.cs` |
| **Bảo tàng** | Ở phía bắc Tòa Thị Chính. Quyên góp cổ vật/đá quý/chiến lợi phẩm trùm (20 hiện vật), mốc thưởng gửi qua thư. | `MuseumCounter.cs`, `MuseumUI.cs` |

## 🌾 Nông trại (bổ sung)

- **9 cây trồng mới theo mùa** (dâu tây, tỏi, cải xoăn, dưa hấu, ngô, việt quất, cà tím, củ dền, củ cải đông) - bán ở Thương Nhân. Dâu/ngô/việt quất/cà tím **thu hoạch nhiều lần**. Hết mùa thì cây trái mùa tàn (nhà kính không bị).
- **Quạ & Bù Nhìn**: quạ có thể ăn cây ngoài trời; chế tạo Bù Nhìn (gỗ + lúa mì) rồi trang bị + [Space] để cắm.
- **Cây khổng lồ**: bí ngô/bắp cải/cải xoăn/dưa hấu chín đôi khi hóa khổng lồ, thu ~6 nông sản.
- **Lễ hội**: Săn Trứng (ngày 13 Xuân), Thi Câu Cá (ngày 20 Hạ), Hội Chợ Nông Sản (ngày 16 Thu), Ánh Sao (ngày 25 Đông) - Bảng Lễ Hội ở quảng trường thị trấn.
- **Nâng cấp nhà** (bảng cạnh nhà, 3 cấp): +thể lực tối đa, rương chứa đồ lớn hơn (rương trong nhà, tầng trệt).
- **Độ vui vật nuôi**: vuốt ve [E] + giữ kho có thức ăn → thêm sản phẩm và giá bán cao hơn.
- Sửa lỗi cũ: số ngày lớn của cây trồng giờ được lưu (trước đây tải lại game thì mọi cây chín sau 3 ngày).

---

## 🤝 Xã hội, chiến đấu & đời sống (đợt 5)

| Nhóm | Tính năng | Ghi chú kỹ thuật |
|------|-----------|------------------|
| Online | **Bạn bè & Bang hội**: kết bạn, trạng thái online, lập bang (1000 vàng), mời/kick/phong chức, kho bang, chat bang `/g` | `socialService.js`, bảng `friendships`, `guilds`, `guild_*`; presence trong Redis (TTL 90s) |
| Online | **Thăm nông trại bạn bè**: vào zone `farm_<id>` (chỉ chủ + bạn bè), tưới giúp tối đa 40 ô/ngày | `farm_helps`; chủ nông trại nhận khi đăng nhập/online |
| Online | **Bảng xếp hạng**: giàu nhất, đấu trường, cấp, câu cá, nông dân, diệt quái/trùm, thợ mỏ | chỉ số tự báo bị giới hạn tăng/ngày để chống gian lận |
| Online | **Điểm danh 7 ngày + nhiệm vụ ngày/tuần** | tiến độ đếm từ chính dòng kinh tế trên server, nhận thưởng 1 lần (`quest_claims`) |
| Chiến đấu | **Lò Rèn**: rèn kiếm/giáp đồng → vàng; cường hóa +1..+10 (+10%/cấp) | server tung xúc xắc; từ +6 thất bại tụt 1 cấp |
| Chiến đấu | **Kỹ năng**: Lướt, Chém Xoáy, Hồi Phục (phím 1/2/3) | `PlayerAbilities.cs` |
| Chiến đấu | **Thú cưng** (Trại Thú Cưng ở thị trấn): chó, mèo, husky, sói, ong vàng, rồng con - đi theo, đánh quái, nhặt đồ, lên cấp | `PetSystem`, `Pet.cs` |
| Chiến đấu | **Đấu trường PvP**: thách đấu người chơi gần (menu người chơi), máu 400, Elo K=32 | gateway xử lý trận, trạng thái trong Redis nên chạy được qua nhiều gateway |
| Đời sống | **Trang trí** (phím V): 21 món nội thất/sân vườn mua ở Thợ Mộc | `PlacementSystem` lưu trong save |
| Đời sống | **Thời tiết**: nắng, mây, mưa, bão (sét hỏng 5% cây), tuyết (cây trái mùa ngừng lớn), nắng nóng, sương mù | xác định theo ngày nên mọi người chơi thấy giống nhau |
| Đời sống | **Tổ ong, cây ăn quả** (táo/lê/anh đào/nho), **máy làm mứt, máy ép nước** | cây giống ở Thương Nhân |
| Đời sống | **Câu cá nâng cao**: 4 cá huyền thoại theo mùa (Câu cá ≥ 6), bẫy cua, hồ cá cảnh (thưởng khi đủ 6 loài / đủ tất cả) | |

Mọi model mới đều là CC0 từ poly.pizza (xem `CREDITS.md`), không có model tự vẽ.

---

## 👕 Trang phục & bộ giáp Trung cổ

Hai loại trang phục tách riêng trong **Tủ Đồ** (phím **C**), có nhân vật 3D xem trước:

| Loại | Vật phẩm | Làm ở đâu | Tác dụng |
|------|----------|-----------|----------|
| Trang phục nông trại (`Outfit`) | Yếm Nông Dân, Thợ Làm Vườn, Thám Hiểm, Thường Ngày, Lễ Hội | **Bàn May** (thị trấn + khu chế biến) - từ Vải Dệt + nguyên liệu nhuộm | Giảm 15% thể lực cho 1 kỹ năng, hoặc chạy nhanh hơn 8% |
| Bộ giáp Trung cổ cả bộ (`Armor`) | Da, Sắt, Hiệp Sĩ Bạc, Thập Tự Quân, Vàng Hoàng Gia, Hắc Kỵ Sĩ | **Lò Rèn** | Phòng thủ (cường hóa được) |

- Mặc trang phục nông trại thì giữ nguyên đầu/mặt/tóc của nhân vật đã tạo, chỉ thay thân/quần/giày (nam) hoặc cả bộ đồ (nữ).
- Bộ giáp gồm giáp thân + giáp tay + quần + ủng + mũ trụ; tắt "Hiện bộ giáp" để mặc đồ nông trại mà vẫn giữ phòng thủ.
- Người chơi khác thấy trang phục của bạn (gateway kiểm tra bạn thật sự sở hữu món đó).
- Model: thân giáp từ "King" (Quaternius, CC0) đã chuyển sang bộ xương nam/nữ của game, mũ trụ "Helmet" (CreativeTechLab, CC-BY). Cách tạo lại: `tools/outfits/README.md`.

---

## ➕ Mở rộng nhanh

- **Thêm quái mới:** thêm 1 khối vào `data/enemies.json` + 1 ảnh vào `assets/enemies/`, rồi `SpawnEnemy("id", pos)` trong `Main.cs`.
- **Thêm vật phẩm/cây trồng:** thêm vào `data/items.json` (nhớ cặp `seed` → `GrowsIntoCropId`).
- **Thêm nhiệm vụ:** thêm vào `data/quests.json`, gán `QuestToGive` cho một NPC.
- **Thêm công thức / loài cá / đồ hái lượm / thành tựu / sở thích NPC:** sửa `data/recipes.json`, `fish.json`, `forage.json`, `achievements.json`, `npc_gifts.json` - không cần sửa code.
- **Thêm khu vực (rừng/hang):** tạo scene mới, dùng chung Player/Enemy/UI, chuyển cảnh bằng `GetTree().ChangeSceneToFile(...)`.

---

## 📌 Ghi chú

- Các file `*.png.import` sẽ được Godot tự sinh khi mở project lần đầu — điều này là bình thường.
- Bản lưu nằm ở `user://savegame.json` (Windows: `%APPDATA%/Godot/app_userdata/Hiep Si Ve Vuon/`).
