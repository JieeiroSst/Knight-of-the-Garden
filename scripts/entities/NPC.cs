using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Systems;
using HiepSiVeVuon.Core;
using HiepSiVeVuon.UI;

namespace HiepSiVeVuon.Entities
{
    // NPC: hoi thoai theo muc do tin tuong (trust), giao nhiem vu, ban hang.
    // Co che "hoi duong" cot loi cua game: loi thoai thay doi theo do than thiet.
    public partial class NPC : CharacterBody3D
    {
        [Export] public string NpcId = "elder";
        [Export] public string NpcName = "Ông Già Làng";

        // Loi thoai theo cap do tin tuong (tieng Viet - luon co, la nguon goc/fallback)
        [Export] public string[] DialogueLow = { "Chào người lạ. Ta chưa tin ai dễ dàng." };
        [Export] public string[] DialogueMid = { "À, là cậu. Cậu đang làm ăn khá đấy." };
        [Export] public string[] DialogueHigh = { "Bạn của ta, ta sẽ kể cậu nghe một bí mật..." };

        // Ban dich tieng Anh (tuy chon) - neu Loc.Current == EN va mang nay co du lieu, PickDialogue
        // se dung mang nay thay vi mang tieng Viet o tren; neu rong/null thi TU DONG fallback ve
        // tieng Viet (khong bat buoc moi NPC phai co ban dich).
        [Export] public string[] DialogueLowEn = System.Array.Empty<string>();
        [Export] public string[] DialogueMidEn = System.Array.Empty<string>();
        [Export] public string[] DialogueHighEn = System.Array.Empty<string>();

        // Nhiem vu NPC nay giao (id trong quests.json)
        [Export] public string QuestToGive = "";

        // Cua hang: danh sach itemId ban ra
        [Export] public string[] ShopItems = new string[0];

        public int Trust = 0;   // 0..100

        [Export] public float ModelScale = 22f;

        // Moi NPC mot model rieng (Quaternius) cho hop vai trong lang.
        private static readonly Dictionary<string, string> ModelPathByNpcId = new()
        {
            { "elder", "res://assets3d/quaternius/characters/Worker.gltf" },
            { "merchant", "res://assets3d/quaternius/characters/Suit.gltf" },
            { "blacksmith", "res://assets3d/quaternius/characters/Adventurer.gltf" },
            { "herbalist", "res://assets3d/quaternius/characters/Casual_2.gltf" },
            { "ranger", "res://assets3d/quaternius/characters/Swat.gltf" },
        };

        // Cac NpcId KHONG co trong dict tren (vd nguoi cham bo/ngua/ga, va nay la dan thi tran
        // sinh hoat) truoc day deu roi ve CUNG 1 model mac dinh (Worker.gltf), nhin y het nhau
        // hang loat - thay bang chon theo BAM (hash) CO DINH tu NpcId trong 5 model co san, cho
        // moi NPC 1 dien mao rieng nhung ON DINH (cung 1 NpcId luon ra cung 1 model moi lan tai
        // lai, khong doi ngau nhien giua cac lan chay).
        private static readonly string[] FallbackModels =
        {
            "res://assets3d/quaternius/characters/Worker.gltf",
            "res://assets3d/quaternius/characters/Suit.gltf",
            "res://assets3d/quaternius/characters/Adventurer.gltf",
            "res://assets3d/quaternius/characters/Casual_2.gltf",
            "res://assets3d/quaternius/characters/Swat.gltf",
        };

        // Luu lai (thay vi bien cuc bo) de cac lop con (vd FarmhandNpc) co the dieu khien
        // hoat canh Di/Dung khi them AI di chuyen rieng.
        protected Node3D _model;
        protected AnimationPlayer _animPlayer;

        public override void _Ready()
        {
            AddToGroup("npcs");
            _model = GetNodeOrNull<Node3D>("Model");
            if (_model != null)
            {
                string path = ModelPathByNpcId.TryGetValue(NpcId, out var p)
                    ? p : FallbackModels[(uint)NpcId.GetHashCode() % FallbackModels.Length];
                _animPlayer = CharacterRig.Attach(_model, path, ModelScale);
                _animPlayer?.Play("Idle");
            }
        }

        public void Interact()
        {
            QuestSystem.Instance.OnTalkedTo(NpcId);
            Trust = Mathf.Min(100, Trust + 5); // moi lan noi chuyen tang chut tin tuong

            string line = PickDialogue();
            var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueBox;

            // Giao nhiem vu neu du dieu kien
            if (!string.IsNullOrEmpty(QuestToGive)
                && !QuestSystem.Instance.IsActive(QuestToGive)
                && !QuestSystem.Instance.IsCompleted(QuestToGive))
            {
                QuestSystem.Instance.AcceptQuest(QuestToGive);
                string questTitle = ItemDatabase.Instance.GetQuestDisplayTitle(QuestToGive);
                line += "\n\n" + string.Format(Loc.T("npc.new_quest_fmt"), questTitle);
            }

            // Mo cua hang neu co
            if (ShopItems.Length > 0)
                line += "\n" + Loc.T("npc.shop_hint");

            dialogue?.Show(NpcName, line);

            if (ShopItems.Length > 0)
                OpenShop();
        }

        // protected virtual: cho phep NPC con (vd WarehouseManagerNpc) ghi de de tra ve cau thoai
        // DONG (chua so lieu that, vd ton kho hien tai) thay vi chi chon ngau nhien tu 1 mang co
        // dinh nhu mac dinh.
        protected virtual string PickDialogue()
        {
            string[] pool = Trust >= 60 ? PickPool(DialogueHigh, DialogueHighEn)
                          : Trust >= 25 ? PickPool(DialogueMid, DialogueMidEn)
                          : PickPool(DialogueLow, DialogueLowEn);
            if (pool == null || pool.Length == 0) return "...";
            return pool[(int)(GD.Randi() % (uint)pool.Length)];
        }

        // Dung ban dich tieng Anh neu dang o che do EN VA NPC nay co du lieu ban dich (mang khong
        // rong) - neu khong (chua dich hoac dang o VI) thi fallback ve mang tieng Viet goc.
        // protected: cho phep NPC con (vd GuardNpc voi DialogueRain/DialogueNight) tai su dung
        // cung logic thay vi tu viet lai.
        protected static string[] PickPool(string[] vi, string[] en) =>
            Loc.Current == Loc.Lang.EN && en != null && en.Length > 0 ? en : vi;

        private void OpenShop()
        {
            var shop = GetTree().GetFirstNodeInGroup("shop_ui") as ShopUI;
            shop?.Open(this);
        }
    }
}
