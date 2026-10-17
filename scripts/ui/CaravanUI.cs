using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Bang hang doan thuong nhan luu dong: do hiem, so luong co han (server tru ton kho, ai mua truoc duoc).
    public partial class CaravanUI : CanvasLayer
    {
        public static CaravanUI Instance { get; private set; }
        private VBoxContainer _list;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 20;
            var panel = UiStyle.MakePanel(new Vector2(250, 70), new Vector2(460, 400));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(18, 14), CustomMinimumSize = new Vector2(424, 0) };
            panel.AddChild(vb);
            vb.AddChild(UiStyle.Title(Loc.T("world.caravan_title")));
            vb.AddChild(UiStyle.Small(Loc.T("world.caravan_hint")));
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(424, 280) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(410, 0) };
            scroll.AddChild(_list);
            var close = new Button { Text = Loc.T("common.close") };
            close.Pressed += () => Visible = false;
            vb.AddChild(close);
            Visible = false;
        }

        public void Open() { Visible = true; Refresh(); }

        public void Refresh()
        {
            if (!Visible) return;
            UiStyle.Clear(_list);
            var cv = WorldEventsClient.Instance?.CurrentCaravan;
            if (cv == null) { _list.AddChild(UiStyle.Small(Loc.T("world.caravan_gone"))); return; }
            foreach (var o in cv.Stock)
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = $"{ItemDatabase.Instance.GetDisplayName(o.Item)}", CustomMinimumSize = new Vector2(190, 0) });
                row.AddChild(new Label { Text = string.Format(Loc.T("world.caravan_offer_fmt"), o.Price, o.Left), CustomMinimumSize = new Vector2(130, 0) });
                var buy = new Button { Text = Loc.T("world.caravan_buy"), Disabled = o.Left <= 0 || GameManager.Instance.Gold < o.Price };
                string item = o.Item;
                buy.Pressed += () => WorldEventsClient.Instance?.Buy(item, 1);
                row.AddChild(buy);
                _list.AddChild(row);
            }
        }

        public override void _Input(InputEvent e)
        {
            if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
        }
    }
}
