using System;
using UnityEngine;
using Zjcs.Official.HomePlayerInfoUI;

namespace Zjcs.Official.HomePlayerPanelUI
{
    // Resolves services for the original dynamically pooled status prefabs.
    public sealed class HomePlayerPanelScope : MonoBehaviour
    {
        public IHomePlayerPanelBindings Bindings { get; set; }
        public static IHomePlayerPanelBindings Required(Component source)
        {
            var scope = source.GetComponentInParent<HomePlayerPanelScope>(true);
            if (scope == null || scope.Bindings == null)
                throw new InvalidOperationException("Bind the original player-panel scope before activation.");
            return scope.Bindings;
        }
    }

    public class HomePlayerPanelUIBase : HomePlayerInfoUIBase
    {
        protected IHomePlayerPanelBindings PanelBindings => (IHomePlayerPanelBindings)InfoBindings;
    }
}
