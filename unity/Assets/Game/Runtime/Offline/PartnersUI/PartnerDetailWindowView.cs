using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerDetailWindowContext
    {
        public IPartnerDetailWindowServices Npcs;
        public IPartnerDetailPropertyServices Properties;
        public IPartnerGiftUiServices Gifts;
        public IPartnerProfileUiServices Profiles;
        public IPartnerAchievementUiServices Achievements;
        public IPartnerTreasureIconServices Treasures;
        public OfflinePartnerDetailPresentationServices Presentation;
        public IProfessionWindowService Windows;
        public IProfessionImageResourceLoader Images;
        public TMP_FontAsset Font;
        public Func<string, string> Localize;
        public Action<GameObject> BindNativeTree;
        public Action<AudioClip, GameObject> PostSound;
    }

    // Bind local data before activation. Every visible node, control, transition
    // and tab remains owned by the complete original NpcDetailPanel prefab.
    public sealed class PartnerDetailWindowView : MonoBehaviour
    {
        private PartnerDetailWindowContext context;
        private GameObject bindingRoot, content;
        private Func<string, string> previousLocalize;
        private Func<TMP_FontAsset> previousFont;
        private Func<Image, IProfessionImageResourceLoader> previousImages;
        private Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        private OfflinePartnerUiSoundScope sound;
        public GameObject Content => content;
        public NpcDetailPanel Panel => content ? content.GetComponent<NpcDetailPanel>() : null;
        public WorldPopupWindow Window => content ? content.GetComponent<WorldPopupWindow>() : null;
        public void Open(OfficialPartnerDetailWindowAssetCatalog assets, PartnerDetailWindowContext value,
            List<IPartnerDetailWindowNpcData> npcs, int index,
            NpcDetailPanel.TabType initialTab = NpcDetailPanel.TabType.Detail)
        {
            if (!assets || value == null || value.Npcs == null || value.Properties == null || value.Gifts == null ||
                value.Profiles == null || value.Achievements == null || value.Treasures == null || value.Presentation == null ||
                value.Windows == null || value.Images == null || !value.Font || value.Localize == null ||
                value.BindNativeTree == null || value.PostSound == null || npcs == null || index < 0 || index >= npcs.Count)
                throw new ArgumentException("Bind all original detail window services before opening.");
            if (context != null) throw new InvalidOperationException("Original detail window is already open.");
            context = value;
            previousLocalize = WorldMultiLanguageText.Localize; previousFont = WorldMultiLanguageText.LanguageFont;
            previousImages = ProfessionImageResourceBridge.Provider; previousPool = ProfessionGameObjectPoolBridge.Provider;
            try
            {
                var words = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in assets.Texts) words[entry.key] = entry.text;
                WorldMultiLanguageText.Localize = key => words.TryGetValue(key, out var text) ? text : value.Localize(key);
                WorldMultiLanguageText.LanguageFont = () => value.Font;
                sound = new OfflinePartnerUiSoundScope(OfficialPartnerPanelAssetCatalog.Load().GetSound);
                bindingRoot = new GameObject("Original partner detail bindings", typeof(RectTransform));
                var rect = (RectTransform)bindingRoot.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                bindingRoot.SetActive(false);
                ProfessionImageResourceBridge.Provider = image => image && image.transform.IsChildOf(rect) ? value.Images : previousImages?.Invoke(image);
                ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => {
                    if (scope && scope.IsChildOf(rect))
                    {
                        return InstantiateAndBind(prefab, parent, keepWorld);
                    }
                    if (previousPool != null) return previousPool(scope, prefab, parent, keepWorld);
                    throw new InvalidOperationException("Original item pool request is outside the detail window.");
                };
                content = Instantiate(assets.PanelPrefab, rect, false);
                BindTree(content);
                if (!Panel || !Window) throw new InvalidOperationException("Complete original detail controller/window is unbound.");
                Panel.Configure(value.Npcs);
                content.SetActive(true); bindingRoot.SetActive(true);
                foreach (var close in content.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(value.Windows);
                Panel.Init(npcs, index);
                Window.Show();
                SelectInitialTab(initialTab);
            }
            catch { Close(); throw; }
        }
        private void SelectInitialTab(NpcDetailPanel.TabType type)
        {
            if (type == NpcDetailPanel.TabType.Detail) return;
            var field = typeof(NpcDetailPanel).GetField("mTabDatas", BindingFlags.Instance | BindingFlags.NonPublic);
            var tabs = field?.GetValue(Panel) as List<NpcDetailPanel.TabData>;
            var target = tabs?.Find(value => value.TabType == type);
            if (target?.TabItem == null)
                throw new InvalidOperationException("Original partner detail tab is not bound: " + type);
            target.TabItem.isOn = true;
        }
        private void BindTree(GameObject root)
        {
            context.BindNativeTree(root);
            context.Presentation.BindTree(root);
            foreach (var item in root.GetComponentsInChildren<NpcDetailPanelPropContent>(true)) item.Configure(context.Properties);
            foreach (var item in root.GetComponentsInChildren<NpcDetailPanelGiftContent>(true)) item.Configure(context.Gifts);
            foreach (var item in root.GetComponentsInChildren<NpcFriendshipLevelContent>(true)) item.Configure(context.Gifts);
            foreach (var item in root.GetComponentsInChildren<NpcDetailPanelProfileContent>(true)) item.Configure(context.Profiles);
            foreach (var item in root.GetComponentsInChildren<PartnerAchievementPanel>(true)) item.Configure(context.Achievements);
            foreach (var item in root.GetComponentsInChildren<NpcTreasureIcon>(true)) item.Configure(context.Treasures);
            foreach (var item in root.GetComponentsInChildren<WorldButtonSound>(true)) item.Post = context.PostSound;
            foreach (var item in root.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) item.Post = context.PostSound;
            foreach (var item in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) item.Post = context.PostSound;
            foreach (var item in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) item.Refresh();
        }
        private GameObject InstantiateAndBind(GameObject prefab, Transform parent, bool keepWorld)
        {
            var staging = new GameObject("Inactive original detail pool binding", typeof(RectTransform));
            staging.SetActive(false);
            var rect = (RectTransform)staging.transform; rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var instance = Instantiate(prefab, rect, keepWorld);
            bool active = instance.activeSelf; instance.SetActive(false);
            instance.transform.SetParent(parent, keepWorld);
            Destroy(staging);
            BindTree(instance); instance.SetActive(active);
            return instance;
        }
        public void Close()
        {
            if (context == null) return;
            if (bindingRoot) { bindingRoot.SetActive(false); Destroy(bindingRoot); }
            bindingRoot = null; content = null;
            WorldMultiLanguageText.Localize = previousLocalize; WorldMultiLanguageText.LanguageFont = previousFont;
            ProfessionImageResourceBridge.Provider = previousImages; ProfessionGameObjectPoolBridge.Provider = previousPool;
            sound?.Dispose(); sound = null; context = null;
            previousLocalize = null; previousFont = null; previousImages = null; previousPool = null;
        }
        private void OnDestroy() => Close();
    }
}
