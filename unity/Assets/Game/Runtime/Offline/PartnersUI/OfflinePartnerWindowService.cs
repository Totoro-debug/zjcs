using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    // Window ownership stays with the containing feature. Observe the actual
    // original destruction event, including destruction outside the close path.
    public sealed class OfflinePartnerWindowService : IProfessionWindowService
    {
        private readonly Action<GameObject> close;
        private readonly Action closeLast;

        public OfflinePartnerWindowService(Action<GameObject> close, Action closeLast)
        {
            this.close = close ?? throw new ArgumentNullException(nameof(close));
            this.closeLast = closeLast ?? throw new ArgumentNullException(nameof(closeLast));
        }

        public void Close(GameObject window) => close(window);
        public void CloseLast() => closeLast();
        public void ObserveDestroyed(WorldPopupWindow window, Action callback)
            => window.OnWindowDestory += callback;
        public void StopObservingDestroyed(WorldPopupWindow window, Action callback)
            => window.OnWindowDestory -= callback;
    }
}
