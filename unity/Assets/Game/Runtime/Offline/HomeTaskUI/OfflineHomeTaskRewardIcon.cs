using System;
using Zjcs.Official.ProfessionUI;
using TaskUI = Zjcs.Official.HomeTaskUI;

namespace Zjcs.Offline.HomeTaskUI
{
    // The task UI's narrow icon port forwards to the original general ItemIcon.
    // It does not duplicate icon state, text, effects or click handling.
    public sealed class OfflineHomeTaskRewardIcon : TaskUI.ItemIcon
    {
        public ItemIcon Native { get; private set; }
        public void Bind(ItemIcon icon) => Native = icon ? icon : throw new ArgumentNullException(nameof(icon));
        public override TaskUI.ItemInfo myInfo => Native.myInfo == null ? null : new TaskUI.ItemInfo { ClassId = Native.myInfo.ClassId };
        public override TaskUI.ItemIcon SetClickPopupInfoDialog() { Native.SetClickPopupInfoDialog(); return this; }
        public override TaskUI.ItemIcon SetCenterHintContent(bool show) { Native.SetCenterHintContent(show); return this; }
        public override void SetRequireCount(long count) => Native.SetRequireCount(count);
        public override void SetAttachment(TaskUI.ItemIconAttachmentType type, bool show)
        {
            if (type != TaskUI.ItemIconAttachmentType.GotTip) throw new ArgumentOutOfRangeException(nameof(type));
            Native.SetAttachment(ItemIconAttachmentType.GotTip, show);
        }
        public override void ResetCountTextScale() => Native.ResetCountTextScale();
    }

}
