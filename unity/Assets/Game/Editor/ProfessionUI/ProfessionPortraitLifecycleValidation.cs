using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    // A texture/lifecycle fixture, not a replacement character or profession UI.
    [InitializeOnLoad]
    public static class ProfessionPortraitLifecycleValidation
    {
        private const string Active = "Zjcs.ProfessionPortraitLifecycle.Active";
        private static bool ready, initialized;
        private static int frames;
        private static readonly List<CharacterPortraitTexture2D> textures = new List<CharacterPortraitTexture2D>();
        private sealed class Manager : ICharacterPortraitManager
        {
            public readonly CharacterPortraitCache Cache = new CharacterPortraitCache();
            public readonly List<string> Calls = new List<string>();
            public ICharacterPortraitRequestor Last;
            public CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor requestor)
            { Calls.Add("get"); Last = requestor; return Cache; }
            public void ClearAllCharacterPortraitPersistenceData() => throw new NotSupportedException("Not part of this lifecycle fixture.");
        }
        static ProfessionPortraitLifecycleValidation()
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
                EditorApplication.isPaused = false;
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Portrait lifecycle fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized)
                {
                    initialized = true;
                    ValidateTextures();
                    ValidatePool();
                    ValidateRequestAndCache();
                }
                if (++frames > 3)
                {
                    Debug.Log("Profession original portrait lifecycle passed: GPU read/copy bytes, request key, cancellation and notification order, cache expiry/subscription, LIFO texture pool, one-per-bucket shrink and low-memory lifecycle. Character photographer, appearance and full profession UI are not covered.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static CharacterPortraitTexture2D Texture()
        {
            // Non-multiple-of-four height deliberately exercises the source RGBA
            // fallback used by its 260x310 profession portrait dimensions.
            var pair = new CharacterPortraitTexture2D(8, 6);
            textures.Add(pair);
            return pair;
        }
        private static void ValidateTextures()
        {
            CharacterPortraitTexture2D pair = Texture();
            Require(!pair.TexIsETC2Format && pair.Width == 8 && pair.Height == 6 && pair.CostMemBytes() == 8 * 6 * 4, "original uncompressed allocation and memory calculation");
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(8, 6, 0, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture.active = target;
                GL.Clear(false, true, new Color(1, 0, 0, 1));
                pair.ReadPixelsFromRT(target, out byte[] bytes);
                Require(RenderTexture.active == target && bytes.Length == 8 * 6 * 4, "source readback restores the prior active target and returns exact byte length");
                Require(Enumerable.Range(0, 48).All(i => bytes[i * 4] == 255 && bytes[i * 4 + 1] == 0 && bytes[i * 4 + 2] == 0 && bytes[i * 4 + 3] == 255), "actual rendered RGBA pixels survive source readback");
                CharacterPortraitTexture2D copy = Texture();
                copy.LoadImage(bytes);
                Require(copy.Tex.GetPixelData<byte>(0).ToArray().SequenceEqual(bytes), "source persisted-pixel reload is byte exact");
                var retained = new CharacterPortraitTexture2D(copy.Tex, 8, 6, true);
                retained.DestroyTexture();
                Require(retained.Tex == copy.Tex, "source don't-destroy texture ownership");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("portrait-texture-fixture.png"), pair.Tex.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private static void ValidatePool()
        {
            var lowMemory = new GAction();
            var pool = new CharacterPortraitTexturePool(lowMemory);
            var borrowed = Enumerable.Range(0, 6).Select(_ => pool.GetTexture(8, 6)).ToArray();
            textures.AddRange(borrowed);
            foreach (var pair in borrowed) pool.Recycle(pair);
            Require(ReferenceEquals(pool.GetTexture(8, 6), borrowed[5]), "original pool is LIFO");
            pool.Recycle(borrowed[5]);
            pool.ReleaseResourcesCache();
            Require(borrowed[5].Tex == null && borrowed[4].Tex != null && borrowed[3].Tex != null, "original cache shrink removes one per bucket per call");
            pool.ReleaseResourcesCache(); pool.ReleaseResourcesCache(); pool.ReleaseResourcesCache();
            Require(borrowed.Take(3).All(p => p.Tex != null) && borrowed.Skip(3).All(p => p.Tex == null), "original shrink retains three");
            lowMemory.Invoke();
            Require(borrowed.All(p => p.Tex == null), "original low-memory signal drains every queued texture");
            var after = pool.GetTexture(8, 6); textures.Add(after); pool.Recycle(after);
            pool.Dispose(); lowMemory.Invoke();
            Require(after.Tex != null, "original Dispose unsubscribes low-memory without inventing cache-release behavior");
        }
        private static void ValidateRequestAndCache()
        {
            var owner = new GameObject("Portrait lifecycle fixture", typeof(RectTransform));
            owner.SetActive(false);
            var raw = owner.AddComponent<RawImage>();
            var component = owner.AddComponent<ProfessionCharacterPortrait>();
            var requestor = new CharacterPortraitRequestor();
            Field(requestor, "mRawImage", raw); Field(component, "mRequestor", requestor);
            var manager = new Manager();
            var pair = Texture();
            manager.Cache.UpdateTexture(pair, 11, false, out var old);
            Require(old == null && !manager.Cache.NeedRecycle && manager.Cache.AppearanceHashCode == 11, "source initial cache state");
            component.Bind(manager);
            owner.SetActive(true);
            object appearance = new object();
            component.GeneratePortrait(7, 0, 200, appearance, image => manager.Calls.Add(image.texture == null ? "clear" : "texture"));
            Require(manager.Calls.SequenceEqual(new[] { "get", "texture" }) && raw.texture == pair.Tex && raw.rectTransform.sizeDelta == new Vector2(8, 6), "original request/subscribe/texture/native-size order");
            Require(requestor.GetRequestId() == unchecked(((long)(50 + 200) << 54) + 7) && ReferenceEquals(requestor.GetAppearanceData(), appearance) && requestor.GetSex() == 0 && requestor.GetProfession() == 200 && requestor.GetPortraitType() == ProfessionPortraitKind.Profession && requestor.NeedPersist(), "source request identity and opaque appearance payload");
            Require(!manager.Cache.IsExpire(float.MaxValue), "a subscribed portrait cannot expire");
            Require(requestor.TryGetCancellationToken(out CancellationToken first) && requestor.TryGetCancellationToken(out CancellationToken second) && first.IsCancellationRequested && !second.IsCancellationRequested, "replacing a request cancels its previous token");
            second.Register(() => manager.Calls.Add("cancel"));
            owner.SetActive(false);
            Require(manager.Calls.SequenceEqual(new[] { "get", "texture", "cancel", "clear" }) && manager.Cache.Requestors.Count == 0 && raw.texture == null, "original profession disable cancels before base unsubscribe and notification");
            float now = Time.realtimeSinceStartup;
            Require(!manager.Cache.IsExpire(now + 59) && manager.Cache.IsExpire(now + 61), "original 60-second cache expiry");
            owner.SetActive(true);
            Require(manager.Calls.Skip(4).SequenceEqual(new[] { "get", "texture" }) && manager.Cache.Requestors.Count == 1, "original enable/init lifecycle re-subscribes once");
            var replacement = Texture();
            manager.Cache.UpdateTexture(replacement, 12, true, out old);
            Require(ReferenceEquals(old, pair) && raw.texture == replacement.Tex && manager.Cache.NeedRecycle, "source cache update returns previous texture using incoming recycle flag and publishes replacement");
            owner.SetActive(false);
            manager.Cache.Clear();
            Require(manager.Cache.AppearanceHashCode == 0 && !manager.Cache.NeedRecycle && manager.Cache.ForceGetTexture() == null, "source cache clear state");
            Object.Destroy(owner);
        }
        private static void Field(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Profession portrait lifecycle: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (var pair in textures) pair.DestroyTexture();
            EditorApplication.Exit(code);
        }
    }
}
