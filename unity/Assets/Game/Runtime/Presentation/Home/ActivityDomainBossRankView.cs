using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ActivityRankUI;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Home
{
    // Owns the offline bindings and lifetime. The original prefab/controller
    // owns every visible rank/empty/first/self/range/rule control.
    public sealed class ActivityDomainBossRankView : MonoBehaviour
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class Texts { public TextRow[] texts; }
        [Serializable] private sealed class Config : IProfessionRuleTipData
        {
            public int ShowPlayerCountLimit;
            public ActivityRankRule[] Rules;
            public string GetTitleKey(string key) => Rules.Single(row => row.Key == key).TitleKey;
            public IReadOnlyList<string> GetTipsKey(string key) => Rules.Single(row => row.Key == key).TipKeys;
        }
        private OfflinePartnerActorUiRuntime actors;
        private AppearanceAssetManager partManager;
        private OfflinePartnerUiTextureResources partTextures;
        private IProfessionAppearanceTextureBindings actorTextures;
        private OfflineProfessionImageCatalog images;
        private OfflineProfessionNativeSkinClipBindings skinClip;
        private IProfessionAppearanceSkinClipBindings previousSkinClip;
        private Func<string, string> previousLocalize;
        private Func<TMP_FontAsset> previousFont;
        private Func<Image, IProfessionImageResourceLoader> previousImages;
        private Func<string, string> localize;
        private OfflinePartnerWindowService windows;
        private Config config;
        private GameObject content, rule;
        private Action closed;
        private Action<AudioClip, GameObject> postSound;
        private bool opened;
        public GameObject Content => content;

        public void Open(HomeFeatureRuntime home, int activityId, Action openPlayer,
            Action<AudioClip, GameObject> sound, Action onClosed)
        {
            if (opened) throw new InvalidOperationException("Rank window is already open.");
            opened = true; closed = onClosed; postSound = sound ?? throw new ArgumentNullException(nameof(sound));
            previousLocalize = WorldMultiLanguageText.Localize;
            previousFont = WorldMultiLanguageText.LanguageFont;
            previousImages = ProfessionImageResourceBridge.Provider;
            previousSkinClip = ProfessionAppearanceSkinClipBindings.Current;
            try
            {
                var labels = JsonUtility.FromJson<Texts>(Required<TextAsset>("texts").text).texts.ToDictionary(row => row.key, row => row.text);
                localize = key => labels.TryGetValue(key, out string value) ? value : previousLocalize?.Invoke(key) ?? key;
                config = JsonUtility.FromJson<Config>(Required<TextAsset>("config").text);
                var identities = home.Profile.EnsureLocalActorIdentities();
                if (identities.Status != ProgressionResultStatus.Applied && identities.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException("Could not migrate the selected profile's local actor identities.");
                images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                images.Entries = home.PartnerRosterAssets.Images.Entries
                    .Concat(OfficialPartnerStarGodAssetCatalog.LoadDefault().CreateImageCatalog().Entries)
                    .Concat(home.PartnerGiftAssets.Images.Entries)
                    .GroupBy(row => row.Path).Select(group => group.First()).ToArray();
                var resources = new OfflineProfessionImageResourceLoader(images);
                var partAssets = Required<PartnerNativeDetailActorAssets>("PartAssets");
                var appearanceTables = ProfessionAppearanceTables.Load(
                    Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                partManager = new AppearanceAssetManager(
                    Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),
                    callback => new LowMemorySubscription(callback));
                var parts = new OfflinePartnerUiPartBindings(partAssets, appearanceTables.SpineSkinInfoParser,
                    root => partTextures.BindTree(root));
                actors = new OfflinePartnerActorUiRuntime(home.Profile, home.DailyDungeonProgression,
                    OfficialPartnerAssetCatalog.LoadDefault(), resources, localize, home.OpenPartnerDetail, openPlayer, parts);
                actorTextures = ProfessionAppearanceTextureBindings.Current;
                partTextures = new OfflinePartnerUiTextureResources(partAssets, partManager, actorTextures);
                ProfessionAppearanceTextureBindings.Current = partTextures;
                skinClip = new OfflineProfessionNativeSkinClipBindings(previousSkinClip);
                ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionCurrentUiSkinClipBindings(skinClip);
                WorldMultiLanguageText.Localize = localize;
                ProfessionImageResourceBridge.Provider = image => image && image.transform.IsChildOf(transform)
                    ? resources : previousImages?.Invoke(image);
                windows = new OfflinePartnerWindowService(CloseWindow, () => CloseWindow(rule ? rule : content));
                gameObject.SetActive(false);
                content = Instantiate(Required<GameObject>("ActivityDomainBossRankPanel"), transform, false);
                content.name = "ActivityDomainBossRankPanel";
                var nativeFont = content.GetComponentsInChildren<TMP_Text>(true).First().font;
                WorldMultiLanguageText.LanguageFont = () => nativeFont;
                BindTree(content);
                var player = actors.PlayerInfo.BuildData(actors.PlayerInfo.MainPlayer).TargetData;
                var bindings = new ActivityRankBindings(actors.TeamIcons, id => id == player.TargetId ? player
                    : throw new InvalidOperationException("A remote actor cannot enter a local activity ranking."),
                    OpenRule, config.ShowPlayerCountLimit, true);
                content.GetComponent<ActivityDomainBossRankPanel>().Configure(bindings);
                var source = content.GetComponent<TeamTopContentDataActivityDomainBoss>();
                var catalog = OfficialDailyChallengeCatalog.LoadDefault();
                source.Configure(id => home.DailyChallenges.DomainBossMaxDamage(id, DateTime.Now, catalog.DurationHours(id)),
                    () => player.TargetId,
                    damage => string.Format(localize("SecretAreeRankItem_damage_desc"), WorldBattleReportNumberFormat.Compact(damage)));
                content.GetComponent<ActivityDomainBossRankPanel>().Init(activityId, "ActivityDomainBoss");
                content.SetActive(true); gameObject.SetActive(true);
                InitWindows(content, 300);
                content.GetComponent<WorldPopupWindow>().Show();
            }
            catch { Close(); throw; }
        }

        private static T Required<T>(string name) where T : UnityEngine.Object
            => Resources.Load<T>("DailyChallenges/Rank/" + name)
                ?? throw new InvalidOperationException("Packaged original activity rank resource missing: " + name);

        private void BindTree(GameObject root)
        {
            actors.BindTree(root);
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
            { text.Localizer = localize; text.Refresh(); }
            foreach (var button in root.GetComponentsInChildren<RuleBtn>(true)) button.Configure(OpenRule);
            foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = postSound;
            foreach (var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = postSound;
            foreach (var sound in root.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = postSound;
        }

        private void InitWindows(GameObject root, int order)
        {
            foreach (var close in root.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(windows);
            var window = root.GetComponent<WorldPopupWindow>();
            if (window.Canvas) window.SetCanvasSortingOrder(order);
        }

        private void OpenRule(string key, bool dynamicHeight)
        {
            if (rule) CloseWindow(rule);
            rule = Instantiate(Required<GameObject>(dynamicHeight ? "DynamicHeightPanelRuleDialog" : "RuleDialog"), transform, false);
            rule.name = "ActivityRankRuleDialog";
            BindTree(rule);
            var controller = rule.GetComponent<RuleDialog>();
            controller.Rules = config;
            controller.SetRule(key);
            foreach (var text in rule.GetComponentsInChildren<WorldMultiLanguageText>(true)) { text.Localizer = localize; text.Refresh(); }
            rule.SetActive(true); InitWindows(rule, 310);
            rule.GetComponent<WorldPopupWindow>().Show();
        }

        private void CloseWindow(GameObject window)
        {
            if (window == rule) { rule.SetActive(false); Destroy(rule); rule = null; return; }
            Close(); Destroy(gameObject);
        }

        public void Close()
        {
            if (!opened) return;
            opened = false;
            if (rule) { rule.SetActive(false); Destroy(rule); rule = null; }
            if (content) { content.SetActive(false); Destroy(content); content = null; }
            if (partTextures != null) ProfessionAppearanceTextureBindings.Current = actorTextures;
            actors?.Dispose(); actors = null;
            partManager?.Dispose(); partManager = null; partTextures = null;
            WorldMultiLanguageText.Localize = previousLocalize;
            WorldMultiLanguageText.LanguageFont = previousFont;
            ProfessionImageResourceBridge.Provider = previousImages;
            ProfessionAppearanceSkinClipBindings.Current = previousSkinClip;
            skinClip?.Dispose(); skinClip = null;
            if (images) Destroy(images); images = null;
            var callback = closed; closed = null; callback?.Invoke();
        }

        private void OnDestroy() => Close();
        private void Update() => partManager?.Update();

        private sealed class LowMemorySubscription : IDisposable
        {
            private Action callback;
            public LowMemorySubscription(Action value)
            { callback = value; Application.lowMemory += callback.Invoke; }
            public void Dispose()
            { if (callback == null) return; Application.lowMemory -= callback.Invoke; callback = null; }
        }
    }
}
