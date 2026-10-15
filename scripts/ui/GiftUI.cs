using Godot;
using System.Linq;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Tang qua NPC - bam [G] canh NPC (xem Player.TryGiveGift). Hien so tim, sinh nhat, danh
    // sach vat pham trong tui do de chon tang (1 lan/ngay/NPC, xem FriendshipSystem.GiveGift).
    public partial class GiftUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _title, _info;
        private string _npcId;
        private ProgressBar _heartBar;

        public override void _Ready()
        {
            AddToGroup("gift_ui");
            var panel = UiStyle.MakePanel(new Vector2(260, 50), new Vector2(440, 440));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(400, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            _heartBar = HeartBar(0);
            vb.AddChild(_heartBar);
            _info = UiStyle.Small("");
            vb.AddChild(_info);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(400, 300) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(385, 0) };
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }

        public void Open(NPC npc)
        {
            _npcId = npc.NpcId;
            FriendshipSystem.Instance.RegisterName(npc.NpcId, npc.NpcName);
            Refresh();
            Visible = true;
        }

        // Thanh do than thiet (0..1000 diem = 10 tim) - dung ProgressBar thay vi ky tu tim de khong
        // phu thuoc font co glyph hay khong.
        public static ProgressBar HeartBar(int points)
        {
            var bar = new ProgressBar { MaxValue = FriendshipSystem.MaxPoints, Value = points, CustomMinimumSize = new Vector2(150, 14), ShowPercentage = false };
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.9f, 0.35f, 0.45f) });
            bar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            return bar;
        }

        private void Refresh()
        {
            var fs = FriendshipSystem.Instance;
            var db = ItemDatabase.Instance;
            _title.Text = string.Format(Loc.T("gift.title_fmt"), fs.GetName(_npcId));
            _heartBar.Value = fs.GetPoints(_npcId);
            string info = string.Format(Loc.T("gift.hearts_fmt"), fs.GetHearts(_npcId)) + "   " + fs.GetBirthdayText(_npcId);
            if (fs.IsBirthdayToday(_npcId)) info += "  " + Loc.T("gift.bday_today");
            if (fs.GiftedToday(_npcId)) info += "\n" + Loc.T("gift.already_today");
            _info.Text = info;

            UiStyle.Clear(_list);
            bool canGift = !fs.GiftedToday(_npcId);
            foreach (var id in Inventory.Instance.Slots.Select(s => s.ItemId).Distinct().ToList())
            {
                var def = db.GetItem(id);
                if (def == null || def.Type == ItemType.Quest) continue;
                var row = new HBoxContainer();
                _list.AddChild(row);
                row.AddChild(new Label { Text = $"{db.GetDisplayName(id)} x{Inventory.Instance.CountOf(id)}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true });
                var btn = new Button { Text = Loc.T("gift.give_btn"), Disabled = !canGift };
                btn.Pressed += () =>
                {
                    string err = fs.GiveGift(_npcId, id);
                    if (err != null) Notify.Show(err);
                    Visible = false;
                };
                row.AddChild(btn);
            }
        }
    }
}
