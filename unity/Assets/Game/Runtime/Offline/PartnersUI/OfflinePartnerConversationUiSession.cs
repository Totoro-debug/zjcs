using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.PartnersUI
{
    // Compose the actual saved conversation source and its transaction boundary.
    // Creating this host does not create conversations or register clock hooks.
    public sealed class OfflinePartnerConversationUiSession
    {
        private readonly OfflinePartnerConversationService transactions;

        public OfflinePartnerConversationUiSession(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog character, IPartnerConversationConfiguration configuration,
            IOfflinePartnerConversationWorld world, Func<DateTime> clock, Func<bool> functionLimited,
            int serverTimeZoneHours, IOfflinePartnerDialoguePresentation dialoguePresentation,
            IOfflinePartnerDialoguePlaybackPresentation playbackPresentation, Action committed)
        {
            if (committed == null) throw new ArgumentNullException(nameof(committed));
            var player = new OfflinePartnerConversationPlayer(session, character, configuration,
                world, clock, _ => Source.SynchronizeCommittedState());
            Source = new OfflinePartnerDialogueUiServices(session, player, dialoguePresentation);
            transactions = new OfflinePartnerConversationService(session, character, player,
                functionLimited, serverTimeZoneHours, () => {
                    Source.SynchronizeCommittedState();
                    committed();
                });
            Playback = new OfflinePartnerDialoguePlaybackServices(Source, transactions,
                configuration, playbackPresentation);
        }

        public OfflinePartnerDialogueUiServices Source { get; }
        public OfflinePartnerDialoguePlaybackServices Playback { get; }

        // Called by PartnerPanelView once for each actual Open, before controls
        // subscribe. The transaction retains the original order and day limits.
        public void PreparePanelOpen()
        {
            var result = transactions.RefreshOnPanelOpen();
            if (!result.IsApplied && result.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(result.Message ?? "Local partner conversations could not be refreshed.");
            // RefreshOnPanelOpen also reloads on the unchanged/locked branch.
            Source.SynchronizeCommittedState();
        }

        public void Bind(PartnerPanelContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Dialogues != null && !ReferenceEquals(context.Dialogues, Playback))
                throw new InvalidOperationException("The partner window already belongs to another dialogue source.");
            if (context.PrepareOpen != null && context.PrepareOpen != PreparePanelOpen)
                throw new InvalidOperationException("The partner window already has another opening transaction.");
            context.Dialogues = Playback;
            context.BindPartnerTipDotDialogues?.Invoke(Playback);
            context.PrepareOpen = PreparePanelOpen;
        }
    }
}
