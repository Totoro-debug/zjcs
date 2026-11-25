using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Offline.HomeSceneInput;

[InitializeOnLoad]
public static class AirshipVisualProbe
{
    private const string Key = "AirshipVisualProbe.Active";
    static AirshipVisualProbe() { EditorApplication.playModeStateChanged += Change; }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void Change(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            new GameObject("AirshipVisualProbe").AddComponent<AirshipVisualRunner>();
    }
    public static void Finish(Exception error)
    {
        SessionState.EraseBool(Key);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public class AirshipVisualRunner : MonoBehaviour
{
    private int errors;
    private void Observe(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    private sealed class Empty : IDisposable { public void Dispose() { } }
    private sealed class Lease : IAirshipSpriteLease { public Sprite Sprite { get; set; } public void Dispose() { } }
    private sealed class AirshipServices : IAirshipFurnitureServices
    {
        public Action<int, Sprite> OnDisplay; public int AnimationCalls, Loads;
        public IDisposable SubscribeDisplayItemChange(Action<int, Sprite> callback) { OnDisplay = callback; return new Empty(); }
        public IAirshipSpriteLease LoadItemBackground(bool repaired)
        {
            Loads++;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_" + (repaired ? "2" : "1") + ".asset");
            if (!sprite) throw new Exception("Original airship background unavailable");
            return new Lease { Sprite = sprite };
        }
        public void PlayAnimation(SkeletonAnimation skeleton, string animation, bool loop)
        {
            AnimationCalls++;
            if (skeleton.AnimationState == null) { skeleton.loop = loop; skeleton.AnimationName = animation; }
            else skeleton.AnimationState.SetAnimation(0, animation, loop);
        }
        public void OpenTradePanel() { throw new Exception("Trade is unavailable in this fixture"); }
    }
    private sealed class BannerServices : IAirShipBannerServices
    {
        public bool HasAirship => true;
        public IDisposable SubscribeRepair(Action callback) => new Empty();
        public IDisposable SubscribeActivities(Action callback) => new Empty();
        public System.Collections.Generic.IEnumerable<AirshipActivityEntry> ActivityEntries() => Array.Empty<AirshipActivityEntry>();
        public void ClickActivity(AirshipActivityEntry entry) { throw new Exception("Unexpected activity"); }
        public string SubtitleKey(AirshipActivityEntry entry) { throw new Exception("Unexpected activity"); }
        public GameObject LoadActivityBanner(AirshipActivityEntry entry, Transform slot) { throw new Exception("Unexpected activity"); }
        public void ReleaseBanners() { }
    }
    private sealed class Context : IHomeSceneClickContext { public bool InMyHome => true; public bool AllNoTipWindowsClosed => true; }
    private sealed class Services : IHomeFurnitureServices, IHomeFurniturePlayer
    {
        public bool InMyHome => true; public IHomeFurniturePlayer Player => this; public IHomeFurniturePlayerData PlayerData => this;
        public event Action<EFurniture> OnRepairFurniture; public event Action<long> OnDebugTimeOffset;
        public bool Present = true;
        public bool HasFurniture(EFurniture furniture) => Present;
        public bool IsMetFurnitureRepairCondition(FurnitureInfo info) => false;
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo info) => false;
        public bool HasStartedStory(string name) => false;
        public FurnitureInfo GetFurnitureInfo(EFurniture furniture) => new FurnitureInfo();
        public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action) => null;
        public IDisposable SubscribePostHomeChange(Action action) => null;
        public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> action) { }
        public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> action) { }
        public bool CheckFunctionDisabled(EFurniture furniture) => true;
        public void ShowError(int id) { } public void PopupRepair(EFurniture furniture, bool isBuild) { }
        public void PopupPlayerBag() { } public void PopupTreasurePanel() { }
        public void DoStoryAction(Action action) { } public IHomeFurnitureStoryContext CreateStoryContext(string owner) => null;
        public void PlayStory(IHomeFurnitureStoryContext context, string name) { } public void DisposeStory(IHomeFurnitureStoryContext context) { }
        public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => null;
        public void SystemMessage(string message) { } public bool IsFunctionLimit(int type) => false;
        public void PopupNpcPanel(int tab = 0) { } public DateTime GetDateTime() => DateTime.Now;
        public DateTime GetDailyResetTimeFromDate(DateTime time) => time; public void RefreshDailyDialogues(bool invokeByPlayer) { }
    }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        AudioListener.volume = 0;
        Application.logMessageReceived += Observe;
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        if (!pipeline) { AirshipVisualProbe.Finish(new Exception("Native pipeline unavailable")); yield break; }
        GraphicsSettings.renderPipelineAsset = pipeline;
        QualitySettings.renderPipeline = pipeline;
        string[] names = { "Airship", "AirshipSnow", "Airship_YellowDuck", "Airship_YellowDuckSnow" };
        foreach (string name in names)
        {
            var input = new OfflineHomeSceneInput(() => null, () => null, new Context());
            var stage = new GameObject("InactiveStage"); stage.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/Airship/Native/" + name + ".prefab");
            var instance = Instantiate(prefab, stage.transform, false);
            foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(script is SkeletonAnimation) && !(script is AirshipFurniture) && !(script is RenderExistingMesh) &&
                    !(script is SkeletonSlotColor) && !(script is SkeletonRendererCustomMaterials) && !(script is AirShipBanner) && !(script is HomeScrollTextBord) && !(script is WorldMultiLanguageText) && !(script is TMPro.TMP_Text)) script.enabled = false;
            var furniture = instance.GetComponentInChildren<AirshipFurniture>(true);
            var furnitureServices = new Services();
            furniture.Services = furnitureServices;
            var airshipServices = new AirshipServices();
            furniture.AirshipServices = airshipServices;
            foreach (var banner in instance.GetComponentsInChildren<AirShipBanner>(true)) banner.BannerServices = new BannerServices();
            foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = key => key;

            foreach (ClickableHomeObj clickable in instance.GetComponentsInChildren<ClickableHomeObj>(true)) input.Bind(clickable);
            var field = typeof(FurnitureBase).GetField("mStateSwitcher", BindingFlags.NonPublic | BindingFlags.Instance);
            var state = (WorldStateSwitcher<FurnitureBase.EFurnitureState>)field.GetValue(furniture);
            state.SetState(FurnitureBase.EFurnitureState.Functional);
            stage.SetActive(true);
            yield return null;
            yield return null;
            var banners = instance.GetComponentsInChildren<AirShipBanner>(true);
            if (banners.Length != (name.Contains("YellowDuck") ? 0 : 1)) { AirshipVisualProbe.Finish(new Exception(name + " banner count")); yield break; }
            if (banners.Length == 1)
            {
                if (!banners[0].isActiveAndEnabled) { AirshipVisualProbe.Finish(new Exception(name + " banner inactive")); yield break; }
                var texts = banners[0].GetComponentsInChildren<WorldMultiLanguageText>(true);
                if (Array.Find(texts, text => text.myKey == "ui_airship_billboard_repaired") == null)
                { AirshipVisualProbe.Finish(new Exception(name + " banner default text missing")); yield break; }
            }
            if (airshipServices.Loads == 0 || airshipServices.AnimationCalls == 0 || airshipServices.OnDisplay == null)
            { AirshipVisualProbe.Finish(new Exception(name + " original controller did not initialize")); yield break; }
            var slots = (System.Collections.Generic.List<SpriteRenderer>)typeof(AirshipFurniture)
                .GetField("mItemIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(furniture);
            if (slots.Count > 0)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_2.asset");
                airshipServices.OnDisplay(0, sprite);
                if (slots[0].sprite != sprite) { AirshipVisualProbe.Finish(new Exception(name + " display event failed")); yield break; }
            }
            var backgrounds = (System.Collections.Generic.List<SpriteRenderer>)typeof(AirshipFurniture)
                .GetField("mItemBgList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(furniture);
            var lights = (System.Collections.Generic.List<SpriteRenderer>)typeof(AirshipFurniture)
                .GetField("mLightSpriteRenderers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(furniture);
            furnitureServices.Present = false;
            furniture.UpdateState();
            var off = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_1.asset");
            foreach (var background in backgrounds) if (background.sprite != off) { AirshipVisualProbe.Finish(new Exception(name + " unrepaired background")); yield break; }
            foreach (var light in lights) if (light.material.GetFloat("_LightAtten") != 0f) { AirshipVisualProbe.Finish(new Exception(name + " unrepaired light")); yield break; }
            furnitureServices.Present = true;
            furniture.UpdateState();
            var on = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Home/Airship/Native/Image/Home/Dynamic/Airship/SmallPoster_2.asset");
            foreach (var background in backgrounds) if (background.sprite != on) { AirshipVisualProbe.Finish(new Exception(name + " repaired background")); yield break; }
            foreach (var light in lights) if (light.material.GetFloat("_LightAtten") != 1f) { AirshipVisualProbe.Finish(new Exception(name + " repaired light")); yield break; }
            yield return null;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { AirshipVisualProbe.Finish(new Exception(name + " has no active renderers")); yield break; }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var cameraObject = new GameObject("AirshipCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * 1.7f, 1f) * 1.2f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var target = new RenderTexture(480, 800, 24); target.Create(); camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(480, 800, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 480, 800), 0, 0); image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
            Destroy(image); Destroy(target); Destroy(cameraObject); Destroy(stage);
            yield return null;
            input.Dispose();
        }
        Application.logMessageReceived -= Observe;
        AirshipVisualProbe.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
}
