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
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private const string TeamTargetsKey = "Zjcs.Map12.TeamTargets.Acceptance";
        private static bool teamTargetsStarted;
        public static void RunTeamTargets()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ImportTeamTargetSprites();
                ProjectTools.Validate();
                SessionState.SetBool(TeamTargetsKey, true);
                SessionState.SetInt(TeamTargetsKey + ".result", 1);
                SessionState.SetFloat(TeamTargetsKey + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                HookTeamTargets(); EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [InitializeOnLoadMethod] private static void RestoreTeamTargets()
        { if (SessionState.GetBool(TeamTargetsKey, false)) HookTeamTargets(); }
        private static void HookTeamTargets()
        {
            teamTargetsStarted = false;
            EditorApplication.update -= TickTeamTargets;
            EditorApplication.update += TickTeamTargets;
            EditorApplication.playModeStateChanged -= TeamTargetsModeChanged;
            EditorApplication.playModeStateChanged += TeamTargetsModeChanged;
        }
        private static void TeamTargetsModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(TeamTargetsKey, false)) return;
            SessionState.SetBool(TeamTargetsKey, false);
            EditorApplication.update -= TickTeamTargets;
            EditorApplication.playModeStateChanged -= TeamTargetsModeChanged;
            EditorApplication.Exit(SessionState.GetInt(TeamTargetsKey + ".result", 1));
        }
        private static void TickTeamTargets()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(TeamTargetsKey + ".deadline", 0))
            { Debug.LogError("Original team target input verification timed out."); EditorApplication.isPlaying = false; return; }
            if (teamTargetsStarted || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            teamTargetsStarted = true;
            new GameObject("OriginalTeamTargetVerification").AddComponent<Runner>().StartCoroutine(VerifyTeamTargets());
        }
        private static IEnumerator VerifyTeamTargets()
        {
            foreach (var steps in new[] { VerifyTeamTargetSteps(), VerifyTeamControlSteps() })
            {
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            }
            SessionState.SetInt(TeamTargetsKey + ".result", 0);
            Debug.Log("Map12 original target prefabs passed native binding, actual raycast input, dictionary-order " +
                "display, folding, inherited stele-key selection, confirm/hide and pooled reinitialization at two portrait sizes.");
            EditorApplication.isPlaying = false;
        }
        private sealed class TargetTestServices : ITeamTargetServices
        {
            public readonly List<int> Errors = new List<int>();
            public Dictionary<string, string> Texts;
            public int CategorySort(TeamTargetCatalog catalog) => catalog == TeamTargetCatalog.BattleStele ? 0 : 10;
            public bool HasMultipleTargets(TeamTargetCatalog catalog) => catalog == TeamTargetCatalog.BattleStele;
            public int TeamMaxPlayer(TeamTargetCatalog catalog) => catalog == TeamTargetCatalog.BattleStele ? 3 : 4;
            public string EnumName(TeamTargetCatalog catalog) => Localize("TeamTargetCatalog." + catalog);
            public string Localize(string key) => Texts != null && Texts.TryGetValue(key, out string value) ? value : key;
            public string Format(string key, params object[] arguments) => key + ":" + string.Join(",", arguments);
            public int CompareDelegateTargets(TeamTargetData left, TeamTargetData right) => throw new InvalidDataException("Unexpected delegate catalog.");
            public void ShowError(int errorCode, bool useDefaultTip)
            { SteleRequire(!useDefaultTip, "Original empty target error uses false."); Errors.Add(errorCode); }
        }
        private sealed class SimpleTarget : TeamTargetData
        { public SimpleTarget(ITeamTargetServices services, TeamTargetCatalog catalog) : base(services) { TargetCatalog = catalog; } }
        [Serializable] private sealed class TeamTargetGeometryData { public TeamTargetGeometry[] sprites; }
        [Serializable] private sealed class TeamTargetGeometry
        {
            public string path, texturePath, guid, name;
            public TeamTargetRect rect;
            public Vector2 pivot;
            public float ppu;
            public Vector4 border;
            public Vector2[] vertices;
            public int[] triangles;
        }
        [Serializable] private sealed class TeamTargetRect { public float x, y, width, height; }
        private static void VerifyTeamTargetSpriteGeometry()
        {
            var data = JsonUtility.FromJson<TeamTargetGeometryData>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Official/World/Map/12/Source/team-target-sprite-geometry.json").text);
            foreach (var row in data.sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(row.path);
                SteleRequire(sprite != null && AssetDatabase.AssetPathToGUID(row.path) == row.guid
                    && sprite.rect.width == row.rect.width && sprite.rect.height == row.rect.height
                    && sprite.pixelsPerUnit == row.ppu && Vector2.Distance(sprite.pivot, row.pivot) < 0.00001f
                    && Vector4.Distance(sprite.border, row.border) < 0.00001f,
                    "Original target Sprite rect/pivot/PPU/border differs: " + row.path);
                var vertices = sprite.vertices;
                SteleRequire(vertices.Length == row.vertices.Length && vertices.Zip(row.vertices, Vector2.Distance).All(d => d < 0.00001f)
                    && sprite.triangles.Select(index => (int)index).SequenceEqual(row.triangles),
                    "Original target Sprite geometry differs: " + row.path + "; actual=" + string.Join(",", vertices)
                    + "; expected=" + string.Join(",", row.vertices) + "; actual indices=" + string.Join(",", sprite.triangles)
                    + "; expected indices=" + string.Join(",", row.triangles));
                SteleRequire(sprite.uv.Length == row.vertices.Length && sprite.uv.Zip(row.vertices,
                    (uv, vertex) => Vector2.Distance(uv, new Vector2((vertex.x * row.ppu + row.pivot.x) / row.rect.width,
                        (vertex.y * row.ppu + row.pivot.y) / row.rect.height))).All(distance => distance < 0.00001f),
                    "Original target Sprite UV mapping differs after lossless texture extraction: " + row.path);
            }
        }
        private static T TeamTargetField<T>(object owner, string name) => (T)owner.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);
        private static IEnumerator VerifyTeamTargetSteps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(repository) || !Directory.Exists(output)
                || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(Path.Combine(repository, "output/validation"))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Use the owned original team validation workspace.");
            Application.runInBackground = true;
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (Canvas boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.name.StartsWith("BootSplash")))
                UnityEngine.Object.Destroy(boot.gameObject);
            VerifyTeamTargetSpriteGeometry();
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text);
            var camera = new GameObject("OriginalTeamTargetsCamera").AddComponent<Camera>();
            camera.backgroundColor = Color.black; camera.gameObject.AddComponent<AudioListener>();
            if (EventSystem.current == null) new GameObject("TeamTargetsSimulatedInput", typeof(EventSystem));
            var previousPool = ProfessionGameObjectPoolBridge.Provider;
            var previousFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset");
            ProfessionGameObjectPoolBridge.Provider = (_, prefab, parent, stays) => UnityEngine.Object.Instantiate(prefab, parent, stays);
            try
            {
                foreach (int width in new[] { 450, 720 })
                {
                    SetUiGameViewSize(width, width == 450 ? 800 : 1280);
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (Screen.width != width) { SteleRequire(Time.realtimeSinceStartup < deadline, "Target portrait resize timed out."); yield return null; }
                    var canvas = new GameObject("OriginalTeamTargetsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    ConfigureCanvas(canvas, camera, data);
                    var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/Team/TeamPanelTarget"), canvas.transform, false);
                    root.SetActive(false);
                    SteleRequire(root.GetComponentsInChildren<Transform>(true).Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)) == 0,
                        "Original target prefab contains missing scripts.");
                    var panel = root.GetComponentInChildren<TeamPanelTarget>(true);
                    SteleRequire(panel != null, "Original target controller is not bound.");
                    var panelObject = panel.gameObject;
                    var services = new TargetTestServices { Texts = JsonUtility.FromJson<TeamControlTexts>(Resources.Load<TextAsset>("Map12/Team/target-texts").text).texts.ToDictionary(row => row.key, row => row.value) }; panel.Configure(services);
                    foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = services.Localize;
                    ProfessionGameObjectPoolBridge.Provider = (_, prefab, parent, stays) => { var clone = UnityEngine.Object.Instantiate(prefab, parent, stays); foreach (var text in clone.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = services.Localize; return clone; };
                    int sounds = 0;
                    var source = root.AddComponent<AudioSource>();
                    foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    { SteleRequire(sound.ClickEvent != null, "Original target click PCM missing."); sound.Post = (clip, _) => { sounds++; source.PlayOneShot(clip); }; }
                    var plain = new SimpleTarget(services, TeamTargetCatalog.None);
                    var first = new TeamSteleTargetData(services, 1813, 2530);
                    var second = new TeamSteleTargetData(services, 3013, 5519);
                    SteleRequire(!first.Equals(second) && first.ToTeamTargetKey() == second.ToTeamTargetKey(),
                        "Stele value identity and inherited UI key were conflated.");
                    SteleRequire(first.TryGetLimitErrorTip(false, 1145, out string tip) && tip.EndsWith(services.Localize("TeamTargetCatalog.BattleStele"))
                        && !first.TryGetLimitErrorTip(true, 6102, out _), "Original target error mapping differs.");
                    SteleRequire(TeamTargetField<TeamPanelTargetItem>(panel, "mItemTemp") != null, "Original target item prefab pointer failed native import.");
                    int confirmed = 0;
                    Action<TeamTargetData> confirm = target => { SteleRequire(ReferenceEquals(target, first), "Target callback value differs."); confirmed++; };
                    panel.Init(null, new List<TeamTargetData> { plain, first, second }, confirm);
                    root.SetActive(true);
                    yield return null; yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-target-initial.png"));
                    var items = TeamTargetField<List<TeamPanelTargetItem>>(panel, "mItemList");
                    SteleRequire(root.GetComponentsInChildren<Image>(true).Where(image => !(image is NoDrawingRayCast))
                        .All(image => image.sprite != null), "Original native target Image lost its Sprite reference.");
                    SteleRequire(items.Count == 2 && panel.Catalogs[0] == TeamTargetCatalog.BattleStele
                        && TeamTargetField<TeamTargetCatalog>(items[0], "mTeamTargetCatalog") == TeamTargetCatalog.None,
                        "Original dictionary display order or separately sorted Catalogs changed.");
                    SimulateUiClick(TeamTargetField<Button>(panel, "mConfirmBtn"), camera);
                    SteleRequire(services.Errors.SequenceEqual(new[] { 1252 }) && panelObject.activeSelf && confirmed == 0,
                        "Empty confirmation must preserve visible panel and emit original error.");
                    var steleItem = items[1];
                    var subContent = TeamTargetField<Transform>(steleItem, "mSubItemContent");
                    SteleRequire(!subContent.gameObject.activeSelf, "Unselected multi-target catalog should start folded.");
                    SimulateUiClick(TeamTargetField<Button>(steleItem, "mBtn"), camera);
                    yield return null; yield return new WaitForEndOfFrame();
                    SteleRequire(subContent.gameObject.activeSelf, "Original target catalog click did not unfold.");
                    var subs = TeamTargetField<List<TeamPanelTargetSubItem>>(steleItem, "mSubItemList");
                    SteleRequire(subs.Count == 2 && !TeamTargetField<bool>(subs[0], "mIsLastOne")
                        && TeamTargetField<bool>(subs[1], "mIsLastOne"), "Original last-subitem state differs.");
                    SimulateUiClick(TeamTargetField<Button>(subs[0], "mBtn"), camera);
                    SteleRequire(ReferenceEquals(panel.CurrentTarget, first) && subs.All(sub => TeamTargetField<bool>(sub, "mSelected")),
                        "Original inherited catalog key must select both stele rows.");
                    SimulateUiClick(TeamTargetField<Button>(subs[1], "mBtn"), camera);
                    SteleRequire(ReferenceEquals(panel.CurrentTarget, first), "Selected original subitem should ignore another click.");
                    yield return new WaitForEndOfFrame(); CaptureSteleUiFrame(Path.Combine(output, width + "-team-target-selected.png"));
                    SimulateUiClick(TeamTargetField<Button>(panel, "mConfirmBtn"), camera);
                    SteleRequire(confirmed == 1 && !panelObject.activeSelf, "Original target confirm callback/hide differs.");
                    panelObject.SetActive(true); yield return null; yield return new WaitForEndOfFrame();
                    SimulateUiClick(TeamTargetField<Button>(panel, "mConfirmBtn"), camera);
                    SteleRequire(confirmed == 1 && !panelObject.activeSelf, "Disabled original confirmation callback was retained.");
                    panel.Init(first, new List<TeamTargetData> { first }, confirm); panelObject.SetActive(true);
                    yield return null; yield return new WaitForEndOfFrame();
                    SteleRequire(TeamTargetField<List<TeamPanelTargetItem>>(panel, "mItemList").Count == 1,
                        "Original target pool retained removed catalog items.");
                    SimulateUiClick(TeamTargetField<Button>(panel, "mHideBtn"), camera, new Vector2(Screen.width * 0.05f, Screen.height * 0.5f));
                    SteleRequire(!panelObject.activeSelf && confirmed == 1, "Original hide button failed to close its own controller object.");
                    SteleRequire(sounds > 0, "Original target-confirm and close PCM dispatch failed.");
                    UnityEngine.Object.Destroy(canvas); yield return null;
                }
            }
            finally
            { ProfessionGameObjectPoolBridge.Provider = previousPool; WorldMultiLanguageText.LanguageFont = previousFont; UnityEngine.Object.Destroy(camera.gameObject); }
        }
    }
}
