using Godot;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // Ruong bau an khap the gioi (xem Main.BuildAdventure) - mo 1 LAN (luu theo ChestId trong
    // AdventureSystem), ra vang + vat pham theo cap (Tier 1..3). Ruong hang nguc doi ChestId moi
    // tuan nen mo lai duoc.
    public partial class TreasureChest : FarmFacility
    {
        public string ChestId = "";
        public int Tier = 1;

        private MeshInstance3D _lid;
        private OmniLight3D _glow;

        private static readonly string[][] LootByTier =
        {
            new[] { "potion", "thach_anh", "dong_xu_co", "moi_cau", "tra_thao_moc", "dau_tay_seed", "dua_hau_seed" },
            new[] { "hong_ngoc", "ngoc_luc_bao", "tuong_da_co", "dong_xu_co", "banh_nang_luong", "co_vat", "ngo_seed" },
            new[] { "kim_cuong", "mat_na_co", "chen_vang_co", "ngoc_linh_hon", "da_quy", "hong_ngoc" },
        };

        public override void _Ready()
        {
            AddToGroup("treasure_chests");
            AddCollisionBox(new Vector3(30, 22, 22));
            var wood = new Color(0.45f, 0.27f, 0.13f);
            var gold = new Color(0.95f, 0.75f, 0.25f);
            AddBox(new Vector3(30, 14, 20), new Vector3(0, 7, 0), wood);
            AddBox(new Vector3(32, 2, 22), new Vector3(0, 1, 0), gold);
            AddBox(new Vector3(4, 4, 1), new Vector3(0, 10, 10.5f), gold); // o khoa
            _lid = AddBox(new Vector3(30, 6, 20), new Vector3(0, 17, 0), wood.Lightened(0.1f));
            AddBox(new Vector3(32, 1.5f, 22), new Vector3(0, 20.5f, 0), gold);
            _glow = new OmniLight3D { LightColor = gold, LightEnergy = 1.2f, OmniRange = 70f, Position = Vector3.Up * 25f };
            AddChild(_glow);
            UpdateVisual();
        }

        private bool Opened => AdventureSystem.Instance.IsChestOpened(ChestId);

        private void UpdateVisual()
        {
            bool opened = Opened;
            if (_lid != null)
            {
                _lid.Position = opened ? new Vector3(0, 22, -9) : new Vector3(0, 17, 0);
                _lid.RotationDegrees = opened ? new Vector3(-70, 0, 0) : Vector3.Zero;
            }
            if (_glow != null) _glow.Visible = !opened;
        }

        public override void _Process(double delta)
        {
            if (_glow != null && _glow.Visible) _glow.LightEnergy = 1.0f + Mathf.Sin(Time.GetTicksMsec() / 250f) * 0.4f;
            else if (_glow != null && !Opened) UpdateVisual(); // vua nap save
        }

        public override void Interact()
        {
            if (Opened) { Notify.Show(Loc.T("adv.chest_empty")); return; }
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            int tier = Mathf.Clamp(Tier, 1, 3);
            int gold = rng.RandiRange(80, 200) * tier;
            EconomyClient.Instance.GrantGold(gold, "chest", ChestId + "#g");
            var pool = LootByTier[tier - 1];
            int rolls = tier == 3 ? 3 : 2;
            var got = new System.Collections.Generic.List<string>();
            for (int i = 0; i < rolls; i++)
            {
                string id = pool[rng.RandiRange(0, pool.Length - 1)];
                int n = id.EndsWith("_seed") ? 5 : 1;
                EconomyClient.Instance.GrantReward(id, n, "chest", $"{ChestId}#i{i}");
                got.Add($"{ItemDatabase.Instance.GetDisplayName(id)} x{n}");
            }
            AdventureSystem.Instance.MarkChestOpened(ChestId);
            GameManager.Instance.AddExp(25 * tier);
            Notify.Show(string.Format(Loc.T("adv.chest_opened_fmt"), gold, string.Join(", ", got)));
            UpdateVisual();
        }
    }
}
