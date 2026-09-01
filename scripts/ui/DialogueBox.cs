using Godot;
using HiepSiVeVuon.Systems;

namespace HiepSiVeVuon.UI
{
    // Hop thoai hien loi NPC. Nhan E/Chuot de dong.
    public partial class DialogueBox : CanvasLayer
    {
        private PanelContainer _panel;
        private Label _nameLabel;
        private Label _textLabel;
        private bool _open = false;
        private readonly LocalizedLabelSet _loc = new();

        public override void _Ready()
        {
            AddToGroup("dialogue_ui");
            Build();
            Loc.LanguageChanged += _loc.Refresh;
        }

        public override void _ExitTree()
        {
            Loc.LanguageChanged -= _loc.Refresh;
        }

        private void Build()
        {
            _panel = new PanelContainer();
            _panel.Position = new Vector2(160, 380);
            _panel.CustomMinimumSize = new Vector2(640, 120);
            _panel.Visible = false;
            AddChild(_panel);

            var vb = new VBoxContainer();
            _panel.AddChild(vb);
            _nameLabel = new Label();
            _nameLabel.AddThemeColorOverride("font_color", Colors.Gold);
            vb.AddChild(_nameLabel);
            _textLabel = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
            _textLabel.CustomMinimumSize = new Vector2(620, 0);
            vb.AddChild(_textLabel);
            var hint = new Label();
            _loc.Track(hint, "dialogue.close_hint");
            hint.AddThemeColorOverride("font_color", Colors.Gray);
            vb.AddChild(hint);
        }

        public void Show(string npcName, string text)
        {
            _nameLabel.Text = npcName;
            _textLabel.Text = text;
            _panel.Visible = true;
            _open = true;
        }

        public override void _Input(InputEvent e)
        {
            if (_open && (e.IsActionPressed("interact") || e.IsActionPressed("attack")))
            {
                _panel.Visible = false;
                _open = false;
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
