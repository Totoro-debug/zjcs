using System;
using System.IO;
using Zjcs.Editor.Character;

public static class AcceptancePreparationCacheRegression
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "zjcs-preparation-cache-test-" + Guid.NewGuid().ToString("N"));
        string input = Path.Combine(root, "inputs");
        string output = Path.Combine(root, "outputs");
        string receipt = Path.Combine(root, "cache.receipt");
        Directory.CreateDirectory(input);
        string source = Path.Combine(input, "source.txt");
        string product = Path.Combine(output, "prefab.txt");
        File.WriteAllText(source, "original");
        int builds = 0, validations = 0;
        Action build = () =>
        {
            builds++;
            Directory.CreateDirectory(output);
            File.WriteAllText(product, File.ReadAllText(source));
        };
        Action validate = () =>
        {
            validations++;
            Require(File.Exists(product) && File.ReadAllText(product) == File.ReadAllText(source), "built content is current");
        };
        Func<bool, bool> run = force => AcceptancePreparationCache.Ensure(receipt,
            new[] { input }, new[] { output }, "Unity-test/Windows/v1", build, validate, null, force);
        try
        {
            Require(run(false) && builds == 1, "cold cache builds");
            Require(!run(false) && builds == 1 && validations == 2, "unchanged assets reuse output and still validate");
            string originalReceipt = File.ReadAllText(receipt);
            Require(!run(false) && File.ReadAllText(receipt) == originalReceipt, "warm run does not rewrite the receipt");

            DateTime timestamp = File.GetLastWriteTimeUtc(source);
            File.WriteAllText(source, "modified");
            File.SetLastWriteTimeUtc(source, timestamp);
            Require(run(false), "source content change with identical size and timestamp rebuilds");
            string metadata = source + ".meta";
            File.WriteAllText(metadata, "import setting A");
            Require(run(false), "new importer metadata rebuilds");
            File.WriteAllText(metadata, "import setting B");
            Require(run(false), "changed importer metadata rebuilds");
            File.Delete(metadata);
            Require(run(false), "removed input rebuilds");
            string added = Path.Combine(input, "new-animation.bytes");
            File.WriteAllText(added, "animation");
            Require(run(false), "new source file rebuilds");

            timestamp = File.GetLastWriteTimeUtc(product);
            File.WriteAllText(product, "tampered");
            File.SetLastWriteTimeUtc(product, timestamp);
            Require(run(false), "output tampering with identical size and timestamp rebuilds");
            File.Delete(product);
            Require(run(false), "missing output rebuilds");
            File.WriteAllText(Path.Combine(output, "extra.prefab"), "extra");
            Require(run(false), "changed output inventory rebuilds");
            Require(run(true), "explicit forced rebuild bypasses cache");
            File.WriteAllText(receipt, "truncated");
            Require(run(false), "corrupt receipt rebuilds");
            Require(AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                "Unity-test/Windows/v2", build, validate, null), "tool/Unity/platform context change rebuilds");

            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                "v3", () => { throw new InvalidOperationException("expected build failure"); }, validate, null));
            Require(!File.Exists(receipt), "failed build never leaves a valid receipt");
            run(false);
            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                "Unity-test/Windows/v1", build, () => { throw new InvalidOperationException("expected validation failure"); }, null));
            Require(!File.Exists(receipt), "failed warm validation invalidates receipt");
            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                "v4", () => { build(); File.WriteAllText(source, "concurrent edit"); }, () => { }, null));
            Require(!File.Exists(receipt), "inputs changing during a build never get cached");
            run(false);
            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                "Unity-test/Windows/v1", build, () => File.WriteAllText(added, "concurrent validation edit"), null));
            Require(!File.Exists(receipt), "inputs changing during warm validation never get reused");
            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input },
                new[] { Path.Combine(root, "missing-output") }, "v5", () => { }, () => { }, null));
            Require(!File.Exists(receipt), "missing required build output never gets cached");
            string configuration = "original configuration";
            ExpectFailure(() => AcceptancePreparationCache.Ensure(receipt, new[] { input }, new[] { output },
                () => configuration, () => { build(); configuration = "concurrent configuration edit"; }, validate, null));
            Require(!File.Exists(receipt), "catalog or platform changes during a build never get cached");
            Console.WriteLine("Acceptance preparation cache regression passed; content changes, invalid outputs, failures, force and concurrent edits checked.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void ExpectFailure(Action action)
    {
        bool failed = false;
        try { action(); } catch (InvalidOperationException) { failed = true; }
        Require(failed, "expected preparation to fail");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Cache regression failed: " + message);
    }
}
