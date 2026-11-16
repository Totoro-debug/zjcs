using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeRepairEffectValidation
{
    private const string Active = "Zjcs.HomeRepairEffectValidation.Active";
    static HomeRepairEffectValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("RepairEffectValidation").AddComponent<HomeRepairEffectRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Repair effect validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_REPAIR_EFFECT_PASSED: native particle bindings, game-time state transition, pause and release.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeRepairEffectRunner : MonoBehaviour
{
    private Camera camera;
    private RenderTexture target;
    private int errors;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += Log;
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeRepairEffectValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeRepairEffectValidation.Finish(errors == 0 ? null : new Exception("Unexpected runtime errors: " + errors));
    }
    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    private IEnumerator Check()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline, "Original project 2D render pipeline");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var cameraObject = new GameObject("Camera", typeof(Camera));
        camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .08f, .08f, 1);
        target = new RenderTexture(608, 1080, 24);
        camera.targetTexture = target;
        var assets = Resources.Load<HomeRepairEffectAssets>("Home/HomeRepairEffectAssets");
        Require(assets, "Original repair catalog");
        foreach (int type in new[] { 0, 1, 2, 3, 5, 7, 8, 9, 10, 12 })
        {
            var entry = assets.Find(type);
            float showTime = type == 7 ? .6f : .2f;
            float fullTime = type == 7 ? 1.5f : 1f;
            Require(entry != null && entry.Prefab && Mathf.Abs(entry.ShowTime - showTime) < .0001f
                && Mathf.Abs(entry.FullTime - fullTime) < .0001f,
                "Selected serialized original effect and timings: " + type);
            Vector3 rootOffset = type == 2 ? new Vector3(.018f, -.047f, 0) : Vector3.zero;
            Require(Vector3.Distance(entry.RootOffset, rootOffset) < .0001f,
                "Original furniture root offset: " + type);
            var anchor = new GameObject("OriginalFurnitureSlot");
            anchor.SetActive(false);
            var effect = Object.Instantiate(entry.Prefab, anchor.transform, false);
            effect.SetActive(false);
            anchor.SetActive(true);
            var renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Require(renderers.Length == 5, "Original particle renderer count: " + type);
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                    if (material) Require(material.shader && material.shader.isSupported, "Original Windows material shader");
            bool switched = false;
            Time.timeScale = 0;
            anchor.AddComponent<HomeRepairEffectPlayer>().Play(effect, entry.ShowTime, entry.FullTime, () => switched = true);
            Require(effect.activeSelf && !switched, "Effect starts before switching state");
            yield return new WaitForSecondsRealtime(.25f);
            Require(effect && effect.activeSelf && !switched, "Paused game time keeps original state");
            Time.timeScale = 1;
            yield return new WaitForSeconds(showTime * .6f);
            Require(effect && effect.activeSelf && !switched, "State remains unchanged before original delay");
            camera.Render();
            Save("repair-effect-" + type + ".png");
            yield return new WaitForSeconds(showTime * .6f);
            Require(effect && effect.activeSelf && switched, "State changes while effect remains active");
            yield return new WaitForSeconds(fullTime - showTime + .2f);
            Require(!anchor, "Effect is released after its original duration");
        }
        target.Release();
    }
    private void Save(string name)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        RenderTexture.active = previous;
        Object.Destroy(texture);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
