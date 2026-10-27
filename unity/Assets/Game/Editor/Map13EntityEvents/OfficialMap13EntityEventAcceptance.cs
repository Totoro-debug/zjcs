using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.World.Map13EntityEvents;

namespace Zjcs.Editor.Map13EntityEvents
{
    public static class OfficialMap13EntityEventAcceptance
    {
        private const string Key = "Zjcs.Map13.EntityEventsExecution";
        private static bool started;
        [Serializable] private sealed class Record { public string path, sha256; }
        [Serializable] private sealed class Receipt { public string manifest; public Record[] inputs, outputs; public Record assembly, code_manifest, generator; }

        private static string Repository
        {
            get
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
                return directory?.FullName ?? throw new DirectoryNotFoundException("Map13 repository is unavailable.");
            }
        }

        public static void ValidateSources()
        {
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Repository, "assets/reference/official-map/map13-entity-events-source-manifest.json")));
            Require(receipt.manifest == "871_156598", "Entity-event source version differs.");
            foreach (var record in receipt.inputs.Concat(receipt.outputs).Concat(new[] { receipt.assembly, receipt.code_manifest, receipt.generator }))
            {
                using (var hash = SHA256.Create())
                    Require(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(Repository, record.path)))).Replace("-", "").ToLowerInvariant() == record.sha256,
                        "Entity-event source bytes differ: " + record.path);
            }
        }

        public static void Run()
        {
            ValidateSources();
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 120);
            SessionState.SetInt(Key + ".result", 1);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map13 entity-event execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            try
            {
                Verify();
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map13 entity-event Play Mode passed: original zero-to-four argument dispatch, receiver snapshots and nested mutation, duplicate receiver removal, Merge/Clear, exception continuation, retained delegate dictionary and thread-local empty list pools. Event dispatch only; no native entity rendering, mouse input or saves.");
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.isPlaying = false;
        }

        private sealed class Receiver : IEventReceiver
        {
            public Action<int[]> Call;
            public Receiver(Action<int[]> call) { Call = call; }
        }
        private sealed class E0 : EventDelegateBase<E0, Receiver>
        {
            private readonly Dictionary<Delegate, Receiver> actions = new Dictionary<Delegate, Receiver>();
            protected override void FireOnReceiver(Receiver receiver) => receiver.Call(new int[0]);
            public void Add(Action action) => RegisterEvent(actions, action, new Receiver(_ => action()));
            public void Remove(Action action) => UnRegisterEvent(actions, action);
            public int ActionCount => actions.Count;
        }
        private sealed class E1 : EventDelegateBase<E1, Receiver, int>
        { protected override void FireOnReceiver(Receiver receiver, int a) => receiver.Call(new[] { a }); }
        private sealed class E2 : EventDelegateBase<E2, Receiver, int, int>
        { protected override void FireOnReceiver(Receiver receiver, int a, int b) => receiver.Call(new[] { a, b }); }
        private sealed class E3 : EventDelegateBase<E3, Receiver, int, int, int>
        { protected override void FireOnReceiver(Receiver receiver, int a, int b, int c) => receiver.Call(new[] { a, b, c }); }
        private sealed class E4 : EventDelegateBase<E4, Receiver, int, int, int, int>
        { protected override void FireOnReceiver(Receiver receiver, int a, int b, int c, int d) => receiver.Call(new[] { a, b, c, d }); }
        private sealed class Capture : ILogHandler
        {
            public int Exceptions, Errors;
            public void LogException(Exception exception, UnityEngine.Object context)
            { Require(exception is InvalidOperationException && exception.Message == "expected event receiver failure", "Unexpected receiver exception."); Exceptions++; }
            public void LogFormat(LogType type, UnityEngine.Object context, string format, params object[] args)
            { Require(type == LogType.Error, "Unexpected event diagnostic."); Errors++; }
        }

        private static void Verify()
        {
            var previous = Debug.unityLogger.logHandler;
            var capture = new Capture(); Debug.unityLogger.logHandler = capture;
            try
            {
                var order = new List<string>();
                var value = new E0(); bool nested = false;
                var b = new Receiver(_ => order.Add("B"));
                var c = new Receiver(_ => order.Add("C"));
                var a = new Receiver(_ =>
                {
                    order.Add("A");
                    if (nested) return;
                    nested = true; value -= b; value += c; value.FireEvent();
                });
                value += a; value += b; value.FireEvent();
                Require(order.SequenceEqual(new[] { "A", "A", "C", "B" }), "Receiver mutation changed the original outer snapshot or nested dispatch order.");
                Require(E0.CacheStack.Count >= 2 && E0.CacheStack.All(list => list.Count == 0), "Nested event lists were not returned empty to the pool.");
                value.Clear(); order.Clear();
                value += b; value += b; value -= b; value.FireEvent();
                Require(order.SequenceEqual(new[] { "B" }), "Original duplicate receiver registration/removal differs.");
                var merged = new E0(); merged.Merge(value); merged.Merge(value); value.Clear(); order.Clear(); merged.FireEvent();
                Require(order.SequenceEqual(new[] { "B", "B" }), "Merge must copy receivers in their original order.");
                int calls = 0; Action action = () => calls++;
                var delegates = new E0(); delegates.Add(action); delegates.FireEvent(); delegates.Add(action); delegates.FireEvent();
                Require(calls == 2 && delegates.ActionCount == 1, "Duplicate delegate registration changed receiver count.");
                delegates.Remove(action); delegates.FireEvent(); delegates.Add(action); delegates.FireEvent();
                Require(calls == 2 && delegates.ActionCount == 1, "Original delegate dictionary retention after removal differs.");
                delegates.Clear(); delegates.Add(action); delegates.FireEvent();
                Require(calls == 2 && delegates.ActionCount == 1, "Clear incorrectly discarded the original delegate dictionary.");
                delegates.Remove(() => { });
                E0 absent = null; absent += b; absent -= b;
                Require(absent == null && capture.Errors == 6, "Null event/delegate diagnostics differ.");

                var failure = new Receiver(_ => throw new InvalidOperationException("expected event receiver failure"));
                var zero = new E0(); zero += failure; zero += new Receiver(args => Require(args.Length == 0, "Zero-argument event differs.")); zero.FireEvent();
                CheckArguments(failure);
                Require(capture.Exceptions == 5, "Receiver exceptions must be caught once for each argument arity.");

                int mainPoolCount = E0.CacheStack.Count; Exception threadError = null;
                var worker = new Thread(() =>
                {
                    try
                    {
                        Require(E0.CacheStack.Count == 0, "Receiver list pool crossed thread boundaries.");
                        var local = new E0(); local += new Receiver(_ => { }); local.FireEvent();
                        Require(E0.CacheStack.Count == 1 && E0.CacheStack.Peek().Count == 0, "Worker event list was not returned empty.");
                    }
                    catch (Exception error) { threadError = error; }
                });
                worker.Start(); Require(worker.Join(10000), "Worker event dispatch timed out.");
                if (threadError != null) throw threadError;
                Require(E0.CacheStack.Count == mainPoolCount, "Worker modified the main-thread receiver list pool.");
            }
            finally { Debug.unityLogger.logHandler = previous; }
        }

        private static void CheckArguments(Receiver failure)
        {
            int checks = 0;
            var one = new E1(); one += failure; one += new Receiver(args => { Require(args.SequenceEqual(new[] { 17 }), "One argument differs."); checks++; }); one.FireEvent(17);
            var two = new E2(); two += failure; two += new Receiver(args => { Require(args.SequenceEqual(new[] { 17, -4 }), "Two arguments differ."); checks++; }); two.FireEvent(17, -4);
            var three = new E3(); three += failure; three += new Receiver(args => { Require(args.SequenceEqual(new[] { 17, -4, 0 }), "Three arguments differ."); checks++; }); three.FireEvent(17, -4, 0);
            var four = new E4(); four += failure; four += new Receiver(args => { Require(args.SequenceEqual(new[] { 17, -4, 0, int.MaxValue }), "Four arguments differ."); checks++; }); four.FireEvent(17, -4, 0, int.MaxValue);
            Require(checks == 4, "A receiver exception prevented a later receiver from firing.");
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    }
}
