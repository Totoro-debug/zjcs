using System;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.HomeTaskUI;

namespace Zjcs.Offline.HomeTaskUI
{
    // The player owner forwards committed events. Registration and event fan-out
    // use the selected original ConditionHandler, including reference counts.
    public sealed class OfflineHomeTaskConditions : IHomeTaskConditions, IDisposable
    {
        readonly OfflineHomeTaskConditionReader reader;
        readonly HomeConditionNotifications notifications;
        bool disposed;

        public OfflineHomeTaskConditions(OfflineHomeTaskConditionReader reader,
            IHomeConditionPlayer player, Action<Exception> reportException)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            notifications = new HomeConditionNotifications(player, reportException);
        }

        public void Register(ICondition condition, Action refresh)
        { EnsureAlive(); notifications.Register(reader.RequireNotification(condition), refresh); }
        public void Deregister(ICondition condition, Action refresh)
        {
            if (disposed) return;
            notifications.Deregister(reader.RequireNotification(condition), refresh);
        }

        public void NotifyWorldMapProgressUpdate() { EnsureAlive(); notifications.NotifyWorldMapProgressUpdate(); }
        public void NotifyProfessionPromote() { EnsureAlive(); notifications.NotifyProfessionPromote(); }
        public void NotifyOpenChest() { EnsureAlive(); notifications.NotifyOpenChest(); }
        public void NotifyTaskAdd() { EnsureAlive(); notifications.NotifyTaskAdd(); }
        public void NotifyTaskUpdate() { EnsureAlive(); notifications.NotifyTaskUpdate(); }
        public void NotifyTaskRemove() { EnsureAlive(); notifications.NotifyTaskRemove(); }
        public void NotifyAddFinishedTask() { EnsureAlive(); notifications.NotifyAddFinishedTask(); }
        public void NotifyDemonBattlefieldLayerUpdate() { EnsureAlive(); notifications.NotifyDemonBattlefieldLayerUpdate(); }
        public void NotifyUpdateBlackboardData() { EnsureAlive(); notifications.NotifyUpdateBlackboardData(); }
        public void NotifyActiveableEntUpdate(bool isGoddess)
        { EnsureAlive(); notifications.NotifyActiveableEntUpdate(isGoddess); }

        void EnsureAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTaskConditions)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            notifications.Destroy();
        }
    }
}
