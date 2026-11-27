using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Editor;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TreasureStarsNativeValidation
{
    private const string Active = "Zjcs.TreasureStarsNative.Active";
    private static int checks, errors;
    private static Camera camera;
    private static RenderTexture target;
    private static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_STARS_OUTPUT");
    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var steps = Steps();
            while (true)
            {
                bool next;
                try { next = steps.MoveNext(); }
                catch (Exception error) { Finish(error); yield break; }
                if (!next) break;
                yield return steps.Current;
            }
            Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
        }
    }
    static TreasureStarsNativeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            Application.runInBackground = true;
            AudioListener.volume = 0;
            Application.logMessageReceived += Log;
            new GameObject("TreasureStarsNativeValidation").AddComponent<Runner>();
        };
        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Treasure star native runtime verification timed out."));
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_TREASURE_STARS_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets")
            || string.IsNullOrEmpty(Work)) throw new InvalidOperationException("Select the independent source validation project explicitly.");
        ValidationGameView.Portrait();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active+".Deadline", (float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    private static void Require(bool value, string detail)
    { checks++; if (!value) throw new InvalidOperationException(detail); }
    private static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    private static IEnumerator Steps()
    {
        checks = errors = 0;
        camera = new GameObject("Native UI render camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 731.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1);
        target = new RenderTexture(675, 1463, 24); target.Create(); camera.targetTexture = target;
        var canvas = new GameObject("Native UI validation host", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2340);
        // A zero-size centered slot hosts the unchanged standalone prefab, whose native anchors are bottom-left.
        var host = new GameObject("Original prefab host slot", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(canvas.transform, false); host.anchorMin = host.anchorMax = new Vector2(0.5f, 0.5f); host.sizeDelta = Vector2.zero;
        var prefab = Resources.Load<GameObject>("Treasure/NativeStars/TreasureStarInfo");
        Require(prefab != null, "Original star prefab imports and loads.");
        var go = Object.Instantiate(prefab, host, false);
        var display = go.GetComponent<TreasureStarInfo>();
        Require(display != null && display.StarMiniInfo != null && display.StarStateSwitcher != null, "Original controller and serialized references are bound.");
        foreach (var node in go.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing original script: "+node.name);
        var slot = display.StarMiniInfo.StarListSlot;
        Require(slot != null && slot.childCount > 0, "Original star slots are bound.");
        int count = slot.childCount;
        // Initialize every original star Animator before querying its live parameters.
        display.Show(Quality.Gold, count, count, showBlankStar:true);
        yield return null;
        var animators = slot.GetComponentsInChildren<Animator>(true);
        Require(animators.Length == count && animators.All(a => a.runtimeAnimatorController != null), "Every original star has its native Animator graph.");
        foreach (var animator in animators)
            Require(animator.parameters.Any(p => p.name == "TwinkleState" && p.type == AnimatorControllerParameterType.Bool), "Original twinkle bool parameter exists.");
        var twinkle = animators[0].runtimeAnimatorController.animationClips.Single(c=>c.name=="Star_Twinkle_On");
        Require(AnimationUtility.GetCurveBindings(twinkle).Any(b=>b.path==string.Empty && b.type==typeof(Image) && b.propertyName=="m_Color.a"), "Imported original twinkle clip binds the root Image alpha.");
        Debug.Log("TREASURE_STARS_NATIVE_STAGE: bound original hierarchy and alpha curve");
        foreach (Quality quality in Enum.GetValues(typeof(Quality)))
        {
            for (int star=0; star<=count; star++)
            foreach (int max in new[] { 0, count/2, count }.Distinct())
            foreach (bool blank in new[] { false, true })
            {
                display.Show(quality, star, max, showBlankStar:blank);
                Require(go.activeSelf && display.StarStateSwitcher.State == false, "Normal state activates original star view.");
                var sprite = display.StarMiniInfo.GetQulitySprite(quality);
                Require(sprite != null, "Original quality sprite resolves: "+quality);
                for (int i=0; i<count; i++)
                {
                    var child = slot.GetChild(i);
                    var image = child.GetChild(0).GetComponent<Image>();
                    Require(child.gameObject.activeSelf == (i<star || (blank && i<max)), "Original filled/blank parent visibility.");
                    Require(image != null && image.gameObject.activeSelf == (i<star) && image.sprite == sprite, "Original per-quality filled-star reference and visibility.");
                }
            }
        }
        Require(display.StarMiniInfo.GetQulitySprite((Quality)100) == display.StarMiniInfo.GetQulitySprite(Quality.None), "Unlisted quality uses original None fallback.");
        Debug.Log("TREASURE_STARS_NATIVE_STAGE: quality/star/blank boundaries passed");
        display.Show(Quality.Gold, Math.Min(3,count), count, showBlankStar:true);
        yield return null;
        Capture("normal");
        display.Show(Quality.Red, count, count, isSuper:true);
        Require(display.StarStateSwitcher.State && go.activeSelf, "Awakened view follows original bool state selector.");
        yield return null;
        Capture("awakened");
        Debug.Log("TREASURE_STARS_NATIVE_STAGE: normal and awakened rendered");
        display.Show(Quality.Gold, Math.Min(3,count), count, showBlankStar:true, blink:true);
        yield return null;
        for (int i=0; i<count; i++)
            Require(animators[i].GetBool("TwinkleState") == (i==Math.Min(3,count)-1), "Only the promoted star blinks.");
        yield return new WaitForSeconds(0.1f);
        float firstAlpha = animators[Math.Min(3,count)-1].GetComponent<Image>().color.a;
        var first = Capture("twinkle-start");
        yield return new WaitForSeconds(0.37f);
        float secondAlpha = animators[Math.Min(3,count)-1].GetComponent<Image>().color.a;
        var second = Capture("twinkle-later");
        Require(Mathf.Abs(firstAlpha-secondAlpha)>0.01f, "The native Animator changes Image alpha: "+firstAlpha+" -> "+secondAlpha);
        Require(!first.SequenceEqual(second), "The actual native animation changes rendered pixels over time.");
        display.StopStarBlink();
        foreach (var animator in animators.Where(a=>a.isActiveAndEnabled)) Require(!animator.GetBool("TwinkleState"), "Explicit stop clears active animator flags.");
        display.Show(Quality.Gold, count, count, blink:true);
        display.Hide();
        Require(!go.activeSelf && !display.StarMiniInfo.gameObject.activeSelf, "Hide disables both original controller roots.");
        display.Show(Quality.Blue, 1, count);
        yield return null;
        Require(go.activeSelf && display.StarMiniInfo.gameObject.activeSelf, "Hidden control restores without replacing its native hierarchy.");
        var clone = Object.Instantiate(prefab, host, false).GetComponent<TreasureStarInfo>();
        clone.Show(Quality.Purple, count, count);
        Require(clone.StarMiniInfo.StarListSlot != slot, "A second native instance retains its own serialized star references.");
        Require(slot.GetChild(1).GetChild(0).gameObject.activeSelf == false, "Clone initialization does not mutate the first instance.");
        Object.Destroy(clone.gameObject); Object.Destroy(go);
        yield return null;
    }
    private static byte[] Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply(); RenderTexture.active = previous;
        var bytes = texture.GetRawTextureData();
        Require(bytes.Where((b,i)=>i%4<3).Distinct().Count()>8, "Actual native star artwork renders into the owned target.");
        File.WriteAllBytes(Path.Combine(Work,name+".png"),texture.EncodeToPNG()); Object.Destroy(texture);
        return bytes;
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false); Application.logMessageReceived -= Log;
        if (error == null)
        {
            File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");
            Debug.Log("TREASURE_STARS_NATIVE_PASSED: "+checks);
        }
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
