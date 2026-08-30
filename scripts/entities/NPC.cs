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
        [Export] public string NpcName = "Ong Gia Lang";

        // Loi thoai theo cap do tin tuong
        [Export] public string[] DialogueLow = { "Chao nguoi la. Ta chua tin ai de dang." };
        [Export] public string[] DialogueMid = { "A, la cau. Cau dang lam an kha day." };
        [Export] public string[] DialogueHigh = { "Ban cua ta, ta se ke cau nghe mot bi mat..." };

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

        public override void _Ready()
        {
            AddToGroup("npcs");
            var model = GetNodeOrNull<Node3D>("Model");
            if (model != null)
            {
                string path = ModelPathByNpcId.TryGetValue(NpcId, out var p)
                    ? p : "res://assets3d/quaternius/characters/Worker.gltf";
                var player = CharacterRig.Attach(model, path, ModelScale);
                player?.Play("Idle");
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
                var q = ItemDatabase.Instance.GetQuest(QuestToGive);
                line += $"\n\n[Nhiem vu moi: {q?.Title}]";
            }

            // Mo cua hang neu co
            if (ShopItems.Length > 0)
                line += "\n(Nhan cua hang: mua hat giong & do dung)";

            dialogue?.Show(NpcName, line);

            if (ShopItems.Length > 0)
                OpenShop();
        }

        private string PickDialogue()
        {
            string[] pool = Trust >= 60 ? DialogueHigh
                          : Trust >= 25 ? DialogueMid
                          : DialogueLow;
            if (pool == null || pool.Length == 0) return "...";
            return pool[(int)(GD.Randi() % (uint)pool.Length)];
        }

        private void OpenShop()
        {
            var shop = GetTree().GetFirstNodeInGroup("shop_ui") as ShopUI;
            shop?.Open(this);
        }
    }
}
