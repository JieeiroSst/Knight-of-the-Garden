using Godot;
using System.Collections.Generic;
using HiepSiVeVuon.Data;
using HiepSiVeVuon.Core;

namespace HiepSiVeVuon.Systems
{
    // Theo doi tien do nhiem vu. Quest data-driven tu quests.json.
    public partial class QuestSystem : Node
    {
        public static QuestSystem Instance { get; private set; }

        // questId -> tien do hien tai
        public Dictionary<string, int> Active = new();
        public HashSet<string> Completed = new();

        [Signal] public delegate void QuestUpdatedEventHandler(string questId);
        [Signal] public delegate void QuestCompletedEventHandler(string questId);

        public override void _EnterTree()
        {
            Instance = this;
        }

        public void AcceptQuest(string questId)
        {
            if (Active.ContainsKey(questId) || Completed.Contains(questId)) return;
            Active[questId] = 0;
            GD.Print($"Nhận nhiệm vụ: {ItemDatabase.Instance.GetQuest(questId)?.Title}");
            EmitSignal(SignalName.QuestUpdated, questId);
        }

        // Goi khi giet quai
        public void OnEnemyKilled(string enemyId)
        {
            ReportProgress("kill", enemyId);
        }

        // Goi khi thu thap item
        public void OnItemCollected(string itemId)
        {
            ReportProgress("collect", itemId);
        }

        // Goi khi noi chuyen NPC
        public void OnTalkedTo(string npcId)
        {
            ReportProgress("talk", npcId);
        }

        private void ReportProgress(string type, string targetId)
        {
            var toComplete = new List<string>();
            foreach (var kv in Active)
            {
                var def = ItemDatabase.Instance.GetQuest(kv.Key);
                if (def == null) continue;
                if (def.ObjectiveType == type && def.TargetId == targetId)
                {
                    Active[kv.Key] = Mathf.Min(def.TargetCount, kv.Value + 1);
                    EmitSignal(SignalName.QuestUpdated, kv.Key);
                    if (Active[kv.Key] >= def.TargetCount) toComplete.Add(kv.Key);
                }
            }
            foreach (var q in toComplete) CompleteQuest(q);
        }

        private void CompleteQuest(string questId)
        {
            var def = ItemDatabase.Instance.GetQuest(questId);
            if (def == null) return;

            // Trao thuong
            if (def.RewardGold > 0) GameManager.Instance.AddGold(def.RewardGold);
            if (!string.IsNullOrEmpty(def.RewardItemId))
                Inventory.Instance.AddItem(def.RewardItemId, def.RewardItemCount);

            Active.Remove(questId);
            Completed.Add(questId);
            GD.Print($"Hoàn thành nhiệm vụ: {def.Title}");
            EmitSignal(SignalName.QuestCompleted, questId);
        }

        public int GetProgress(string questId) => Active.TryGetValue(questId, out var v) ? v : 0;
        public bool IsActive(string questId) => Active.ContainsKey(questId);
        public bool IsCompleted(string questId) => Completed.Contains(questId);

        public void Reset()
        {
            Active.Clear();
            Completed.Clear();
        }
    }
}
