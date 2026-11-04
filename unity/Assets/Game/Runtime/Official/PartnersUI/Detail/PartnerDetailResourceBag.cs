using System;
using System.Collections.Generic;

namespace Zjcs.Official.PartnersUI
{
    // Original R3 bag release order, including Clear for portrait replacement.
    internal sealed class PartnerDetailResourceBag : IDisposable
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
        public void Clear()
        {
            if (values != null)
            {
                for (int i = 0; i < count; i++) values[i]?.Dispose();
                values = null; count = 0;
            }
        }
        public void Dispose() { Clear(); disposed = true; }
    }
}
