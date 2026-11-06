using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeUiGraphic
    {
        private void UpdateOriginalSeparatorInstructions()
        {
            separatorPartInstructions.Clear();
            if (State == null || State.SlotCount == 0) return;
            if (enableSeparatorSlots)
            {
                if (separatorSlotNames.Length > 0)
                {
                    separatorSlots.Clear();
                    foreach (string name in separatorSlotNames)
                    {
                        var slot = State.FindSlot(name);
                        if (slot == null) throw new InvalidOperationException("Restore the original UI separator slot: " + name);
                        if (!separatorSlots.Contains(slot)) separatorSlots.Add(slot);
                    }
                }
                var order = State.DrawOrder;
                for (int index = 0; index < order.Length; index++)
                    if (separatorSlots.Contains(order[index]))
                        separatorPartInstructions.Add(new ProfessionAppearancePartInstruction(index));
            }
            separatorPartInstructions.Add(new ProfessionAppearancePartInstruction(State.SlotCount - 1));
            EnsureDynamicSeparatorParents();
        }
        private IReadOnlyList<ProfessionNativeMeshBatches.Slot> OriginalSeparatedDrawSlots()
        {
            UpdateOriginalSeparatorInstructions();
            var original = State.CreateDrawSlots();
            if (!enableSeparatorSlots) return original;
            var result = new ProfessionNativeMeshBatches.Slot[original.Length];
            int part = 0;
            for (int index = 0; index < original.Length; index++)
            {
                var slot = original[index];
                bool split = index == separatorPartInstructions[part].endSlot;
                result[index] = new ProfessionNativeMeshBatches.Slot(slot.Index, slot.Attachment,
                    slot.Color, slot.Blend, slot.Active, slot.BreakAfter || split, slot.Deform);
                if (split && part < separatorPartInstructions.Count - 1) part++;
            }
            return result;
        }
        private void BindOriginalSeparatorParents()
        {
            if (!enableSeparatorSlots)
            {
                ReturnDynamicSeparatorGraphics();
                return;
            }
            int part = 0;
            for (int index = 0; index < batches.Batches.Count; index++)
            {
                while (part < separatorPartInstructions.Count - 1 &&
                    batches.Batches[index].FirstDrawIndex > separatorPartInstructions[part].endSlot) part++;
                var parent = separatorParts[part];
                if (!parent) throw new InvalidOperationException("Original UI separator transform is unavailable.");
                if (graphics[index].transform.parent != parent) graphics[index].transform.SetParent(parent, true);
            }
            OrderDynamicSeparatorGraphics();
        }
    }
}
