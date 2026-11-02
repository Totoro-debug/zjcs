using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Offline.PartnersUI
{
    /// <summary>Original avatar HP widgets consume the same live local player state.</summary>
    public sealed class OfflinePartnerPlayerHpBindings : ITeamPlayerHpServices
    {
        private readonly OfflinePlayerHealth health;
        private readonly Func<bool> mainPlayerAvailable;
        private readonly Func<bool> managerAvailable;

        public OfflinePartnerPlayerHpBindings(OfflinePlayerHealth health, SafeAction onPlayerUpdate,
            Func<bool> mainPlayerAvailable, Func<bool> managerAvailable)
        {
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            OnPlayerUpdate = onPlayerUpdate ?? throw new ArgumentNullException(nameof(onPlayerUpdate));
            this.mainPlayerAvailable = mainPlayerAvailable ?? throw new ArgumentNullException(nameof(mainPlayerAvailable));
            this.managerAvailable = managerAvailable ?? throw new ArgumentNullException(nameof(managerAvailable));
        }

        public bool MainPlayerAvailable => mainPlayerAvailable();
        public bool ManagerAvailable => managerAvailable();
        public int MapCount => health.MapCount;
        public float HpPercent => health.HpPercent;
        public SafeAction OnPlayerUpdate { get; }
        public event Action<float> HpUpdated
        {
            add { health.HpUpdated += value; }
            remove { health.HpUpdated -= value; }
        }
    }
}
