using System;
using System.Collections.Generic;
namespace Zjcs.Official.StoryPlayableRequests
{
    internal sealed class StoryPlayableResourceBag : IDisposable
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
            if (values == null) return;
            for (int i = 0; i < count; i++) values[i]?.Dispose();
            values = null; count = 0;
        }
        public void Dispose() { Clear(); disposed = true; }
    }
}
