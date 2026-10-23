using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Zjcs.Editor.Character
{
    /// <summary>Content receipts for expensive, deterministic acceptance preparation. No gameplay results are cached.</summary>
    internal static class AcceptancePreparationCache
    {
        internal static bool Ensure(string receipt, IEnumerable<string> inputs, IEnumerable<string> outputs,
            string context, Action build, Action validate, Action<string> log, bool force = false)
            => Ensure(receipt, inputs, outputs, () => context, build, validate, log, force);

        internal static bool Ensure(string receipt, IEnumerable<string> inputs, IEnumerable<string> outputs,
            Func<string> context, Action build, Action validate, Action<string> log, bool force = false)
        {
            var timer = Stopwatch.StartNew();
            string[] inputRoots = inputs.ToArray();
            string[] outputRoots = outputs.ToArray();
            log?.Invoke("checking preparation content");
            string initialContext = context();
            string inputHash = Fingerprint(inputRoots, initialContext);
            if (!force && File.Exists(receipt))
            {
                string[] saved = File.ReadAllLines(receipt);
                if (saved.Length == 3 && saved[0] == "acceptance-preparation-v1" && saved[1] == inputHash
                    && saved[2] == Fingerprint(outputRoots, initialContext))
                {
                    try
                    {
                        validate();
                        if (Fingerprint(inputRoots, context()) != inputHash || Fingerprint(outputRoots, context()) != saved[2])
                            throw new InvalidOperationException("Preparation files changed during cache validation. Retry after the changes finish.");
                    }
                    catch { File.Delete(receipt); throw; }
                    log?.Invoke("cache hit; verified inputs, outputs and bindings in " + timer.Elapsed.TotalSeconds.ToString("F2") + "s");
                    return false;
                }
            }

            if (File.Exists(receipt)) File.Delete(receipt);
            log?.Invoke("cache miss; rebuilding preparation assets");
            build();
            validate();
            if (Fingerprint(inputRoots, context()) != inputHash)
                throw new InvalidOperationException("Preparation inputs changed during the build. Retry after the changes finish.");
            foreach (string output in outputRoots)
                if (!File.Exists(output) && !Directory.Exists(output))
                    throw new InvalidOperationException("Preparation did not produce its required output: " + output);
            string outputHash = Fingerprint(outputRoots, context());
            if (Fingerprint(inputRoots, context()) != inputHash)
                throw new InvalidOperationException("Preparation inputs changed while recording outputs. Retry after the changes finish.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(receipt)));
            string temporary = receipt + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, new[] { "acceptance-preparation-v1", inputHash, outputHash }, new UTF8Encoding(false));
                if (File.Exists(receipt)) File.Replace(temporary, receipt, null);
                else File.Move(temporary, receipt);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            log?.Invoke("rebuilt and verified in " + timer.Elapsed.TotalSeconds.ToString("F2") + "s");
            return true;
        }

        internal static string Fingerprint(IEnumerable<string> roots, string context)
        {
            var records = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string root in roots.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(root)) AddFile(records, root);
                else if (Directory.Exists(root))
                {
                    records[root] = "directory";
                    foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                        records[directory] = "directory";
                    foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) AddFile(records, file);
                }
                else records[root] = "missing";
            }
            using (var sha = SHA256.Create())
            {
                var description = new StringBuilder(context.Length + records.Count * 100);
                description.Append(context.Length).Append(':').Append(context).Append('\n');
                foreach (var record in records)
                    description.Append(record.Key.Length).Append(':').Append(record.Key).Append(':').Append(record.Value).Append('\n');
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(description.ToString())));
            }
        }

        private static void AddFile(IDictionary<string, string> records, string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
                records[path] = stream.Length + ":" + Hex(sha.ComputeHash(stream));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
