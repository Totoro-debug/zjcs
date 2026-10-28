using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    public interface ITeamPlayerHpServices
    {
        bool MainPlayerAvailable { get; }
        bool ManagerAvailable { get; }
        int MapCount { get; }
        float HpPercent { get; }
        SafeAction OnPlayerUpdate { get; }
        event Action<float> HpUpdated;
    }

    // Original PlayerUIBase; the manager boundary is explicit before Start.
    public class TeamPlayerUIBase : UIBase
    {
        protected ITeamPlayerHpServices Services;
        public void Configure(ITeamPlayerHpServices value) { Services = value; }
        protected override void Start()
        {
            base.Start();
            if (Services.MainPlayerAvailable) Instance_OnPlayerUpdate();
            if (Services.ManagerAvailable) Services.OnPlayerUpdate.AddListener(Instance_OnPlayerUpdate);
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Services != null && Services.ManagerAvailable)
                Services.OnPlayerUpdate.RemoveListener(Instance_OnPlayerUpdate);
        }
        private void Instance_OnPlayerUpdate()
        {
            if (!Services.MainPlayerAvailable) ResetInitState();
            else InitWrapper(null);
        }
    }
}
