using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.PartnersUI
{
    public static class OfflinePartnerConversationUiSessionFactory
    {
        public static OfflinePartnerConversationUiSession Create(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog character, OfficialPartnerCatalog partners, Func<DateTime> clock,
            Func<bool> functionLimited, int serverTimeZoneHours,
            IOfflinePartnerDialoguePresentation dialoguePresentation,
            IOfflinePartnerDialoguePlaybackPresentation playbackPresentation, Action committed)
        {
            if (session == null || character == null || partners == null)
                throw new ArgumentNullException();
            ProgressionResult identity = session.EnsureLocalActorIdentities();
            if (!identity.IsApplied && identity.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(identity.Message ?? "Local partner identities could not be initialized.");

            var conditionRules = new PartnerConversationConditionRules(partners.Conditions,
                partners.Rows("conditions"));
            IPartnerConversationConfiguration configuration = OfficialPartnerConversationAssetCatalog
                .LoadDefault().Load(conditionRules.Bind);
            IOfflinePartnerConversationWorld world = new OfflinePartnerConversationWorld(session,
                OfficialPartnerConversationWorldAssetCatalog.LoadDefault());
            return new OfflinePartnerConversationUiSession(session, character, configuration, world,
                clock, functionLimited, serverTimeZoneHours, dialoguePresentation,
                playbackPresentation, committed);
        }
    }
}
