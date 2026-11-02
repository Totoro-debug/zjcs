using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.PartnersNpc
{
    internal sealed class OfflinePartnerNpcAssemblies : IDisposable
    {
        private readonly Dictionary<string, NativeMap14SharedAssemblyCatalog.AssemblyPayload> payloads =
            new Dictionary<string, NativeMap14SharedAssemblyCatalog.AssemblyPayload>(StringComparer.Ordinal);
        private readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private bool disposed;
        internal Assembly Shared { get; }
        internal Assembly FirstPass { get; }
        internal Assembly Logging { get; }

        internal OfflinePartnerNpcAssemblies(OfficialPartnerNpcRuntimeCatalog catalog)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            foreach (var payload in catalog.Dependencies.Assemblies)
            {
                // Historical map code keeps its own Console/FirstPass modules.
                // Shared ABI/publisher modules are reused by verified identity.
                if (payload.Name != "Console" && payload.Name != "Framework.FirstPass.Runtime") Add(payload);
            }
            foreach (var payload in catalog.Assemblies) Add(payload);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                Logging = Load("Microsoft.Extensions.Logging.Abstractions");
                Load("LiteDB");
                FirstPass = Load("Zjcs.Partners884.Framework.FirstPass.Runtime");
                Shared = Load("Zjcs.Partners884.Console");
            }
            catch { Dispose(); throw; }
        }

        private void Add(NativeMap14SharedAssemblyCatalog.AssemblyPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Name) || !payload.Data ||
                !Guid.TryParse(payload.ModuleId, out _) || string.IsNullOrEmpty(payload.Sha256))
                throw new InvalidOperationException("NPC assembly payload is incomplete.");
            NativeMap14SharedAssemblies.Verify(payload.Data.bytes, payload.Sha256);
            payloads.Add(payload.Name, payload);
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            return payloads.ContainsKey(name) ? Load(name) : null;
        }

        private Assembly Load(string name)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerNpcAssemblies));
            if (loaded.TryGetValue(name, out var result)) return result;
            var payload = payloads[name];
            var existing = AppDomain.CurrentDomain.GetAssemblies().Where(value => value.GetName().Name == name).ToArray();
            if (existing.Any(value => value.ManifestModule.ModuleVersionId.ToString() != payload.ModuleId))
                throw new InvalidOperationException("Conflicting NPC dependency version: " + name);
            result = existing.FirstOrDefault() ?? Assembly.Load(payload.Data.bytes);
            if (result.GetName().Name != name || result.ManifestModule.ModuleVersionId.ToString() != payload.ModuleId)
                throw new InvalidOperationException("NPC assembly identity differs: " + name);
            loaded.Add(name, result);
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            loaded.Clear();
        }
    }
}
