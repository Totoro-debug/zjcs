using System.Collections.Generic;

namespace Zjcs.Official.PartnersUI
{
    public enum ComparerOrderType { Asc, Desc }

    // Original NpcDialogueComparer, dialogue red-dot extensions and language key.
    public static class PartnerDialogueListRules
    {
        public static int Compare(long x, long y, ComparerOrderType order)
        {
            long value;
            switch (order)
            {
                case ComparerOrderType.Asc: value = unchecked(x - y); break;
                case ComparerOrderType.Desc: value = unchecked(y - x); break;
                default:
                    UnityEngine.Debug.LogError("unsupport comparerOrderType : " + order);
                    return 0;
            }
            if (value == 0) return 0;
            if (value <= 0) return -1;
            return 1;
        }

        public static int CompareDialogues(PartnerDialogueData x, PartnerDialogueData y)
        {
            int result = Compare((long)x.DialogueStatus, (long)y.DialogueStatus, ComparerOrderType.Asc);
            if (result != 0) return result;
            result = Compare(x.DateTime.Ticks, y.DateTime.Ticks, ComparerOrderType.Desc);
            if (result != 0) return result;
            return Compare(x.DialogueId, y.DialogueId, ComparerOrderType.Asc);
        }

        public static bool HasRedDot(PartnerDialogueData data, IPartnerDialogueUiServices services)
        {
            var nextNodes = services.GetNextNodes(data.DialogueClassId, data.CurrentNode);
            if ((nextNodes?.Count ?? 0) <= 0) return false;
            foreach (int node in nextNodes)
                if (services.IsNodeUnlocked(data.DialogueClassId, node)) return true;
            return false;
        }

        public static int GetRedDotCount(List<PartnerDialogueData> dialogues, IPartnerDialogueUiServices services)
        {
            int result = 0;
            foreach (var dialogue in dialogues)
                if (HasRedDot(dialogue, services)) result++;
            return result;
        }

        public static string GetNpcDialogueTextKey(int dialogueClassId, int dialogueNodeId) =>
            $"npc_dialogue_{dialogueClassId}_{dialogueNodeId}_text";
    }
}
