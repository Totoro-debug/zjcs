using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public static class CurrentRoundUiValidation
{
    const string Key = "Zjcs.CurrentRoundUiValidation";
    [InitializeOnLoadMethod] static void Hook()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Current round UI validation").AddComponent<CurrentRoundUiRunner>();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (SessionState.GetInt(Key + ".exit", 1) == 0 || Environment.GetEnvironmentVariable("ZJCS_ROUND_KEEP_EDITOR") != "1")
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
            }
        };
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        if (string.IsNullOrEmpty(work)) return;
        string request = Path.Combine(work, "round-ui-run.request");
        if (File.Exists(request))
        { File.Delete(request); SessionState.SetBool(Key + ".requested", true); AssetDatabase.Refresh(); return; }
        if (!SessionState.GetBool(Key + ".requested", false)) return;
        SessionState.SetBool(Key + ".requested", false); Run();
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".exit", 1);
        EditorApplication.isPlaying = true;
    }
    internal static void Finish(string failure)
    {
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "round-ui-result.json"),
            JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
        SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1); EditorApplication.isPlaying = false;
    }
    [Serializable] sealed class Result { public bool passed; public string error; }
}

public sealed class CurrentRoundUiRunner : MonoBehaviour
{
    [Serializable] sealed class StoredData
    {
        public int entityId; public double preTime, nextTime, interval;
        public BattleRoundData Restore() => new BattleRoundData { EntityId = entityId, PreTime = preTime, NextTime = nextTime, Interval = interval };
    }
    [Serializable] sealed class Checkpoint { public StoredData[] data; public int acting; }
    sealed class Bindings : IBattleRoundUIBindings, IBattleRoundCardBindings, IDisposable
    {
        public BattleRoundPortraitAssets Assets;
        public GameRoundScene Scene { get; set; }
        public Camera SceneCamera { get; set; }
        public GamePlayManagerConfig Configuration => Assets.Configuration;
        public BattleRoundDriver Driver { get; } = new BattleRoundDriver();
        public bool IsPlaying { get; set; }
        public readonly Dictionary<int, BattleRoundRole> Roles = new Dictionary<int, BattleRoundRole>();
        public readonly Dictionary<string, Sprite> Portraits = new Dictionary<string, Sprite>();
        readonly FightDynamicTextureETC2 atlas = new FightDynamicTextureETC2();
        public BattleRoundPlayer MainPlayer { get; } = new BattleRoundPlayer { Id = -1 };
        public Color MimicsMonsterColor => Color.white;
        public event Action<BattleRoundDamage> Damage;
        public event Action<int, int, int, int> Die;
        public event Action<int> EntityAdded { add { } remove { } }
        public event Action SameGameUpdated { add { } remove { } }
        public void Init() => atlas.Initialize(Configuration, Assets.LoadTexture);
        public void Dispose() => atlas.Dispose();
        public bool HasEntity(int id) => Roles.ContainsKey(id);
        public bool ShowOnRoundProgress(int id) => HasEntity(id);
        public BattleRoundRole GetRole(int id) => Roles[id];
        public GameEffectData GetFaction(BattleRoundRole role)
            => role == Roles[1] ? Configuration.DefaultEffect : Configuration.FactionEffectDatas[role == Roles[2] ? 0 : 1];
        public bool TryGetNpcIcon(int id, out string path) { path = id.ToString(); return Portraits.ContainsKey(path); }
        public bool TryGetMonsterIcon(int id, out BattleRoundMonsterIcon icon) { icon = null; return false; }
        public Sprite GetSprite(string path) => Portraits.TryGetValue(path, out var sprite) ? sprite : Assets.LoadSprite(path);
        public IBattleRoundPlayerRequest CreatePlayerRequest(Action<BattleRoundPlayer> callback)
            => throw new InvalidOperationException("NPC round validation requested online player data.");
        public GameObject InstantiateCard(GameObject prefab, Transform parent)
        {
            var inactive = new GameObject("Inactive native card binding"); inactive.SetActive(false);
            var card = Object.Instantiate(prefab, inactive.transform, false);
            BindPortraits(card, Configuration);
            card.transform.SetParent(parent, false); card.SetActive(true); Object.Destroy(inactive);
            return card;
        }
        public void BindPortraits(GameObject card, GamePlayManagerConfig configuration)
        {
            foreach (var portrait in card.GetComponentsInChildren<GameRoundPlayerPortrait>(true)) portrait.BindConfiguration(configuration);
            foreach (var head in card.GetComponentsInChildren<HeadCharacterPortraitETC2Scene>(true)) head.BindAtlas(atlas);
            foreach (var head in card.GetComponentsInChildren<HeadCharacterPortraitETC2>(true)) head.BindAtlas(atlas);
            foreach (var handler in card.GetComponentsInChildren<GameRoundPortraitETC2SceneHandler>(true)) handler.BindAtlas(atlas);
            foreach (var handler in card.GetComponentsInChildren<GameRoundPortraitETC2Handler>(true)) handler.BindAtlas(atlas);
        }
        public void PrepareCard(IGameRoundCharacterItem card, int id)
        {
            if (card is GameRoundCharacterItemScene scene) scene.Bind(this);
            else ((GameRoundCharacterItem)card).Bind(this);
        }
        public void Hurt(int id) => Damage?.Invoke(new BattleRoundDamage { TargetId = id, Damage = -1, HasHitEntity = true, DamageType = 1 });
        public void Kill(int id) => Die?.Invoke(0, 0, id, 0);
    }
    string failure;
    IEnumerator Start()
    {
        AudioListener.volume = 0; Application.runInBackground = true; Application.logMessageReceived += Observe;
        var steps = Check();
        while (true)
        {
            bool next = false; object current = null;
            try { next = steps.MoveNext(); if (next) current = steps.Current; }
            catch (Exception error) { failure = error.ToString(); }
            if (!next || failure != null) break;
            yield return current;
        }
        Time.timeScale = 1; Application.logMessageReceived -= Observe; CurrentRoundUiValidation.Finish(failure);
    }
    void Observe(string text, string trace, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert || text.Contains("referenced script") && text.Contains("missing")) failure = text + "\n" + trace; }
    static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    static GameRoundCharacterItemScene Card(GameRoundScene scene, int id)
        => scene.GetComponentsInChildren<GameRoundCharacterItemScene>(true)
            .Single(card => card.gameObject.activeInHierarchy && card.RoundRuntimeData is BattleRoundData data && data.EntityId == id);
    static void Capture(Camera camera, string name)
    {
        int width = Screen.width, height = Screen.height;
        var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(target); Object.Destroy(image);
    }
    IEnumerator Check()
    {
        var assets = Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");
        Require(assets != null && assets.Textures.Length == 3 && assets.Sprites.Length == 3, "Current portrait configuration/faction closure.");
        var camera = new GameObject("Round camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -30);
        camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 6; camera.backgroundColor = new Color(.06f, .09f, .12f); camera.clearFlags = CameraClearFlags.SolidColor;
        var canvas = new GameObject("Overlay host", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var scene = Object.Instantiate(Resources.Load<GameObject>("Combat/RoundUI/GameRoundScene")).GetComponent<GameRoundScene>();
        Require(scene.GetComponentsInChildren<Transform>(true).Length == 4 && scene.IconRoot.name == "Slot" && scene.Line.name == "Linear", "Complete native scene hierarchy.");
        var bindings = new Bindings { Assets = assets, Scene = scene, SceneCamera = camera };
        bindings.Init();
        foreach (int id in new[] { 8411, 11001, 11002 })
        {
            var portrait = Resources.Load<Sprite>("RoundTest/Npc_" + id); Require(portrait != null, "Original NPC portrait " + id);
            bindings.Portraits.Add(id.ToString(), portrait);
        }
        for (int i = 1; i <= 3; i++)
        {
            bindings.Roles.Add(i, new BattleRoundRole { NpcClassId = new[] { 8411, 11001, 11002 }[i - 1] });
            bindings.Driver.DataList.Add(new BattleRoundData { EntityId = i, Interval = 10000, NextTime = (i - 1) * 2500 });
        }
        bindings.Driver.CurActingEntityId = 1;
        var inactive = new GameObject("Inactive round binding"); inactive.SetActive(false);
        var ui = Object.Instantiate(Resources.Load<GameObject>("Combat/RoundUI/RoundUI"), inactive.transform, false).GetComponent<GameRoundUI>();
        ui.Initialize(bindings); ui.transform.SetParent(canvas.transform, false); Object.Destroy(inactive);
        yield return new WaitForSeconds(.6f);
        Require(scene.GetComponentsInChildren<GameRoundCharacterItemScene>(true).Length == 16, "Original sixteen-card pool.");
        Require(Card(scene, 1).IsCurrent && Card(scene, 1).transform.localPosition.x > Card(scene, 2).transform.localPosition.x
            && Card(scene, 2).transform.localPosition.x > Card(scene, 3).transform.localPosition.x, "Original initial actor/time order.");
        foreach (int id in new[] { 1, 2, 3 })
        {
            var card = Card(scene, id);
            Require(card.NpcIconGO.activeInHierarchy && card.NpcIconSr.Sprite != null, "Native NPC card image " + id);
            Require(card.GetComponentsInChildren<MonoBehaviour>(true).Length == 7 && card.GetComponent<Animator>().runtimeAnimatorController != null, "Complete scene card component/animation closure.");
        }
        Capture(camera, "round-current-initial");
        var checkpoint = JsonUtility.ToJson(new Checkpoint { data = bindings.Driver.DataList.Select(data => new StoredData
            { entityId = data.EntityId, preTime = data.PreTime, nextTime = data.NextTime, interval = data.Interval }).ToArray(), acting = 1 });
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "round-ui-checkpoint.json"), checkpoint);
        Require(!bindings.IsPlaying, "Preparation-stage test setup.");
        var changed = bindings.Driver.FindData(2); changed.NextTime = 7500;
        bindings.Driver.NotifyUpdate(changed, BattleRoundUpdateState.Delay); yield return new WaitForSeconds(.3f);
        Require(Card(scene, 3).transform.localPosition.x > Card(scene, 2).transform.localPosition.x, "Current official round updates must run during preparation.");
        void CheckProjection(float referenceHeight)
        {
            Canvas.ForceUpdateCanvases();
            var expected = RectTransformUtility.WorldToScreenPoint(null, ui.IconBack.position);
            var actual = camera.WorldToScreenPoint(scene.transform.position);
            Require(Vector2.Distance(expected, actual) < .1f && Mathf.Abs(scene.transform.position.z + 16) < .00001f, "Current overlay/camera projection.");
            float scale = camera.orthographicSize / 5 * referenceHeight / ((RectTransform)canvas.transform).rect.height;
            Require(Mathf.Abs(scene.transform.localScale.x - scale) < .00001f, "Current official scale/cache formula.");
        }
        CheckProjection(1920); camera.orthographicSize = 8; yield return null; yield return null; CheckProjection(1920);
        scaler.referenceResolution = new Vector2(1080, 2160); yield return null; yield return null; CheckProjection(1920);
        scene.SetVisible(false); yield return null; Require(scene.transform.position == new Vector3(0, 1000, 0), "Native scene hide.");
        scene.SetVisible(true); yield return null; yield return null; CheckProjection(1920);
        bindings.IsPlaying = true; bindings.Hurt(2); yield return new WaitForSeconds(.04f);
        Require(Card(scene, 2).GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("UIGameRound_Hurt"), "Original hurt notification.");
        bindings.Driver.DataList.Remove(bindings.Driver.FindData(3)); bindings.Kill(3); yield return new WaitForSeconds(.8f);
        Require(!scene.GetComponentsInChildren<GameRoundCharacterItemScene>().Any(card => card.RoundRuntimeData is BattleRoundData data && data.EntityId == 3), "Death callback/pool return.");
        ui.gameObject.SetActive(false); var saved = JsonUtility.FromJson<Checkpoint>(checkpoint);
        bindings.Driver.DataList.Clear(); bindings.Driver.DataList.AddRange(saved.data.Select(data => data.Restore())); bindings.Driver.CurActingEntityId = saved.acting;
        ui.gameObject.SetActive(true); yield return new WaitForSeconds(.4f);
        CheckProjection(2160);
        Require(Card(scene, 1).IsCurrent && Card(scene, 2).transform.localPosition.x > Card(scene, 3).transform.localPosition.x, "Checkpoint restore order.");
        Capture(camera, "round-current-restored");
        scene.SetVisible(false); var config = Object.Instantiate(assets.Configuration); config.EnablePlayerPortraitETC2_Scene = false;
        // The complete original UGUI card is the alternate official branch.
        var cardPrefab = ui.IconPrefab; var holder = new GameObject("Inactive UGUI card binding"); holder.SetActive(false);
        var uiCard = Object.Instantiate(cardPrefab, holder.transform, false).GetComponent<GameRoundCharacterItem>();
        bindings.BindPortraits(uiCard.gameObject, config);
        uiCard.Bind(bindings); uiCard.transform.SetParent(canvas.transform, false); Object.Destroy(holder);
        uiCard.Init(saved.data[0].Restore(), true, bindings.Roles[1]); yield return new WaitForSeconds(.3f);
        Require(uiCard.NpcIconImg.gameObject.activeInHierarchy && uiCard.NpcIconImg.sprite != null && uiCard.Animator.runtimeAnimatorController != null,
            "Complete original UGUI card fields and NPC branch.");
        Require(uiCard.GetComponentsInChildren<UIMesh>(true).Single().Mesh.vertexCount == 4, "Original native UGUI mesh.");
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera; canvas.GetComponent<Canvas>().worldCamera = camera;
        canvas.GetComponent<Canvas>().planeDistance = 10; yield return null; yield return null; Capture(camera, "round-current-ugui");
        Object.Destroy(uiCard.gameObject); Object.Destroy(config); Object.Destroy(canvas); Object.Destroy(scene.gameObject);
        yield return null; yield return null; bindings.Dispose(); Object.Destroy(camera.gameObject);
        Require(scene == null && ui == null, "Round scene/UI lifetime.");
        Debug.Log("Current round UI passed: native resources, NPC/faction images, scene and UGUI cards, preparation updates, scale/cache, hide/show, hurt/death, checkpoint restoration and lifetime.");
    }
}
