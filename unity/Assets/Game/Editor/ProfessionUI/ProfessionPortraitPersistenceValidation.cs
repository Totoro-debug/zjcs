using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    // A disk/texture fixture; it does not provide a game save implementation.
    [InitializeOnLoad]
    public static class ProfessionPortraitPersistenceValidation
    {
        private const string Active = "Zjcs.ProfessionPortraitPersistence.Active";
        private static bool ready, initialized, expectDiagnostic;
        private static int frames, expectedDiagnostics;
        private static readonly List<CharacterPortraitTexture2D> textures = new List<CharacterPortraitTexture2D>();
        private static readonly List<string> unexpectedDiagnostics = new List<string>();

        [Serializable] private sealed class StateFile { public int Id; public int CodeVersion; }
        private sealed class FileState : IPersistentState<CharacterPortraitPersisterState>
        {
            private readonly string path;
            public Action BeforeWrite;
            public int Writes;
            public CharacterPortraitPersisterState State { get; private set; }
            public FileState(string path)
            {
                this.path = path;
                ReadStateAsync().GetAwaiter().GetResult();
            }
            public UniTask WriteStateAsync()
            {
                BeforeWrite?.Invoke();
                Writes++;
                File.WriteAllText(path, JsonUtility.ToJson(new StateFile { Id = State.Id, CodeVersion = State.CodeVersion }));
                return UniTask.CompletedTask;
            }
            public UniTask ReadStateAsync()
            {
                var value = File.Exists(path) ? JsonUtility.FromJson<StateFile>(File.ReadAllText(path)) : new StateFile();
                State = new CharacterPortraitPersisterState { Id = value.Id, CodeVersion = value.CodeVersion };
                return UniTask.CompletedTask;
            }
            public UniTask ClearStateAsync()
            {
                if (File.Exists(path)) File.Delete(path);
                State = new CharacterPortraitPersisterState();
                return UniTask.CompletedTask;
            }
        }

        static ProfessionPortraitPersistenceValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Portrait persistence fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized)
                {
                    initialized = true;
                    Application.logMessageReceived += Diagnostic;
                    ValidatePersistence();
                    ValidateExpiry();
                    Require(unexpectedDiagnostics.Count == 0, string.Join("\n", unexpectedDiagnostics));
                }
                if (++frames > 3)
                {
                    Debug.Log("Profession original portrait persistence passed: real GPU bytes survive disk and state-provider reconstruction; version 3 invalidation/write order, request/hash names, empty data, locked-file failures, three-day access-time expiry and clear/recreate. Full profession UI and production save binding are not covered.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void ValidatePersistence()
        {
            string root = ProfessionValidationWorkspace.Output("portrait-persistence-fixture-" + Guid.NewGuid().ToString("N"));
            Require(!Directory.Exists(root), "the fixture must have a fresh caller-owned root");
            Directory.CreateDirectory(root);
            string cache = Path.Combine(root, "CharacterPortrait");
            Directory.CreateDirectory(cache);
            File.WriteAllBytes(Path.Combine(cache, "old-version-entry"), new byte[] { 9 });
            string statePath = Path.Combine(root, "fixture-state.json");
            var state = new FileState(statePath);
            state.State.Id = 17;
            state.State.CodeVersion = 2;
            bool observedWrite = false;
            state.BeforeWrite = () =>
            {
                Require(state.State.CodeVersion == 3 && !Directory.EnumerateFiles(cache).Any(), "source clears old entries before changing/writing code version");
                observedWrite = true;
            };
            var persister = new CharacterPortraitPersister(state, root);
            Require(observedWrite && state.Writes == 1, "source writes changed version exactly once");

            byte[] pixels = RenderPixels();
            var request = new CharacterPortraitDetailRequestData { RequestId = 12, AppearanceHashCode = -28, VersionId = 999 };
            persister.SetPersistentTexture(request, pixels);
            string entry = Path.Combine(cache, "12_-28");
            Require(File.ReadAllBytes(entry).SequenceEqual(pixels), "source writes the original raw texture bytes");
            request.VersionId++;
            Require(persister.TryGetPersistentTexture(request, out byte[] loaded) && pixels.SequenceEqual(loaded), "VersionId does not change the source cache filename");
            var reloadedState = new FileState(statePath);
            var reopened = new CharacterPortraitPersister(reloadedState, root);
            Require(reloadedState.State.Id == 17 && reloadedState.State.CodeVersion == 3 && reloadedState.Writes == 0, "state-provider reconstruction retains the source version without clearing/rewrite");
            Require(reopened.TryGetPersistentTexture(request, out loaded) && loaded.SequenceEqual(pixels), "new persister recovers identical disk bytes");
            var texture = new CharacterPortraitTexture2D(8, 6); textures.Add(texture);
            texture.LoadImage(loaded);
            Require(texture.Tex.GetPixelData<byte>(0).ToArray().SequenceEqual(pixels), "persisted pixels reload into a new actual Unity texture");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("portrait-persistence-fixture.png"), texture.Tex.EncodeToPNG());

            var different = request; different.AppearanceHashCode++;
            Require(!reopened.TryGetPersistentTexture(different, out loaded) && loaded == null, "appearance hash selects a different source entry");
            File.WriteAllBytes(Path.Combine(cache, "12_-27"), Array.Empty<byte>());
            Require(!reopened.TryGetPersistentTexture(different, out loaded) && loaded.Length == 0, "source rejects empty disk entries without changing the out value");
            Expect(() => reopened.SetPersistentTexture(request, null));
            Expect(() => reopened.SetPersistentTexture(request, Array.Empty<byte>()));
            Require(File.ReadAllBytes(entry).SequenceEqual(pixels), "empty writes leave the existing source entry intact");
            using (var locked = new FileStream(entry, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Expect(() => Require(!reopened.TryGetPersistentTexture(request, out var failure) && failure == null, "source read failure returns null/false"));
                Expect(() => reopened.SetPersistentTexture(request, new byte[] { 3 }));
            }
            Require(expectedDiagnostics == 4 && File.ReadAllBytes(entry).SequenceEqual(pixels), "source diagnostics and caught write failure preserve existing bytes");
            reopened.ClearAllStorage();
            Require(Directory.Exists(cache) && !Directory.EnumerateFileSystemEntries(cache).Any() && File.Exists(statePath), "source clear recreates only its cache folder and preserves external state");
            reopened.SetPersistentTexture(request, pixels);
            Require(reopened.TryGetPersistentTexture(request, out loaded) && loaded.SequenceEqual(pixels), "source cache is reusable after clear");
        }
        private static byte[] RenderPixels()
        {
            var texture = new CharacterPortraitTexture2D(8, 6); textures.Add(texture);
            var rt = RenderTexture.GetTemporary(8, 6, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                GL.Clear(false, true, Color.red);
                texture.ReadPixelsFromRT(rt, out byte[] bytes);
                return bytes;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        }
        private static void ValidateExpiry()
        {
            string root = ProfessionValidationWorkspace.Output("portrait-expiry-fixture-" + Guid.NewGuid().ToString("N"));
            Require(!Directory.Exists(root), "fresh expiry fixture root");
            var collection = new CharacterPortraitPersistentCollection(root);
            string cache = Path.Combine(root, "CharacterPortrait");
            foreach (string name in new[] { "expired", "recent", "future" }) collection.SaveData(name, new byte[] { 1 });
            File.SetLastAccessTimeUtc(Path.Combine(cache, "expired"), DateTime.UtcNow.AddDays(-4));
            File.SetLastWriteTimeUtc(Path.Combine(cache, "expired"), DateTime.UtcNow);
            File.SetLastAccessTimeUtc(Path.Combine(cache, "recent"), DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(Path.Combine(cache, "recent"), DateTime.UtcNow.AddDays(-10));
            File.SetLastAccessTimeUtc(Path.Combine(cache, "future"), DateTime.UtcNow.AddDays(1));
            Require(collection.ClearAllExpireData() == 1 && !File.Exists(Path.Combine(cache, "expired")) && File.Exists(Path.Combine(cache, "recent")) && File.Exists(Path.Combine(cache, "future")), "source expires using access time plus three days, independently of last-write time");
            Require(collection.ClearAllExpireData() == 0, "source repeat expiry counts only removed files");
            collection.ClearAllData();
            Require(Directory.Exists(cache) && !Directory.EnumerateFileSystemEntries(cache).Any(), "source collection clear recreates its root");
        }
        private static void Expect(Action action)
        {
            int before = expectedDiagnostics;
            expectDiagnostic = true;
            try { action(); }
            finally { expectDiagnostic = false; }
            Require(expectedDiagnostics == before + 1, "expected exactly one original empty-data/I/O diagnostic");
        }
        private static void Diagnostic(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (expectDiagnostic) expectedDiagnostics++;
            else unexpectedDiagnostics.Add(message);
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Portrait persistence: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            Application.logMessageReceived -= Diagnostic;
            foreach (var texture in textures) { if (texture.Tex != null) texture.DestroyTexture(); }
            textures.Clear();
            EditorApplication.Exit(code);
        }
    }
}
