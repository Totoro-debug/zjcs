using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceTextureValidation
    {
        private const string Active = "Zjcs.ProfessionTextures.Active";
        private static bool ready;
        private static readonly List<Object> owned = new List<Object>();

        private sealed class Handle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            public bool IsValid { get; set; } = true;
            public int Status { get; set; } = 2;
            public T Result { get; set; }
            public int Waits, Releases;
            private Action<IProfessionUnsafeAssetHandle<T>> completed;
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed
            {
                add { completed += value; if (Status != 1) value(this); }
                remove { completed -= value; }
            }
            public void WaitForAsyncComplete() { Waits++; }
            public void Release() { Releases++; IsValid = false; }
            public void Complete(int status) { Status = status; completed?.Invoke(this); }
        }
        private sealed class Bindings : IProfessionAppearanceTextureBindings
        {
            public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
            public readonly List<Handle<Texture>> Handles = new List<Handle<Texture>>();
            public readonly List<string> Calls = new List<string>();
            public Handle<Texture> Next;
            public Material[] Materials;
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
            {
                Require(typeof(T) == typeof(Texture), "fixture texture request type");
                Calls.Add(guid);
                Handle<Texture> handle = Next ?? new Handle<Texture> { Result = Textures[guid] };
                Next = null; Handles.Add(handle);
                return (IProfessionUnsafeAssetHandle<T>)(object)handle;
            }
            public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
            { Require(atlas, "original non-null atlas branch"); return Materials[materialIndex]; }
        }
        static ProfessionAppearanceTextureValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Appearance texture fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var bindings = new Bindings(); ProfessionAppearanceTextureBindings.Current = bindings;
                CheckProxy(bindings); CheckLoader(bindings); CheckSkinAsset();
                Debug.Log("Profession original application texture chain passed: synchronous proxy reuse/wait/release, original synchronous LoadAsync and callback queue/failure, main/dye request state and release, placeholder mapping/replacement and atlas material restoration, reference counters, frame-age/forced expiry, original null-material dye exception and unimplemented queries, skin dictionary identity/duplicates. Resources and atlas are fixtures; no production skin textures, native actor or profession-page validation.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CheckProxy(Bindings bindings)
        {
            Texture texture = Own(new Texture2D(2, 2)); bindings.Textures["proxy"] = texture;
            var invalid = new SpineTextureAssetProxy(); int callbacks = 0;
            Require(!invalid.IsValid && !invalid.Load(), "empty original GUID does not load");
            invalid.LoadAsync(t => { Require(!t, "invalid GUID callback"); callbacks++; });
            invalid.LoadAsync(null); Require(callbacks == 1 && bindings.Calls.Count == 0, "original invalid/null callback branches");
            var proxy = Proxy("proxy"); Require(proxy.Load() == texture, "synchronous original load");
            var first = bindings.Handles[0];
            Require(proxy.Load() == texture && first.Waits == 1 && bindings.Calls.Count == 1, "reuse waits on existing valid handle");
            proxy.LoadAsync(t => { Require(t == texture, "completed original callback"); callbacks++; });
            Require(callbacks == 2 && bindings.Calls.Count == 1, "completed handle callback is immediate");
            proxy.Unload(); proxy.Unload(); Require(first.Releases == 1, "unload clears handle after release");
            var pending = new Handle<Texture> { Status = 1, Result = texture }; bindings.Next = pending;
            var order = new List<int>();
            proxy.LoadAsync(t => order.Add(1)); proxy.LoadAsync(t => order.Add(2));
            Require(order.Count == 0 && bindings.Calls.Count == 2, "LoadAsync uses one synchronous resource operation and queues pending callbacks");
            pending.Complete(2); Require(order.Count == 2 && order[0] == 1 && order[1] == 2, "original callback combination order");
            proxy.Unload();
            var failed = new Handle<Texture> { Status = 3 }; bindings.Next = failed;
            Require(!proxy.Load() && failed.Releases == 1, "failed synchronous result releases and clears original handle");
            proxy.Unload(); Require(failed.Releases == 1, "failed handle is not released twice");
            var request = new YooAssetTextureHandle();
            request.Load(new YooAssetTextureReference { AssetReference = Proxy("proxy") });
            int count = bindings.Calls.Count; request.Load(new YooAssetTextureReference { AssetReference = Proxy("absent") });
            Require(request.WasRequested && request.WasSuccessfullyLoaded && request.IsValid && request.Result == texture && bindings.Calls.Count == count, "original texture handle loads once");
            request.Release(); Require(!request.WasRequested && !request.IsValid && !request.Result, "original texture handle release");
            bindings.Textures["null"] = null;
            request.Load(new YooAssetTextureReference { AssetReference = Proxy("null") });
            Require(request.WasSuccessfullyLoaded && !request.Result, "original null texture still completes request state"); request.Release();
        }
        private static void CheckLoader(Bindings bindings)
        {
            var placeholder = Own(new Texture2D(1, 1)); var secondPlaceholder = Own(new Texture2D(1, 1));
            Texture main = Own(new Texture2D(2, 2)), dye = Own(new Texture2D(2, 2)), second = Own(new Texture2D(2, 2));
            bindings.Textures["main"] = main; bindings.Textures["dye"] = dye; bindings.Textures["second"] = second;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            var material = Own(new Material(shader)); var secondMaterial = Own(new Material(shader));
            material.mainTexture = placeholder; secondMaterial.mainTexture = secondPlaceholder;
            var loader = Own(ScriptableObject.CreateInstance<YooAssetTextureLoader>());
            loader.placeholderMap = new[] {
                new AppearanceOnDemandTextureLoader<YooAssetTextureReference,YooAssetRequest>.PlaceholderTextureMapping {
                    placeholderTexture = placeholder, targetTextureReference = new YooAssetTextureReference { AssetReference = Proxy("main") }, dyeTextureReference = new YooAssetTextureReference { AssetReference = Proxy("dye") } },
                new AppearanceOnDemandTextureLoader<YooAssetTextureReference,YooAssetRequest>.PlaceholderTextureMapping {
                    placeholderTexture = secondPlaceholder, targetTextureReference = new YooAssetTextureReference { AssetReference = Proxy("second") } }
            };
            loader.BeginCustomTextureLoading(); loader.TryInit();
            Require(loader.PlaceholderTextureIndexDict[placeholder] == 0 && loader.PlaceholderTextureIndexDict[secondPlaceholder] == 1 && loader.GetPlaceholderTextureName("body") == "body_placeholder", "original mapping order and name");
            Material overridden = null; loader.RequestLoadMaterialTextures(material, ref overridden);
            Require(material.mainTexture == main && material.GetTexture("_DyeTex") == dye && !overridden, "original main/dye material replacement");
            Texture replacement = secondPlaceholder, callback = null;
            loader.RequestLoadTexture(secondPlaceholder, ref replacement, t => callback = t);
            Require(replacement == secondPlaceholder && callback == second, "first synchronous request keeps ref replacement, invokes callback");
            loader.RequestLoadTexture(secondPlaceholder, ref replacement);
            Require(replacement == second, "subsequent loaded request replaces ref texture");
            Require(loader.GetAlreadyLoadedTexture(0, out Texture loadedDye) == main && loadedDye == dye, "original already-loaded query");
            loader.AddLoadedTextureRef(placeholder); loader.AddLoadedTextureRef(1);
            loader.unloadAfterSecondsUnused = float.MaxValue;
            int count = bindings.Handles.Count;
            loader.TryUnloadUnusedTextures(true);
            Require(bindings.Handles[count - 3].Releases == 0 && bindings.Handles[count - 1].Releases == 0, "reference protection precedes forced expiry");
            loader.RemoveLoadedTextureRef(main); loader.RemoveLoadedTextureRef(1);
            loader.EndCustomTextureLoading();
            Require(bindings.Handles[count - 3].Releases == 0, "original frame age protects recent request");
            // A native atlas implementation is supplied explicitly; this test
            // object only establishes identity for the non-null branch.
            loader.atlasAsset = Own(ScriptableObject.CreateInstance<SkinTextureAsset>());
            bindings.Materials = new[] { material, secondMaterial };
            loader.TryUnloadUnusedTextures(true);
            Require(bindings.Handles[count - 3].Releases == 1 && bindings.Handles[count - 2].Releases == 1 && bindings.Handles[count - 1].Releases == 1 && material.mainTexture == placeholder && !material.GetTexture("_DyeTex") && secondMaterial.mainTexture == secondPlaceholder, "original release main/dye then restore atlas placeholders");
            loader.RequestUnloadTexture(999);
            // Preserve the original OR in CreateTextureRequest: a non-null dye
            // with no material throws after both handles have loaded.
            replacement = placeholder;
            Throws<NullReferenceException>(() => loader.RequestLoadTexture(placeholder, ref replacement));
            loader.RequestUnloadTexture(0);
            loader.RequestLoadMaterialTextures(secondMaterial, ref overridden);
            loader.RemoveLoadedTextureRef(1); // Original counters may go negative.
            loader.unloadAfterSecondsUnused = -1;
            loader.TryUnloadUnusedTextures();
            Require(secondMaterial.mainTexture == secondPlaceholder, "negative counter and expired frame age unload");
            Require(loader.AssignPlaceholderTextures(out var modified) && modified == null && loader.AssignTargetTextures(out modified) && modified == null, "original assignment return contract");
            Throws<NotImplementedException>(() => loader.HasPlaceholderAssigned(material));
            Throws<NotImplementedException>(() => loader.HasPlaceholderTexturesAssigned(out var unused));
            loader.skeletonDataAsset = loader.atlasAsset; ScriptableObject skeleton = loader.skeletonDataAsset;
            loader.Clear(true);
            Require(!loader.atlasAsset && loader.skeletonDataAsset == skeleton && loader.placeholderMap == null && loader.PlaceholderTextureIndexDict == null, "original Clear keeps skeleton pointer and clears atlas/map state");
            var duplicate = Own(ScriptableObject.CreateInstance<YooAssetTextureLoader>());
            var mapping = new AppearanceOnDemandTextureLoader<YooAssetTextureReference,YooAssetRequest>.PlaceholderTextureMapping { placeholderTexture = placeholder };
            duplicate.placeholderMap = new[] { mapping, mapping };
            Throws<ArgumentException>(() => duplicate.TryInit());
        }
        private static void CheckSkinAsset()
        {
            var asset = Own(ScriptableObject.CreateInstance<SkinTextureAsset>());
            var rows = new List<SkinTextureConfig> { new SkinTextureConfig { SkinName = "a" }, new SkinTextureConfig { SkinName = "b" } };
            typeof(SkinTextureAsset).GetField("mSkinTextureConfigList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, rows);
            asset.OnAfterDeserialize();
            Require(ReferenceEquals(rows, asset.SkinTextureConfigList) && ReferenceEquals(rows[1], asset.GetConfig("b")), "original skin list and dictionary identity");
            rows.Add(new SkinTextureConfig { SkinName = "a" });
            Throws<ArgumentException>(() => asset.OnAfterDeserialize());
            Require(ReferenceEquals(rows[0], asset.GetConfig("a")), "original duplicate failure retains partial dictionary");
        }
        private static SpineTextureAssetProxy Proxy(string guid)
        { var proxy = new SpineTextureAssetProxy(); JsonUtility.FromJsonOverwrite("{\"mAssetGuid\":\"" + guid + "\"}", proxy); return proxy; }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false); ProfessionAppearanceTextureBindings.Current = null;
            foreach (Object value in owned) if (value) Object.DestroyImmediate(value);
            EditorApplication.Exit(code);
        }
    }
}
