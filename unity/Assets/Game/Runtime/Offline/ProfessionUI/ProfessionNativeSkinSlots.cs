using System;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent attachment state, using the published skin-change behavior
    // and original compound keys. It neither evaluates nor substitutes poses.
    public sealed class ProfessionNativeSkinSlots
    {
        private readonly ProfessionNativeSkinCatalog catalog;
        private readonly string[] setup;
        private readonly object[] attachments;
        public ProfessionNativeSkin Skin { get; private set; }
        public int Count => setup.Length;

        public ProfessionNativeSkinSlots(ProfessionNativeSkinCatalog catalog, string[] setup)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (setup == null || setup.Length == 0) throw new ArgumentException("Original slot setup is empty.");
            this.setup = (string[])setup.Clone();
            attachments = new object[setup.Length];
            ResetSlots();
        }

        public string SetupName(int slot) => setup[slot];
        public object Attachment(int slot) => attachments[slot];
        public void SetAttachment(int slot, object attachment) { attachments[slot] = attachment; }
        public object Resolve(int slot, string name) => catalog.ResolveAttachment(Skin, slot, name);
        public void SetAttachmentByName(int slot, string name)
        {
            object value = Resolve(slot, name);
            if (name != null && value == null)
                throw new ArgumentException("Original attachment key is absent from active and default skins: " + name);
            attachments[slot] = value;
        }

        public void ResetSlots()
        {
            for (int i = 0; i < Count; i++) attachments[i] = catalog.ResolveAttachment(Skin, i, setup[i]);
        }

        public void SetSkin(ProfessionNativeSkin next)
        {
            if (next != null && next.SlotCount != Count)
                throw new ArgumentException("Original skin and slot layouts differ.");
            if (ReferenceEquals(Skin, next)) return;
            if (next != null)
            {
                if (Skin == null)
                {
                    for (int i = 0; i < Count; i++)
                    {
                        if (setup[i] == null) continue;
                        object replacement = next.GetAttachment(i, setup[i]);
                        if (replacement != null) attachments[i] = replacement;
                    }
                }
                else
                {
                    foreach (ProfessionNativeSkin.Entry old in Skin.Entries)
                    {
                        if (!ReferenceEquals(attachments[old.Slot], old.Attachment)) continue;
                        object replacement = next.GetAttachment(old.Slot, old.Key);
                        if (replacement != null) attachments[old.Slot] = replacement;
                    }
                }
            }
            Skin = next;
        }
    }
}
