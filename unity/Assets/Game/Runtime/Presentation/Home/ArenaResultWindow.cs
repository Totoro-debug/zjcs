using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // A result is a view of an already committed local transaction. Reopening
    // it or its statistics never grants rewards or changes arena rank again.
    public sealed class ArenaResultWindow : MonoBehaviour, IArenaResultHost
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class EndDelays { public float normal, round_limit; }
        [Serializable] private sealed class Texts { public TextRow[] texts; public EndDelays end_delays; }
        private HomeFeatureRuntime home;
        private OfficialArenaResultAssets assets;
        private OfficialPartnerFriendshipWindowAssetCatalog windowAssets;
        private PartnerWindowBlurCapture blur;
        private WorldWindowBlurMask mask;
        private OfflineUiFontInstance font;
        private OfflineProfessionImageCatalog images;
        private OfflineProfessionImageResourceLoader resources;
        private AppearanceAssetManager manager;
        private OfflinePartnerActorUiRuntime actors;
        private OfflinePartnerUiTextureResources textures;
        private IProfessionAppearanceTextureBindings previousTextures;
        private OfflineProfessionNativeSkinClipBindings skinClip;
        private readonly List<IDisposable> scopes = new List<IDisposable>();
        private Dictionary<string, string> texts;
        private EndDelays delays;
        private Transform staging, parent;
        private Camera camera;
        private string previousCameraTag;
        private Action closed;
        private Action<CombatRatingPromoteWayType> promotion;
        private CombatRatingPromoteWayType nextPromotion;
        private bool closing, releasing;
        public GameObject Panel { get; private set; }
        public GameTimeoutUI TimeoutTip { get; private set; }
        private GameObject timeoutRoot;
        public PlayerInfoData Opponent { get; private set; }
        public ArenaFightResult Settlement { get; private set; }
        public bool IsReady => Panel && Panel.activeInHierarchy
            && Panel.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show;

        public void Initialize(HomeFeatureRuntime value, Transform popupParent, ArenaChallengeResult result,
            FightBattleCheckpoint checkpoint, FightBattleReport report, Action onClosed,
            Action<CombatRatingPromoteWayType> navigate,
            Action<GameObject, GameEndPanelBase, FightBattleCheckpoint, FightBattleReport> bindReport)
        {
            if (home != null) throw new InvalidOperationException("Arena result is already initialized.");
            if (result == null || result.awardCount < 0 || (!result.fastVictory
                && (checkpoint == null || !checkpoint.Finished || checkpoint.Outcome == null
                    || checkpoint.Outcome.Invalid || report == null || report.StageId != checkpoint.StageId
                    || result.won != checkpoint.Outcome.Won)))
                throw new ArgumentException("A settled arena challenge and its actual final report are required.");
            home = value ?? throw new ArgumentNullException(nameof(value));
            parent = popupParent; closed = onClosed; promotion = navigate;
            assets = OfficialArenaResultAssets.Load();
            windowAssets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            blur = gameObject.AddComponent<PartnerWindowBlurCapture>(); blur.Initialize(windowAssets.BlurMaterial);
            gameObject.AddComponent<OfflineProfessionSoundHost>().Bind(assets.Audio);
            if (!FindObjectOfType<AudioListener>()) gameObject.AddComponent<AudioListener>();
            font = new OfflineUiFontInstance(assets.Font);
            var identity = home.Profile.EnsureLocalActorIdentities();
            if (identity.Status != ProgressionResultStatus.Applied && identity.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException("Could not prepare the arena result actor identity.");
            var data = JsonUtility.FromJson<Texts>(assets.Bindings.text);
            texts = data.texts.ToDictionary(row => row.key, row => row.text);
            delays = data.end_delays ?? throw new InvalidOperationException("Original arena result timing is unbound.");
            images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
            images.Entries = home.PartnerRosterAssets.Images.Entries
                .Concat(Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages").Entries)
                .GroupBy(row => row.Path).Select(group => group.First()).ToArray();
            resources = new OfflineProfessionImageResourceLoader(images);
            var partAssets = Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/Rank/PartAssets");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            manager = new AppearanceAssetManager(Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),
                callback => new LowMemorySubscription(callback));
            var parts = new OfflinePartnerUiPartBindings(partAssets, tables.SpineSkinInfoParser, root => textures.BindTree(root));
            actors = new OfflinePartnerActorUiRuntime(home.Profile, home.DailyDungeonProgression,
                OfficialPartnerAssetCatalog.LoadDefault(), resources, Localize,
                _ => throw new NotSupportedException("Arena result has no NPC management control."),
                () => throw new NotSupportedException("Arena result has no player management control."), parts);
            previousTextures = ProfessionAppearanceTextureBindings.Current;
            textures = new OfflinePartnerUiTextureResources(partAssets, manager, previousTextures);
            ProfessionAppearanceTextureBindings.Current = textures;
            skinClip = new OfflineProfessionNativeSkinClipBindings();
            var inactive = new GameObject("Bind original arena result before activation");
            inactive.SetActive(false); inactive.transform.SetParent(transform, false); staging = inactive.transform;
            Opponent = BuildOpponent(result);
            Settlement = new ArenaFightResult { PreviousRank = result.previousRank,
                CurrentRank = result.currentRank, AwardCount = result.awardCount, HasOutputAddition = false };
            camera = home.Scene.Camera; previousCameraTag = camera.gameObject.tag; camera.gameObject.tag = "MainCamera";
            Panel = InstantiateBound(assets.Panel, parent, false); Panel.name = "GameEndPanelArena";
            Panel.GetComponent<GameEndPanelArena>().Bind(this);
            foreach (var item in Panel.GetComponentsInChildren<CombatRatingPromoteWayItem>(true)) item.Navigate = Promote;
            var maskRoot = Instantiate(windowAssets.BlurMaskPrefab, staging, false);
            maskRoot.SetActive(false); maskRoot.transform.SetParent(parent, false);
            mask = maskRoot.GetComponent<WorldWindowBlurMask>(); mask.BlurManager = blur;
            StartCoroutine(Show(result, checkpoint, report, bindReport));
        }

        private PlayerInfoData BuildOpponent(ArenaChallengeResult result)
        {
            var guard = home.Arena.FindGuard(result.guardTemplateId)
                ?? throw new InvalidOperationException("Settled original arena guard template is absent.");
            var appearance = new AppearanceData();
            var creation = CharacterCreationAssetCatalog.LoadDefault();
            foreach (var skin in creation.GetDefaultSkinInfos(result.guardSex, guard.profession))
                appearance.SkinMap.Add((SkinType)skin.Type, new SkinData { Type = (SkinType)skin.Type, SkinId = skin.Id });
            var append = guard.appendAppearances.Single(row => row.sex == result.guardSex);
            foreach (var skin in append.skins) appearance.AppendAppearanceDict.Add((SkinType)skin.skinType, skin.skinId);
            string name = Localize(guard.nameKey);
            string icon = Convert.ToString(home.DailyDungeonProgression.Catalog.Rows("game_settings").First()["DefPlayerIcon"]);
            return new PlayerInfoData {
                TargetData = new GuardTarget(guard.id), Icon = icon, Name = name,
                NameArg = actors.PlayerInfo.CreateNameArg(name, true),
                CombatRating = ArenaBattleRunner.CalculateGuardCombatRating(home, guard), SubRank = guard.subRank,
                Level = guard.level, Profession = guard.profession, Sex = (Sex)result.guardSex, AppearanceData = appearance
            };
        }

        public void BindReportIdentity(OfficialBattleReportView.Presentation presentation,
            CharacterSkillPanel adapter, GameObject lifetime, FightBattleCheckpoint checkpoint)
        {
            var result = home.Arena.LastResult;
            var guard = home.Arena.FindGuard(result.guardTemplateId);
            var skins = guard.appendAppearances.Single(row => row.sex == result.guardSex).skins
                .ToDictionary(row => row.skinType, row => row.skinId);
            var portraits = OfficialBattleCharacterPortraits.ForProfile(lifetime, home.DailyDungeonProgression,
                home.ActiveProfileSlotId, home.SavesRoot, (unit, excludes) => unit.Id == 2
                    ? Zjcs.Presentation.CharacterCreation.CharacterCreationNativeModelFactory.CreateArenaGuardModel(
                        result.guardSex, guard.profession, skins, excludes) : null);
            presentation.PlayerName = id => id == 1 ? home.Profile.Save.Player.CharacterName
                : id == 2 ? Opponent.Name : throw new InvalidOperationException("Unknown arena report owner: " + id);
            presentation.PlayerIcon = (icon, id) =>
            {
                if (id == 1) { adapter.BindReportPlayerIcon(icon); return; }
                var unit = checkpoint.Units.Single(value => value.Id == id);
                var portrait = portraits(unit)
                    ?? throw new InvalidOperationException("The original guard report portrait is unbound.");
                OfficialBattleReportIdentityView.Bind(icon, new OfficialBattleReportIdentityView.Identity {
                    Name = Opponent.Name, Profession = guard.profession, PlayerPortrait = portrait.texture,
                    ProfessionIcon = OfficialCharacterAssetCatalog.LoadDefault().FindSprite(
                        home.DailyDungeonProgression.Catalog.ProfessionIconUri(guard.profession))
                });
            };
        }

        private IEnumerator Show(ArenaChallengeResult result, FightBattleCheckpoint checkpoint, FightBattleReport report,
            Action<GameObject, GameEndPanelBase, FightBattleCheckpoint, FightBattleReport> bindReport)
        {
            var popup = Panel.GetComponent<WorldPopupWindow>(); popup.SetMaskClickAction(LeaveGame);
            bool timeout = checkpoint?.Outcome != null && (checkpoint.Outcome.Reason == 3 || checkpoint.Outcome.Reason == 4);
            float delay = timeout ? delays.round_limit : delays.normal;
            if (timeout)
            {
                var payload = Resources.Load<TextAsset>("Combat/TimeoutUI/bindings");
                foreach (var row in JsonUtility.FromJson<Texts>(payload.text).texts) texts[row.key] = row.text;
                timeoutRoot = InstantiateBound(Resources.Load<GameObject>("Combat/TimeoutUI/FightMainPanelTimeout"), parent, true);
                TimeoutTip = timeoutRoot.GetComponentInChildren<GameTimeoutUI>(true);
                TimeoutTip.Play(delay, "game_end_roundlimit");
            }
            yield return new WaitForSeconds(delay);
            yield return mask.Prepare(popup, false);
            if (closing) yield break;
            var actions = Panel.GetComponent<GameEndPanelBase>();
            bindReport(Panel, actions, checkpoint, report);
            Panel.SetActive(true);
            actions.InitMsg(new BattleResultData {
                StageId = home.Arena.Catalog.settings.StageId, StageType = 28,
                FastVictory = result.fastVictory, Win = result.won, Report = report,
                Players = Array.Empty<BattleResultPlayer>()
            });
            popup.SetCanvasSortingOrder(parent.GetComponentsInChildren<Canvas>(true)
                .Select(item => item.sortingOrder).DefaultIfEmpty(0).Max() + 10);
            popup.Show(); mask.AttachToWindow(popup);
        }
        private string Localize(string key) => texts.TryGetValue(key, out var text) ? text
            : home.Catalog.LocalizedText(key, null) ?? key;
        private GameObject InstantiateBound(GameObject prefab, Transform target, bool active)
        {
            var instance = Instantiate(prefab, staging, false); instance.SetActive(false);
            instance.transform.SetParent(target, false);
            scopes.Add(OfflineUiBindingModule.Register(instance.transform, new OfflineUiBindings {
                Localize = Localize, Images = _ => resources,
                Pool = (_, source, owner, world) => !world ? InstantiateBound(source, owner, true)
                    : throw new NotSupportedException("Inspect the original arena pooled world-transform branch."),
                SkinClip = new OfflineProfessionCurrentUiSkinClipBindings(skinClip)
            }));
            actors.BindTree(instance);
            foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
            { text.Localizer = Localize; text.FontProvider = () => font.Font; text.Refresh(); }
            foreach (var sound in instance.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = PostSound;
            foreach (var sound in instance.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = PostSound;
            foreach (var sound in instance.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = PostSound;
            instance.SetActive(active); return instance;
        }
        private static void PostSound(AudioClip clip, GameObject emitter)
        {
            var source = emitter.GetComponent<AudioSource>() ?? emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.PlayOneShot(clip);
        }
        public void OpenOpponent() => throw new NotSupportedException("The selected result icons have no Btn binding.");
        private void Promote(CombatRatingPromoteWayType type) { nextPromotion = type; LeaveGame(); }
        public void LeaveGame()
        {
            if (closing) return;
            closing = true; Panel.GetComponent<WorldPopupWindow>().Hide(Release);
        }
        public void Release()
        {
            if (releasing) return;
            releasing = true; StopAllCoroutines();
            if (Panel) Panel.SetActive(false);
            if (timeoutRoot) timeoutRoot.SetActive(false);
            if (mask) { mask.UnattachToWindow(); Destroy(mask.gameObject); }
            if (Panel) Destroy(Panel);
            if (timeoutRoot) Destroy(timeoutRoot);
            foreach (var scope in scopes) scope.Dispose(); scopes.Clear();
            if (ProfessionAppearanceTextureBindings.Current == textures) ProfessionAppearanceTextureBindings.Current = previousTextures;
            actors?.Dispose(); manager?.Dispose(); skinClip?.Dispose(); font?.Dispose();
            if (images) Destroy(images);
            if (camera && camera.CompareTag("MainCamera")) camera.gameObject.tag = previousCameraTag;
            Destroy(gameObject);
            var callback = closed; closed = null; callback?.Invoke();
            if (nextPromotion != CombatRatingPromoteWayType.None) promotion?.Invoke(nextPromotion);
        }
        private void Update() => manager?.Update();
        private void OnDestroy() { if (!releasing) Release(); }
        private sealed class GuardTarget : IProfessionInfoTarget
        {
            public GuardTarget(long id) { TargetId = id; }
            public ActorTargetType TargetType => ActorTargetType.Player;
            public long TargetId { get; }
            public long ServerId => 0;
            public bool IsNpc => false;
            public string GetFigurePath(Sex sex) => throw new NotSupportedException("Selected arena result does not bind PlayerFigure.");
            public bool CheckActorCanAssist(int type) => false;
        }
        private sealed class LowMemorySubscription : IDisposable
        {
            private Action callback;
            public LowMemorySubscription(Action value) { callback = value; Application.lowMemory += callback.Invoke; }
            public void Dispose() { if (callback == null) return; Application.lowMemory -= callback.Invoke; callback = null; }
        }
    }
}
