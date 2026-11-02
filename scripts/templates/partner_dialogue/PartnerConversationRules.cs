using System;
using System.Collections.Generic;

namespace Zjcs.Official.Partners
{
    /*STATUS*/

    /*TYPE*/

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

        /*CREATE*/

        /*ADVANCE*/
    }
}
