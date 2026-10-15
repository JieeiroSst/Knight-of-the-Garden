using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Systems
{
    // [autoload] Thu cung: so huu (nhan nuoi bang the "pet_*" mua o Trai Thu Cung), 1 con dang di theo,
    // cap 1..10 theo EXP (danh quai). Luu qua SaveSystem.
    public partial class PetSystem : Node
    {
        public static PetSystem Instance { get; private set; }

        public const int MaxLevel = 10;
        public HashSet<string> Owned = new();
        public Dictionary<string, int> Xp = new();
        public string Active;

        [Signal] public delegate void ChangedEventHandler();

        public override void _EnterTree() => Instance = this;

        public void Reset()
        {
            Owned.Clear();
            Xp.Clear();
            Active = null;
        }

        public int Level(string petId) => Mathf.Min(MaxLevel, 1 + (Xp.TryGetValue(petId, out var x) ? x : 0) / 100);

        // Nhan nuoi: dung the thu cung trong tui (mua o Trai Thu Cung qua EconomyClient.Buy).
        public bool Adopt(string petId)
        {
            if (Owned.Contains(petId) || !Inventory.Instance.RemoveItem(petId, 1)) return false;
            Owned.Add(petId);
            Active = petId;
            Notify.Show(string.Format(Loc.T("pet.adopted_fmt"), DisplayName(petId)));
            EmitSignal(SignalName.Changed);
            return true;
        }

        public void SetActive(string petId)
        {
            Active = petId != null && Owned.Contains(petId) ? petId : null;
            EmitSignal(SignalName.Changed);
        }

        public void AddXp(string petId, int amount)
        {
            int before = Level(petId);
            Xp[petId] = (Xp.TryGetValue(petId, out var x) ? x : 0) + amount;
            if (Level(petId) > before) Notify.Show(string.Format(Loc.T("pet.levelup_fmt"), DisplayName(petId), Level(petId)));
        }

        public static string DisplayName(string petId) =>
            PetCatalog.Pets.TryGetValue(petId, out var p) ? (Loc.Current == Loc.Lang.EN ? p.NameEn : p.NameVi) : petId;
    }
}
