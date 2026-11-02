using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerDialoguePlaybackPresentation
    {
        string GetNpcNameKey(int npcClassId);
        void InitializeActorIcon(Component icon, long actorId, PartnerDialogueSpokesMan speaker);
        ItemIcon GetRewardIcon(int itemClassId, Transform parent);
        void RecycleRewardIcon(ItemIcon icon);
        void ShowAcknowledgeFailure(ProgressionResult result, bool invokeByPlayer);
    }

    public sealed class OfflinePartnerDialoguePlaybackServices : IPartnerDialoguePlaybackServices
    {
        private readonly OfflinePartnerDialogueUiServices source;
        private readonly OfflinePartnerConversationService transactions;
        private readonly IOfflinePartnerDialoguePlaybackPresentation presentation;
        public OfflinePartnerDialoguePlaybackServices(OfflinePartnerDialogueUiServices source,
            OfflinePartnerConversationService transactions, IPartnerConversationConfiguration configuration,
            IOfflinePartnerDialoguePlaybackPresentation presentation)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }
        public IPartnerConversationConfiguration Configuration { get; }
        public bool MainPlayerAvailable => source.MainPlayerAvailable;
        public string PlayerName => source.PlayerName;
        public int DialogueStartNodeId => source.DialogueStartNodeId;
        public IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs => source.Npcs;
        public IReadOnlyDictionary<long, PartnerDialogueData> Dialogues => source.Dialogues;
        public event Action<PartnerDialogueData> DialogueAdded { add => source.DialogueAdded += value; remove => source.DialogueAdded -= value; }
        public event Action<PartnerDialogueData> DialogueRemoved { add => source.DialogueRemoved += value; remove => source.DialogueRemoved -= value; }
        public event Action<PartnerDialogueData> DialogueUpdated { add => source.DialogueUpdated += value; remove => source.DialogueUpdated -= value; }
        public IReadOnlyList<int> GetNextNodes(int dialogue, int node) => source.GetNextNodes(dialogue, node);
        public bool IsNodeUnlocked(int dialogue, int node) => source.IsNodeUnlocked(dialogue, node);
        public GameObject Instantiate(GameObject prefab, Transform parent, bool reset) => source.Instantiate(prefab, parent, reset);
        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => source.InitializeNpcIcon(icon, npc);
        public void ShowError(int code) => source.ShowError(code);
        public void ShowDialogue(PartnerDialogueData dialogue) => source.ShowDialogue(dialogue);
        public string GetNpcNameKey(int npcClassId) => presentation.GetNpcNameKey(npcClassId);
        public void InitializeActorIcon(Component icon, long actorId, PartnerDialogueSpokesMan speaker) => presentation.InitializeActorIcon(icon, actorId, speaker);
        public ItemIcon GetRewardIcon(int itemClassId, Transform parent) => presentation.GetRewardIcon(itemClassId, parent);
        public void RecycleRewardIcon(ItemIcon icon) => presentation.RecycleRewardIcon(icon);
        public PartnerDialogueAck Acknowledge(long dialogueId, int nodeId, bool invokeByPlayer)
        {
            ProgressionResult result;
            try { result = transactions.Acknowledge(dialogueId, nodeId); }
            catch (IOException failure) { return SaveFailed(failure, invokeByPlayer); }
            catch (UnauthorizedAccessException failure) { return SaveFailed(failure, invokeByPlayer); }
            if (result.IsApplied) source.SynchronizeCommittedState();
            bool error = !result.IsApplied && result.Status != ProgressionResultStatus.Unchanged;
            if (error) presentation.ShowAcknowledgeFailure(result, invokeByPlayer);
            return new PartnerDialogueAck(error);
        }
        private PartnerDialogueAck SaveFailed(Exception error, bool invokeByPlayer)
        {
            presentation.ShowAcknowledgeFailure(new ProgressionResult {
                Status = ProgressionResultStatus.InvalidState, Message = error.Message }, invokeByPlayer);
            return new PartnerDialogueAck(true);
        }
    }
}
