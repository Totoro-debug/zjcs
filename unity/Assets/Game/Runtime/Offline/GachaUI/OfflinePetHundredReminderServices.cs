using System;
using Zjcs.Official.GachaUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.GachaUI
{
    public sealed class OfflinePetHundredReminderServices : IGachaHundredReminderServices
    {
        private readonly OfflinePetGachaService gacha;
        private readonly Func<DateTime> clock;
        private readonly Action<Action<MarkableConfirmDialog>> popup;
        private readonly Action<PetGachaResult> saveFailure;

        public OfflinePetHundredReminderServices(OfflinePetGachaService gacha, Func<DateTime> clock,
            Action<Action<MarkableConfirmDialog>> popup, Action<PetGachaResult> saveFailure = null)
        {
            this.gacha = gacha ?? throw new ArgumentNullException(nameof(gacha));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.popup = popup ?? throw new ArgumentNullException(nameof(popup));
            this.saveFailure = saveFailure;
        }

        public DateTime LocalNow => clock();
        public bool ShouldConfirmHundred(int gachaType, DateTime now)
        {
            CheckType(gachaType);
            return gacha.ShouldConfirmHundred(now);
        }
        public void IgnoreHundredReminder(int gachaType, DateTime now)
        {
            CheckType(gachaType);
            var result = gacha.SuppressHundredReminder(now, "pet-gacha:ignore-hundred:" + Guid.NewGuid().ToString("N"));
            // The original settings write does not gate the draw continuation.
            if (!result.IsApplied && result.Code != PetGachaResultCode.Unchanged) saveFailure?.Invoke(result);
        }
        public void PopupMarkableConfirm(Action<MarkableConfirmDialog> initialize) => popup(initialize);
        private void CheckType(int type)
        {
            if (type != gacha.Catalog.Gacha.GachaType) throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
