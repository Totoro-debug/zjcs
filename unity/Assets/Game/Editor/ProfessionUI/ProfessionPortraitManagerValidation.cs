using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    // These bindings exercise orchestration, not the production character renderer.
    [InitializeOnLoad]
    public static class ProfessionPortraitManagerValidation
    {
        private const string Active = "Zjcs.ProfessionPortraitManager.Active";
        private static bool ready, initialized;
        private static int frames;
        private static CharacterPortraitManager manager;
        private static Bindings environment;
        private static readonly List<CharacterPortraitTexture2D> textures = new List<CharacterPortraitTexture2D>();
        public sealed class SkinFixture { public int SkinId; }
        public sealed class AppearanceFixture
        {
            public long ModifyTimeStamp;
            public Dictionary<SkinType, SkinFixture> SkinMap = new Dictionary<SkinType, SkinFixture>();
        }
        private sealed class VersionState : IPersistentState<CharacterPortraitPersisterState>
        {
            public CharacterPortraitPersisterState State { get; } = new CharacterPortraitPersisterState { CodeVersion = 3 };
            public UniTask WriteStateAsync() => throw new InvalidOperationException("The unchanged source version should not be written.");
            public UniTask ReadStateAsync() => throw new NotSupportedException("Not part of the manager fixture.");
            public UniTask ClearStateAsync() => throw new NotSupportedException("Not part of the manager fixture.");
        }
        private sealed class Request : ICharacterPortraitRequestor, IDisposable
        {
            public long Id;
            public bool Persist = true, AllowToken = true;
            public readonly AppearanceFixture Appearance = new AppearanceFixture();
            public Bindings Bindings;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            public long GetRequestId() => Id;
            public object GetAppearanceData()
            {
                if (Bindings != null)
                {
                    Bindings.Calls.Add("detail:" + Bindings.Hash); Bindings.Reads++;
                    Appearance.ModifyTimeStamp = Bindings.Hash;
                }
                return Appearance;
            }
            public ProfessionPortraitKind GetPortraitType() => ProfessionPortraitKind.Profession;
            public int GetSex() => 1;
            public int GetProfession() => 1;
            public string GetAnimationName() => "100/idle_01";
            public bool NeedPersist() => Persist;
            public bool TryGetCancellationToken(out CancellationToken token) { token = cancellation.Token; return AllowToken; }
            public void OnTextureUpdate(Texture2D texture) { }
            public void StopRequest() => cancellation.Cancel();
            public void Dispose() => cancellation.Dispose();
        }
        public sealed class Studio : MonoBehaviour, IProfessionPortraitStudio
        {
            public CharacterPortraitTexturePool Pool;
            public Bindings Environment;
            public Component Instance => this;
            public void Init(CharacterPortraitTexturePool pool) { Pool = pool; Environment.Calls.Add("init"); }
            public CharacterPortraitTexture2D CapturePortraitTexture(ICharacterPortraitRequestor request, out byte[] bytes)
            {
                Environment.Calls.Add("capture:" + request.GetRequestId());
                Environment.Captures++;
                bytes = null;
                if (Environment.FailCapture) return null;
                var parameters = Environment.Settings.GetParameters(request.GetPortraitType());
                var result = Pool.GetTexture(parameters.TextureWidth, parameters.TextureHeight); textures.Add(result);
                RenderTexture previous = RenderTexture.active;
                var rt = RenderTexture.GetTemporary(parameters.TextureWidth, parameters.TextureHeight, 0, RenderTextureFormat.ARGB32);
                try
                {
                    RenderTexture.active = rt; GL.Clear(false, true, Color.red);
                    result.ReadPixelsFromRT(rt, out bytes);
                    Environment.LastBytes = bytes;
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
                return result;
            }
        }
        public sealed class Bindings : IProfessionPortraitManagerBindings
        {
            public readonly List<string> Calls = new List<string>();
            public PlayerPortraitSettings Settings { get; set; }
            public ICharacterPortraitPersister Persister { get; set; }
            public GAction LowMemory { get; } = new GAction();
            public IReadOnlyList<ProfessionPortraitSkinHash> AppearanceHashes { get; } = new[] { new ProfessionPortraitSkinHash("fixture", 15), new ProfessionPortraitSkinHash("fixture", 17) };
            public IReadOnlyList<ProfessionPortraitSkinName> SkinNames { get; } = new[] { new ProfessionPortraitSkinName(101, "fixture") };
            public IProfessionPortraitAppearanceReader AppearanceReader { get; } = new ProfessionPortraitAppearanceReader();
            public Studio Studio;
            public long Hash = 1;
            public int Captures, Reads;
            public bool FailCapture;
            public byte[] LastBytes;
            public IProfessionPortraitStudio InstantiateStudio()
            {
                Calls.Add("instantiate");
                Studio = new GameObject("Portrait manager capture fixture").AddComponent<Studio>();
                Studio.Environment = this;
                return Studio;
            }
        }
        static ProfessionPortraitManagerValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Portrait manager fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized) { initialized = true; Validate(); }
                if (++frames > 3)
                {
                    Require(environment.Studio == null, "source manager disposal destroys the studio component");
                    Debug.Log("Profession original portrait manager passed: original detail/XOR helper, deferred same-frame queue draining, second appearance read, cache and real disk reuse, NeedPersist write gate, cancellation/no-token/failure branches, skin hash first-wins mapping, forced cleanup/subscriber protection/fourth-pass shrink and disposal. Capture and appearance objects are fixtures; live characters and full profession UI are not covered.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Validate()
        {
            string root = ProfessionValidationWorkspace.Output("portrait-manager-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            environment = new Bindings
            {
                Settings = AssetDatabase.LoadAssetAtPath<PlayerPortraitSettings>("Assets/Official/ProfessionUI/Portrait/PlayerPortraitSettings.asset"),
                Persister = new CharacterPortraitPersister(new VersionState(), root)
            };
            manager = new CharacterPortraitManager(environment);
            manager.Initialize();
            ICharacterPortraitManager api = manager;
            Require(environment.Calls.SequenceEqual(new[] { "instantiate", "init" }) && manager.GetSkinHash(101) == 15, "source initialization order and first appearance-hash mapping");
            using (var hashRequest = new Request { Id = 19 })
            {
                hashRequest.Appearance.ModifyTimeStamp = 123;
                hashRequest.Appearance.SkinMap.Add(SkinType.eye, new SkinFixture { SkinId = 101 });
                hashRequest.Appearance.SkinMap.Add(SkinType.body, null);
                hashRequest.GetDetailRequestData(environment.Settings, manager, environment.AppearanceReader, out var detail);
                Require(detail.RequestId == 19 && detail.VersionId == 123 && detail.AppearanceHashCode == (123 ^ 15), "original detail helper uses timestamp XOR only weighted skin hashes and does not read zero-weight skin values");
                var parameters = JsonUtility.FromJson<PlayerPortraitSettings.PhotographyParameters>("{\"mAppearanceHashWeights\":{\"mSerializeBuffer\":[{\"Key\":0,\"Value\":5},{\"Key\":5,\"Value\":-1}]}}");
                Require(hashRequest.Appearance.GetAppearanceHashCode(parameters, manager, environment.AppearanceReader) == (123 ^ 15), "positive weights select hashes without multiplication; negative weights skip null skin values");
                var valid = new CharacterPortraitCache();
                valid.UpdateTexture(environment.Settings.LoadingTexture, detail.AppearanceHashCode, false, out _);
                detail.VersionId++;
                Require(valid.IsVaildCacha(in detail), "original validity compares appearance hash independently of VersionId");
            }
            using var first = new Request { Id = 10, Bindings = environment };
            CharacterPortraitCache cache = api.GetCharacterPortrait(first);
            Require(cache.ForceGetTexture() == environment.Settings.LoadingTexture && environment.Captures == 0, "source returns loading cache before queued capture");
            environment.Hash = 2;
            manager.Update();
            Require(environment.Captures == 1 && environment.Reads == 2 && cache.AppearanceHashCode == 2 && cache.NeedRecycle, "source rereads appearance after queue release");
            Require(environment.Calls.TakeLast(2).SequenceEqual(new[] { "detail:2", "capture:10" }), "source second read precedes capture");
            Require(File.ReadAllBytes(Path.Combine(root, "CharacterPortrait", "10_2")).SequenceEqual(environment.LastBytes), "source capture writes exact GPU bytes with updated appearance hash");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("portrait-manager-fixture.png"), cache.ForceGetTexture().Tex.EncodeToPNG());
            Require(ReferenceEquals(cache, api.GetCharacterPortrait(first)) && environment.Captures == 1 && environment.Reads == 3, "source hash-valid cache skips disk/capture");

            environment.Hash = 3;
            using var cancelled = new Request { Id = 10, Bindings = environment };
            api.GetCharacterPortrait(cancelled); cancelled.StopRequest(); manager.Update();
            Require(environment.Captures == 1 && cache.AppearanceHashCode == 2, "source request cancellation preserves the prior cache");
            using var disk = new Request { Id = 11, Persist = false, Bindings = environment };
            var diskData = new CharacterPortraitDetailRequestData { RequestId = 11, AppearanceHashCode = 3 };
            environment.Persister.SetPersistentTexture(diskData, environment.LastBytes);
            CharacterPortraitCache diskCache = api.GetCharacterPortrait(disk);
            textures.Add(diskCache.ForceGetTexture());
            Require(environment.Captures == 1 && diskCache.AppearanceHashCode == 3 && diskCache.ForceGetTexture().Tex.GetPixelData<byte>(0).ToArray().SequenceEqual(environment.LastBytes), "source disk reuse is independent of NeedPersist");

            using var noWrite = new Request { Id = 12, Persist = false, Bindings = environment };
            using var secondQueued = new Request { Id = 13, Persist = false, Bindings = environment };
            var noWriteCache = api.GetCharacterPortrait(noWrite);
            var secondCache = api.GetCharacterPortrait(secondQueued);
            Require(environment.Captures == 1, "source captures are deferred until update");
            manager.Update();
            Require(environment.Captures == 3 && noWriteCache.AppearanceHashCode == 3 && secondCache.AppearanceHashCode == 3, "source update drains every queued request in the same frame");
            Require(!File.Exists(Path.Combine(root, "CharacterPortrait", "12_3")) && !File.Exists(Path.Combine(root, "CharacterPortrait", "13_3")), "source NeedPersist gates only capture writes");
            using var noToken = new Request { Id = 14, AllowToken = false, Bindings = environment };
            var noTokenCache = api.GetCharacterPortrait(noToken); manager.Update();
            Require(environment.Captures == 3 && noTokenCache.ForceGetTexture() == environment.Settings.LoadingTexture, "source missing request token leaves loading cache without capture");
            using var failed = new Request { Id = 16, Bindings = environment };
            var failureCache = api.GetCharacterPortrait(failed);
            int failureDiagnostics = 0;
            Application.LogCallback failedCapture = (message, stack, type) => { if (type == LogType.Error && message.Contains("拍摄失败")) failureDiagnostics++; };
            Application.logMessageReceived += failedCapture;
            environment.FailCapture = true;
            try { manager.Update(); }
            finally { environment.FailCapture = false; Application.logMessageReceived -= failedCapture; }
            Require(environment.Captures == 4 && failureDiagnostics == 1 && failureCache.ForceGetTexture() == environment.Settings.DisableTexture && failureCache.AppearanceHashCode == -1 && !failureCache.NeedRecycle, "source failed capture selects original disable art, hash and ownership without persistence");

            var caches = Field<Dictionary<long, CharacterPortraitCache>>(manager, "mCaches");
            foreach (var cached in caches.Values) Set(cached, "mClearTimeStamp", -1f);
            cache.SubcribeTexture(first);
            Set(manager, "mTextureClearMark", 3);
            ForceCleanup();
            Require(caches.Count == 1 && caches.ContainsKey(10), "source expiration preserves subscribed cache and removes the other entries after recycling");
            cache.Unsubscribe(first);
            ForceCleanup(); ForceCleanup();
            Require(caches.Count == 0 && textures.Distinct().All(x => x.Tex != null), "source first three cleanup passes preserve queued texture resources");
            ForceCleanup();
            Require(textures.Distinct().Count(x => x.Tex == null) == 1 && Field<int>(manager, "mTextureClearMark") == 3 && Field<float>(manager, "mMemoryClearThink") > Time.time + 9.9f, "source fourth pass shrinks one texture in the bucket, resets the mark and schedules ten seconds later");
            using var destroyCancelled = new Request { Id = 15, Bindings = environment };
            api.GetCharacterPortrait(destroyCancelled);
            manager.Dispose(); manager = null;
            Require(environment.Captures == 4, "source destroy cancellation aborts pending capture");
            api.ClearAllCharacterPortraitPersistenceData();
            Require(!Directory.EnumerateFiles(Path.Combine(root, "CharacterPortrait")).Any(), "source clear delegates to actual original persister");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Portrait manager: " + message); }
        private static T Field<T>(object value, string name)
            => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Set(object value, string name, object replacement)
            => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, replacement);
        private static void ForceCleanup()
        {
            // Advance only this fixture's private timer; do not wait forty real seconds.
            Set(manager, "mMemoryClearThink", -1f);
            manager.Update();
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            manager?.Dispose(); manager = null;
            foreach (var texture in textures) { if (texture.Tex != null) texture.DestroyTexture(); }
            textures.Clear();
            EditorApplication.Exit(code);
        }
    }
}
