using System;
using UnityEngine;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    public sealed class PartnerDialogueAck : CustomYieldInstruction
    {
        public bool Error { get; }
        public PartnerDialogueAck(bool error) { Error = error; }
        public override bool keepWaiting => false;
    }

    public interface IPartnerDialoguePlaybackServices : IPartnerDialogueUiServices
    {
        IPartnerConversationConfiguration Configuration { get; }
        string GetNpcNameKey(int npcClassId);
        void InitializeActorIcon(Component icon, long actorId, PartnerDialogueSpokesMan speaker);
        ItemIcon GetRewardIcon(int itemClassId, Transform parent);
        void RecycleRewardIcon(ItemIcon icon);
        PartnerDialogueAck Acknowledge(long dialogueId, int nodeId, bool invokeByPlayer);
    }

    // Keep the original award payload opaque to this routing controller. The
    // host binds the real source category and the original award window.
    public interface IPartnerDialogueAwardServices
    {
        void RegisterHandler(Func<object, bool> handler);
        void RemoveHandler(Func<object, bool> handler);
        bool IsConversationAward(object message);
        void PopupAward(object message);
    }

}
