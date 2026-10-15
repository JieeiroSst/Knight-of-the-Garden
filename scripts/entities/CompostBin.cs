using Godot;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Thung U Phan: bo 5 do huu co (nong san/do hai luom/rong/thao duoc) vao, sau 2 ngay ra 4
    // Phan Bon Co Ban - tan dung nong san xau/du thua thay vi vut bo. Trang thai la STATIC (chi
    // co 1 thung trong the gioi) de SaveSystem luu/nap truc tiep, khong phu thuoc thu tu dung canh.
    public partial class CompostBin : FarmFacility
    {
        public const int BatchSize = 5;
        public const int CompostDays = 2;
        public const int OutputCount = 4;
        public const string OutputId = "fertilizer_basic";

        public static int Filled = 0;
        public static int DaysLeft = 0;
        public static bool IsReady = false;

        private MeshInstance3D _fill;
        private int _shownFill = -1; // cap nhat hinh khi trang thai tinh doi (vd vua nap save)

        public override void _Ready()
        {
            AddToGroup("compost_bins");
            AddCollisionBox(new Vector3(34, 24, 34));
            var slat = new Color(0.3f, 0.45f, 0.22f);
            AddBox(new Vector3(34, 22, 3), new Vector3(0, 11, 15.5f), slat);
            AddBox(new Vector3(34, 22, 3), new Vector3(0, 11, -15.5f), slat);
            AddBox(new Vector3(3, 22, 28), new Vector3(15.5f, 11, 0), slat);
            AddBox(new Vector3(3, 22, 28), new Vector3(-15.5f, 11, 0), slat);
            _fill = AddBox(new Vector3(28, 1, 28), new Vector3(0, 2, 0), new Color(0.25f, 0.17f, 0.1f));
            GameManager.Instance.DayChanged += OnDayChanged;
            UpdateVisual();
        }

        public override void _ExitTree()
        {
            GameManager.Instance.DayChanged -= OnDayChanged;
        }

        public override void _Process(double delta)
        {
            int key = IsReady ? 100 : Filled;
            if (key != _shownFill) UpdateVisual();
        }

        public static void Reset()
        {
            Filled = 0;
            DaysLeft = 0;
            IsReady = false;
        }

        private void OnDayChanged(int day)
        {
            if (Filled >= BatchSize && DaysLeft > 0 && --DaysLeft == 0)
                IsReady = true;
            UpdateVisual();
        }

        private static bool IsOrganic(ItemDef def)
        {
            if (def == null) return false;
            if (def.Type == ItemType.Crop) return true;
            if (ItemDatabase.Instance.Forage.Any(f => f.ItemId == def.Id)) return true;
            return def.Id is "rong" or "sen" or "thao_duoc_rung" or "thao_duoc_dam_lay";
        }

        public override void Interact()
        {
            if (IsReady)
            {
                if (!Inventory.Instance.AddItem(OutputId, OutputCount, "compost")) { Notify.Show(Loc.T("mail.inventory_full")); return; }
                Reset();
                AchievementSystem.Instance?.Add("compost_made");
                Notify.Show(string.Format(Loc.T("compost.collected_fmt"), OutputCount, ItemDatabase.Instance.GetDisplayName(OutputId)));
                UpdateVisual();
                return;
            }
            if (Filled >= BatchSize)
            {
                Notify.Show(string.Format(Loc.T("compost.busy_fmt"), DaysLeft));
                return;
            }

            // Bo vao do huu co RE NHAT truoc (giu lai nong san gia tri cao). Tim truoc roi moi
            // RemoveItem - khong sua Inventory.Slots trong luc dang duyet.
            var candidates = Inventory.Instance.Slots
                .Select(s => ItemDatabase.Instance.GetItem(s.ItemId))
                .Where(IsOrganic)
                .Distinct()
                .OrderBy(d => d.SellPrice)
                .ToList();
            int added = 0;
            foreach (var def in candidates)
            {
                while (Filled < BatchSize && Inventory.Instance.RemoveItem(def.Id, 1)) { Filled++; added++; }
                if (Filled >= BatchSize) break;
            }
            if (added == 0)
            {
                Notify.Show(Loc.T("compost.need_organic"));
                return;
            }
            if (Filled >= BatchSize)
            {
                DaysLeft = CompostDays;
                Notify.Show(string.Format(Loc.T("compost.started_fmt"), CompostDays));
            }
            else Notify.Show(string.Format(Loc.T("compost.added_fmt"), added, Filled, BatchSize));
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_fill == null) return;
            _shownFill = IsReady ? 100 : Filled;
            float h = IsReady ? 18f : Mathf.Max(1f, 18f * Filled / BatchSize);
            _fill.Scale = new Vector3(1, h, 1);
            _fill.Position = new Vector3(0, h / 2f, 0);
        }
    }
}
