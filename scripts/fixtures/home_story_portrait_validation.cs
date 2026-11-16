using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryPortrait;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class HomeStoryPortraitValidation
{
    private const string Active = "Zjcs.HomeStoryPortraitValidation.Active";
    static HomeStoryPortraitValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeStoryPortraitValidation").AddComponent<HomeStoryPortraitRunner>();
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
        if (error == null) Debug.Log("HOME_STORY_PORTRAITS_PASSED: original prefabs, Sprite proxies, faces and region.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeStoryPortraitRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        var check = Check();
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { HomeStoryPortraitValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeStoryPortraitValidation.Finish(null);
    }
    private IEnumerator Check()
    {
        var camera = new GameObject("PortraitValidationCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var canvasObject = new GameObject("OriginalPortraitCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        var loader = HomeStoryPortraitAssets.LoadDefault();
        var expectedSprites = new Dictionary<string, string> {
__SPRITE_BINDINGS__
        };
        Require(loader.Sprites.Length == expectedSprites.Count, "All original Sprite proxy bindings");
        foreach (var binding in loader.Sprites)
        {
            Require(expectedSprites.TryGetValue(binding.Guid, out string expectedGuid)
                && binding.Sprite == AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(expectedGuid)),
                "Original Sprite proxy identity: " + binding.Guid);
            var handle = loader.LoadAssetAsyncByGuid<Sprite>(binding.Guid);
            bool complete = false;
            handle.Completed += value => { complete = value.Result == binding.Sprite; };
            Require(complete && handle.IsValid, "Original Sprite asynchronous load contract");
            handle.Dispose();
            Require(!handle.IsValid, "Sprite handle is released");
        }
        var scope = new GameObject("OriginalPortraitResourceScope", typeof(RectTransform), typeof(HomeHudResourceScope));
        scope.transform.SetParent(canvas.transform, false);
        scope.GetComponent<HomeHudResourceScope>().Loader = loader;
        foreach (var entry in new[] { new KeyValuePair<string, string>("8402", "__PREFAB_8402__"),
                                      new KeyValuePair<string, string>("8411", "__PREFAB_8411__") })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.Value));
            Require(prefab, "Original portrait prefab " + entry.Key);
            Require(prefab == (entry.Key == "8402" ? loader.Portrait8402 : loader.Portrait8411), "Packaged original portrait binding");
            var root = Object.Instantiate(prefab, scope.transform, false);
            Require(root.GetComponentsInChildren<Transform>(true).Length == 3
                && Array.TrueForAll(root.GetComponentsInChildren<Component>(true), value => value), "Original portrait components");
            var portrait = root.GetComponent<PortraitFace>();
            Require(portrait, "Original portrait face component");
            portrait.RegionProvider = () => RegionCodeEnum.CN;
            portrait.SetDirection(ETalkPortraitDirectionType.Left);
            portrait.LoadMain();
            var main = (Image)typeof(PortraitFace).GetField("mPortraitMainImg", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(portrait);
            var face = (Image)typeof(PortraitFace).GetField("mPortraitFaceImg", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(portrait);
            Require(main.enabled && main.sprite && main.sprite == loader.LoadAssetByGuid<Sprite>(portrait.mPortraitMainProxy.Guid).Result,
                "Original main portrait Sprite");
            var data = (PortraitFaceData[])typeof(PortraitFace).GetField("mFacesProxy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(portrait);
            string expression = entry.Key == "8402" ? "doubt" : "happy";
            var selected = Array.Find(data, value => value.Type == expression);
            Require(selected != null, "Official expression exists: " + expression);
            portrait.ChangeFace(expression);
            var expected = selected.GetFaceSpriteProxy(ETalkPortraitDirectionType.Left, RegionCodeEnum.CN);
            Require(face.enabled && face.sprite && face.sprite == loader.LoadAssetByGuid<Sprite>(expected.Guid).Result,
                "Original expression Sprite and local region");
            var previous = face.sprite;
            portrait.ChangeFace("__unknown_original_expression__");
            Require(face.sprite == previous, "Original unknown expression preserves the displayed face");
            portrait.ChangeFace("");
            Require(!face.enabled && !face.sprite, "Original empty expression hides the face");
            portrait.SetDirection(ETalkPortraitDirectionType.Right);
            portrait.ChangeFace(expression);
            var right = selected.GetFaceSpriteProxy(ETalkPortraitDirectionType.Right, RegionCodeEnum.CN);
            Require(face.enabled && face.sprite == loader.LoadAssetByGuid<Sprite>(right.Guid).Result,
                "Original right-facing expression fallback");
            portrait.ChangeFace("");
            portrait.SetDirection(ETalkPortraitDirectionType.Left);
            portrait.ChangeFace(expression);
            portrait.SetColor(new Color(.7f, .8f, .9f, 1));
            Require(main.color == face.color && main.color.r == .7f, "Original portrait tint applies to both layers");
            portrait.SetColor(Color.white);
            yield return null;
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Require(image && image.width == 608 && image.height == 1080, "Actual portrait Game View");
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "story-portrait-" + entry.Key + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            Object.Destroy(root);
            yield return null;
        }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
