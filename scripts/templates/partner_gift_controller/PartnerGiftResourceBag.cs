using System;
using System.Collections.Generic;

namespace Zjcs.Official.PartnersUI
{
    // Same APK R3 disposal order as ProfessionResourceOperationBag: ascending,
    // observe reentrant additions, then clear; dispose additions after disposal.
    internal sealed class PartnerGiftResourceBag : IDisposable
    {
        private List<IDisposable> values;
        private int count;
        private bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else
            {
                if (values == null) values = new List<IDisposable>();
                values.Add(value); count++;
            }
            return value;
        }
        public void Dispose()
        {
            if (values != null)
            {
                for (int i = 0; i < count; i++) values[i]?.Dispose();
                values = null; count = 0;
            }
            disposed = true;
        }
    }
}
