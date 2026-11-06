using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityMonsters(string output)
        {
            var fixture = new GameObject("Original activity monster viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            fixture.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // This component fixture has no world scene. Clear the framebuffer
            // each frame so previous poses cannot survive behind the overlay.
            var cameraObject = new GameObject("Activity icon validation clear camera", typeof(Camera));
            cameraObject.transform.SetParent(fixture.transform, false);
            var clearCamera = cameraObject.GetComponent<Camera>();
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            clearCamera.backgroundColor = Color.black;
            clearCamera.cullingMask = 0;
            var assets = Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/ActivityMonsterAssets");
            var prefabs = Resources.Load<OfflineProfessionImageCatalog>("DailyChallenges/ActivityMonsterPrefabs");
            Require(assets && prefabs && prefabs.Entries.Length == 6, "The six original Spine activity icon bindings are absent.");
            var managerConfig = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
            using var manager = new AppearanceAssetManager(managerConfig, action => {
                Application.LowMemoryCallback callback = () => action(); Application.lowMemory += callback;
                return new ActivityMonsterSubscription(() => Application.lowMemory -= callback);
            });
            var textures = new OfflinePartnerUiTextureResources(assets, manager);
            var loader = new ActivityMonsterTrackedLoader(new OfflineProfessionImageResourceLoader(prefabs));
            var previous = ProfessionAppearanceTextureBindings.Current;
            ProfessionAppearanceTextureBindings.Current = textures;
            GameObject host = null;
            try
            {
                host = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ActivityMonsterIcons/ActivityDomainBossMonsterUI"), fixture.transform, false);
                Require(host.GetComponentsInChildren<MonoBehaviour>(true).All(item => item != null), "An original activity monster host script is missing.");
                var view = host.GetComponent<MonsterUI>();
                Require(view && view.Slot == host.transform && host.transform.localScale == new Vector3(1.2f, 1.2f, 1.2f),
                    "The original activity monster Slot or scale differs.");
                int[] icons = { 3703, 3704, 3705, 3707, 3708, 3709 };
                // Explicit icon identities are component-fixture inputs. The
                // complete page must supply MonsterInfo.IconId from its stage.
                view.MonsterIconId = id => icons.Contains(id) ? id : (int?)null;
                view.ResourceLoader = loader;
                view.InstantiatePrefab = (prefab, parent) => {
                    var staging = new GameObject("Inactive original icon binding", typeof(RectTransform));
                    staging.SetActive(false); staging.transform.SetParent(parent, false);
                    var instance = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                    instance.SetActive(false); instance.transform.SetParent(parent, false);
                    UnityEngine.Object.Destroy(staging);
                    textures.BindTree(instance); instance.SetActive(true); return instance;
                };
                foreach (int id in icons)
                {
                    view.Init(id);
                    yield return null;
                    yield return new WaitForSeconds(.35f);
                    Require(view.Slot.childCount == 1, "Switching icons retained a previous model: " + id);
                    var model = view.Slot.GetChild(0).gameObject;
                    var native = model.GetComponent<PartnerNativeUiActor>();
                    Require(native && native.Graphic.State != null && native.Pose != null, "Original monster pose did not initialize: " + id);
                    Require(native.AnimationState.GetCurrent(0)?.Animation.Name == native.StartingAnimation,
                        "The original initial animation differs: " + id);
                    Require(model.transform.localPosition == Vector3.zero
                        && model.transform.localScale == assets.LoadPrefab("Appearance/Monster/" + id + "/MonsterIcon_" + id).transform.localScale,
                        "Original MonsterUI instantiation changed model geometry: " + id);
                    Require(textures.LoadedResourceTextureCount > 0 && (native.Graphic.allowMultipleCanvasRenderers
                        ? native.Graphic.CustomTextureOverride.Count > 0 : native.Graphic.OverrideTexture),
                        "The original High texture loader did not bind: " + id);
                    var before = ActivityMonsterVertices(model);
                    Require(before.Length > 0, "Original monster rendered no vertices: " + id);
                    int requests = loader.Requests;
                    view.Init(id);
                    Require(loader.Requests == requests && view.Slot.GetChild(0).gameObject == model,
                        "The original same-ID guard reloaded the icon: " + id);
                    yield return new WaitForSeconds(.3f);
                    Require(!before.SequenceEqual(ActivityMonsterVertices(model)), "Original idle animation did not advance: " + id);
                    yield return new WaitForEndOfFrame();
                    var image = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.Combine(output, "activity-monster-" + id + ".png"), image.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(image); }
                    host.SetActive(false); manager.Update();
                    Require(textures.LoadedResourceTextureCount == 0, "Disabling the original view leaked High textures: " + id);
                    host.SetActive(true); yield return null;
                    Require(textures.LoadedResourceTextureCount > 0, "Reopening the original view did not reload High textures: " + id);
                    Debug.Log("[Battle hosts] Original activity monster icon passed: " + id);
                }
                Require(loader.Requests == 6 && loader.Disposals == 0, "Original MonsterUI must retain loaded handles until destruction.");
                UnityEngine.Object.Destroy(host); yield return null; manager.Update();
                Require(loader.Disposals == 6 && textures.LoadedResourceTextureCount == 0,
                    "Destroying the original monster view leaked handles or texture requests.");
                Debug.Log("[Battle hosts] Six original activity Spine icons: native meshes, idle animations, High textures, switching, same-ID guard, disable/reopen and destroy ownership passed.");
            }
            finally
            {
                if (host) UnityEngine.Object.DestroyImmediate(host);
                ProfessionAppearanceTextureBindings.Current = previous;
                UnityEngine.Object.Destroy(fixture);
                UnityEngine.Object.Destroy(managerConfig);
            }
        }

        private static Vector3[] ActivityMonsterVertices(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            return root.GetComponentsInChildren<CanvasRenderer>().Select(item => item.GetMesh())
                .Where(mesh => mesh).SelectMany(mesh => mesh.vertices).ToArray();
        }

        private sealed class ActivityMonsterSubscription : IDisposable
        {
            private Action release;
            public ActivityMonsterSubscription(Action action) { release = action; }
            public void Dispose() { var callback = release; release = null; callback?.Invoke(); }
        }

        private sealed class ActivityMonsterTrackedLoader : IProfessionImageResourceLoader
        {
            private readonly IProfessionImageResourceLoader inner;
            public int Requests, Disposals;
            public ActivityMonsterTrackedLoader(IProfessionImageResourceLoader value) { inner = value; }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object
            { Requests++; return new Lease<T>(inner.LoadAsset<T>(path), () => Disposals++); }
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object => LoadAsset<T>(path);
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object => throw new NotSupportedException();
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => throw new NotSupportedException();
            private sealed class Lease<T> : IProfessionAssetOperation<T> where T : UnityEngine.Object
            {
                private readonly IProfessionAssetOperation<T> inner;
                private Action release;
                public Lease(IProfessionAssetOperation<T> value, Action callback) { inner = value; release = callback; }
                public T Result => inner.Result;
                public bool IsValid => inner.IsValid;
                public int Status => inner.Status;
                public event Action<IProfessionAssetOperation<T>> Completed { add { value?.Invoke(this); } remove { } }
                public void Dispose() { inner.Dispose(); var callback = release; release = null; callback?.Invoke(); }
            }
        }
    }
}
