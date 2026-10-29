using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private const string FullTeamKey = "Zjcs.Map12.FullTeam.Acceptance";
        private static bool fullTeamStarted;
        public static void RunTeamPanel()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                VerifyTeamPanelNativeAssets();
                // Shared project validation passed before this fixture; verify changed native assets above.
                SessionState.SetBool(FullTeamKey, true);
                SessionState.SetInt(FullTeamKey + ".result", 1);
                SessionState.SetFloat(FullTeamKey + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                HookFullTeam(); EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [InitializeOnLoadMethod] private static void RestoreFullTeam()
        { if (SessionState.GetBool(FullTeamKey, false)) HookFullTeam(); }
        private static void HookFullTeam()
        {
            fullTeamStarted = false;
            EditorApplication.update -= TickFullTeam;
            EditorApplication.update += TickFullTeam;
            EditorApplication.playModeStateChanged -= FullTeamModeChanged;
            EditorApplication.playModeStateChanged += FullTeamModeChanged;
        }
        private static void FullTeamModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(FullTeamKey, false)) return;
            SessionState.SetBool(FullTeamKey, false);
            EditorApplication.update -= TickFullTeam;
            EditorApplication.playModeStateChanged -= FullTeamModeChanged;
            EditorApplication.Exit(SessionState.GetInt(FullTeamKey + ".result", 1));
        }
        private static void TickFullTeam()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(FullTeamKey + ".deadline", 0))
            { Debug.LogError("Original full team input verification timed out."); EditorApplication.isPlaying = false; return; }
            if (fullTeamStarted || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            fullTeamStarted = true;
            new GameObject("OriginalFullTeamVerification").AddComponent<Runner>().StartCoroutine(VerifyFullTeam());
        }
        private static IEnumerator VerifyFullTeam()
        {
            var steps = VerifyFullTeamSteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(FullTeamKey + ".result", 0);
            Debug.Log("Map12 original full TeamPanel passed two-size popup animation, source hierarchy, actual raycast buttons, target popup, pooled rows, kick/leave, permission and lifecycle fixtures. Real offline NPC/save/battle integration remains separate.");
            EditorApplication.isPlaying = false;
        }
        private sealed class FullTeamFixture : ITeamPanelServices, ITeamPlayerHpServices,
            IWorldMemberInviteServices, IPlayerPlayServices, IPlayerPlayStateSource, ICharacterPortraitManager
        {
            public readonly TeamIconsFixture IconServices = new TeamIconsFixture();
            public readonly TargetTestServices TargetServices = new TargetTestServices();
            public readonly List<string> Calls = new List<string>();
            public Dictionary<string, string> Texts;
            public TeamPanelTeam Team;
            public bool PrivateMap, GameAllows = true, InviteAllows = true;
            public int LastError;
            public Action Confirmed;
            public readonly CharacterPortraitCache Cache = new CharacterPortraitCache();
            public bool ManagerAvailable => true;
            public bool MainPlayerAvailable => true;
            public long MainPlayerId => 1;
            public int MainPlayerProfession => 10;
            public TeamPanelTeam MainTeam => Team;
            public TeamPanelBriefInfo MainPlayerBriefInfo => Brief(new TeamIconTarget { Id = 1 });
            public bool IsOnlyOnPrivateMap => PrivateMap;
            public bool AllowAcceptJoinTeamWhenGame => GameAllows;
            public bool AllowTeamOperationByGame => GameAllows;
            public bool CheckAllowInvite() => InviteAllows;
            public TeamTargetData RecordedTarget { get; set; }
            public TeamTargetData NoneTarget => new SimpleTarget(TargetServices, TeamTargetCatalog.None);
            public ITeamIconServices Icons => IconServices;
            public ITeamPlayerHpServices Hp => this;
            public ICharacterPortraitManager Portraits => this;
            public ITeamTargetServices Targets => TargetServices;
            public IWorldMemberInviteServices MemberInvite => this;
            public PlayerPlayStateModule PlayerPlayStateModule { get; }
            public FullTeamFixture() { PlayerPlayStateModule = new PlayerPlayStateModule(this); }
            public event Action TeamUpdated, TeamTargetUpdated;
            public event Action<TeamPanelPlayer> TeamPlayerAdded, TeamPlayerRemoved;
            public event Action<long, long> TeamLeaderUpdated;
            public int Subscribers => new Delegate[] { TeamUpdated, TeamTargetUpdated, TeamPlayerAdded, TeamPlayerRemoved, TeamLeaderUpdated }.Sum(e => e?.GetInvocationList().Length ?? 0);
            public TeamPanelBriefInfo Brief(IProfessionInfoTarget target) => new TeamPanelBriefInfo
                { Id = target.TargetId, TargetData = target, Level = 5, SubRank = 3,
                  ActiveSkills = new Dictionary<int, object>(), ActiveSkillSlotLevel = _ => 0 };
            public Task<TeamPanelBriefInfo> GetPlayerBriefInfo(IProfessionInfoTarget target, bool invokeByPlayer)
            { SteleRequire(!invokeByPlayer, "Original team add brief query flag differs."); Calls.Add("brief:" + target.TargetId); return Task.FromResult(Brief(target)); }
            public void QueryTargets(Action<List<TeamTargetData>, int> complete)
                => complete(new List<TeamTargetData> { new TeamSteleTargetData(TargetServices, 1813, 2530), NoneTarget }, 0);
            public void UpdateTeamTarget(TeamTargetData target, Action<TeamTargetData> complete)
            { Calls.Add("target"); if (Team != null) Team.Target = target; complete(target); TeamTargetUpdated?.Invoke(); }
            public void OpenInvite(TeamPanelInviteType page, TeamTargetData target, List<long> excludedPlayers = null)
            { Calls.Add("invite:" + page); SteleRequire(excludedPlayers == null, "Original full team invite excludes must be null."); }
            public void OpenAssemble(TeamTargetData target, List<int> professions) { Calls.Add("assemble:" + string.Join(",", professions)); }
            public void OpenNearby(TeamTargetData target, List<TeamTargetData> targets) { Calls.Add("nearby"); }
            public void Close(GameObject panel) { Calls.Add("close"); panel.SetActive(false); }
            public void Confirm(Component owner, string key, Action confirmed) { Calls.Add(key); Confirmed = confirmed; }
            public void OperPlayerTeamDismiss() { Calls.Add("dismiss"); Team = null; TeamUpdated?.Invoke(); }
            public void OperPlayerTeamKick(long id)
            { Calls.Add("kick:" + id); var player = Team.GetPlayer(id); Team.Players.Remove(player); TeamPlayerRemoved?.Invoke(player); }
            public void OperPlayerTeamLeave() { Calls.Add("leave"); }
            public void ShowError(int code, bool defaultTip) { LastError = code; SteleRequire(!defaultTip, "Original team guard default tip differs."); }
            public void Error(string message) { throw new InvalidDataException(message); }
            public void SystemMessage(string text) { Calls.Add("message:" + text); }
            public string Localize(string key) => Texts.TryGetValue(key, out string text) ? text : key;
            public string Format(string key, params object[] args) => string.Format(Localize(key), args);
            public void RecycleSkillIcons(UnityEngine.Object owner) { Calls.Add("recycle-skills"); }
            public int ActiveSkillSlotsCount => 6;
            public int MaxSkillSlotBaseLevel(int subRank) => throw new InvalidDataException("No skills in fixture.");
            public ITeamPanelSkillIcon GetSkillIcon(object item, Transform parent, UnityEngine.Object owner) => throw new InvalidDataException("No skills in fixture.");
            public int MapCount => 1;
            public float HpPercent => .75f;
            public SafeAction OnPlayerUpdate { get; } = new SafeAction();
            public event Action<float> HpUpdated;
            public bool IsLeader => Team == null || Team.TeamLeaderId == MainPlayerId;
            public bool PlayerSettingsAllowMemberInvite { get; set; } = true;
            public void OperPlayerAllowMemberInvite(bool allow, Action<bool> complete) { Calls.Add("allow:" + allow); complete(allow); }
            IPlayerPlayStateSource IPlayerPlayServices.MainPlayer => this;
            public IEnumerable<IPlayerPlayState> GetStates() => Array.Empty<IPlayerPlayState>();
            public PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState state, IPlayerPlayAction action, IPlayerPlayActionEnv env)
                => throw new InvalidDataException("No active state in this panel fixture.");
            public void Notify(PlayerPlayText text) { Calls.Add("play-notify:" + text.Key); }
            public CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor requestor) => Cache;
            public void ClearAllCharacterPortraitPersistenceData() => throw new InvalidDataException("Fixture never clears real portrait persistence.");
            public void AddMember(long id)
            {
                var player = new TeamPanelPlayer { PlayerId = id, Profession = 10, TargetData = new TeamIconTarget { Id = id, Npc = true } };
                Team.Players.Add(player); TeamPlayerAdded?.Invoke(player);
            }
            public void ChangeLeader(long id) { long old = Team.TeamLeaderId; Team.TeamLeaderId = id; TeamLeaderUpdated?.Invoke(id, old); }
        }
        private static void SimulateFullTeamClick(Button expected, Camera camera, Vector2? point = null)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)expected.transform;
            Vector2 position = point ?? RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            SteleRequire(rect.rect.width > 0 && rect.rect.height > 0 && expected.IsActive() && expected.IsInteractable(),
                "Original team button has no active native hit rectangle: " + expected.name);
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            var hit = hits.FirstOrDefault().gameObject;
            SteleRequire(hit != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == expected.gameObject,
                "Original team button is occluded/outside its native hit rectangle: " + expected.name + "; hits=" + string.Join(",", hits.Take(6).Select(h => h.gameObject.name)));
            pointer.pointerCurrentRaycast = hits[0]; pointer.pressPosition = position; pointer.pointerPressRaycast = hits[0];
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static IEnumerator VerifyFullTeamSteps()
        {
            VerifyInviteAnimationRuntimeSamples();
            using var nativeInviteAnimations = new Zjcs.Offline.World.NativeMap12InviteAnimations();
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            SteleRequire(!string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(repository)
                && Path.GetFullPath(output).StartsWith(Path.GetFullPath(Path.Combine(repository, "output/validation"))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Use owned full team validation workspace.");
            Application.runInBackground = true;
            foreach (var startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (var boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.name.StartsWith("BootSplash")))
                UnityEngine.Object.Destroy(boot.gameObject);
            yield return null;
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text);
            var camera = new GameObject("OriginalFullTeamCamera").AddComponent<Camera>();
            camera.backgroundColor = Color.black; camera.gameObject.AddComponent<AudioListener>();
            if (EventSystem.current == null) new GameObject("FullTeamSimulatedInput", typeof(EventSystem));
            var previousPool = ProfessionGameObjectPoolBridge.Provider;
            var previousFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset");
            try
            {
                foreach (int width in new[] { 450, 720 })
                {
                    SetUiGameViewSize(width, width == 450 ? 800 : 1280);
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (Screen.width != width) { SteleRequire(Time.realtimeSinceStartup < deadline, "Full team portrait resize timed out."); yield return null; }
                    var s = new FullTeamFixture { Texts = new Dictionary<string, string>() };
                    foreach (string name in new[] { "texts", "target-texts", "panel-texts" })
                        foreach (var text in JsonUtility.FromJson<TeamControlTexts>(Resources.Load<TextAsset>("Map12/Team/" + name).text).texts) s.Texts[text.key] = text.value;
                    s.TargetServices.Texts = s.Texts;
                    var defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Official/World/Map/12/UI/Team/Sprites/DefaultPortrait-679de31627.png");
                    SteleRequire(defaultTexture != null, "Original portrait loading texture is unavailable.");
                    s.Cache.UpdateTexture(new CharacterPortraitTexture2D(defaultTexture, defaultTexture.width, defaultTexture.height, true), -1, false, out _);
                    ProfessionGameObjectPoolBridge.Provider = (_, prefab, parent, stays) =>
                    {
                        var clone = UnityEngine.Object.Instantiate(prefab, parent, stays);
                        nativeInviteAnimations.Bind(clone);
                        foreach (var text in clone.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = s.Localize;
                        return clone;
                    };
                    var canvas = new GameObject("OriginalFullTeamCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    ConfigureCanvas(canvas, camera, data);
                    var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/Team/TeamPanel"), canvas.transform, false);
                    root.SetActive(false);
                    nativeInviteAnimations.Bind(root);
                    foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = s.Localize;
                    var panel = root.GetComponentInChildren<TeamPanel>(true);
                    panel.Configure(s);
                    panel.Init(null, TeamPanelInviteType.Player, new TeamSteleTargetData(s.TargetServices, 1813, 2530), null, true);
                    root.SetActive(true);
                    var popup = root.GetComponent<WorldPopupWindow>(); popup.Show();
                    yield return new WaitForSeconds(popup.mShowClip.length + .1f);
                    Canvas.ForceUpdateCanvases();
                    var rows = TeamTargetField<List<TeamPanelItem>>(panel, "mMemberItemList");
                    SteleRequire(rows.Count == 3 && s.Subscribers == 5, "Original full team solo row count/subscriptions differ.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-panel-solo.png"));
                    SimulateFullTeamClick(TeamTargetField<Button>(panel, "mInviteBtn"), camera);
                    SteleRequire(s.Calls.Last() == "invite:Player", "Original invite button dispatch differs.");
                    var permission = TeamTargetField<WorldAllowMemberInviteToggle>(panel, "mAllowMemberInviteToggle");
                    var permissionToggle = TeamTargetField<Toggle>(permission, "mAllowToggle");
                    SimulateTeamToggleClick(permissionToggle, camera);
                    SteleRequire(!s.PlayerSettingsAllowMemberInvite, "Original full panel permission click did not persist its response.");
                    SimulateTeamToggleClick(permissionToggle, camera);
                    SteleRequire(s.PlayerSettingsAllowMemberInvite, "Original full panel permission click did not restore original value.");
                    s.GameAllows = false;
                    SimulateFullTeamClick(TeamTargetField<Button>(panel, "mInviteBtn"), camera);
                    SteleRequire(s.LastError == 1169, "Original invite game guard differs."); s.GameAllows = true;
                    s.InviteAllows = false;
                    SimulateFullTeamClick(TeamTargetField<Button>(rows[1], "mAddMemberBtn"), camera);
                    SteleRequire(s.LastError == 6168, "Original add-member permission guard differs."); s.InviteAllows = true;
                    SimulateFullTeamClick(TeamTargetField<Button>(panel, "mTargetBtn"), camera);
                    yield return null;
                    var target = TeamTargetField<TeamPanelTarget>(panel, "mTeamPanelTarget");
                    SteleRequire(target.gameObject.activeSelf, "Original full panel target chooser failed to open.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-panel-target.png"));
                    SimulateFullTeamClick(TeamTargetField<Button>(target, "mHideBtn"), camera, new Vector2(Screen.width * .05f, Screen.height * .5f));
                    SteleRequire(!target.gameObject.activeSelf, "Original full panel target chooser failed to hide.");
                    root.SetActive(false);
                    SteleRequire(s.Subscribers == 0, "Original full team disable leaked manager listeners.");
                    s.Team = new TeamPanelTeam { TeamLeaderId = 1, Target = s.RecordedTarget };
                    s.Team.Players.Add(new TeamPanelPlayer { PlayerId = 1, Profession = 10, TargetData = new TeamIconTarget { Id = 1 } });
                    panel.Init(s.Team, TeamPanelInviteType.Player, s.RecordedTarget, null, false);
                    root.SetActive(true); popup.Show(); yield return new WaitForSeconds(popup.mShowClip.length + .1f);
                    s.AddMember(42); yield return null;
                    SteleRequire(s.Calls.Contains("brief:42") && rows.Count == 3 && s.Subscribers == 5, "Original live member update/pool differs.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-panel-member.png"));
                    var operations = TeamTargetField<TeamPanelItemButton>(rows[1], "mPanelItemButton");
                    SimulateFullTeamClick(TeamTargetField<Button>(operations, "mBtnKickout"), camera);
                    yield return null;
                    SteleRequire(s.Team.Players.Count == 1 && s.Calls.Contains("kick:42"), "Original native kick button failed.");
                    s.AddMember(42); yield return null;
                    s.ChangeLeader(42); yield return null;
                    var selfOperations = TeamTargetField<TeamPanelItemButton>(rows[0], "mPanelItemButton");
                    SimulateFullTeamClick(TeamTargetField<Button>(selfOperations, "mBtnLeave"), camera);
                    SteleRequire(s.Calls.Contains("leave"), "Original member leave must dispatch directly without dismiss confirmation.");
                    s.ChangeLeader(1); yield return null;
                    s.AddMember(43); yield return null;
                    SteleRequire(!TeamTargetField<Button>(panel, "mInviteBtn").gameObject.activeSelf
                        && !TeamTargetField<Button>(panel, "mNeighborhoodBtn").gameObject.activeSelf
                        && !TeamTargetField<Button>(panel, "mAssembleBtn").gameObject.activeSelf,
                        "Original full-team invite/nearby/assemble visibility differs.");
                    yield return new WaitForEndOfFrame();
                    CaptureSteleUiFrame(Path.Combine(output, width + "-team-panel-full.png"));
                    SimulateFullTeamClick(TeamTargetField<Button>(panel, "mDismissBtn"), camera);
                    SteleRequire(s.Confirmed != null && s.Calls.Contains("TeamDismiss.ConfirmTip") && !s.Calls.Contains("dismiss"), "Original dismiss must request confirmation before executing.");
                    s.Confirmed(); yield return null;
                    SteleRequire(s.Team == null && !TeamTargetField<Button>(panel, "mDismissBtn").gameObject.activeSelf, "Original dismiss update failed.");
                    popup.Hide(null); yield return new WaitForSeconds(popup.mHideClip.length + .1f);
                    SteleRequire(!root.activeSelf && s.Subscribers == 0 && s.Cache.Requestors.Count == 0, "Original hide leaked team listeners/portrait requests.");
                    UnityEngine.Object.Destroy(canvas); yield return null; s.IconServices.Info.Cleanup();
                }
            }
            finally { ProfessionGameObjectPoolBridge.Provider = previousPool; WorldMultiLanguageText.LanguageFont = previousFont; UnityEngine.Object.Destroy(camera.gameObject); }
        }
    }
}
