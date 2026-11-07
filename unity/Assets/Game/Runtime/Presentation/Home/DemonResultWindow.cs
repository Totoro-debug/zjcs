using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // Owns the native result and its read-only item popup. The settled battle
    // stays visible until native leave; grants remain in the weekly session.
    public sealed class DemonResultWindow : MonoBehaviour, IBattleBossResultHost, IFightThumbsUpUIHost
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class EndDelays { public float normal, round_limit; }
        [Serializable] private sealed class Texts { public TextRow[] texts; public EndDelays end_delays; }
        private HomeFeatureRuntime home;
        private OfficialDemonResultAssets assets;
        private OfficialPartnerFriendshipWindowAssetCatalog windowAssets;
        private PartnerWindowBlurCapture blur;
        private OfflinePartnerActorUiRuntime actors;
        private AppearanceAssetManager manager;
        private OfflinePartnerUiTextureResources textures;
        private IProfessionAppearanceTextureBindings previousTextures;
        private OfflineProfessionNativeSkinClipBindings skinClip;
        private OfflineUiFontInstance font;
        private OfflineProfessionImageCatalog images;
        private OfflineProfessionImageResourceLoader resources;
        private readonly List<IDisposable> scopes = new List<IDisposable>();
        private readonly ResultRenderBindings rt = new ResultRenderBindings();
        private Dictionary<string, string> texts;
        private EndDelays endDelays;
        private IProfessionInfoTarget player;
        private Transform staging, popupParent;
        private Camera camera;
        private string previousCameraTag;
        private bool releasing, closing;
        private Action closed;
        private OfflineDemonResultItemBindings items;
        private WorldWindowBlurMask mask;
        private WorldWindowBlurMask itemMask;
        private GameObject itemDetail;
        private GameObject timeoutRoot;
        public GameObject Panel { get; private set; }
        public GameTimeoutUI TimeoutTip { get; private set; }
        public GameObject ItemDetail => itemDetail;
        public WorldWindowBlurMask ItemMask => itemMask;
        public bool IsReady => Panel && Panel.activeInHierarchy
            && Panel.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show;

        public void Initialize(HomeFeatureRuntime host, Transform parent, FightBattleCheckpoint checkpoint,
            FightBattleReport report, OfflineWeeklyChallengeSession.ChallengeAward award,
            Action onClosed,
            Action<GameObject, GameEndPanelBase, FightBattleCheckpoint, FightBattleReport> bindReport)
        {
            if (home != null) throw new InvalidOperationException("The native result is already initialized.");
            if (checkpoint == null || checkpoint.StageId != 4800 || !checkpoint.Finished
                || checkpoint.Outcome == null || checkpoint.Outcome.Invalid || report == null || report.StageId != 4800)
                throw new ArgumentException("A completed demon battle and its final report are required.");
            home = host ?? throw new ArgumentNullException(nameof(host));
            closed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            popupParent = parent;
            assets = OfficialDemonResultAssets.Load();
            windowAssets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            blur = gameObject.AddComponent<PartnerWindowBlurCapture>();
            blur.Initialize(windowAssets.BlurMaterial);
            gameObject.AddComponent<OfflineProfessionSoundHost>().Bind(assets.Audio);
            if (!FindObjectOfType<AudioListener>()) gameObject.AddComponent<AudioListener>();
            font = new OfflineUiFontInstance(assets.Font);
            var identities = home.Profile.EnsureLocalActorIdentities();
            if (identities.Status != ProgressionResultStatus.Applied && identities.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException("Could not prepare the original result actor identity.");
            var data = JsonUtility.FromJson<Texts>(assets.Bindings.text);
            texts = data.texts.ToDictionary(row => row.key, row => row.text);
            endDelays = data.end_delays ?? throw new InvalidOperationException("Original result delays are unbound.");
            images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
            var starGodImages = OfficialPartnerStarGodAssetCatalog.LoadDefault().CreateImageCatalog();
            try
            {
                images.Entries = home.PartnerRosterAssets.Images.Entries
                    .Concat(Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages").Entries)
                    .Concat(starGodImages.Entries).Concat(home.PartnerGiftAssets.Images.Entries)
                    .GroupBy(row => row.Path).Select(group => group.First()).ToArray();
            }
            finally { Destroy(starGodImages); }
            resources = new OfflineProfessionImageResourceLoader(images);
            var partAssets = Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/Rank/PartAssets");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            manager = new AppearanceAssetManager(Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),
                callback => new LowMemorySubscription(callback));
            var parts = new OfflinePartnerUiPartBindings(partAssets, tables.SpineSkinInfoParser, root => textures.BindTree(root));
            actors = new OfflinePartnerActorUiRuntime(home.Profile, home.DailyDungeonProgression,
                OfficialPartnerAssetCatalog.LoadDefault(), resources, Localize,
                id => throw new NotSupportedException("Result statistics do not navigate to partner management."),
                () => throw new NotSupportedException("Result statistics do not navigate to player management."), parts);
            previousTextures = ProfessionAppearanceTextureBindings.Current;
            textures = new OfflinePartnerUiTextureResources(partAssets, manager, previousTextures);
            ProfessionAppearanceTextureBindings.Current = textures;
            skinClip = new OfflineProfessionNativeSkinClipBindings();
            var inactive = new GameObject("Bind original result before activation");
            inactive.SetActive(false); inactive.transform.SetParent(transform, false); staging = inactive.transform;
            player = actors.PlayerInfo.BuildData(actors.PlayerInfo.MainPlayer).TargetData;
            items = new OfflineDemonResultItemBindings(home, assets, images, InstantiateUi, popupParent,
                ShowItemDetail, CloseItemDetail);
            camera = home.Scene.Camera;
            previousCameraTag = camera.gameObject.tag;
            camera.gameObject.tag = "MainCamera";
            Panel = InstantiateBound(assets.Panel, popupParent, false);
            Panel.name = "GameEndPanelDemonKing";
            mask = CreateMask(Panel);
            StartCoroutine(ShowResult(checkpoint, report, award, bindReport));
        }

        private IEnumerator ShowResult(FightBattleCheckpoint checkpoint, FightBattleReport report,
            OfflineWeeklyChallengeSession.ChallengeAward award,
            Action<GameObject, GameEndPanelBase, FightBattleCheckpoint, FightBattleReport> bindReport)
        {
            var popup = Panel.GetComponent<WorldPopupWindow>();
            popup.SetMaskClickAction(LeaveGame);
            // Selected solo DemonKing is neither a team challenge nor playback.
            bool roundLimit = checkpoint.Outcome.Reason == 3 || checkpoint.Outcome.Reason == 4;
            float delay = roundLimit ? endDelays.round_limit : endDelays.normal;
            if (roundLimit)
            {
                // Local reasons 3/4 exhaust global/player rounds. Neither denotes
                // the distinct official TimeLimit (wall-clock) result.
                var bindings = Resources.Load<TextAsset>("Combat/TimeoutUI/bindings");
                var prefab = Resources.Load<GameObject>("Combat/TimeoutUI/FightMainPanelTimeout");
                if (!bindings || !prefab) throw new InvalidOperationException("Original battle timeout assets are unbound.");
                foreach (var row in JsonUtility.FromJson<Texts>(bindings.text).texts) texts[row.key] = row.text;
                timeoutRoot = InstantiateBound(prefab, popupParent, true);
                TimeoutTip = timeoutRoot.GetComponentInChildren<GameTimeoutUI>(true);
                TimeoutTip.Play(delay, "game_end_roundlimit");
            }
            yield return new WaitForSeconds(delay);
            yield return mask.Prepare(popup, false);
            if (closing || !Panel) yield break;
            Panel.SetActive(true);
            Panel.GetComponent<GameEndPanelDemonKing>().Init(this);
            var actions = Panel.GetComponent<GameEndPanelBase>();
            bindReport(Panel, actions, checkpoint, report);
            actions.InitMsg(new BattleResultData {
                StageId = checkpoint.StageId, StageType = checkpoint.StageType,
                Win = checkpoint.Outcome.Won, Report = report, DamageValue = PlayerDamage(report),
                Players = new[] { new BattleResultPlayer { TargetData = new BattleResultTarget { TargetId = MainPlayerId } } }
            });
            Panel.GetComponent<GameEndPanelDemonKing>().RefreshAward(award == null
                ? Array.Empty<OfficialBattleAwardEntry>()
                : new[] { new OfficialBattleAwardEntry { ItemId = award.ItemId, Count = award.Count } });
            popup.SetCanvasSortingOrder(NextOrder()); popup.Show(); mask.AttachToWindow(popup);
        }

        private static long PlayerDamage(FightBattleReport report)
        {
            var group = FightBattleReportGrouping.Build(report, 1).Players.SingleOrDefault(value => value.TargetId == 1);
            long total = 0;
            if (group != null)
                foreach (var unit in group.Normal.Concat(group.Summon).Concat(group.Pet))
                    total = checked(total + Math.Max(0, unit.DamageDealt));
            return total;
        }
        private int NextOrder() => popupParent.GetComponentsInChildren<Canvas>(true)
            .Select(value => value.sortingOrder).DefaultIfEmpty(0).Max() + 10;
        private string Localize(string key) => texts.TryGetValue(key, out var text) ? text : key;
        public GameObject InstantiateUi(GameObject prefab, Transform parent) => InstantiateBound(prefab, parent, true);
        private GameObject InstantiateBound(GameObject prefab, Transform parent, bool activate)
        {
            var instance = Instantiate(prefab, staging, false);
            instance.SetActive(false); instance.transform.SetParent(parent, false);
            scopes.Add(OfflineUiBindingModule.Register(instance.transform, new OfflineUiBindings {
                Localize = Localize, Images = image => items?.Resources ?? resources,
                Pool = (scope, source, target, keepWorld) => !keepWorld ? InstantiateUi(source, target)
                    : throw new NotSupportedException("Inspect the original pooled UI world-transform branch."),
                SkinClip = new OfflineProfessionCurrentUiSkinClipBindings(skinClip)
            }));
            actors.BindTree(instance); items?.BindTree(instance);
            foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
            { text.Localizer = Localize; text.FontProvider = () => font.Font; text.Refresh(); }
            foreach (var view in instance.GetComponentsInChildren<TempRTUI>(true)) view.Bindings = rt;
            foreach (var sound in instance.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = PostSound;
            foreach (var sound in instance.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = PostSound;
            foreach (var sound in instance.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = PostSound;
            instance.SetActive(activate);
            return instance;
        }
        private static void PostSound(AudioClip clip, GameObject emitter)
        {
            var source = emitter.GetComponent<AudioSource>() ?? emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.PlayOneShot(clip);
        }
        private WorldWindowBlurMask CreateMask(GameObject window)
        {
            var root = Instantiate(windowAssets.BlurMaskPrefab, staging, false);
            root.SetActive(false); root.transform.SetParent(window.transform.parent, false);
            var value = root.GetComponent<WorldWindowBlurMask>(); value.BlurManager = blur; return value;
        }
        private void ShowItemDetail(GameObject window)
        {
            if (itemDetail) throw new InvalidOperationException("A result item detail is already open.");
            itemDetail = window; window.SetActive(false);
            var popup = window.GetComponent<WorldPopupWindow>();
            popup.SetMaskClickAction(() => CloseItemDetail(window));
            itemMask = CreateMask(window); StartCoroutine(PrepareItemDetail(window, popup));
        }
        private IEnumerator PrepareItemDetail(GameObject window, WorldPopupWindow popup)
        {
            yield return itemMask.Prepare(popup, false);
            if (closing || !window || itemDetail != window) yield break;
            window.SetActive(true); popup.SetCanvasSortingOrder(NextOrder()); popup.Show(); itemMask.AttachToWindow(popup);
        }
        private void CloseItemDetail(GameObject window)
        {
            if (window != itemDetail) throw new InvalidOperationException("An unrelated item detail requested close.");
            window.GetComponent<WorldPopupWindow>().Hide(() => {
                ReleaseMask(ref itemMask); Destroy(window); itemDetail = null;
            });
        }
        public void LeaveGame()
        {
            if (closing) return;
            closing = true;
            if (itemDetail) CloseItemDetail(itemDetail);
            Panel.GetComponent<WorldPopupWindow>().Hide(Release);
        }
        public void Release()
        {
            if (releasing) return;
            releasing = true; StopAllCoroutines();
            // Disable native controllers while their pools, render seats and fonts
            // are still alive. Child OnDisable releases its original registrations.
            if (itemDetail) itemDetail.SetActive(false);
            if (Panel) Panel.SetActive(false);
            if (timeoutRoot) timeoutRoot.SetActive(false);
            ReleaseMask(ref itemMask); ReleaseMask(ref mask);
            if (itemDetail) Destroy(itemDetail);
            if (Panel) Destroy(Panel);
            if (timeoutRoot) Destroy(timeoutRoot);
            foreach (var scope in scopes) scope.Dispose();
            scopes.Clear();
            if (ProfessionAppearanceTextureBindings.Current == textures)
                ProfessionAppearanceTextureBindings.Current = previousTextures;
            actors?.Dispose(); manager?.Dispose(); skinClip?.Dispose(); rt.RenderSeats.Dispose();
            items?.Dispose(); font?.Dispose();
            if (images) Destroy(images);
            if (camera && camera.CompareTag("MainCamera")) camera.gameObject.tag = previousCameraTag;
            Destroy(gameObject);
            var callback = closed; closed = null; callback?.Invoke();
        }
        private static void ReleaseMask(ref WorldWindowBlurMask value)
        {
            if (!value) return;
            value.UnattachToWindow(); Destroy(value.gameObject); value = null;
        }
        private void Update() { manager?.Update(); items?.RefreshInventory(); }
        private void OnDestroy() { if (!releasing) Release(); }
        public BattleResultBoss GetBoss(int id) => id == 4800
            ? new BattleResultBoss { Prefab = assets.Boss, StageNameKey = "stage_4800_name" }
            : throw new ArgumentOutOfRangeException(nameof(id));
        public IProfessionInfoTarget ResolvePlayer(int id) => id == 1 ? player : throw new ArgumentOutOfRangeException(nameof(id));
        public long MainPlayerId => player.TargetId;
        public string PlayerName(IProfessionInfoTarget target) => home.Profile.Save.Player.CharacterName;
        public bool IsTeamMember(long id) => false;
        public IFightThumbsUpUIHost ThumbsUp => this;
        public bool IsLimited(int type) => true;
        public int MyCount(int type) => 0;
        public BattleThumbsUpRelative OtherRelative(int type, long id) => BattleThumbsUpRelative.None;
        public bool AlreadyReported(long game, long id) => false;
        public event Action<int> CountChanged { add { } remove { } }
        public event Action<long> Reported { add { } remove { } }
        public void Request(int type, long game, long target, Action success, Action failed) => throw new NotSupportedException();
        public void OpenReport(long target, string name, long game) => throw new NotSupportedException();
        public void SetThumbsUpToTeam(long target) => throw new NotSupportedException();

        private sealed class LowMemorySubscription : IDisposable
        {
            private Action callback;
            public LowMemorySubscription(Action value)
            { callback = value; Application.lowMemory += callback.Invoke; }
            public void Dispose()
            { if (callback == null) return; Application.lowMemory -= callback.Invoke; callback = null; }
        }

        // The selected 3803 prefab directly binds atlas materials. Bloom requests
        // retain native ownership here; composition is still a separate migration.
        private sealed class ResultRenderBindings : IActivityRtUIBindings
        {
            public readonly SpineRTUIManager RenderSeats = new SpineRTUIManager();
            ISpineRTUIManager IActivityRtUIBindings.RenderSeats => RenderSeats;
            private readonly HashSet<IUIBloomRequirer> owners = new HashSet<IUIBloomRequirer>();
            public GameObject InstantiateTarget(GameObject source, Transform parent)
                => BattleNativeMonsterRenderer.CreatePrefab(source, parent, null).gameObject;
            public Material[] GetIndependentMaterials(GameObject instance) => null;
            public void RegisterBloom(IUIBloomRequirer value)
            { if (!owners.Add(value)) throw new InvalidOperationException("Duplicate demon RT bloom owner."); }
            public void UnregisterBloom(IUIBloomRequirer value)
            { if (!owners.Remove(value)) throw new InvalidOperationException("Unbalanced demon RT bloom owner."); }
        }
    }
}
