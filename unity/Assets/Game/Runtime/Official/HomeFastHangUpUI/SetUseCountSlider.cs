using Zjcs.Official.HomeCountControlsUI;

namespace Zjcs.Official.HomeFastHangUpUI
{
    // Keep the native script identity and the existing acceleration service API.
    public class SetUseCountSlider : HomeCountControlsUI.SetUseCountSlider
    {
        private IHomeFastHangUpServices services;
        public IHomeFastHangUpServices Services
        {
            protected get => services;
            set { services = value; LimitServices = value == null ? null : new ServicesAdapter(value); }
        }
        private sealed class ServicesAdapter : IUseCountSliderServices
        {
            private readonly IHomeFastHangUpServices owner;
            public ServicesAdapter(IHomeFastHangUpServices owner) => this.owner = owner;
            public double FastItemMaxCountMessageColdDown => owner.FastItemMaxCountMessageColdDown;
            public string TextFormatted(string key, params object[] args) => owner.Window.TextFormatted(key, args);
            public void SystemMessage(string message) => owner.Window.SystemMessage(message);
        }
    }
}
