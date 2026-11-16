using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Official.HomeStoryPortrait;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.HomeStory;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class HomeStoryCharacterValidation
{
    private const string Active = "Zjcs.HomeStoryCharacterValidation.Active";
    static HomeStoryCharacterValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeStoryCharacterValidation").AddComponent<HomeStoryCharacterRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureNativeFrame();
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_STORY_CHARACTERS_PASSED: original character group, animation events, portraits and release.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeStoryCharacterRunner : MonoBehaviour
{
    private sealed class Services : IStoryPortraitServices
    {
        private readonly HomeStoryCharacterServices runtime;
        public int Acquired, Released;
        public Services(HomeStoryPortraitAssets assets)
        {
            var settings = HomeStorySettings.LoadDefault();
            Require(settings.IsVoiceOver(100000) && !settings.IsVoiceOver(840200)
                && !settings.IsVoiceOver(841100) && settings.DialogVoiceStopEventKey == "Voice_StopAll",
                "Original narrator and voice stop settings");
            runtime = new HomeStoryCharacterServices(assets, () => RegionCodeEnum.CN, settings.IsVoiceOver);
        }
        public RegionCodeEnum Region() => runtime.Region();
        public bool IsVoiceOver(int roleId) => runtime.IsVoiceOver(roleId);
        public IHomeHudAssetHandle<GameObject> LoadPortrait(string path)
        {
            var value = runtime.LoadPortrait(path);
            Acquired++;
            return new Handle(value, () => Released++);
        }
    }
    private sealed class Handle : IHomeHudAssetHandle<GameObject>
    {
        private bool valid = true;
        private readonly Action release;
        private readonly IHomeHudAssetHandle<GameObject> handle;
        public GameObject Result => handle.Result;
        public bool IsValid => valid && handle.IsValid;
        public int Status => 2;
        public event Action<IHomeHudAssetHandle<GameObject>> Completed { add { value?.Invoke(this); } remove { } }
        public Handle(IHomeHudAssetHandle<GameObject> value, Action release) { handle = value; this.release = release; }
        public GameObject WaitForAsyncComplete() => Result;
        public void Dispose() { if (valid) { valid = false; handle.Dispose(); release(); } }
    }
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        var steps = Check();
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { HomeStoryCharacterValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeStoryCharacterValidation.Finish(null);
    }
    private IEnumerator Check()
    {
        var camera = new GameObject("CharacterValidationCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var canvasObject = new GameObject("OriginalCharacterCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HomeHudResourceScope));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        var assets = HomeStoryPortraitAssets.LoadDefault();
        canvasObject.GetComponent<HomeHudResourceScope>().Loader = assets;
        var service = new Services(assets);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__PREFAB_GUID__"));
        Require(prefab, "Original character group prefab");
        Require(HomeStoryCharacterAssets.LoadDefault().CharacterGroup == prefab, "Packaged original character group binding");
        var root = Object.Instantiate(prefab, canvas.transform, false);
        var left = root.transform.Find("LeftCharacter").GetComponent<TalkDialogCharacter>();
        var right = root.transform.Find("RightCharacter").GetComponent<TalkDialogCharacter>();
        Require(left && right, "Original left and right character controllers");
        left.Services = service; right.Services = service;
        var leftAnimation = left.GetComponent<Animation>();
        var rightAnimation = right.GetComponent<Animation>();
        Require(leftAnimation && rightAnimation, "Original character animations");
        const string first = "UI/Portrait/Npc_Portrait_8402", second = "UI/Portrait/Npc_Portrait_8411";
        left.ChangeStatus(ETalkPortraitStatusType.Enter, first, "doubt");
        Require(left.CurrentPortraitPath == first && left.GetComponentInChildren<PortraitFace>(), "Original first portrait");
        left.ChangeStatus(ETalkPortraitStatusType.Shutup);
        Require(leftAnimation.IsPlaying("TalkDialog_LeftCharacter_Dark"), "Original left dark animation starts");
        right.ChangeStatus(ETalkPortraitStatusType.Talk, second, "happy");
        Require(right.CurrentPortraitPath == second && rightAnimation.IsPlaying("TalkDialog_RightCharacter_Light"),
            "Original right portrait and light animation");
        yield return new WaitForSeconds(.2f);
        var face = left.GetComponentInChildren<PortraitFace>();
        var main = (Image)typeof(PortraitFace).GetField("mPortraitMainImg", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(face);
        var dark = (Color)typeof(TalkDialogCharacter).GetField("mCharacterDarkColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(left);
        Require(main.color == dark, "Original timed dark portrait color");
        yield return new WaitForEndOfFrame();
        SaveFrame("story-character-pair.png");
        left.ChangeStatus(ETalkPortraitStatusType.Talk, second, "happy");
        const string switchClip = "TalkDialog_CharacterAndTalkContent_LSwitch";
        Require(leftAnimation.IsPlaying(switchClip), "Original portrait switch animation starts");
        yield return new WaitForSeconds(leftAnimation[switchClip].length + .15f);
        Require(left.CurrentPortraitPath == second && left.GetComponentInChildren<PortraitFace>(),
            "Original animation event switches portrait at the midpoint");
        left.ChangeStatus(ETalkPortraitStatusType.Leave);
        Require(leftAnimation.IsPlaying("TalkDialog_CharacterAndTalkContent_L_Out"), "Original leave animation starts");
        Require(service.Acquired == 3 && service.Released == 0, "Original portrait loads remain owned until disable");
        root.SetActive(false);
        Require(service.Released == 3, "Original portrait handles are released on disable");
    }
    private static void SaveFrame(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        Require(image && image.width == 608 && image.height == 1080, "Actual portrait Game View");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
        Object.Destroy(image);
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
