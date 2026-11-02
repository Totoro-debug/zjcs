using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Current = Zjcs.Official.CombatAppearance;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerLocalPlayerWidgetValidation
{
    const string Key = "PartnerLocalPlayerWidgetValidation.Active";
    static PartnerLocalPlayerWidgetValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original local player widget validation").AddComponent<PartnerLocalPlayerWidgetRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Actual local player widget validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json"))
            || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSettings.asyncShaderCompilation = false; ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "local-player-widget-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerLocalPlayerWidgetRunner : MonoBehaviour
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    IEnumerator Start()
    {
        var steps = Check();
        while (true)
        {
            object value;
            try { if (!steps.MoveNext()) break; value = steps.Current; }
            catch (Exception error) { PartnerLocalPlayerWidgetValidation.Finish(error); yield break; }
            yield return value;
        }
        PartnerLocalPlayerWidgetValidation.Finish(null);
    }
    IEnumerator Check()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var resources = Resources.Load<ProfessionNativeSceneAppearanceResources>("Partners/appearance-resources");
        var config = ProfessionNativePortraitStudioResources.LoadDefault().AppearanceConfig;
        var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        var defaults = new ProfessionAppearanceSkinDefaults(tables,
            Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, message => { throw new Exception(message); });
        var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "local-widget-profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "名字不是翻译键";
            player.InitialWeaponType = 200; player.SubRank = 1;
            SetAppearance(player, config, tables, Sex.Male, 1);
        }).IsApplied, "Create actual isolated player");
        session.EnsureLocalActorIdentities();
        long id = session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        var character = new OfflineCharacterProgressionService(session, catalog);
        var appearance = new OfflineProfessionAppearanceBindings(id, () => character.Player, item => catalog.ItemType(item), tables, config, rules, defaults);
        var now = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
        var source = new OfflinePartnerPlayerInfoSource(session, character, appearance, () => new DateTimeOffset(now).ToUnixTimeSeconds(),
            _ => null, _ => throw Outside(), (_, __) => throw Outside());
        var health = new OfflinePlayerHealth(session, character, () => 0, () => Array.Empty<bool>(), () => now);
        var hp = new OfflinePartnerPlayerHpBindings(health, source.OnPlayerUpdate, () => source.MainPlayer != null, () => true);
        var ports = new Ports(player => source.BuildData(player));
        var info = new OfflinePartnerPlayerInfoBindings(source, ports);
        var lowMemory = new GAction();
        using var manager = new AppearanceAssetManager(Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"), lowMemory.Subscribe);
        using var textures = new OfflineProfessionSceneTextureResources(resources);
        var oldTextures = ProfessionAppearanceTextureBindings.Current;
        var oldClip = ProfessionAppearanceSkinClipBindings.Current;
        var oldLocalize = WorldMultiLanguageText.Localize;
        ProfessionAppearanceTextureBindings.Current = textures;
        ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionCurrentUiSkinClipBindings(oldClip);
        WorldMultiLanguageText.Localize = key => key == "名字不是翻译键" ? "错误翻译" : key;
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var staging = new GameObject("Inactive original player widget owner"); staging.SetActive(false);
        var canvasObject = new GameObject("Actual widget viewport", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(800, 800);
        var camera = new GameObject("Actual widget camera", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 160; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .16f, .2f, 1); canvas.worldCamera = camera;
        var render = new RenderTexture(320, 320, 24); render.Create(); camera.targetTexture = render;
        var eventObject = new GameObject("Simulated widget input", typeof(EventSystem));
        var events = eventObject.GetComponent<EventSystem>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Partners/UI/Panel/NpcPanel.prefab");
        var prototype = prefab.GetComponentsInChildren<PlayerInfoUI>(true).First(row => row.PlayerAppearance is Current.PlayerAnimationAppearance);
        var errors = new List<string>();
        var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            AssetDatabase.GUIDToAssetPath("a349287c39df984af5f51e2338d53e9f"));
        Require(clickClip && clickClip.samples > 0, "Verified current partner common click PCM");
        var audioCatalog = ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
        audioCatalog.Events = new[] { new OfflineProfessionAudioCatalog.Entry { EventId = 3772241779, Clip = clickClip } };
        var soundObject = new GameObject("Original partner common click host");
        soundObject.AddComponent<OfflineProfessionSoundHost>().Bind(audioCatalog);
        Application.LogCallback log = (message, stack, kind) => {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack);
        };
        Application.logMessageReceived += log;
        GameObject owner = null;
        try
        {
            for (int pass = 0; pass < 2; pass++)
            {
                owner = Object.Instantiate(prototype.gameObject, staging.transform, false);
                var widget = owner.GetComponent<PlayerInfoUI>();
                var icon = owner.GetComponent<PlayerIconMe>();
                var graphic = owner.GetComponentInChildren<ProfessionNativeUiGraphic>(true);
                Require(icon && graphic, "Actual original player widget components");
                widget.ConfigureInfo(info); icon.ConfigureInfo(info);
                foreach (var value in owner.GetComponentsInChildren<TeamPlayerUIBase>(true)) value.Configure(hp);
                new OfflineProfessionUiPlayerBindings(resources, manager, new Parts(config, tables.SpineSkinInfoParser), appearance).BindTree(owner);
                owner.transform.SetParent(canvasObject.transform, false);
                owner.SetActive(true); icon.Init();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Require(icon.InitFinished && widget.PlayerInfoData.Id == id && widget.NameText.Text == source.MainPlayer.Name,
                    "Original PlayerIconMe initialization consumes the actual local identity and plain name");
                Require(!widget.PlayerHeadPortrait.gameObject.activeSelf && widget.PlayerAppearance.gameObject.activeInHierarchy,
                    "Actual original refresh selects the current self avatar rather than the portrait cache");
                Require(graphic.State != null && widget.PlayerInfoData.AppearanceData == appearance.AppearanceData,
                    "Original refresh initializes the actual committed appearance");
                Require(widget.PlayerInfoData.Level == character.Player.Level
                    && widget.PlayerInfoData.CombatRating == character.EvaluateProps(new DateTimeOffset(now).ToUnixTimeSeconds()).CombatRating,
                    "Actual original data retains calculated local level and combat rating");
                var slider = (TeamPlayerIconMeHpSliderNormal)icon.IconMeHpSlider;
                Require(slider.HpSlider, "Actual original prefab uses its native UI health slider");
                Require(Math.Abs(slider.HpSlider.value - health.HpPercent) < .00001f, "Original slider initializes from the live local health service");
                var mask = graphic.GetComponentInParent<Mask>(true);
                var center = ((RectTransform)mask.transform).TransformPoint(((RectTransform)mask.transform).rect.center);
                camera.transform.position = new Vector3(center.x, center.y, center.z - 10);
                yield return null;
                Capture(camera, render, Path.Combine(work, "local-player-widget-" + pass + ".png"));
                int beforePopup = ports.Popups;
                Click(events, camera, widget.Btn, center);
                var voice = GameObject.Find("ProfessionSound:3772241779");
                Require(voice && voice.GetComponent<AudioSource>().clip == clickClip
                    && voice.GetComponent<AudioSource>().isPlaying, "Simulated original pointer plays the verified common click PCM");
                Require(ports.Popups == beforePopup + 1 && ports.LastData.Id == id && ports.LastScene == icon.PanelPopupSceneType
                    && ports.LastSkill == 77, "Simulated original avatar click forwards actual player data and original popup arguments");
                if (pass == 0)
                {
                    Require(health.SetHpPercent(.375f).IsApplied, "Persist a real local health update");
                    Require(Math.Abs(slider.HpSlider.value - .375f) < .00001f, "Original HP subscription applies only the committed value");
                    Require(session.ApplyCharacterMutation(player => {
                        player.CharacterName = "存档改名"; player.InitialWeaponType = 201;
                        SetAppearance(player, config, tables, Sex.Female, 10);
                    }).IsApplied, "Commit the real local name/sex/profession mutation");
                    source.SynchronizeCommittedPlayer();
                    yield return null;
                    Require(widget.NameText.Text == "存档改名" && widget.PlayerInfoData.Sex == Sex.Female
                        && widget.PlayerInfoData.Profession == 10 && graphic.State.Source == resources.Models.Single(row => row.Sex == "female").Asset.Data,
                        "Original manager notification refreshes name, profession and the correct female skeleton");
                    owner.SetActive(false);
                    string heldName = widget.PlayerInfoData.Name;
                    Require(session.ApplyCharacterMutation(player => player.CharacterName = "关闭后读档").IsApplied, "Commit while the widget is closed");
                    source.SynchronizeCommittedPlayer();
                    Require(widget.PlayerInfoData.Name == heldName, "Disabled original widget unsubscribes from manager notifications");
                    owner.SetActive(true);
                    yield return null;
                    Require(widget.NameText.Text == "关闭后读档", "Original re-enable refreshes the current committed generation");
                }
                else Require(widget.NameText.Text == "关闭后读档" && Math.Abs(slider.HpSlider.value - .375f) < .00001f,
                    "Actually reopened profile restores the original name, appearance and HP widget");
                Object.DestroyImmediate(owner); owner = null;
                resources.Loader.TryUnloadUnusedTextures(true);
                Require(textures.LoadedTextureCount == 0, "Original widget destruction releases all requested High textures");
                if (pass == 0)
                {
                    session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                    character = new OfflineCharacterProgressionService(session, catalog);
                    appearance = new OfflineProfessionAppearanceBindings(id, () => character.Player, item => catalog.ItemType(item), tables, config, rules, defaults);
                    source = new OfflinePartnerPlayerInfoSource(session, character, appearance, () => new DateTimeOffset(now).ToUnixTimeSeconds(),
                        _ => null, _ => throw Outside(), (_, __) => throw Outside());
                    health = new OfflinePlayerHealth(session, character, () => 0, () => Array.Empty<bool>(), () => now);
                    hp = new OfflinePartnerPlayerHpBindings(health, source.OnPlayerUpdate, () => source.MainPlayer != null, () => true);
                    info = new OfflinePartnerPlayerInfoBindings(source, ports);
                }
                yield return null;
            }
            Require(errors.Count == 0, "No actual original UI/runtime errors: " + string.Join("\n", errors));
        }
        finally
        {
            if (owner) Object.DestroyImmediate(owner);
            Application.logMessageReceived -= log;
            Object.DestroyImmediate(soundObject); Object.DestroyImmediate(audioCatalog);
            ProfessionAppearanceTextureBindings.Current = oldTextures;
            ProfessionAppearanceSkinClipBindings.Current = oldClip;
            WorldMultiLanguageText.Localize = oldLocalize;
            Object.DestroyImmediate(staging); Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(eventObject); render.Release(); Object.DestroyImmediate(render);
        }
    }
    static void SetAppearance(OfflinePlayerProgress player, AppearanceConfig config, ProfessionAppearanceTables tables, Sex sex, int profession)
    {
        var data = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, sex, (Profession)profession);
        player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
        player.Wardrobe.BaseSkins = data.SkinMap.Select(pair => new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId }).ToList();
        player.Wardrobe.Dyes = data.DyeMap.Select(pair => new OfflineAppearanceDye { Type = (int)pair.Key,
            ColorId = pair.Value.ColorId, Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V }).ToList();
    }
    static void Capture(Camera camera, RenderTexture render, string path)
    {
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
        var previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply(); RenderTexture.active = previous;
        var colors = image.GetPixels32();
        Require(!colors.Any(color => color.r > 245 && color.g < 10 && color.b > 245), "No shader error pixels in the actual original widget");
        Require(colors.Count(color => color.r > 120 && color.g > 80 && color.b > 45 && color.r > color.b) > 200
            && colors.Count(color => color.b > color.r + 40 && color.b > color.g + 10) > 5,
            "Actual visible native face, hair and selected original eye dye");
        File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
    }
    static void Click(EventSystem events, Camera camera, Button button, Vector3 worldPoint)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, worldPoint),
            button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Actual pointer raycast reaches the original avatar button");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    static NotSupportedException Outside() => new NotSupportedException("Outside the original local player widget fixture");
    sealed class Parts : IProfessionAppearancePartBindings
    {
        public AppearanceConfig Config { get; }
        readonly SpineSkinInfoParser parser;
        public Parts(AppearanceConfig config, SpineSkinInfoParser parser) { Config = config; this.parser = parser; }
        public bool HasEffect(string skin) => parser.HasEffect(skin);
        public IProfessionAppearancePartAsset LoadPrefab(string path) => throw Outside();
        public GameObject Instantiate(GameObject prefab, Transform parent, bool world) => Object.Instantiate(prefab, parent, world);
    }
    // The original selected row has no subrank/level/CP/profession-name fields,
    // no profession activation root, figure, model slot or title. Any accidental
    // use of their services fails instead of fabricating unrelated UI behavior.
    sealed class Ports : IProfessionPlayerInfoBindings
    {
        readonly Func<IProfessionInfoPlayer, PlayerInfoData> build;
        public int Popups;
        public PlayerInfoData LastData;
        public int LastScene, LastSkill;
        public Ports(Func<IProfessionInfoPlayer, PlayerInfoData> build) { this.build = build; }
        public IProfessionInfoPlayer MainPlayer => throw Outside();
        public bool ManagerAvailable => true;
        public SafeAction OnPlayerUpdate => throw Outside();
        public IProfessionImageResourceLoader Resources => throw Outside();
        public PlayerInfoData BuildData(IProfessionInfoPlayer player) => throw Outside();
        public PlayerInfoData BuildLiteData(object info, IProfessionInfoTarget target) => throw Outside();
        public object BuildBriefInfo(IProfessionInfoPlayer player, bool online)
        { Require(online, "Original self-popup requests the online brief-info branch"); return build(player); }
        public object CreateNameArg(string name, bool translate) => throw Outside();
        public object CompactNumber(long value) => throw Outside();
        public string SubRankKey(int rank) => throw Outside();
        public string ProfessionNameKey(int profession) => throw Outside();
        public string GetFullBodyGuid(int profession, Sex sex) => throw Outside();
        public string FirstProfessionIcon(int profession) => throw Outside();
        public string ThirdProfessionIcon(int profession) => throw Outside();
        public ProfessionInfoText NameWithPlanes(object name, long server) => throw Outside();
        public bool HasHonorTitle(object appearance) => throw Outside();
        public ProfessionInfoNpcDefinition GetNpc(int classId) => throw Outside();
        public GameObject InstantiateKeepScale(GameObject prefab, Transform parent) => throw Outside();
        public void OverrideNpcInitialAnimation(GameObject model, int npcId) => throw Outside();
        public TeamTargetCatalog? TeamCatalog(Component component) => throw Outside();
        public ProfessionInfoRobotInvite RobotInvite(Component component) => throw Outside();
        public ProfessionInfoRobotInvite DefaultRobotInvite() => throw Outside();
        public bool CanJointTogether(int target, int main) => throw Outside();
        public int DuelSkillGroup(long player, long target)
        { Require(player == -1 && target == -1, "Original self-popup uses its original sentinel arguments"); return 77; }
        public void Popup(Component owner, Action<IProfessionPlayerInfoDetailPanel> initialized)
        { Popups++; initialized(new PopupBoundary(this)); }
        sealed class PopupBoundary : IProfessionPlayerInfoDetailPanel
        {
            readonly Ports owner;
            public PopupBoundary(Ports owner) { this.owner = owner; }
            public void Init(object brief, int scene, int skill)
            { owner.LastData = (PlayerInfoData)brief; owner.LastScene = scene; owner.LastSkill = skill; }
        }
    }
}
