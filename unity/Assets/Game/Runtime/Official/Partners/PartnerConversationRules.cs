using System;
using System.Collections.Generic;

namespace Zjcs.Official.Partners
{
    public enum PartnerConversationStatus
{
    InProgress,
    Pause,
    End
}

    public enum PartnerConversationType
{
    None,
    Auto,
    Daily
}

    public interface IPartnerConversationCondition
    {
        bool IsMet(IPartnerConversationPlayer player);
    }
    public interface IPartnerConversationConfiguration
    {
        PartnerConversationNode Get(int dialogueClassId, int node);
    }
    public interface IPartnerConversationPlayer
    {
        IPartnerConversationConfiguration DialogueConfiguration { get; }
        DateTime Now { get; }
        void FireNpcDialogueUpdate(PartnerConversationRecord record);
    }
    public sealed class PartnerConversationNode
    {
        public int ClassId, Node, SpokesMan, RewardFriendship;
        public PartnerConversationType Type;
        public List<int> NextIds, NextNodes;
        public bool IsDayLimit;
        public IPartnerConversationCondition UnlockCondition, ChainSequenceCondition;
        public Dictionary<int, long> RewardItem;
    }
    // Original persisted fields and mutation order; database identity generation,
    // clock and player/configuration lookup are explicit local-service boundaries.
    public sealed class PartnerConversationRecord
    {
        public long PlayerId, NpcId, DialogueId;
        public int DialogueClassId, CurrentNode;
        public List<int> DialogueNodes = new List<int>();
        public PartnerConversationStatus DialogueStatus;
        public DateTime DateTime;

            public static PartnerConversationRecord Create(long playerId, long npcId, int dialogClassId, int nodeId, long dialogueId, DateTime dateTime)
    {
        PartnerConversationRecord playerNpcDialogueDataWrap = new PartnerConversationRecord();
        playerNpcDialogueDataWrap.PlayerId = playerId;
        playerNpcDialogueDataWrap.NpcId = npcId;
        playerNpcDialogueDataWrap.DialogueId = dialogueId;
        playerNpcDialogueDataWrap.DialogueClassId = dialogClassId;
        playerNpcDialogueDataWrap.CurrentNode = nodeId;
        playerNpcDialogueDataWrap.DialogueStatus = PartnerConversationStatus.InProgress;
        playerNpcDialogueDataWrap.DateTime = dateTime;
        playerNpcDialogueDataWrap.DialogueNodes.Add(nodeId);
        return playerNpcDialogueDataWrap;
    }

            public void SetCurrentNodeAndUpdateStatus(int nodeId, IPartnerConversationPlayer player)
    {
        CurrentNode = nodeId;
        DialogueNodes.Add(nodeId);
        DateTime = player.Now;
        PartnerConversationStatus dialogueStatus = DialogueStatus;
        IPartnerConversationConfiguration npcDialogueInfoParser = player.DialogueConfiguration;
        PartnerConversationNode npcDialogueInfo = npcDialogueInfoParser.Get(DialogueClassId, CurrentNode);
        if ((npcDialogueInfo.NextNodes?.Count ?? 0) <= 0)
        {
            dialogueStatus = PartnerConversationStatus.End;
        }
        else
        {
            foreach (int nextNode in npcDialogueInfo.NextNodes)
            {
                if (!npcDialogueInfoParser.Get(DialogueClassId, nextNode).UnlockCondition.IsMet(player))
                {
                    dialogueStatus = PartnerConversationStatus.Pause;
                    break;
                }
            }
        }
        DialogueStatus = dialogueStatus;
        player.FireNpcDialogueUpdate(this);
    }
    }
}
