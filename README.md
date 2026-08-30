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
| **J** hoặc **Chuột trái** | Tấn công |
| **Space** | Dùng công cụ trên ô đất (trồng / tưới / thu hoạch) |
| **E** | Tương tác (nói chuyện NPC, nhặt đồ) |
| **I** | Mở/đóng túi đồ (bấm item để trang bị/dùng) |
| **F5** | Lưu game |

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

## ➕ Mở rộng nhanh

- **Thêm quái mới:** thêm 1 khối vào `data/enemies.json` + 1 ảnh vào `assets/enemies/`, rồi `SpawnEnemy("id", pos)` trong `Main.cs`.
- **Thêm vật phẩm/cây trồng:** thêm vào `data/items.json` (nhớ cặp `seed` → `GrowsIntoCropId`).
- **Thêm nhiệm vụ:** thêm vào `data/quests.json`, gán `QuestToGive` cho một NPC.
- **Thêm khu vực (rừng/hang):** tạo scene mới, dùng chung Player/Enemy/UI, chuyển cảnh bằng `GetTree().ChangeSceneToFile(...)`.

---

## 📌 Ghi chú

- Các file `*.png.import` sẽ được Godot tự sinh khi mở project lần đầu — điều này là bình thường.
- Bản lưu nằm ở `user://savegame.json` (Windows: `%APPDATA%/Godot/app_userdata/Hiep Si Ve Vuon/`).
