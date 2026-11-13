using System;
using Zjcs.Official.HomeCurrencyWindowsUI;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Owns the local clock/message boundary for the original PeriodicManager.
    // The owner supplies active-player availability and an explicit reset calendar.
    public sealed class OfflineHomePeriodicEvents : IHomePeriodicServices, IDisposable
    {
        private sealed class Subscription : IDisposable
        {
            private Action release;
            public Subscription(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }

        private readonly Func<bool> hasPlayer;
        private readonly Func<DateTime> utcNow;
        private readonly HomePeriodicManager manager;
        private Action<HomeCurrencyEnterNewDay> enterNewDay;
        private bool disposed;

        public OfflineHomePeriodicEvents(IHomeQuickBuyCalendar calendar, Func<bool> hasPlayer,
            Func<DateTime> utcNow = null)
        {
            Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            this.hasPlayer = hasPlayer ?? throw new ArgumentNullException(nameof(hasPlayer));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            manager = new HomePeriodicManager(this);
        }

        public IHomeQuickBuyCalendar Calendar { get; }
        bool IHomePeriodicServices.HasPlayer => hasPlayer();
        // The local UTC clock is available immediately; no server synchronization is required.
        bool IHomePeriodicServices.TimeSynced => true;
        DateTime IHomePeriodicServices.GetDateTime() => utcNow();
        void IHomePeriodicServices.PublishEnterNewDay(HomeCurrencyEnterNewDay message) => enterNewDay?.Invoke(message);

        public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> callback)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomePeriodicEvents));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            enterNewDay += callback;
            return new Subscription(() => enterNewDay -= callback);
        }

        // The production caller passes Unity Time.time, as in the selected client.
        public void Update(float time)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomePeriodicEvents));
            manager.Update(time);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            enterNewDay = null;
        }
    }
}
