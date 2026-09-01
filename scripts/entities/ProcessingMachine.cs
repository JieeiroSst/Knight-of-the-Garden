using Godot;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Entities
{
    // May che bien nong san (Lo Say/May Ep/May Lam Pho Mai/May Mayonnaise/May Det, xem
    // Main.BuildProcessingArea) - nguoi choi bam [E]: (1) may TRONG + co nguyen lieu hop le trong
    // tui do -> bo 1 nguyen lieu vao, bat dau dem ngay; (2) DANG che bien -> bao con bao nhieu
    // ngay; (3) XONG -> lay thanh pham ra. Dung MOT script chung cho moi loai may (giong tinh
    // than WildAnimal.cs/Enemy.cs) - khac nhau qua [Export].
    public partial class ProcessingMachine : StaticBody3D
    {
        [Export] public string MachineName = "May Che Bien";
        // AcceptsAnyCrop=true (Lo Say/May Ep): nhan BAT KY vat pham ItemType.Crop nao, dau ra ghep
        // OutputPrefix+inputId (vd "mut_"+"wheat"="mut_wheat"). false: chi nhan DUNG FixedInputId
        // (vd May Lam Pho Mai chi nhan "milk"), dau ra la FixedOutputId co dinh.
        [Export] public bool AcceptsAnyCrop = false;
        [Export] public string FixedInputId = "";
        [Export] public string OutputPrefix = "";
        [Export] public string FixedOutputId = "";
        [Export] public int ProcessDays = 2;

        private string _pendingOutputId = null;
        private int _daysLeft = 0;

        public bool IsBusy => _pendingOutputId != null && _daysLeft > 0;
        public bool IsDone => _pendingOutputId != null && _daysLeft <= 0;

        public override void _Ready()
        {
            AddToGroup("processing_machines");
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        private void OnDayChanged(int day)
        {
            if (_pendingOutputId != null && _daysLeft > 0) _daysLeft--;
        }

        public void Interact()
        {
            if (IsDone)
            {
                Inventory.Instance.AddItem(_pendingOutputId, 1);
                var outDef = ItemDatabase.Instance.GetItem(_pendingOutputId);
                GD.Print($"{MachineName}: lay ra {outDef?.Name}.");
                _pendingOutputId = null;
                return;
            }
            if (IsBusy)
            {
                GD.Print($"{MachineName} dang che bien, con {_daysLeft} ngay.");
                return;
            }

            if (AcceptsAnyCrop)
            {
                // Tim TRUOC (khong sua Slots), roi moi RemoveItem RIENG - goi RemoveItem NGAY
                // trong foreach dang duyet Inventory.Instance.Slots se lam thay doi chinh list dang
                // duyet (RemoveItem co the xoa han 1 slot), nem InvalidOperationException.
                string foundId = null;
                foreach (var stack in Inventory.Instance.Slots)
                {
                    var def = ItemDatabase.Instance.GetItem(stack.ItemId);
                    if (def != null && def.Type == ItemType.Crop) { foundId = stack.ItemId; break; }
                }
                if (foundId != null && Inventory.Instance.RemoveItem(foundId, 1))
                {
                    _pendingOutputId = OutputPrefix + foundId;
                    _daysLeft = ProcessDays;
                    var def = ItemDatabase.Instance.GetItem(foundId);
                    GD.Print($"{MachineName}: dang che bien {def?.Name}, {ProcessDays} ngay nua xong.");
                }
                else
                {
                    GD.Print($"{MachineName}: can nong san (crop) de che bien.");
                }
            }
            else if (!string.IsNullOrEmpty(FixedInputId) && Inventory.Instance.RemoveItem(FixedInputId, 1))
            {
                _pendingOutputId = FixedOutputId;
                _daysLeft = ProcessDays;
                GD.Print($"{MachineName}: dang che bien, {ProcessDays} ngay nua xong.");
            }
            else
            {
                var inDef = ItemDatabase.Instance.GetItem(FixedInputId);
                GD.Print($"{MachineName}: can {inDef?.Name ?? FixedInputId} de che bien.");
            }
        }
    }
}
