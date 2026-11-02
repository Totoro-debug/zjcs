using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.PartnersUI
{
    // Views of the fields consumed by the original dialogue address book. The
    // host retains identity and mutable state; these are not server substitutes.
    public enum PartnerDialogueStatus { InProgress, Pause, End }
    public sealed class PartnerDialogueNpcData
    {
        public long NpcId, PlayerId;
        public int NpcClassId;
    }
    public sealed class PartnerDialogueData
    {
        public long NpcId, DialogueId;
        public int DialogueClassId, CurrentNode;
        public PartnerDialogueStatus DialogueStatus;
        public DateTime DateTime;
    }
    public interface IPartnerDialogueUiServices
    {
        bool MainPlayerAvailable { get; }
        string PlayerName { get; }
        int DialogueStartNodeId { get; }
        IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs { get; }
        IReadOnlyDictionary<long, PartnerDialogueData> Dialogues { get; }
        event Action<PartnerDialogueData> DialogueAdded;
        event Action<PartnerDialogueData> DialogueRemoved;
        event Action<PartnerDialogueData> DialogueUpdated;
        IReadOnlyList<int> GetNextNodes(int dialogueClassId, int currentNode);
        bool IsNodeUnlocked(int dialogueClassId, int nodeId);
        GameObject Instantiate(GameObject prefab, Transform parent, bool resetTransformOnly);
        void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc);
        void ShowError(int code);
        void ShowDialogue(PartnerDialogueData dialogue);
    }
}
