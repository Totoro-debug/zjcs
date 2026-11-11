using System;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Official.HomePlayerInfoUI
{
    // Share the already migrated original PlayerUIBase initialization lifecycle.
    public class HomePlayerInfoUIBase : HomeExperiencePlayerUIBase
    {
        protected IHomePlayerInfoBindings InfoBindings => (IHomePlayerInfoBindings)Bindings;
        public void Configure(IHomePlayerInfoBindings bindings)
            => base.Configure(bindings ?? throw new ArgumentNullException(nameof(bindings)));
    }
}
