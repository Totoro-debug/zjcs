using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        // The grant is produced by the real zero-step PartEnd transaction;
        // this fixture only displays that persisted result.
        private static IEnumerator ExerciseDemonResultPanel(HomeFeatureRuntime home,
            FightBattleCheckpoint checkpoint, FightBattleReport report, string output,
            OfflineWeeklyChallengeSession.ChallengeAward award)
        {
            var assets = OfficialDemonResultAssets.Load();
            Require(assets.Audio && assets.Audio.Events.Select(value => value.EventId).OrderBy(value => value)
                .SequenceEqual(new uint[] { 662330091, 3406024700, 3406024701, 3406024702, 3772241779 }),
                "Original demon result sound events are unbound.");
            var previousFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = null;
            using var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>(
                "Combat/OfficialBattleRenderPipelineAssets").Enter();
            // Isolate the native result from the restored home's independent
            // overlay canvases. The production window-container integration is
            // covered separately; this fixture does not redesign their layers.
            var previousCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>()
                .Where(value => value.enabled).ToArray();
            foreach (var previous in previousCanvases) previous.enabled = false;
            var fixture = new GameObject("Original demon result panel fixture", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = fixture.GetComponent<Canvas>();
            if (!UnityEngine.Object.FindObjectOfType<AudioListener>()) fixture.AddComponent<AudioListener>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // TempRTUI renders from the original MainCamera callback. The battle
            // camera has already been disposed when this settlement is opened.
            var camera = new GameObject("Demon result fixture camera", typeof(Camera));
            camera.transform.SetParent(fixture.transform, false); camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = Color.black;
            camera.GetComponent<Camera>().cullingMask = 0;
            camera.GetComponent<Camera>().depth = 100;
            using var bindings = new DemonResultPanelFixture(home, assets, fixture.transform);
            try
            {
                var content = bindings.InstantiateUi(assets.Panel, fixture.transform);
                bindings.Panel = content;
                var panel = content.GetComponent<GameEndPanelDemonKing>();
                var actions = content.GetComponent<GameEndPanelBase>();
                panel.Init(bindings); actions.Bind(bindings);
                actions.InitMsg(new BattleResultData {
                    StageId = checkpoint.StageId, StageType = checkpoint.StageType,
                    Win = checkpoint.Outcome.Won, Report = report, DamageValue = 0,
                    Players = new[] { new BattleResultPlayer { TargetData = new BattleResultTarget {
                        TargetId = bindings.MainPlayerId } } }
                });
                panel.RefreshAward(award == null ? Array.Empty<OfficialBattleAwardEntry>() : new[] {
                    new OfficialBattleAwardEntry { ItemId = award.ItemId, Count = award.Count }
                });
                var window = content.GetComponent<WorldPopupWindow>();
                if (window.Canvas) window.SetCanvasSortingOrder(5000);
                window.Show();
                yield return new WaitForSeconds(2);
                var rows = content.GetComponentsInChildren<GameEndPlayerSettleItem>(true);
                Require(rows.Length == 1 && rows[0].gameObject.activeInHierarchy
                    && rows[0].PlayerId == bindings.MainPlayerId && !rows[0].IsNpc,
                    "The full demon result did not bind its native self statistics row.");
                foreach (var info in rows[0].GetComponentsInChildren<PlayerInfoUI>())
                    if (info.ProfessionIcon && info.ProfessionIcon.gameObject.activeInHierarchy)
                        Require(info.ProfessionIcon.sprite, "Original settlement profession sprite is unbound.");
                Require(panel.GameEndPanelBoss.BossNameText.Text == bindings.Localize("stage_4800_name"),
                    "The full demon result has a different boss name.");
                Require(panel.GameEndPanelBoss.DamageText.Text.Contains("0"),
                    "The full demon result did not display zero-damage settlement.");
                Require(bindings.Rt.Model && bindings.Rt.Owners.Count == 1,
                    "The full demon result did not acquire its native RT model.");
                var rt = content.GetComponentInChildren<Zjcs.Official.ActivityBattleUI.TempRTUI>();
                Require(rt.RawImage.uvRect == new Rect(0, 0, 1, 1), "Full result RT UV differs.");
                var corners = new Vector3[4]; rt.RawImage.rectTransform.GetWorldCorners(corners);
                Debug.Log("[Demon result panel] RT rect=" + rt.RawImage.rectTransform.rect
                    + " corners=" + string.Join(";", corners.Select(value => value.ToString()))
                    + " leave=" + panel.LeaveGameBtn.transform.position
                    + " root=" + ((RectTransform)content.transform).rect);
                foreach (var label in content.GetComponentsInChildren<TMP_Text>())
                    Require(label.font && label.font.HasCharacters(label.text, out _, false, true),
                        "The original result font is missing displayed characters: " + label.name);
                yield return Capture(output, award == null ? "demon-result-full-no-award" : "demon-result-first-award");
                yield return Combat.BattleGameViewInput.Click(actions.ReportButton);
                Require(bindings.ReportClicks == 1, "The original result report button did not route input.");
                if (award != null)
                {
                    float deadline = Time.realtimeSinceStartup + 20;
                    ItemAwardIcon reward;
                    while (!(reward = content.GetComponentInChildren<ItemAwardIcon>()) || !reward.ItemIcon)
                    {
                        Require(Time.realtimeSinceStartup < deadline, "Original delayed challenge award icon did not appear.");
                        yield return null;
                    }
                    var icon = reward.ItemIcon;
                    Require(icon.myInfo.ClassId == 41021 && icon.myText.Text.Contains("300")
                        && icon.myBottomText.Text == bindings.Localize("item_41021_name")
                        && icon.IconSprite == assets.RewardIcon && icon.IconGameObject.activeInHierarchy,
                        "The original challenge award name/count did not bind.");
                    long inventory = home.ItemCount(41021);
                    yield return Combat.BattleGameViewInput.Click(icon.Button);
                    yield return new WaitForSeconds(1);
                    Require(bindings.ItemDetailContent && bindings.ItemDetailContent.activeInHierarchy,
                        "The original reward click did not open ItemDetailDialog.");
                    var detail = bindings.ItemDetailContent.GetComponent<ItemDetailDialog>();
                    Require(detail.ItemInfo.ClassId == 41021
                        && detail.NameText.Text == bindings.Localize("item_41021_name")
                        && detail.TypeText.Text == bindings.Localize("ItemType.MonsterProduct")
                        && detail.ItemImage.sprite == assets.RewardIcon && detail.QualityBg.sprite == assets.DetailQualityBackground
                        && detail.ItemCountGo.activeSelf && detail.ItemCountText.Text.Contains(inventory.ToString())
                        && !detail.ItemSourceObj.activeSelf && home.ItemCount(41021) == inventory,
                        "Original item details differ from the committed reward inventory/configuration.");
                    yield return Capture(output, "demon-result-award-item-detail");
                    Require(bindings.ItemDetailMask && bindings.ItemDetailMask.AttachWindow
                        == bindings.ItemDetailContent.GetComponent<WorldPopupWindow>(),
                        "Original main-container blur mask did not attach to the item detail.");
                    yield return Combat.BattleGameViewInput.Click(bindings.ItemDetailMask.Button);
                    yield return new WaitForSeconds(1);
                    Require(!bindings.ItemDetailContent && !bindings.ItemDetailMask
                        && home.ItemCount(41021) == inventory && panel.gameObject.activeInHierarchy,
                        "Original item-detail mask close did not restore the result without changing inventory.");
                    Debug.Log("[Demon result first award] Native delayed icon, simulated reward click, original item detail hierarchy/name/type/quality/count, native blur mask close and unchanged inventory passed.");
                }
                yield return Combat.BattleGameViewInput.Click(panel.LeaveGameBtn);
                yield return null;
                Require(bindings.LeaveClicks == 1 && !content.activeSelf && bindings.Rt.Owners.Count == 0,
                    "The original result leave button did not release its render seat.");
                Debug.Log("[Demon result panel] Full native panel, local actor statistics, boss RT, simulated report/leave input and render-seat release passed. First-award coverage is logged separately; report popup and bloom output remain pending.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                foreach (var previous in previousCanvases) if (previous) previous.enabled = true;
                WorldMultiLanguageText.LanguageFont = previousFont;
            }
        }

        private sealed class DemonResultPanelFixture : IBattleBossResultHost, IBattleResultBaseHost,
            IFightThumbsUpUIHost, IDisposable
        {
            [Serializable] private sealed class TextRow { public string key, text; }
            [Serializable] private sealed class Texts { public TextRow[] texts; }
            private readonly HomeFeatureRuntime home;
            private readonly OfficialDemonResultAssets assets;
            private readonly Transform staging;
            private readonly OfflinePartnerActorUiRuntime actors;
            private readonly AppearanceAssetManager manager;
            private readonly OfflinePartnerUiTextureResources textures;
            private readonly IProfessionAppearanceTextureBindings actorTextures;
            private readonly OfflineProfessionNativeSkinClipBindings skinClip;
            private readonly OfflineUiFontInstance font;
            private readonly OfflineProfessionImageCatalog images;
            private readonly IDisposable module;
            private readonly Dictionary<string, string> texts;
            private readonly IProfessionInfoTarget player;
            private readonly ISet<int> unsupported = new HashSet<int>();
            public readonly DemonResultRtFixtureBindings Rt = new DemonResultRtFixtureBindings();
            public GameObject Panel;
            public GameObject ItemDetailContent;
            public WorldWindowBlurMask ItemDetailMask;
            private readonly OfficialPartnerFriendshipWindowAssetCatalog windowAssets;
            private readonly PartnerWindowBlurCapture blurCapture;
            public readonly OfflineDemonResultItemBindings Items;
            public int ReportClicks, LeaveClicks;
            public DemonResultPanelFixture(HomeFeatureRuntime home, OfficialDemonResultAssets assets, Transform parent)
            {
                this.home = home; this.assets = assets;
                windowAssets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                blurCapture = parent.gameObject.AddComponent<PartnerWindowBlurCapture>();
                blurCapture.Initialize(windowAssets.BlurMaterial);
                parent.gameObject.AddComponent<OfflineProfessionSoundHost>().Bind(assets.Audio);
                font = new OfflineUiFontInstance(assets.Font);
                var identities = home.Profile.EnsureLocalActorIdentities();
                Require(identities.Status == Zjcs.Offline.Progression.ProgressionResultStatus.Applied
                    || identities.Status == Zjcs.Offline.Progression.ProgressionResultStatus.Unchanged,
                    "Could not prepare the original result actor identity.");
                texts = JsonUtility.FromJson<Texts>(assets.Bindings.text).texts.ToDictionary(row => row.key, row => row.text);
                images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                images.Entries = home.PartnerRosterAssets.Images.Entries
                    .Concat(Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages").Entries)
                    .Concat(OfficialPartnerStarGodAssetCatalog.LoadDefault().CreateImageCatalog().Entries)
                    .Concat(home.PartnerGiftAssets.Images.Entries)
                    .GroupBy(row => row.Path).Select(group => group.First()).ToArray();
                var resources = new OfflineProfessionImageResourceLoader(images);
                var partAssets = Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/Rank/PartAssets");
                var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                manager = new AppearanceAssetManager(Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),
                    callback => new ActivityMonsterSubscription(() => { }));
                OfflinePartnerUiTextureResources boundTextures = null;
                var parts = new OfflinePartnerUiPartBindings(partAssets, tables.SpineSkinInfoParser,
                    root => boundTextures.BindTree(root));
                actors = new OfflinePartnerActorUiRuntime(home.Profile, home.DailyDungeonProgression,
                    OfficialPartnerAssetCatalog.LoadDefault(), resources, Localize,
                    id => throw new InvalidOperationException("Partner details are outside this fixture."),
                    () => throw new InvalidOperationException("Player details are outside this fixture."), parts);
                actorTextures = ProfessionAppearanceTextureBindings.Current;
                textures = boundTextures = new OfflinePartnerUiTextureResources(partAssets, manager, actorTextures);
                ProfessionAppearanceTextureBindings.Current = textures;
                skinClip = new OfflineProfessionNativeSkinClipBindings();
                module = OfflineUiBindingModule.Register(parent, new OfflineUiBindings {
                    Localize = Localize, Font = () => font.Font,
                    Images = image => Items?.Resources ?? resources,
                    Pool = (scope, prefab, parentNode, keepWorld) => !keepWorld ? InstantiateUi(prefab, parentNode)
                        : throw new NotSupportedException("Inspect the original pooled UI world-transform branch."),
                    SkinClip = new OfflineProfessionCurrentUiSkinClipBindings(skinClip)
                });
                var inactive = new GameObject("Bind original result before activation");
                inactive.SetActive(false); inactive.transform.SetParent(parent, false); staging = inactive.transform;
                player = actors.PlayerInfo.BuildData(actors.PlayerInfo.MainPlayer).TargetData;
                Items = new OfflineDemonResultItemBindings(home, assets, images, InstantiateUi, parent,
                    ShowItemDetail, CloseItemDetail);
            }
            public string Localize(string key) => texts.TryGetValue(key, out var value) ? value : key;
            public GameObject InstantiateUi(GameObject prefab, Transform parent)
            {
                var instance = UnityEngine.Object.Instantiate(prefab, staging, false);
                instance.SetActive(false); instance.transform.SetParent(parent, false);
                actors.BindTree(instance);
                Items?.BindTree(instance);
                foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                { text.Localizer = Localize; text.Refresh(); }
                foreach (var view in instance.GetComponentsInChildren<Zjcs.Official.ActivityBattleUI.TempRTUI>(true)) view.Bindings = Rt;
                foreach (var sound in instance.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = PostSound;
                foreach (var sound in instance.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = PostSound;
                foreach (var sound in instance.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = PostSound;
                instance.SetActive(true);
                return instance;
            }
            private void ShowItemDetail(GameObject window)
            {
                ItemDetailContent = window;
                window.SetActive(false);
                var popup = window.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => CloseItemDetail(window));
                var mask = UnityEngine.Object.Instantiate(windowAssets.BlurMaskPrefab, staging, false);
                mask.SetActive(false); mask.transform.SetParent(window.transform.parent, false);
                ItemDetailMask = mask.GetComponent<WorldWindowBlurMask>();
                ItemDetailMask.BlurManager = blurCapture;
                blurCapture.StartCoroutine(PrepareItemDetail(window, popup));
            }
            private IEnumerator PrepareItemDetail(GameObject window, WorldPopupWindow popup)
            {
                yield return ItemDetailMask.Prepare(popup, false);
                if (!window || ItemDetailContent != window) yield break;
                window.SetActive(true);
                popup.SetCanvasSortingOrder(5010); popup.Show();
                ItemDetailMask.AttachToWindow(popup);
            }
            private void CloseItemDetail(GameObject window)
            {
                Require(window == ItemDetailContent, "An unrelated native window requested item-detail close.");
                window.GetComponent<WorldPopupWindow>().Hide(() => {
                    ItemDetailMask.UnattachToWindow();
                    UnityEngine.Object.Destroy(ItemDetailMask.gameObject); ItemDetailMask = null;
                    UnityEngine.Object.Destroy(window); ItemDetailContent = null;
                });
            }
            private static void PostSound(AudioClip clip, GameObject emitter)
            {
                var source = emitter.GetComponent<AudioSource>() ?? emitter.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0;
                source.PlayOneShot(clip);
            }
            public BattleResultBoss GetBoss(int id) => id == 4800
                ? new BattleResultBoss { Prefab = assets.Boss, StageNameKey = "stage_4800_name" }
                : throw new ArgumentOutOfRangeException(nameof(id));
            public IProfessionInfoTarget ResolvePlayer(int id) => id == 1 ? player : throw new ArgumentOutOfRangeException(nameof(id));
            public long MainPlayerId => player.TargetId;
            public string PlayerName(IProfessionInfoTarget target) => home.Profile.Save.Player.CharacterName;
            public bool IsTeamMember(long id) => false;
            public IFightThumbsUpUIHost ThumbsUp => this;
            public void LeaveGame() { LeaveClicks++; Panel.SetActive(false); }
            public BattleResultStage GetStage(int id) => new BattleResultStage { Id = id, Type = 14 };
            public ISet<int> ShareStageTypes => unsupported;
            public ISet<int> SaveRecordStageTypes => unsupported;
            public ISet<int> ThumbsUpStageTypes => unsupported;
            public bool IsPlayback => false;
            public bool IsAutoSaveRecordStage(int id) => false;
            public bool HasLocalCapturedRecord(long id) => false;
            public IEnumerable<long> TeamMemberTargetIds => Array.Empty<long>();
            public void SetThumbsUpGameId(long id) { }
            public void OpenReport(BattleResultData value) => ReportClicks++;
            public void OpenShare(long id) => throw new NotSupportedException();
            public bool ActiveSaveRecord(long id, int type) => throw new NotSupportedException();
            public void ShowSaveRecordSuccess() => throw new NotSupportedException();
            public bool IsLimited(int type) => true;
            public int MyCount(int type) => 0;
            public BattleThumbsUpRelative OtherRelative(int type, long id) => BattleThumbsUpRelative.None;
            public bool AlreadyReported(long game, long id) => false;
            public event Action<int> CountChanged { add { } remove { } }
            public event Action<long> Reported { add { } remove { } }
            public void Request(int type, long game, long target, Action success, Action failed) => throw new NotSupportedException();
            public void OpenReport(long target, string name, long game) => throw new NotSupportedException();
            public void SetThumbsUpToTeam(long target) => throw new NotSupportedException();
            public void Dispose()
            {
                module.Dispose();
                ProfessionAppearanceTextureBindings.Current = actorTextures;
                actors.Dispose(); manager.Dispose(); skinClip.Dispose(); Rt.RenderSeats.Dispose();
                Items.Dispose();
                font.Dispose();
                UnityEngine.Object.Destroy(images);
            }
        }
    }
}
