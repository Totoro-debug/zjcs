using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.NativeMap14SharedViews
{
    // One identity cache for views of borrowed original entities. This scope
    // owns view subscriptions, never the original entities or their container.
    public sealed class NativeMap14SharedViewCache : IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly Action<NativeMap14SharedViewEntity> bind;
        private readonly Dictionary<object, NativeMap14SharedViewEntity> views
            = new Dictionary<object, NativeMap14SharedViewEntity>(IdentityComparer.Instance);
        private bool disposed;

        public NativeMap14SharedViewCache(NativeMap14SharedAssemblies shared,
            Action<NativeMap14SharedViewEntity> bind = null)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.bind = bind;
        }

        public NativeMap14SharedViewEntity Wrap(object original)
        {
            RequireActive();
            if (original == null) return null;
            if (views.TryGetValue(original, out var known)) return known;
            var view = new NativeMap14SharedViewEntity(shared, original, Wrap);
            views.Add(original, view);
            try { bind?.Invoke(view); }
            catch { views.Remove(original); view.Dispose(); throw; }
            return view;
        }

        public bool Owns(NativeMap14SharedViewEntity view)
        {
            RequireActive();
            return view != null && views.TryGetValue(view.Original, out var known)
                && ReferenceEquals(known, view);
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var view in views.Values) view.Dispose();
            views.Clear();
            disposed = true;
        }

        private void RequireActive()
        { if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedViewCache)); }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object first, object second) => ReferenceEquals(first, second);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
