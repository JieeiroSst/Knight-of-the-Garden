using Godot;
using System.Linq;
using HiepSiVeVuon.Entities;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Danh sach Cot Da Dich Chuyen da kich hoat - bam de dich chuyen toi (Player.FastTravel).
    public partial class WaystoneUI : CanvasLayer
    {
        private VBoxContainer _list;
        private Label _title;
        private Waystone _from;

        public override void _Ready()
        {
            AddToGroup("waystone_ui");
            var panel = UiStyle.MakePanel(new Vector2(280, 60), new Vector2(400, 420));
            AddChild(panel);
            var vb = new VBoxContainer { Position = new Vector2(20, 14), CustomMinimumSize = new Vector2(360, 0) };
            panel.AddChild(vb);
            _title = UiStyle.Title("");
            vb.AddChild(_title);
            vb.AddChild(new HSeparator());
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(360, 300) };
            vb.AddChild(scroll);
            _list = new VBoxContainer { CustomMinimumSize = new Vector2(345, 0) };
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

        public void Open(Waystone from)
        {
            _from = from;
            _title.Text = Loc.T("waystone.title");
            UiStyle.Clear(_list);
            var stones = GetTree().GetNodesInGroup("waystones").OfType<Waystone>()
                .Where(w => w.Active && w != from).OrderBy(w => Loc.T(w.LabelKey)).ToList();
            if (stones.Count == 0) _list.AddChild(UiStyle.Small(Loc.T("waystone.none")));
            foreach (var w in stones)
            {
                var b = new Button { Text = Loc.T(w.LabelKey), Alignment = HorizontalAlignment.Left };
                var target = w;
                b.Pressed += () =>
                {
                    Visible = false;
                    (GetTree().GetFirstNodeInGroup("player") as Player)?.FastTravel(target.ArrivalPoint);
                };
                _list.AddChild(b);
            }
            Visible = true;
        }
    }
}
