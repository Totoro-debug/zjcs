using System;

namespace Zjcs.Official.GachaUI
{
    public interface IGachaHundredReminderServices
    {
        DateTime LocalNow { get; }
        bool ShouldConfirmHundred(int gachaType, DateTime now);
        void IgnoreHundredReminder(int gachaType, DateTime now);
        void PopupMarkableConfirm(Action<MarkableConfirmDialog> initialize);
    }

    public static class GachaPanelUtils
    {
        // Original GachaPanelUtils.HundredGachePreRemind; persistence, clock and
        // WindowManager are provided by the local host. Keep confirmation-time save.
        public static void HundredGachePreRemind(IGachaHundredReminderServices services,
            int gachaType, int count, Action<int> confirmAction)
        {
            if (count < 100) { confirmAction(count); return; }
            if (services.ShouldConfirmHundred(gachaType, services.LocalNow))
            {
                services.PopupMarkableConfirm(window => window.Init(
                    new GachaTextKeyArgWrap("ui_pet_gacha_is_times_hundred"),
                    new GachaTextKeyArgWrap("ui_no_prompts_today"), selected =>
                    {
                        if (selected) services.IgnoreHundredReminder(gachaType, services.LocalNow);
                        confirmAction(count);
                    }));
            }
            else confirmAction(count);
        }
    }
}
