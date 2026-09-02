using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.UI
{
    // Balo (tui do thu hai, 50 o - xem Backpack.cs). Nhan [B] de bat/tat. Bam vat pham ben Tui Do
    // de CAT vao balo, bam vat pham ben Balo de LAY ra tui do - moi lan bam chuyen 1 cai, giong
    // quy uoc Mua/Ban trong ShopUI (khong tu dong chuyen ca stack).
    public partial class BackpackUI : CanvasLayer
    {
        private PanelContainer _panel;
        private GridContainer _invGrid;
        private GridContainer _bagGrid;
        private Label _info;
        private Label _bagHeaderLabel;
        private readonly LocalizedLabelSet _loc = new();

        public override void _Ready()
        {
            Build();
            Inventory.Instance.InventoryChanged += Refresh;
            Backpack.Instance.BackpackChanged += Refresh;
            Loc.LanguageChanged += OnLanguageChanged;
            Visible = false;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            _loc.Refresh();
            _bagHeaderLabel.Text = string.Format(Loc.T("backpack.balo_header_fmt"), Backpack.MaxSlots);
        }

        private void Build()
        {
            _panel = new PanelContainer();
            _panel.Position = new Vector2(240, 60);
            _panel.CustomMinimumSize = new Vector2(480, 460);
            AddChild(_panel);

            var vb = new VBoxContainer();
            _panel.AddChild(vb);

            vb.AddChild(_loc.Track(new Label(), "backpack.title"));

            vb.AddChild(_loc.Track(new Label(), "backpack.inventory_header"));
            _invGrid = new GridContainer { Columns = 8 };
            vb.AddChild(_invGrid);

            _bagHeaderLabel = new Label { Text = string.Format(Loc.T("backpack.balo_header_fmt"), Backpack.MaxSlots) };
            vb.AddChild(_bagHeaderLabel);
            _bagGrid = new GridContainer { Columns = 8 };
            vb.AddChild(_bagGrid);

            _info = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            _info.CustomMinimumSize = new Vector2(460, 40);
            vb.AddChild(_info);
        }

        public override void _Input(InputEvent e)
        {
            if (e.IsActionPressed("toggle_backpack"))
            {
                Visible = !Visible;
                if (Visible) Refresh();
                GetViewport().SetInputAsHandled();
            }
        }

        private void Refresh()
        {
            foreach (Node c in _invGrid.GetChildren()) c.QueueFree();
            foreach (var stack in Inventory.Instance.Slots)
            {
                string id = stack.ItemId;
                var btn = MakeSlotButton(id, stack.Count);
                btn.Pressed += () => DepositToBackpack(id);
                _invGrid.AddChild(btn);
            }

            foreach (Node c in _bagGrid.GetChildren()) c.QueueFree();
            foreach (var stack in Backpack.Instance.Slots)
            {
                string id = stack.ItemId;
                var btn = MakeSlotButton(id, stack.Count);
                var def = ItemDatabase.Instance.GetItem(id);
                // Vu khi/giap/cong cu: bam de TRANG BI TRUC TIEP ngay tu Balo (khong can rut ra
                // Tui Do roi mo rieng man hinh Tui Do de trang bi nhu truoc - dung yeu cau "xem
                // balo thi co the doi vat cam tay"). Inventory.Equip() chi ghi co trang bi, KHONG
                // doi hoi vat pham phai nam trong Inventory.Slots, nen trang bi thang tu Balo an
                // toan, khong can di chuyen vat pham qua lai. Cac loai con lai (nguyen lieu/nong
                // san/hat giong/do dung) van RUT ra Tui Do nhu quy uoc cu.
                if (def != null && (def.Type == ItemType.Weapon || def.Type == ItemType.Armor || def.Type == ItemType.Tool))
                {
                    btn.Pressed += () => EquipFromBackpack(id);
                    // Them han 1 nut chu "Su Dung" duoi icon - bam thang vao icon van trang bi
                    // duoc (giu nguyen tien loi cu), nhung nut co chu ro rang de nguoi choi khong
                    // phai doan "bam vao icon la lam gi" (theo dung yeu cau, thay vi chi co icon
                    // tran khong nhan biet duoc hanh dong).
                    bool isCurrentlyEquipped = def.Type switch
                    {
                        ItemType.Weapon => id == Inventory.Instance.EquippedWeapon,
                        ItemType.Armor => id == Inventory.Instance.EquippedArmor,
                        ItemType.Tool => id == Inventory.Instance.EquippedTool,
                        _ => false,
                    };
                    var cell = new VBoxContainer();
                    cell.AddChild(btn);
                    // O DANG TRANG BI: an nut "Su Dung" (khong the bam de trang bi lai cai da
                    // dang cam) va thay bang chu "Dang dung" - day CHINH la dau hieu de nguoi
                    // choi biet o nao dang cam tren tay, theo dung yeu cau.
                    GD.Print($"[BackpackUI DEBUG] slot={id} type={def.Type} isCurrentlyEquipped={isCurrentlyEquipped}");
                    if (isCurrentlyEquipped)
                    {
                        cell.AddChild(new Label { Text = Loc.T("backpack.currently_used"), HorizontalAlignment = HorizontalAlignment.Center });
                    }
                    else
                    {
                        var useBtn = new Button { Text = Loc.T("backpack.use_button") };
                        useBtn.Pressed += () => EquipFromBackpack(id);
                        cell.AddChild(useBtn);
                    }
                    _bagGrid.AddChild(cell);
                }
                else
                {
                    btn.Pressed += () => WithdrawFromBackpack(id);
                    _bagGrid.AddChild(btn);
                }
            }
        }

        private void EquipFromBackpack(string itemId)
        {
            Inventory.Instance.Equip(itemId);
            _info.Text = string.Format(Loc.T("inventory.equipped_fmt"), ItemDatabase.Instance.GetDisplayName(itemId));
        }

        private Button MakeSlotButton(string itemId, int count)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            var btn = new Button { CustomMinimumSize = new Vector2(48, 48) };
            var tex = ItemDatabase.Instance.GetItemIcon(itemId);
            if (tex != null) { btn.Icon = tex; btn.ExpandIcon = true; }
            btn.Text = count > 1 ? $"x{count}" : "";
            btn.MouseEntered += () => ShowInfo(def, count);
            return btn;
        }

        private void ShowInfo(ItemDef def, int count)
        {
            if (def == null) return;
            _info.Text = $"{ItemDatabase.Instance.GetDisplayName(def.Id)} [{def.Rarity}] x{count}\n{ItemDatabase.Instance.GetDisplayDescription(def.Id)}";
        }

        // LUON them vao noi DEN truoc, chi bo khoi noi DI neu them thanh cong - neu lam nguoc lai
        // (bo truoc, them sau) va noi den vua luc day, vat pham se BIEN MAT khoi ca 2 tui.
        private void DepositToBackpack(string itemId)
        {
            if (!Backpack.Instance.AddItem(itemId, 1)) { _info.Text = Loc.T("backpack.full"); return; }
            Inventory.Instance.RemoveItem(itemId, 1);
        }

        private void WithdrawFromBackpack(string itemId)
        {
            if (!Inventory.Instance.AddItem(itemId, 1)) { _info.Text = Loc.T("backpack.inventory_full"); return; }
            Backpack.Instance.RemoveItem(itemId, 1);
        }
    }
}
