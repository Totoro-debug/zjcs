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
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private const string TeamControlsKey = "Zjcs.Map12.TeamControls.Acceptance";
        private static bool teamControlsStarted;

        public static void RunTeamInvitationControls()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ProjectTools.Validate();
                SessionState.SetBool(TeamControlsKey, true);
                SessionState.SetInt(TeamControlsKey + ".result", 1);
                SessionState.SetFloat(TeamControlsKey + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                HookTeamControls();
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [InitializeOnLoadMethod]
        private static void RestoreTeamControls()
        { if (SessionState.GetBool(TeamControlsKey, false)) HookTeamControls(); }

        private static void HookTeamControls()
        {
            teamControlsStarted = false;
            EditorApplication.update -= TickTeamControls;
            EditorApplication.update += TickTeamControls;
            EditorApplication.playModeStateChanged -= TeamControlsModeChanged;
            EditorApplication.playModeStateChanged += TeamControlsModeChanged;
        }

        private static void TeamControlsModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(TeamControlsKey, false)) return;
            SessionState.SetBool(TeamControlsKey, false);
            EditorApplication.update -= TickTeamControls;
            EditorApplication.playModeStateChanged -= TeamControlsModeChanged;
            EditorApplication.Exit(SessionState.GetInt(TeamControlsKey + ".result", 1));
        }

        private static void TickTeamControls()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(TeamControlsKey + ".deadline", 0))
            {
                Debug.LogError("Original team permission input verification timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (teamControlsStarted || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            teamControlsStarted = true;
            new GameObject("Map12OriginalTeamPermissionVerification").AddComponent<Runner>()
                .StartCoroutine(VerifyTeamControls());
        }

        private static IEnumerator VerifyTeamControls()
        {
            IEnumerator steps = VerifyTeamControlSteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(TeamControlsKey + ".result", 0);
            Debug.Log("Map12 original team permission widget passed at two portrait sizes: actual raycast clicks, " +
                "original sprites/font/text/click dispatch, visibility, asynchronous response correction and event lifetime. " +
                "Full team windows, real NPC services and map battles remain separate.");
            EditorApplication.isPlaying = false;
        }

        [Serializable] private sealed class TeamControlTexts { public TeamControlText[] texts; }
        [Serializable] private sealed class TeamControlText { public string key, value; }
        [Serializable] private sealed class TeamControlLayout { public TeamControlLayoutNode[] nodes; }
        [Serializable] private sealed class TeamControlLayoutNode
        { public string name, rect; public TeamControlLayoutComponent[] components; }
        [Serializable] private sealed class TeamControlLayoutComponent { public string type, fields; }

        private sealed class TeamPermissionServices : IWorldMemberInviteServices
        {
            public bool Leader = true, Setting;
            public readonly List<(bool value, Action<bool> complete)> Pending = new List<(bool, Action<bool>)>();
            public bool IsLeader => Leader;
            public bool PlayerSettingsAllowMemberInvite { get => Setting; set => Setting = value; }
            public void OperPlayerAllowMemberInvite(bool allow, Action<bool> onSuccess)
            { Pending.Add((allow, onSuccess)); }
        }

        private static IEnumerator VerifyTeamControlSteps()
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
            var iconSteps = VerifyTeamIconSteps();
            while (iconSteps.MoveNext()) yield return iconSteps.Current;
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text);
            var texts = JsonUtility.FromJson<TeamControlTexts>(Resources.Load<TextAsset>("Map12/Team/texts").text);
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset");
            SteleRequire(chinese != null, "The original config-selected Chinese font is unavailable.");
            var originalLatin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/MPLUSRounded1c-Medium.asset");
            SteleRequire(originalLatin != null && originalLatin.sourceFontFile != null
                && originalLatin.atlasTextures.Length == 1 && originalLatin.TryAddCharacters("Team123", out string missing)
                && string.IsNullOrEmpty(missing) && originalLatin.atlasTexture.width == 1024,
                "Original empty team font atlas failed native dynamic glyph allocation.");
            var previousFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = () => chinese;
            if (EventSystem.current == null) new GameObject("Map12TeamSimulatedInput", typeof(EventSystem));
            var camera = new GameObject("Map12TeamPermissionCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10; camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            try
            {
                foreach (int width in new[] { 450, 720 })
                {
                    int height = width == 450 ? 800 : 1280;
                    SetUiGameViewSize(width, height);
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (Screen.width != width || Screen.height != height)
                    { if (Time.realtimeSinceStartup > deadline) throw new InvalidDataException("Team widget portrait resize timed out."); yield return null; }
                    var canvas = new GameObject("OriginalTeamPermissionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    ConfigureCanvas(canvas, camera, data);
                    Transform content = CreateTeamControlLayout(canvas.transform, out GameObject layoutRoot);
                    var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/Team/AllowMemberInviteToggle"), content, false);
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                    var view = root.GetComponent<WorldAllowMemberInviteToggle>();
                    var toggle = root.GetComponentInChildren<Toggle>(true);
                    var onImage = toggle.transform.Find("On").GetComponent<Image>();
                    var offImage = toggle.transform.Find("Off").GetComponent<Image>();
                    SteleRequire(onImage.sprite != null && offImage.sprite != null,
                        "Original permission images lost their native Sprite references: on=" + onImage.sprite + "; off=" + offImage.sprite
                        + "; imported=" + string.Join(",", AssetDatabase.LoadAllAssetsAtPath("Assets/Official/World/Map/12/UI/Sprites/BtnCheck_Nor-2bb9ad0ab5.png")
                            .Select(asset => asset.GetType().Name + ":" + GlobalObjectId.GetGlobalObjectIdSlow(asset))));
                    var localized = root.GetComponentInChildren<WorldMultiLanguageText>(true);
                    var font = localized.GetComponent<TMP_Text>();
                    SteleRequire(view != null && toggle != null && localized != null && font.font == chinese,
                        "Original serialized team component/font bindings are missing.");
                    localized.Localizer = key => texts.texts.Single(row => row.key == key).value;
                    var s = new TeamPermissionServices();
                    view.Initialize(s);
                    var audio = root.AddComponent<AudioSource>();
                    int sounds = 0;
                    var click = root.GetComponentInChildren<WorldUiClickSound>(true);
                    SteleRequire(click.AkEvent != null && click.AkEvent.name == "Main_Cursor_Normal", "Original permission click PCM binding differs.");
                    click.Post = (clip, _) => { sounds++; audio.PlayOneShot(clip); };
                    view.RefreshAllow(2);
                    SteleRequire(!root.activeSelf && s.Pending.Count == 0, "Permission visibility requires original minimum target size.");
                    view.RefreshAllow(3);
                    layoutRoot.SetActive(true);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    SteleRequire(root.activeSelf && font.font == chinese && localized.Text == texts.texts.Single().value,
                        "Original language font/key refresh failed.");
                    SimulateTeamToggleClick(toggle, camera);
                    SteleRequire(toggle.isOn && s.Pending.Count == 1 && s.Pending[0].value && !s.Setting && sounds == 1,
                        "Pointer click must await the original successful settings response.");
                    s.Pending[0].complete(true);
                    SteleRequire(s.Setting && s.Pending.Count == 1, "Successful matching response must not emit a second operation.");
                    SteleRequire(onImage.gameObject.activeSelf && !offImage.gameObject.activeSelf,
                        "Original UIToggleObject failed to show only the checked image.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-permission-on.png"));
                    SimulateTeamToggleClick(toggle, camera);
                    SteleRequire(!toggle.isOn && s.Pending.Count == 2 && !s.Pending[1].value && s.Setting,
                        "Original setting changed before the operation response.");
                    s.Pending[1].complete(true);
                    SteleRequire(toggle.isOn && s.Setting && s.Pending.Count == 3 && s.Pending[2].value,
                        "Original response correction must retain Toggle notification semantics.");
                    s.Pending[2].complete(true);
                    SimulateTeamToggleClick(toggle, camera);
                    SteleRequire(s.Pending.Count == 4 && !s.Pending[3].value, "Corrected Toggle lost its original listener.");
                    s.Pending[3].complete(false);
                    SteleRequire(!s.Setting && !toggle.isOn, "Successful unchecked response failed.");
                    SteleRequire(!onImage.gameObject.activeSelf && offImage.gameObject.activeSelf,
                        "Original UIToggleObject failed to show only the unchecked image.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-permission-off.png"));
                    root.SetActive(false);
                    toggle.isOn = true;
                    SteleRequire(s.Pending.Count == 4, "Disabled original permission listener was not detached.");
                    toggle.isOn = false;
                    root.SetActive(true);
                    yield return new WaitForEndOfFrame();
                    SimulateTeamToggleClick(toggle, camera);
                    SteleRequire(s.Pending.Count == 5 && s.Pending[4].value, "Re-enable duplicated or lost the original listener.");
                    s.Pending[4].complete(true);
                    s.Leader = false;
                    toggle.isOn = false;
                    SteleRequire(s.Pending.Count == 5, "Non-leader must not issue a permission operation.");
                    view.RefreshAllow(4);
                    SteleRequire(!root.activeSelf, "Non-leader permission control must be hidden.");
                    UnityEngine.Object.Destroy(canvas);
                    yield return null;
                }
            }
            finally { WorldMultiLanguageText.LanguageFont = previousFont; UnityEngine.Object.Destroy(camera.gameObject); }
        }

        private static void SimulateTeamToggleClick(Toggle expected, Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            var rect = expected.targetGraphic.rectTransform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault().gameObject;
            if (hit == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != expected.gameObject)
                throw new InvalidDataException("Original permission Toggle is occluded/outside its rectangle: " + position
                    + "; hits=" + string.Join(",", hits.Take(6).Select(h => h.gameObject.name)));
            pointer.pointerCurrentRaycast = hits[0]; pointer.pressPosition = position; pointer.pointerPressRaycast = hits[0];
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Transform CreateTeamControlLayout(Transform canvas, out GameObject root)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/12/Source/team-toggle-layout-context.json");
            var data = JsonUtility.FromJson<TeamControlLayout>(source.text);
            SteleRequire(data.nodes.Length == 2 && data.nodes[0].name == "BgDialog1" && data.nodes[1].name == "Content",
                "Original permission parent layout source differs.");
            root = null;
            Transform parent = canvas;
            foreach (var node in data.nodes)
            {
                var go = new GameObject(node.name, typeof(RectTransform));
                if (root == null) { root = go; go.SetActive(false); }
                go.transform.SetParent(parent, false);
                EditorJsonUtility.FromJsonOverwrite(node.rect, go.transform);
                foreach (var component in node.components)
                {
                    Type type;
                    switch (component.type)
                    {
                        case "UGUIJszx.VerticalLayoutGroupJSZX": type = typeof(UGUIJszx.VerticalLayoutGroupJSZX); break;
                        case "UnityEngine.UI.VerticalLayoutGroup": type = typeof(VerticalLayoutGroup); break;
                        case "UnityEngine.UI.ContentSizeFitter": type = typeof(ContentSizeFitter); break;
                        case "UnityEngine.UI.LayoutElement": type = typeof(LayoutElement); break;
                        default: throw new InvalidDataException("Unbound original permission parent layout: " + component.type);
                    }
                    EditorJsonUtility.FromJsonOverwrite(component.fields, go.AddComponent(type));
                }
                parent = go.transform;
            }
            return parent;
        }
    }
}
