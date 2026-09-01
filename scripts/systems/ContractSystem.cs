using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Hop dong dai han: ky 1 lan, sau do TU DONG giao hang dinh ky (khong can nguoi choi bam nut
    // giao tung lan) - moi ky han (GameManager.DayChanged), he thong tu RUT hang tu FarmStorage
    // (thuong vang neu du, phat vang neu thieu). Danh muc hop dong hardcode nho (khong can file
    // JSON rieng - quy mo chi vai muc).
    public class ContractDef
    {
        public string Id;
        public string Title;
        public string ItemId;
        public int AmountPerDelivery;
        public int IntervalDays;
        public int TotalDeliveries;
        public int RewardGold;
        public int PenaltyGold;
    }

    public class ContractProgress
    {
        public int DeliveriesDone;
        public int NextDueDay;
        public int MissedCount;
    }

    public partial class ContractSystem : Node
    {
        public static ContractSystem Instance { get; private set; }

        public static readonly List<ContractDef> Catalog = new()
        {
            new ContractDef { Id = "contract_wheat", Title = "Hop Dong Lua Mi", ItemId = "wheat", AmountPerDelivery = 10, IntervalDays = 7, TotalDeliveries = 4, RewardGold = 80, PenaltyGold = 40 },
            new ContractDef { Id = "contract_milk", Title = "Hop Dong Sua Bo", ItemId = "milk", AmountPerDelivery = 8, IntervalDays = 7, TotalDeliveries = 4, RewardGold = 90, PenaltyGold = 45 },
            new ContractDef { Id = "contract_wool", Title = "Hop Dong Len Cuu", ItemId = "wool", AmountPerDelivery = 6, IntervalDays = 7, TotalDeliveries = 4, RewardGold = 100, PenaltyGold = 50 },
            new ContractDef { Id = "contract_egg", Title = "Hop Dong Trung Ga", ItemId = "egg", AmountPerDelivery = 12, IntervalDays = 7, TotalDeliveries = 4, RewardGold = 70, PenaltyGold = 35 },
        };

        // contractId -> tien do hien tai
        public Dictionary<string, ContractProgress> Active = new();
        public HashSet<string> Completed = new();

        [Signal] public delegate void ContractUpdatedEventHandler(string contractId);

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            GameManager.Instance.DayChanged += OnDayChanged;
        }

        public static ContractDef GetDef(string id) => Catalog.Find(c => c.Id == id);

        public void SignContract(string id)
        {
            if (Active.ContainsKey(id) || Completed.Contains(id)) return;
            var def = GetDef(id);
            if (def == null) return;
            Active[id] = new ContractProgress { DeliveriesDone = 0, NextDueDay = GameManager.Instance.Day + def.IntervalDays };
            GD.Print($"Da ky hop dong: {def.Title}");
            EmitSignal(SignalName.ContractUpdated, id);
        }

        // Moi ngay THAT kiem tra cac hop dong da toi han giao - lien ket TRUC TIEP voi Market.cs:
        // giao hang thanh cong RUT tu FarmStorage, tu dong lam giam ton kho -> day gia mat hang do
        // len o cho khac (dung y "kinh te lien thong" nguoi dung mo ta).
        private void OnDayChanged(int day)
        {
            var toComplete = new List<string>();
            foreach (var kv in Active)
            {
                var def = GetDef(kv.Key);
                if (def == null) continue;
                var prog = kv.Value;
                if (day < prog.NextDueDay) continue;

                if (FarmStorage.Instance.TryRemove(def.ItemId, def.AmountPerDelivery))
                {
                    GameManager.Instance.AddGold(def.RewardGold);
                    prog.DeliveriesDone++;
                    GD.Print($"Da giao hop dong '{def.Title}' ({prog.DeliveriesDone}/{def.TotalDeliveries}).");
                }
                else
                {
                    GameManager.Instance.SpendGold(def.PenaltyGold); // bo qua neu khong du tien - khong co co che no
                    prog.MissedCount++;
                    GD.Print($"Thieu hang giao hop dong '{def.Title}' - bi phat {def.PenaltyGold} vang.");
                }
                prog.NextDueDay = day + def.IntervalDays;
                EmitSignal(SignalName.ContractUpdated, kv.Key);

                if (prog.DeliveriesDone >= def.TotalDeliveries) toComplete.Add(kv.Key);
            }
            foreach (var id in toComplete)
            {
                Active.Remove(id);
                Completed.Add(id);
                GD.Print($"Hoan thanh hop dong: {GetDef(id)?.Title}");
                EmitSignal(SignalName.ContractUpdated, id);
            }
        }

        public bool IsActive(string id) => Active.ContainsKey(id);
        public bool IsCompleted(string id) => Completed.Contains(id);

        public void Reset()
        {
            Active.Clear();
            Completed.Clear();
        }
    }
}
