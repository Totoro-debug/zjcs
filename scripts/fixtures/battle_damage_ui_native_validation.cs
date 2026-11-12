using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.BattleDamageUI;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Object = UnityEngine.Object;

public static class DamageUiNativeValidation
{
    const string Key = "Zjcs.DamageUiNative";
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Native damage UI fixture").AddComponent<DamageUiRunner>();
            if (state == PlayModeStateChange.EnteredEditMode)
            { SessionState.SetBool(Key, false); EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1)); }
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".exit", 1);
        EditorApplication.isPlaying = true;
    }
    internal static void Finish(string failure)
    {
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "damage-ui-result.json"),
            JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
        SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
    [Serializable] sealed class Result { public bool passed; public string error; }
}

public sealed class DamageUiRunner : MonoBehaviour, IStoryPlayableInstanceFactory
{
    [Serializable] sealed class Language { public Text[] texts; }
    [Serializable] sealed class Text { public string key, text; }
    sealed class Entity : ECViewEntityData
    {
        public override int ClassId => 1;
        public override EcEntityIdentifier Id => new EcEntityIdentifier(1, 1);
        public override T GetComponent<T>() => default;
    }
    string failure;
    public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        => Object.Instantiate(prefab, parent, worldPositionStays);
    IEnumerator Start()
    {
        AudioListener.volume = 0; Application.runInBackground = true;
        Application.logMessageReceived += Observe;
        StoryPlayableInstantiation.Resolve = _ => this;
        IEnumerator check = Check();
        while (true)
        {
            bool next = false; object current = null;
            try { next = check.MoveNext(); if (next) current = check.Current; }
            catch (Exception error) { failure = error.ToString(); }
            if (!next || failure != null) break;
            yield return current;
        }
        Time.timeScale = 1; StoryPlayableInstantiation.Resolve = null;
        Application.logMessageReceived -= Observe;
        DamageUiNativeValidation.Finish(failure);
    }
    void Observe(string text, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = text + "\n" + stack; }
    IEnumerator Check()
    {
        var texts = JsonUtility.FromJson<Language>(Resources.Load<TextAsset>("Combat/DamageUI/texts").text)
            .texts.ToDictionary(value => value.key, value => value.text);
        Zjcs.Official.WorldUI.WorldMultiLanguageText.Localize = key => texts.TryGetValue(key, out string text) ? text : "";
        var languageFont = Resources.Load<TMPro.TMP_FontAsset>("Combat/DamageUI/LanguageFont");
        Require(languageFont != null, "Original Chinese language Font binding.");
        Zjcs.Official.WorldUI.WorldMultiLanguageText.LanguageFont = () => languageFont;
        var prefabs = Resources.LoadAll<GameObject>("Combat/DamageUI").OrderBy(p => (int)p.GetComponent<CharacterDamageUI>().Type).ToArray();
        Require(prefabs.Length == 13, "All thirteen native prefabs must load.");
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 5; camera.transform.position = new Vector3(0, 0, -20);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f,.07f,.1f);
        var actor = new GameObject("Original entity").AddComponent<MyEntity>(); actor.Entity = new Entity();
        var root = new GameObject("Original damage pool").transform;
        var pools = new List<GameObjectPoolEmbed<CharacterDamageUI>>();
        var borrowed = new List<CharacterDamageUI>(); int completed = 0;
        DamageColor color = new DamageColor { TextColor = new Color(.9f,.4f,.2f), IconImageColor = Color.green,
            AddImageColor = Color.cyan, BgImageColor = Color.yellow };
        Time.timeScale = 1;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var pool = new GameObjectPoolEmbed<CharacterDamageUI>(root, prefabs[i]); pools.Add(pool);
            var ui = pool.Borrow(); borrowed.Add(ui);
            actor.transform.position = new Vector3((i % 4 - 1.5f) * 2.5f, 3f - i / 4 * 2f, 0);
            if (i == 10 || i == 11 || i == 12) ui.Init(actor, _ => completed++);
            else ui.Init(actor, -1234567, color, _ => completed++, false, false);
            if (i == 11) ui.UpdateText("EStatusCustomAction.CDModify");
            Require(ui.EntityId == actor.Id, "Original entity identity " + i);
            Require(ui.GetComponentsInChildren<Transform>(true).All(t => t != null), "Native hierarchy " + i);
            if (i <= 3 || i >= 5 && i <= 7)
                Require(ui.GetComponentsInChildren<TextMesh>(true).Any(t => t.text == "123万" && t.font != null && t.font.material != null), "Original number formatter and bitmap Font " + i);
        }
        yield return new WaitForSeconds(.15f);
        Time.timeScale = 0;
        camera.Render(); Capture(camera, "damage-ui-native.png");
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "damage-ui-labels.txt"),
            borrowed.SelectMany(ui => ui.GetComponentsInChildren<Renderer>(true).Select(renderer => Describe(ui, renderer))));
        Time.timeScale = 1;
        yield return new WaitForSeconds(.2f);
        Time.timeScale = 0;
        Capture(camera, "damage-ui-native-later.png");
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "damage-ui-texts.txt"),
            borrowed.SelectMany(ui => ui.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(text => DescribeText(ui, text))));
        var visibleTips = borrowed.SelectMany(ui => ui.GetComponentsInChildren<TMPro.TMP_Text>()).ToArray();
        Require(visibleTips.Length == 6 && visibleTips.All(text => text.textInfo != null && text.textInfo.characterCount > 0
            && text.textInfo.meshInfo.Sum(mesh => mesh.vertexCount) > 0 && text.fontSharedMaterial != null),
            "All six original Chinese tip effects generate real glyph meshes.");
        yield return new WaitForSecondsRealtime(2.5f);
        Require(completed == 0, "Original scaled lifetime pauses with the battle clock.");
        for (int i = 0; i < pools.Count; i++)
        {
            var ui = borrowed[i]; pools[i].Return(ui);
            Require(ui.EntityId == EcEntityIdentifier.sDefault && !ui.gameObject.activeSelf, "Original pool clear " + i);
            var reused = pools[i].Borrow(); Require(reused == ui, "Original FIFO pool reuse " + i);
            reused.Init(actor, _ => completed++);
        }
        Time.timeScale = 1;
        float deadline = Time.realtimeSinceStartup + 10;
        while (completed < 13 && Time.realtimeSinceStartup < deadline) yield return null;
        Require(completed == 13, "One completion per recycled UI; previous coroutine stopped.");
        for (int i = 0; i < pools.Count; i++) pools[i].Return(borrowed[i]);
        actor.transform.position = Vector3.zero;
        var feedback = pools[0].Borrow();
        feedback.Init(actor, 0, color, _ => { }, false, true, 1f, true);
        yield return new WaitForSeconds(.25f);
        Require(!feedback.GetComponentInChildren<DamageNumberUI>(true).gameObject.activeInHierarchy,
            "Original elemental immunity hides number and damage images.");
        Require(feedback.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Length > 0),
            "Original elemental immunity retains its caption.");
        Capture(camera, "damage-ui-element-immunity.png");
        pools[0].Return(feedback); feedback = pools[0].Borrow();
        feedback.Init(actor, -100, color, _ => { }, false, false, 0f, true);
        yield return new WaitForSeconds(.25f);
        Require(feedback.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == "破灭伤害"),
            "Original true damage caption.");
        Capture(camera, "damage-ui-true-damage.png");
        pools[0].Return(feedback); feedback = pools[0].Borrow();
        feedback.Init(actor, -100, color, _ => { }, true, true, 1f, true);
        yield return new WaitForSeconds(.25f);
        Require(feedback.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == "破韧"),
            "Original break-resilience branch has priority over elemental/true damage refresh.");
        Capture(camera, "damage-ui-break.png");
        foreach (var ui in borrowed) Object.Destroy(ui.gameObject);
        Debug.Log("Native damage UI passed: 13 complete prefabs, original bitmap numbers, scaled lifetime, clear and FIFO reuse.");
    }
    static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1200, 1000, 24); var pixels = new Texture2D(1200, 1000, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(target); Object.Destroy(pixels);
    }
    static string Describe(CharacterDamageUI ui, Renderer renderer)
    {
        var text = renderer.GetComponent<TextMesh>();
        return ui.Type + " " + renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled
            + " pos=" + renderer.transform.position + " material=" + (renderer.sharedMaterial == null ? "none" : renderer.sharedMaterial.name)
            + (text == null ? "" : " text=" + text.text + " color=" + text.color);
    }
    static string DescribeText(CharacterDamageUI ui, TMPro.TMP_Text text)
    {
        var info = text.textInfo;
        return ui.Type + " " + text.name + " active=" + text.isActiveAndEnabled + " text=" + text.text + " alpha=" + text.alpha
            + " glyphs=" + (info == null ? -1 : info.characterCount)
            + " verts=" + (info == null || info.meshInfo == null || info.meshInfo.Length == 0 ? -1 : info.meshInfo[0].vertexCount)
            + " font=" + (text.font == null ? "none" : text.font.name)
            + " source=" + (text.font == null || text.font.sourceFontFile == null ? "none" : text.font.sourceFontFile.name)
            + " material=" + (text.fontSharedMaterial == null ? "none" : text.fontSharedMaterial.name);
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
