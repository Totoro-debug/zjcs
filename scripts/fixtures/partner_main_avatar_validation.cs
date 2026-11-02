using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Current = Zjcs.Official.CombatAppearance;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerMainAvatarValidation
{
    const string Key = "PartnerMainAvatarValidation.Active";
    static PartnerMainAvatarValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original partner avatar continuous animation validation").AddComponent<PartnerMainAvatarRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original avatar continuous animation validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 600);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "avatar-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerMainAvatarRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        var steps = Check();
        while (true)
        {
            object value;
            try { if (!steps.MoveNext()) break; value = steps.Current; }
            catch (Exception error) { PartnerMainAvatarValidation.Finish(error); yield break; }
            yield return value;
        }
        PartnerMainAvatarValidation.Finish(null);
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    IEnumerator Check()
    {
        var studio = ProfessionNativePortraitStudioResources.LoadDefault();
        var resources = Resources.Load<ProfessionNativeSceneAppearanceResources>("Partners/appearance-resources");
        Require(resources && resources.LoaderGuid == "7747b1622b6dcd941ac31d869760ad8d",
            "Original partner avatar requires its selected High loader");
        var config = studio.AppearanceConfig;
        var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        var defaults = new ProfessionAppearanceSkinDefaults(tables,
            Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, message => { throw new Exception(message); });
        var materialConfig = Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config");
        Require(materialConfig && materialConfig.UIDyeShader, "Selected original UI material configuration");
        var lowMemory = new GAction();
        using var manager = new AppearanceAssetManager(materialConfig, lowMemory.Subscribe);
        using var textures = new OfflineProfessionSceneTextureResources(resources);
        var old = ProfessionAppearanceTextureBindings.Current;
        // This original avatar excludes weapon/back parts in its own serialized
        // IgnoreSkinTypes. Every requested texture follows its original High GUID.
        ProfessionAppearanceTextureBindings.Current = textures;
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Partners/UI/Panel/NpcPanel.prefab");
        var prototype = prefab.GetComponentsInChildren<Current.PlayerAnimationAppearance>(true).First();
        var prototypeInfo = prefab.GetComponentsInChildren<PlayerInfoUI>(true).Single(info => info.PlayerAppearance == prototype);
        Require(prototypeInfo.UseUISpine, "Actual player-info serialized field accepts current original appearance subclass");
        var staging = new GameObject("Inactive original avatar owner"); staging.SetActive(false);
        var canvasGo = new GameObject("Isolated original avatar viewport", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(320, 320);
        var camera = new GameObject("Actual UI validation camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 160;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .2f, 1);
        canvas.worldCamera = camera;
        var render = new RenderTexture(320, 320, 24); render.Create();
        var errors = new List<string>();
        Application.LogCallback capture = (message, stack, kind) => {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack);
        };
        Application.logMessageReceived += capture;
        try
        {
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                canvasGo.SetActive(false);
                var owner = Object.Instantiate(prototypeInfo.gameObject, staging.transform, false);
                var ownerInfo = owner.GetComponent<PlayerInfoUI>();
                var player = (Current.PlayerAnimationAppearance)ownerInfo.PlayerAppearance;
                // Exercise the original serialized player/self state tables.
                // Their portrait branch otherwise overlays the native avatar
                // with the original loading image in this isolated widget.
                ownerInfo.mStateSwitcher.SetState(ActorTargetType.Player);
                ((WorldStateSwitcher<bool>)typeof(PlayerInfoUI).GetField("mIsMyselfSwitcher", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(ownerInfo)).SetState(true);
                var mask = player.GetComponentInParent<Mask>(true);
                Require(mask && !ownerInfo.PlayerHeadPortrait.gameObject.activeSelf && player.gameObject.activeSelf,
                    "Original self state selects UI appearance and disables cached portrait");
                var root = mask.transform.parent.gameObject;
                root.transform.SetParent(canvasGo.transform, false);
                var rect = (RectTransform)root.transform; rect.anchoredPosition = Vector2.zero;
                var graphic = player.GetComponent<ProfessionNativeUiGraphic>();
                var ui = player.GetComponent<Current.AppearanceUI>();
                var character = player.GetComponent<Current.AppearanceCharacter>();
                var parts = player.GetComponent<AppearanceUIPartHandler>();
                Require(graphic && ui && character && parts && root.GetComponentsInChildren<Mask>(true).Length > 0,
                    "Original UI components and mask survive selected-script binding");
                Require(!graphic.freeze && graphic.startingAnimation == "home/idle_M_01"
                    && graphic.AnimationState == null && !graphic.skeletonDataAsset,
                    "Original serialized initial state remains unchanged");
                Require(parts.IgnoreSkinTypes.Select(value => (int)value).SequenceEqual(new[] { 12, 7, 8 }),
                    "Use the original avatar exclusions rather than clearing parts");
                // Capture a deterministic initial frame, then exercise the
                // actual LateUpdate producer with the original unfrozen setting.
                graphic.freeze = true;
                ProfessionNativePlayerPhysicsPose physicalPose = null;
                var binding = new OfflineProfessionUiAppearanceBindings(resources, graphic, manager, config, asset => {
                    physicalPose = new ProfessionNativePlayerPhysicsPose(asset.Data);
                    return new ProfessionNativeUiCharacterPose(asset, physicalPose.Apply,
                        (page, blend) => blend == 0 ? graphic.material : blend == 1 ? graphic.additiveMaterial :
                        blend == 2 ? graphic.multiplyMaterial : blend == 3 ? graphic.screenMaterial : throw new Exception("Original blend"),
                        page => resources.MaterialForPage(asset, page).mainTexture);
                });
                var serialized = new SerializedObject(ui);
                Require(serialized.FindProperty("TextureLoaderGuid").FindPropertyRelative("mAssetGuid").stringValue == resources.LoaderGuid
                    && serialized.FindProperty("SkinTextureAssetGuid").FindPropertyRelative("mAssetGuid").stringValue == resources.SkinGuid,
                    "Preserve the exact original avatar skin/High-loader GUIDs");
                ui.Configure(binding, Proxy<SkinTextureAsset>(resources.SkinGuid), Proxy<YooAssetTextureLoader>(resources.LoaderGuid), parts);
                ui.ConfigureUI(binding); character.Configure(binding, parts);
                parts.ConfigureParts(new Parts(config, tables.SpineSkinInfoParser));
                var data = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, sex, (Profession)profession);
                var saved = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                saved.Wardrobe.Sex = (int)sex; saved.Wardrobe.Profession = profession;
                foreach (var pair in data.SkinMap)
                    saved.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                foreach (var pair in data.DyeMap)
                    saved.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                        Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                var appearance = new OfflineProfessionAppearanceBindings(17, () => saved, _ => saved.InitialWeaponType,
                    tables, config, rules, defaults);
                player.ConfigurePlayerAppearance(appearance);
                var oldClip = ProfessionAppearanceSkinClipBindings.Current;
                ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionCurrentUiSkinClipBindings(oldClip);
                try { canvasGo.SetActive(true); }
                finally { ProfessionAppearanceSkinClipBindings.Current = oldClip; }
                Require(graphic.AnimationState == null, "Awake with no asset must not fabricate a track");
                player.RefreshAppearance(17, sex, profession, appearance.AppearanceData);
                Require(player.AnimName == "map/idle_01" && graphic.startingAnimation == player.AnimName
                    && graphic.AnimationState.GetCurrent(0).Animation.Name == player.AnimName,
                    "Original phase animation selected before first skeleton initialization");
                yield return null;
                Canvas.ForceUpdateCanvases(); graphic.ApplyAnimation(); Canvas.ForceUpdateCanvases();
                Require(graphic.State != null && ui.Skins.Count() >= 4 && graphic.canvasRenderers.Count > 0,
                    "Current original combined skins produce actual native UI meshes");
                Require(parts.LoadedDict.Values.All(entry => entry.Parts.Count == 0), "Original ignored weapon/back parts remain unloaded");
                Require(ui.IndependentMaterialDict.Count > 0 && ui.IndependentMaterialDict.Values.All(mat => mat.shader == materialConfig.UIDyeShader),
                    "Actual selected UI dye material replaces each original placeholder");
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                var prior = RenderTexture.active; RenderTexture.active = render;
                var image = new Texture2D(320, 320, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); image.Apply(); RenderTexture.active = prior;
                var colors = image.GetPixels32();
                Require(!colors.Any(c => c.r > 245 && c.g < 10 && c.b > 245), "No shader error pixels");
                Require(colors.Where((color, index) => Math.Abs(index % 320 - 160) < 24 && Math.Abs(index / 320 - 153) < 24)
                    .Count(color => color.r > 120 && color.g > 80 && color.b > 45 && color.r > color.b) > 200,
                    "Visible colored face and hair rather than the unselected portrait-loading silhouette");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "avatar-" + sex + "-" + profession + ".png"), image.EncodeToPNG());
                var initialWorld = Enumerable.Range(0, graphic.State.Pose.Count).Select(graphic.State.Pose.World).ToArray();
                RecordNativePose(graphic.State, sex, profession);
                Require(physicalPose.PhysicsTime == 0, "Initial skeleton physics clock starts at zero");
                graphic.freeze = false;
                float duration = new ProfessionNativeAnimationChannels(graphic.State.Source, player.AnimName).Duration;
                int movingFrames = 0;
                while (movingFrames < 120 || physicalPose.PhysicsTime <= duration + 1f / 60)
                {
                    if (++movingFrames > 1800) throw new InvalidOperationException("Actual avatar animation did not reach its original loop boundary");
                    yield return null;
                }
                Require(physicalPose.PhysicsTime > duration, "Actual LateUpdate crosses the original animation loop boundary");
                Require(initialWorld.Where((matrix, index) => graphic.State.Pose.IsActive(index)
                    && matrix != graphic.State.Pose.World(index)).Any(), "Original positive-time bone poses move");
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                RenderTexture.active = render; image.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); image.Apply(); RenderTexture.active = prior;
                Require(!colors.SequenceEqual(image.GetPixels32()), "Continuous native animation changes actual GPU pixels");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "avatar-moving-" + sex + "-" + profession + ".png"), image.EncodeToPNG());
                float elapsed = physicalPose.PhysicsTime;
                graphic.freeze = true;
                graphic.PlayAnimation(player.AnimName, true); graphic.ApplyAnimation();
                Require(physicalPose.PhysicsTime == elapsed, "Track replay retains the original skeleton physics clock");
                var initial = graphic.State;
                var otherSex = sex == Sex.Male ? Sex.Female : Sex.Male;
                var otherData = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, otherSex, (Profession)profession);
                player.RefreshAppearance(17, otherSex, profession, otherData);
                Require(graphic.State != initial && graphic.State.Source != initial.Source,
                    "Current UI sex switch binds the matching new native pose and skin layout");
                player.RefreshAppearance(17, sex, profession, appearance.AppearanceData);
                Require(physicalPose.PhysicsTime == 0, "Replacing the original skeleton resets its physical clock");
                yield return null;
                Canvas.ForceUpdateCanvases(); graphic.ApplyAnimation(); Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                RenderTexture.active = render; image.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); image.Apply(); RenderTexture.active = prior;
                Require(colors.SequenceEqual(image.GetPixels32()), "Releasing and switching sex back restores identical actual GPU pixels");
                Object.DestroyImmediate(image);
                Debug.Log("[Original partner avatar continuous] " + sex + "/" + profession + ", animation=" + player.AnimName + ", frames=" + movingFrames + ", duration=" + duration + ", physical seconds=" + elapsed
                    + ", batches=" + graphic.canvasRenderers.Count + ", dye materials=" + ui.IndependentMaterialDict.Count);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(owner);
                resources.Loader.TryUnloadUnusedTextures(true);
                Require(textures.LoadedTextureCount == 0, "Original avatar destruction releases every requested High texture");
                Require(errors.Count == 0, "No original application or native renderer errors: " + string.Join("\n", errors));
                yield return null;
            }
        }
        finally
        {
            Application.logMessageReceived -= capture;
            ProfessionAppearanceTextureBindings.Current = old;
            Object.DestroyImmediate(staging); Object.DestroyImmediate(canvasGo); Object.DestroyImmediate(camera.gameObject); render.Release(); Object.DestroyImmediate(render);
        }
    }
    static AssetGuidProxy<T> Proxy<T>(string guid) where T : Object
    { var value = new AssetGuidProxy<T>(); JsonUtility.FromJsonOverwrite("{\"mAssetGuid\":\"" + guid + "\"}", value); return value; }
    static void RecordNativePose(ProfessionNativeAppearanceSkeleton displayed, Sex sex, int profession)
    {
        var native = new ProfessionNativeAppearanceSkeleton(displayed.Source);
        native.SetSkin(displayed.Skin); native.SetToSetupPose(); native.UpdateCache();
        var solver = new ProfessionNativePlayerPhysicsPose(displayed.Source);
        var animation = new ProfessionNativeAnimationChannels(displayed.Source, "map/idle_01", originalBezierSegments: true);
        var skin = (ProfessionNativeSkin)displayed.Skin;
        string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "native-pose-" + sex + "-" + profession + ".txt");
        using var output = new StreamWriter(path);
        output.WriteLine("B|" + string.Join(",", skin.Bones));
        output.WriteLine("C|" + string.Join(",", skin.Constraints));
        float time = 0;
        for (int frame = 0; frame <= 240; frame++)
        {
            float delta = frame == 0 ? 0 : 1f / 60;
            time += delta; solver.Apply(native, animation, time, true, delta);
            for (int bone = 0; bone < native.Pose.Count; bone++)
            {
                if (!native.Pose.IsActive(bone)) continue;
                var matrix = native.Pose.World(bone);
                output.WriteLine(string.Join("|", frame, bone,
                    string.Join(",", new[] { matrix.m00, matrix.m01, matrix.m10, matrix.m11, matrix.m03, matrix.m13 }
                        .Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))));
            }
        }
    }
    sealed class Parts : IProfessionAppearancePartBindings
    {
        public AppearanceConfig Config { get; }
        readonly SpineSkinInfoParser parser;
        public Parts(AppearanceConfig config, SpineSkinInfoParser parser) { Config = config; this.parser = parser; }
        public bool HasEffect(string skin) => parser.HasEffect(skin);
        public IProfessionAppearancePartAsset LoadPrefab(string path) => throw new NotSupportedException("Unexpected default avatar effect: " + path);
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays) => Object.Instantiate(prefab, parent, worldPositionStays);
    }
}
