using System;
using System.Reflection;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // The application initializes EntityUtils with its original EntityHandler.
    // Keep that implementation, including grid conversion, in unchanged IL.
    public sealed class NativeMap14SharedEntityUtilities : IDisposable
    {
        private readonly FieldInfo field;
        private readonly MethodInfo initialize;
        private object owned;

        public NativeMap14SharedEntityUtilities(NativeMap14SharedAssemblies assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            var utility = assemblies.CommonType("EntityUtils");
            field = utility.GetField("sInstance", BindingFlags.NonPublic | BindingFlags.Static);
            initialize = utility.GetMethod("Init", new[] { assemblies.CommonType("IEntityHandler") });
            if (field == null || initialize == null) throw new InvalidOperationException("Original entity utility binding differs.");
            if (field.GetValue(null) != null) return;
            owned = Activator.CreateInstance(assemblies.CommonType("EntityHandler"));
            initialize.Invoke(null, new[] { owned });
        }

        public void Dispose()
        {
            if (owned != null && ReferenceEquals(field.GetValue(null), owned))
                initialize.Invoke(null, new object[] { null });
            owned = null;
        }
    }
}
