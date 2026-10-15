using Godot;
using System.Collections.Generic;
using System.Linq;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Data;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Thung Giao Hang (xem ShippingBin.cs/ShippingUI.cs) - nay do SERVER giu (game online):
    // bo hang vao = op "ship" (server tru do + chot gia), sang ngay game moi server tu ban va cong vang,
    // client nhan su kien "ship_payout" o lan dong bo ke tiep (EconomyClient.HandleEvent -> thu bao cao).
    // Lop nay chi la LOP HIEN THI + chuyen tiep thao tac sang EconomyClient.
    public partial class ShippingSystem : Node
    {
        public static ShippingSystem Instance { get; private set; }

        // Giu kieu Entry cu (dung lam kieu luu chung o HomeSystem/SaveSystem).
        public class Entry
        {
            public string Id { get; set; }
            public int Count { get; set; }
        }

        [Signal] public delegate void BinChangedEventHandler();

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            EconomyClient.Instance.SnapshotApplied += () => EmitSignal(SignalName.BinChanged);
            // Sang ngay moi: xin dong bo de nhan tien hang da giao ngay hom truoc.
            GameManager.Instance.DayChanged += _ => EconomyClient.Instance.RequestSync();
        }

        // Hang bo vao HOM NAY (con lay lai duoc). Hang ngay truoc dang cho server thanh toan.
        public List<Entry> Bin => EconomyClient.Instance.Bin
            .Where(b => b.day == GameManager.Instance.Day)
            .Select(b => new Entry { Id = b.item, Count = b.count })
            .ToList();

        public static bool CanShip(ItemDef def) =>
            def != null && def.SellPrice > 0 && def.Type is not (ItemType.Quest or ItemType.Weapon or ItemType.Armor or ItemType.Tool or ItemType.Outfit);

        private static float MultOf(ItemDef def) => EconomyClient.ClampMult(GameManager.Instance.GetSeasonalPriceMultiplier(def));

        public int PriceOf(string itemId)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            return def == null ? 0 : Mathf.RoundToInt(def.SellPrice * MultOf(def));
        }

        public int EstimatedTotal => EconomyClient.Instance.Bin
            .Where(b => b.day == GameManager.Instance.Day)
            .Sum(b => (b.unitPrice > 0 ? b.unitPrice : PriceOf(b.item)) * b.count);

        public bool Ship(string itemId, int count)
        {
            var def = ItemDatabase.Instance.GetItem(itemId);
            if (!CanShip(def) || count <= 0) return false;
            count = Mathf.Min(count, Inventory.Instance.CountOf(itemId));
            if (count <= 0 || !EconomyClient.Instance.Ship(itemId, count, MultOf(def))) return false;
            EmitSignal(SignalName.BinChanged);
            return true;
        }

        public bool TakeBack(string itemId)
        {
            if (!EconomyClient.Instance.Unship(itemId)) return false;
            EmitSignal(SignalName.BinChanged);
            return true;
        }

        // Giu API cu (SaveSystem goi khi nap) - thung nay khong con luu o client.
        public void Reset() => EmitSignal(SignalName.BinChanged);
    }
}
