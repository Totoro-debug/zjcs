using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Official.HomeLevelUpUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeLevelUpNativeValidation
{
    const string Key = "Zjcs.HomeLevelUp.Active";
    static HomeLevelUpNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("OriginalLevelUpFixture").AddComponent<HomeLevelUpNativeRunner>();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Exiting", false))
            {
                SessionState.SetBool(Key+".Exiting", false);
                EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key+".ExitCode", 1));
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Native level-up notification timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key+".Deadline", (float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error == null) Debug.Log("HOME_LEVEL_UP_NATIVE_PASSED: complete original notification, native animation/sound, changed and unchanged property branches, init/reinit and resource release. Formula/player endpoints are controlled fixture data.");
        else Debug.LogException(error);
        SessionState.SetInt(Key+".ExitCode", error == null ? 0 : 1);
        SessionState.SetBool(Key+".Exiting", true);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        else EditorApplication.delayCall += () => EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeLevelUpNativeRunner : MonoBehaviour, IHomeLevelUpServices
{
    GameObject prefab, active; Canvas canvas; Camera cameraUi; RenderTexture target;
    TMP_FontAsset font; OfflineProfessionPropertyBindings properties;
    TrackedImages images; OfflinePartnerLocalizationService localizer;
    readonly List<int> requestedLevels = new List<int>();
    int errors, popupCount, closeCount, sounds; bool unchanged;
    public int CurrentLevel => 2;
    public IProfessionImageResourceLoader Resources => images;
    public Dictionary<ProfessionPropertyType, long> CalcCharacterBattleProps(int level)
    {
        requestedLevels.Add(level);
        return new Dictionary<ProfessionPropertyType, long> {
            [(ProfessionPropertyType)51] = level*10, [(ProfessionPropertyType)52] = unchanged ? 20 : level*20,
            [(ProfessionPropertyType)50] = level*100, [(ProfessionPropertyType)53] = 30 };
    }
    public long GetRawProp(Dictionary<ProfessionPropertyType, long> values, ProfessionPropertyType type) => values.TryGetValue(type, out long value) ? value : 0;
    public ProfessionPropertyDefinition GetProperty(ProfessionPropertyType type) => properties.GetProperty(type);
    public void Close(GameObject window)
    { Require(window == active, "original closes its own popup only"); closeCount++; active.SetActive(false); Object.Destroy(active); active = null; }
    public void Popup(Action<LevelUpNotifyTip> initialized)
    {
        popupCount++;
        var staging = new GameObject("LevelUpInactiveBinding"); staging.SetActive(false);
        active = Object.Instantiate(prefab, staging.transform, false); active.SetActive(false);
        foreach (var text in active.GetComponentsInChildren<WorldMultiLanguageText>(true))
            text.Localizer = key => localizer.GetText(key, localizer.CurrentLanguage);
        foreach (var row in active.GetComponentsInChildren<ProfessionPlayerPropItem>(true)) row.Configure(properties);
        foreach (var sound in active.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = (clip, owner) => {
            Require(clip && owner, "original level-up audio bound"); sounds++;
            var source = owner.AddComponent<AudioSource>(); source.clip = clip; source.volume = 0; source.Play();
            Require(source.isPlaying, "muted original sound actually plays");
        };
        var notification = active.GetComponent<LevelUpNotifyTip>(); notification.Services = this;
        active.transform.SetParent(canvas.transform, false); active.SetActive(true);
        var popupCanvas = active.GetComponent<Canvas>(); popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        popupCanvas.worldCamera = cameraUi; popupCanvas.planeDistance = 1;
        initialized(notification); active.GetComponent<WorldPopupWindow>().Show(); Object.Destroy(staging);
    }
    IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var check = Check();
        for (;;) {
            object current = null; bool next;
            try { next = check.MoveNext(); if (next) current = check.Current; }
            catch (Exception error) { HomeLevelUpNativeValidation.Finish(error); yield break; }
            if (!next) break; yield return current;
        }
        HomeLevelUpNativeValidation.Finish(errors == 0 ? null : new Exception("Unexpected notification errors: "+errors));
    }
    IEnumerator Check()
    {
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/UI/LevelUp/LevelUpNotifyTip.prefab");
        Require(prefab && prefab.GetComponentsInChildren<Transform>(true).Length > 50, "complete original notification hierarchy");
        localizer = new OfflinePartnerLocalizationService(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Home/UI/LevelUp/home-level-up-texts.json"));
        var catalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        var entries = new[] { "P_Attack", "P_Defence", "P_MaxHp", "P_Speed" }.Select(name => {
            string path = AssetDatabase.FindAssets(name+" t:Sprite").Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == name);
            return new OfflineProfessionImageCatalog.Entry { Path = "Assets/Image/ItemIcon/Props/"+name,
                AssetGuid = AssetDatabase.AssetPathToGUID(path), Asset = AssetDatabase.LoadAssetAtPath<Sprite>(path) };
        }).ToList();
        font = prefab.GetComponentsInChildren<TMP_Text>(true).First().font;
        var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/ProfessionUI/Fonts/FontAssetFontConfig.json");
        var config = JsonConvert.DeserializeObject<FontAssetFontConfig>(configAsset.text);
        var sourceFont = UnityEngine.Resources.Load<Font>("HomeValidationFont");
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = FontAssetFontService.kFontAssetFontConfigPath, AssetGuid = "fixture-font-config", Asset = configAsset });
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = config.DefaultFontAsset, AssetGuid = "fixture-original-font", Asset = font });
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = config.FontAssetInfoDict[font.name].FontPath, AssetGuid = "fixture-source-font", Asset = sourceFont });
        catalog.Entries = entries.ToArray();
        images = new TrackedImages(new OfflineProfessionImageResourceLoader(catalog));
        var fonts = new FontAssetFontService(images, json => JsonConvert.DeserializeObject<FontAssetFontConfig>(json));
        fonts.Initialize(); Require(font.sourceFontFile == sourceFont, "original font service binds source without replacing native font/material");
        properties = new OfflineProfessionPropertyBindings(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Partners/partner-property-ui.json"), images, localizer,
            (_, __, ___, ____, _____) => { throw new Exception("No detail input expected in this automatic notification"); },
            (_, __, ___, ____, _____, ______) => { throw new Exception("No blessing input expected"); });
        var previousProperties = ProfessionPropertyBindings.Current; ProfessionPropertyBindings.Current = properties;
        var audioCatalog = ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
        audioCatalog.Events = Array.Empty<OfflineProfessionAudioCatalog.Entry>();
        var audioHost = gameObject.AddComponent<OfflineProfessionSoundHost>(); audioHost.Bind(audioCatalog);
        var originalSound = AssetDatabase.FindAssets("GrowUp_LevelUp t:AudioClip").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Single();
        using var soundScope = new OfflinePartnerUiSoundScope(id => id == 2648322711 ? originalSound : null);
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline, "original project's native render pipeline");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("LevelUpCamera", typeof(Camera)).GetComponent<Camera>(); cameraUi.transform.position = new Vector3(0, 0, -10);
        cameraUi.gameObject.AddComponent<AudioListener>();
        cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(.12f, .2f, .28f);
        target = new RenderTexture(608, 1080, 24); cameraUi.targetTexture = target;
        canvas = new GameObject("LevelUpCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Require(!LevelUpNotifyTip.TryPopup(this, 2) && popupCount == 0, "equal level skips popup");
        Require(LevelUpNotifyTip.TryPopup(this, 1), "changed level requests original popup");
        yield return null;
        Require(requestedLevels.SequenceEqual(new[] {1, 2}), "original calculation order from level then to level");
        var rows = active.GetComponentsInChildren<ProfessionPlayerPropItem>(true);
        Require(rows.Length == 4 && rows.Count(row => row.gameObject.activeSelf) == 3, "equal speed hides its original row");
        Require(rows.Where(row => row.gameObject.activeSelf).All(row => row.PropValueFromText.Text.Length > 0 && row.PropValueToText.Text.Length > 0), "actual original from/to labels");
        Require(active.GetComponentsInChildren<TMP_Text>(true).All(text => !text.text.Contains("ui_") && !text.text.Contains("PropType.")), "all serialized and dynamic original Chinese text resolved");
        var popup = active.GetComponent<WorldPopupWindow>();
        Require(popup.mShowClip && Math.Abs(popup.mShowClip.length-2.2f) < .001f, "original 2.2-second animation retained");
        yield return new WaitForSeconds(.75f); Capture("home-level-up-native.png");
        Require(active != null && popup.State != WorldPopupWindow.EState.Show, "window remains through original animation");
        yield return new WaitForSeconds(1.7f); Require(active == null && closeCount == 1, "original OnShow closes its own notification");
        unchanged = true; requestedLevels.Clear(); Require(LevelUpNotifyTip.TryPopup(this, 1), "reopen fresh owner");
        yield return null;
        rows = active.GetComponentsInChildren<ProfessionPlayerPropItem>(true);
        Require(rows.Count(row => row.gameObject.activeSelf) == 2, "second equal property hides independently");
        active.GetComponent<LevelUpNotifyTip>().Init(1, 2);
        Require(requestedLevels.SequenceEqual(new[] {1, 2, 1, 2}), "reinit releases old resources and evaluates again");
        yield return new WaitForSeconds(.6f); Capture("home-level-up-unchanged.png");
        Close(active); yield return null;
        fonts.Dispose();
        Require(images.ActiveOperationCount == 0 && sounds == 2, "all original image handles released and both sound events played");
        Require(audioHost.RegisteredObjectCount == 0, "all original sound emitters unregistered");
        ProfessionPropertyBindings.Current = previousProperties;
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG()); Object.Destroy(texture);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class TrackedImages : IProfessionImageResourceLoader
    {
        readonly IProfessionImageResourceLoader inner; readonly List<Func<bool>> live = new List<Func<bool>>();
        public TrackedImages(IProfessionImageResourceLoader inner) { this.inner = inner; }
        public int ActiveOperationCount => live.Count(valid => valid());
        IProfessionAssetOperation<T> Track<T>(IProfessionAssetOperation<T> operation) where T : Object
        { Require(operation.Result, "real original image handle"); live.Add(() => operation.IsValid); return operation; }
        public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object => Track(inner.LoadAsset<T>(path));
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => Track(inner.LoadAssetAsync<T>(path));
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object => Track(inner.LoadAssetByGuid<T>(guid));
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => Track(inner.LoadAssetAsyncByGuid<T>(guid));
    }
}
