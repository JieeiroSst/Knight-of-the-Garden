using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Menu cong trinh can cu ([E]): Sua (ton go) / Mo ruong / Go bo (chu) + man hinh ruong 2 cot (do
    // trong ruong <-> tui chinh). Moi thao tac la y dinh gui server (PvpClient) - server kiem quyen
    // (chu/to doi/bang hoi) va tra lai noi dung ruong chinh thuc.
    public partial class BaseUI : CanvasLayer
    {
        public static BaseUI Instance { get; private set; }

        private Panel _menu;
        private Label _menuTitle;
        private Button _repair, _open, _remove, _use;
        private BaseStructure _target;

        private Panel _chest;
        private VBoxContainer _chestList, _bagList;
        private long _chestId;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 20;
            _menu = UiStyle.MakePanel(new Vector2(350, 130), new Vector2(260, 250));
            AddChild(_menu);
            var vb = new VBoxContainer { Position = new Vector2(16, 12), CustomMinimumSize = new Vector2(228, 0) };
            _menu.AddChild(vb);
            _menuTitle = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(228, 0) };
            vb.AddChild(_menuTitle);
            _repair = new Button();
            _repair.Pressed += () => { if (_target != null) PvpClient.Instance.Repair(_target.Info.Id); _menu.Visible = false; };
            vb.AddChild(_repair);
            _use = new Button();
            _use.Pressed += () => { _menu.Visible = false; UseStation(); };
            vb.AddChild(_use);
            _open = new Button();
            _open.Pressed += () => { if (_target != null) PvpClient.Instance.ChestOpen(_target.Info.Id); _menu.Visible = false; };
            vb.AddChild(_open);
            _remove = new Button();
            _remove.Pressed += () => { if (_target != null) PvpClient.Instance.Remove(_target.Info.Id); _menu.Visible = false; };
            vb.AddChild(_remove);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => _menu.Visible = false;
            vb.AddChild(close);
            _menu.Visible = false;

            _chest = UiStyle.MakePanel(new Vector2(160, 60), new Vector2(640, 420));
            AddChild(_chest);
            var title = UiStyle.Title(Loc.T("base.chest_title"));
            title.Position = new Vector2(16, 10);
            _chest.AddChild(title);
            _chestList = MakeColumn(_chest, new Vector2(16, 50), Loc.T("base.chest_col"));
            _bagList = MakeColumn(_chest, new Vector2(328, 50), Loc.T("base.bag_col"));
            var closeChest = new Button { Text = Loc.T("common.close"), Position = new Vector2(270, 380), CustomMinimumSize = new Vector2(100, 28) };
            closeChest.Pressed += () => _chest.Visible = false;
            _chest.AddChild(closeChest);
            _chest.Visible = false;

            Inventory.Instance.InventoryChanged += () => { if (_chest.Visible) RefreshBag(); };
        }

        private static VBoxContainer MakeColumn(Control parent, Vector2 pos, string header)
        {
            var h = UiStyle.Small(header);
            h.Position = pos;
            parent.AddChild(h);
            var scroll = new ScrollContainer { Position = pos + new Vector2(0, 22), CustomMinimumSize = new Vector2(296, 300) };
            parent.AddChild(scroll);
            var list = new VBoxContainer { CustomMinimumSize = new Vector2(284, 0) };
            scroll.AddChild(list);
            return list;
        }

        public void OpenStructure(BaseStructure s)
        {
            _target = s;
            var info = s.Info;
            bool mine = info.OwnerId == NetworkClient.Instance?.MyId;
            _menuTitle.Text = $"{Loc.T("base.kind." + info.Kind)} - {(mine ? Loc.T("base.yours") : info.OwnerName)}\n{info.Hp}/{info.MaxHp}";
            _repair.Text = Loc.T("base.repair");
            _repair.Disabled = info.Hp >= info.MaxHp;
            _open.Text = Loc.T("base.open_chest");
            _open.Visible = info.Kind == "chest";
            int tier = CraftingUI.TierOf(info.Kind);
            _use.Visible = mine && tier > 0;
            _use.Text = string.Format(Loc.T("base.use_station_fmt"), Loc.T("base.kind." + info.Kind));
            _remove.Text = Loc.T("base.remove");
            _remove.Visible = mine;
            _menu.Visible = true;
        }

        // Tram che tao cua minh: Lo Ren -> man hinh ren (+ sua do gan lo); con lai -> che tao theo cap tram.
        private void UseStation()
        {
            if (_target == null) return;
            if (_target.Info.Kind == "forge") (GetTree().GetFirstNodeInGroup("forge_ui") as ForgeUI)?.Open();
            else (GetTree().GetFirstNodeInGroup("crafting_ui") as CraftingUI)?.Open(CraftingUI.TierOf(_target.Info.Kind));
        }

        public void ShowChest(long id, Dictionary<string, int> items)
        {
            _chestId = id;
            foreach (Node c in _chestList.GetChildren()) c.QueueFree();
            foreach (var (item, n) in items.OrderBy(kv => kv.Key))
                _chestList.AddChild(Row(item, n, Loc.T("base.take_one"), Loc.T("base.take_all"), take => Move(item, -take, n)));
            RefreshBag();
            _chest.Visible = true;
        }

        private void RefreshBag()
        {
            foreach (Node c in _bagList.GetChildren()) c.QueueFree();
            var bag = new Dictionary<string, int>();
            foreach (var s in Inventory.Instance.Slots) bag[s.ItemId] = (bag.TryGetValue(s.ItemId, out var v) ? v : 0) + s.Count;
            foreach (var (item, n) in bag.OrderBy(kv => kv.Key))
                _bagList.AddChild(Row(item, n, Loc.T("base.put_one"), Loc.T("base.put_all"), put => Move(item, put, n)));
        }

        private static HBoxContainer Row(string item, int n, string oneText, string allText, System.Action<int> act)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = $"{ItemDatabase.Instance.GetDisplayName(item)} x{n}", CustomMinimumSize = new Vector2(150, 0), ClipText = true });
            var one = new Button { Text = oneText };
            one.Pressed += () => act(1);
            row.AddChild(one);
            var all = new Button { Text = allText };
            all.Pressed += () => act(n);
            row.AddChild(all);
            return row;
        }

        // Cap nhat tui ngay (khong tao op kinh te - server tu tru/cong qua chest_move), server tra lai
        // ruong chinh thuc + yeu cau dong bo kinh te de sua neu bi tu choi.
        private void Move(string item, int delta, int available)
        {
            int n = Mathf.Min(Mathf.Abs(delta), available);
            if (n <= 0) return;
            using (EconomyClient.Suppress())
            {
                if (delta > 0) { if (!Inventory.Instance.RemoveItem(item, n)) return; }
                else if (!Inventory.Instance.AddItem(item, n)) { Notify.Show(Loc.T("base.bag_full")); return; }
            }
            PvpClient.Instance.ChestMove(_chestId, item, delta > 0 ? n : -n);
        }

        public override void _Input(InputEvent e)
        {
            if ((_menu.Visible || _chest.Visible) && e.IsActionPressed("ui_cancel"))
            {
                _menu.Visible = false;
                _chest.Visible = false;
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
