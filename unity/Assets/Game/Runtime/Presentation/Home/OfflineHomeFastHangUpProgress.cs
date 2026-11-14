using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.HomeFastHangUpProgressUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeFastHangUpProgressUI;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    public sealed class OfflineHomeFastHangUpProgress : IHomeFastHangUpProgressServices, IDisposable
    {
        readonly HomeFeatureRuntime home;
        readonly IHomeFastHangUpServices fast;
        readonly IProfessionImageResourceLoader images;
        readonly Action<UIBase, bool> visibility;
        readonly Canvas canvas;
        readonly FontAssetFontService fonts;
        AppearanceAssetManager manager;
        OfflineProfessionSceneTextureResources textures;
        IProfessionAppearanceTextureBindings previousTextures;
        IProfessionAppearanceSkinClipBindings previousClips;
        GameObject root;
        bool disposed;
        public bool IsOpen => root && root.activeSelf;

        public OfflineHomeFastHangUpProgress(HomeFeatureRuntime home, Canvas source,
            IHomeFastHangUpServices fast, IProfessionImageResourceLoader images, Action<UIBase, bool> visibility)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.fast = fast ?? throw new ArgumentNullException(nameof(fast));
            this.images = images ?? throw new ArgumentNullException(nameof(images));
            this.visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            canvas = new GameObject("HomeFastHangUpProgressCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(source.transform.parent, false);
            canvas.renderMode = source.renderMode;
            canvas.worldCamera = source.worldCamera;
            canvas.planeDistance = source.planeDistance;
            canvas.sortingOrder = source.sortingOrder + 100;
            canvas.sortingLayerID = source.sortingLayerID;
            var from = source.GetComponent<CanvasScaler>();
            var to = canvas.GetComponent<CanvasScaler>();
            to.uiScaleMode = from.uiScaleMode; to.referenceResolution = from.referenceResolution;
            to.screenMatchMode = from.screenMatchMode; to.matchWidthOrHeight = from.matchWidthOrHeight;
            to.referencePixelsPerUnit = from.referencePixelsPerUnit;
            fonts = OfflineHomeHudAssets.LoadDefault().CreateFontService();
            fonts.Initialize();
        }

        public void Show(int id, Action finished)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeFastHangUpProgress));
            if (root) throw new InvalidOperationException("An acceleration presentation is already open.");
            var assets = OfficialHomeFastHangUpProgressAssetCatalog.LoadDefault();
            var resources = UnityEngine.Resources.Load<ProfessionNativeSceneAppearanceResources>("Partners/appearance-resources");
            AppearanceConfig = ProfessionNativePortraitStudioResources.LoadDefault().AppearanceConfig;
            AppearanceTables = ProfessionAppearanceTables.Load(UnityEngine.Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            AppearanceResolver = new ProfessionAppearanceResolver(AppearanceTables);
            var defaults = new ProfessionAppearanceSkinDefaults(AppearanceTables,
                UnityEngine.Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
                message => { throw new InvalidOperationException(message); });
            var rules = UnityEngine.Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
            home.Profile.EnsureLocalActorIdentities();
            MainPlayerId = home.Profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
            var player = new OfflineProfessionAppearanceBindings(MainPlayerId, () => home.DailyDungeonProgression.Player,
                item => home.DailyDungeonProgression.Catalog.ItemType(item), AppearanceTables, AppearanceConfig, rules, defaults);
            manager = new AppearanceAssetManager(UnityEngine.Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"), new GAction().Subscribe);
            textures = new OfflineProfessionSceneTextureResources(resources);
            var partAssets = UnityEngine.Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/Rank/PartAssets");
            var partTextures = new OfflinePartnerUiTextureResources(partAssets, manager, textures);
            var parts = new OfflinePartnerUiPartBindings(partAssets, AppearanceTables.SpineSkinInfoParser, tree => partTextures.BindTree(tree));
            previousTextures = ProfessionAppearanceTextureBindings.Current;
            previousClips = ProfessionAppearanceSkinClipBindings.Current;
            ProfessionAppearanceTextureBindings.Current = partTextures;
            ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionCurrentUiSkinClipBindings(previousClips);
            var stage = new GameObject("HomeFastHangUpInactiveBinding"); stage.SetActive(false);
            try
            {
                root = Object.Instantiate(assets.progressPrefab, stage.transform, false);
                root.SetActive(false);
                OfflineHomeFastHangUpProgressBindings.Bind(root, this, resources, manager, parts, player);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.font) fonts.SetupFont(text.font);
                var localizer = new OfflinePartnerLocalizationService(assets.texts);
                foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = key => localizer.GetText(key, LanguageType.zh_CN);
                root.transform.SetParent(canvas.transform, false);
                root.SetActive(true);
                root.GetComponent<FastHangUpProgressBarDialog>().Init(id, finished);
            }
            catch { if (root) Close(root); else ReleaseAppearance(); throw; }
            finally { Object.Destroy(stage); }
        }

        public IHomeFastHangUpServices FastHangUp => fast;
        public IProfessionImageResourceLoader Resources => images;
        public AppearanceConfig AppearanceConfig { get; private set; }
        public ProfessionAppearanceTables AppearanceTables { get; private set; }
        public ProfessionAppearanceResolver AppearanceResolver { get; private set; }
        public long MainPlayerId { get; private set; }
        public int RandomRange(int min, int max) => UnityEngine.Random.Range(min, max);
        public void PublishBedFastHangUp() => home.PublishPresentedBedFastHangUp();
        public void SetActiveUIVisible(UIBase owner, bool visible) => visibility(owner, visible);
        public void Close(GameObject value)
        {
            if (value != root) return;
            root.SetActive(false); // Original OnDisable consumes the award before releasing its images.
            Object.Destroy(root); root = null;
            ReleaseAppearance();
        }
        void ReleaseAppearance()
        {
            ProfessionAppearanceTextureBindings.Current = previousTextures;
            ProfessionAppearanceSkinClipBindings.Current = previousClips;
            textures?.Dispose(); textures = null;
            manager?.Dispose(); manager = null;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (root) Close(root);
            fonts.Dispose();
            if (canvas) Object.Destroy(canvas.gameObject);
        }
    }
}
