using Godot;
using System.Collections.Generic;

namespace HiepSiVeVuon.Systems
{
    // Man hinh UI dung code xay dung TRUC TIEP moi Label/Button MOT LAN trong _Ready() - khi
    // nguoi choi doi ngon ngu (Loc.LanguageChanged) can VIET LAI chu tren cac Control DA TON TAI
    // do, khong xay lai ca man hinh. Lop nay ghi nho "Control nao dung khoa nao" (dang ky qua
    // Track) de Refresh() chi can lap lai va gan .Text = Loc.T(key) cho tung cai - moi man hinh
    // localize chi can 3 dong: 1 truong field, goi Track() thay vi gan Text truc tiep luc xay
    // dung, va subscribe Refresh vao Loc.LanguageChanged (nho huy dang ky luc _ExitTree tranh ro
    // ri bo nho tham chieu Control da bi giai phong khi doi scene).
    public class LocalizedLabelSet
    {
        private readonly List<(Label label, string key)> _labels = new();
        private readonly List<(Button button, string key)> _buttons = new();
        private readonly List<(LineEdit lineEdit, string key)> _lineEdits = new();

        public Label Track(Label label, string key)
        {
            _labels.Add((label, key));
            label.Text = Loc.T(key);
            return label;
        }

        public Button Track(Button button, string key)
        {
            _buttons.Add((button, key));
            button.Text = Loc.T(key);
            return button;
        }

        public LineEdit Track(LineEdit lineEdit, string key)
        {
            _lineEdits.Add((lineEdit, key));
            lineEdit.PlaceholderText = Loc.T(key);
            return lineEdit;
        }

        public void Refresh()
        {
            foreach (var (label, key) in _labels)
                if (GodotObject.IsInstanceValid(label)) label.Text = Loc.T(key);
            foreach (var (button, key) in _buttons)
                if (GodotObject.IsInstanceValid(button)) button.Text = Loc.T(key);
            foreach (var (lineEdit, key) in _lineEdits)
                if (GodotObject.IsInstanceValid(lineEdit)) lineEdit.PlaceholderText = Loc.T(key);
        }
    }
}
