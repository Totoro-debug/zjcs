using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Local bytes only. The original Console assembly is never rewritten.
    // This loader does not initialize the game's online entry points.
    public sealed class NativeMap14SharedAssemblies : IDisposable
    {
        private readonly Dictionary<string, NativeMap14SharedAssemblyCatalog.AssemblyPayload> payloads;
        private readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private bool disposed;
        public Assembly Shared { get; }

        public NativeMap14SharedAssemblies(NativeMap14SharedAssemblyCatalog catalog)
        {
            if (typeof(GConfig.IConfig).Assembly.GetName().Name != "Config")
                throw new InvalidOperationException("Original Config source contract is not bound.");
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            payloads = new Dictionary<string, NativeMap14SharedAssemblyCatalog.AssemblyPayload>(StringComparer.Ordinal);
            foreach (var payload in catalog.Assemblies)
            {
                if (payload == null || string.IsNullOrEmpty(payload.Name) || !payload.Data)
                    throw new InvalidOperationException("Original shared assembly binding is incomplete.");
                Verify(payload.Data.bytes, payload.Sha256);
                payloads.Add(payload.Name, payload);
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { Shared = Load("Console"); }
            catch { Dispose(); throw; }
        }

        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            return payloads.ContainsKey(name) ? Load(name) : null;
        }

        private Assembly Load(string name)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedAssemblies));
            if (loaded.TryGetValue(name, out var result)) return result;
            if (!payloads.TryGetValue(name, out var payload))
                throw new InvalidOperationException("Original local assembly is not bound: " + name);
            // Reuse the same verified payload across map reloads. Reject an
            // assembly with the same name but a different original module.
            var existing = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == name).ToArray();
            if (existing.Any(a => a.ManifestModule.ModuleVersionId.ToString() != payload.ModuleId))
                throw new InvalidOperationException("Another shared assembly version is already loaded: " + name);
            result = existing.FirstOrDefault() ?? Assembly.Load(payload.Data.bytes);
            if (!string.Equals(result.GetName().Name, name, StringComparison.Ordinal) ||
                result.ManifestModule.ModuleVersionId.ToString() != payload.ModuleId)
                throw new InvalidOperationException("Original shared assembly identity differs: " + name);
            loaded.Add(name, result);
            return result;
        }

        public Type CommonType(string name)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedAssemblies));
            if (string.IsNullOrEmpty(name) || name.IndexOf('.') >= 0)
                throw new ArgumentException("A source Common type name is required.", nameof(name));
            return Shared.GetType("Common." + name, true);
        }

        public static void Verify(byte[] bytes, string expected)
        {
            using (var hash = SHA256.Create())
            {
                var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new InvalidOperationException("Original local shared payload hash differs.");
            }
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
