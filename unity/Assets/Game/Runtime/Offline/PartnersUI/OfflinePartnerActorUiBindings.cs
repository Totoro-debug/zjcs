using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Offline.PartnersUI
{
    // Shared by the original partner window, conversation window and their
    // pooled rows. Each widget keeps its original self/NPC and portrait branch.
    public sealed class OfflinePartnerActorUiBindings
    {
        private readonly ITeamIconServices icons;
        private readonly ITeamPlayerHpServices health;
        private readonly OfflineProfessionUiPlayerBindings appearance;
        private readonly ICharacterPortraitManager portraits;

        public OfflinePartnerActorUiBindings(ITeamIconServices icons, ITeamPlayerHpServices health,
            OfflineProfessionUiPlayerBindings appearance, ICharacterPortraitManager portraits)
        {
            this.icons = icons ?? throw new ArgumentNullException(nameof(icons));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.portraits = portraits ?? throw new ArgumentNullException(nameof(portraits));
            if (icons.PlayerInfo == null) throw new ArgumentException("Local player-info bindings are required.", nameof(icons));
        }

        public void BindTree(GameObject root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            // Do not initialize widgets here: their original controllers decide
            // which actor to display after all services have been installed.
            foreach (var widget in root.GetComponentsInChildren<PlayerInfoUI>(true))
                widget.ConfigureInfo(icons.PlayerInfo);
            foreach (var widget in root.GetComponentsInChildren<PlayerIconMe>(true))
                widget.ConfigureInfo(icons.PlayerInfo);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerIconOther>(true))
                widget.Configure(icons);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerIcon>(true))
                widget.Configure(icons);
            foreach (var widget in root.GetComponentsInChildren<TeamPlayerUIBase>(true))
                widget.Configure(health);
            foreach (var widget in root.GetComponentsInChildren<CharacterPortraitBase>(true))
                widget.Bind(portraits);
            appearance.BindTree(root);
        }
    }
}
