using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Giuong ngu (phong ngu tang 2 nha nong dan) - bam [E] de ngu: man hinh toi dan, hoi DAY the
    // luc + mau, va TU DONG LUU game. 1 ngay trong game = 1 ngay that (xem GameManager), nen ngu
    // KHONG tua thoi gian - chi la cach hoi phuc nhanh nhat.
    public partial class Bed : FarmFacility
    {
        private bool _sleeping = false;

        public override void _Ready()
        {
            AddToGroup("beds");
            AddCollisionBox(new Vector3(50, 20, 34));
        }

        public override void Interact()
        {
            if (_sleeping) return;
            _sleeping = true;

            var layer = new CanvasLayer { Layer = 110 };
            AddChild(layer);
            var rect = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(rect);

            var tw = CreateTween();
            tw.TweenProperty(rect, "color:a", 1f, 0.6f);
            tw.TweenCallback(Callable.From(() =>
            {
                var gm = GameManager.Instance;
                gm.RestoreStamina(gm.MaxStamina);
                gm.Heal(gm.MaxHp);
                SaveSystem.Instance.SaveGame();
            }));
            tw.TweenInterval(0.8f);
            tw.TweenProperty(rect, "color:a", 0f, 0.6f);
            tw.TweenCallback(Callable.From(() =>
            {
                layer.QueueFree();
                _sleeping = false;
                Notify.Show(Loc.T("bed.slept"));
            }));
        }
    }
}
