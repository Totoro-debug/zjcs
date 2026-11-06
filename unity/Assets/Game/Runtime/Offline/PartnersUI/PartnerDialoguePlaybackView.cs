using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerDialoguePlaybackContext
    {
        public IPartnerDialoguePlaybackServices Dialogues;
        public IPartnerDialogueAwardServices Awards;
        public IPartnerStaticHyperLinkServices HyperLinks;
        public IProfessionWindowService Windows;
        public IProfessionImageResourceLoader Images;
        public TMP_FontAsset Font;
        public Func<string, string> Localize;
        public Action<GameObject> BindNativeTree;
        public Action<AudioClip, GameObject> PostSound;
    }

    public sealed class PartnerDialoguePlaybackView : MonoBehaviour
    {
        private sealed class Pool : LoopScrollPrefabSource
        {
            private readonly LoopScrollPrefabSource source;
            private readonly PartnerDialoguePlaybackView owner;
            internal Pool(LoopScrollPrefabSource source, PartnerDialoguePlaybackView owner) { this.source = source; this.owner = owner; }
            public Transform GetPoolRoot() => source.GetPoolRoot();
            public GameObject GetObject(int index) { var value = source.GetObject(index); owner.BindTree(value); return value; }
            public void ReturnObject(Transform value, int index) => source.ReturnObject(value, index);
        }
        private static readonly FieldInfo PrefabSource = typeof(LoopScrollRectBase).GetField("mPrefabSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private PartnerDialoguePlaybackContext context;
        private GameObject bindingRoot, content;
        private IDisposable uiBinding;
        private OfflineProfessionNativeSkinClipBindings skinClip;
        private OfflinePartnerUiSoundScope sounds;
        public GameObject Content => content;
        public WorldPopupWindow Window => content ? content.GetComponent<WorldPopupWindow>() : null;

        public void Open(OfficialPartnerDialoguePlaybackAssetCatalog assets, PartnerDialoguePlaybackContext value, PartnerDialogueData dialogue)
        {
            if (!assets || value == null || value.Dialogues == null || value.Awards == null || value.HyperLinks == null
                || value.Windows == null || value.Images == null || !value.Font || value.Localize == null
                || value.BindNativeTree == null || value.PostSound == null || dialogue == null
                || !value.Dialogues.Dialogues.TryGetValue(dialogue.DialogueId, out var owned) || !ReferenceEquals(owned, dialogue))
                throw new ArgumentException("Bind the saved dialogue and complete original playback services before opening it.");
            if (context != null) throw new InvalidOperationException("Original conversation window is already open.");
            context = value;
            try
            {
                sounds = new OfflinePartnerUiSoundScope(assets.GetSound);
                skinClip = new OfflineProfessionNativeSkinClipBindings();
                bindingRoot = new GameObject("OriginalConversationWindowBinding", typeof(RectTransform));
                var rect = (RectTransform)bindingRoot.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                bindingRoot.SetActive(false);
                uiBinding = OfflineUiBindingModule.Register(rect, new OfflineUiBindings {
                    Localize = assets.CreateLocalizer(value.Localize), Font = () => value.Font,
                    Images = image => value.Images, SkinClip = new OfflineProfessionCurrentUiSkinClipBindings(skinClip)
                });
                content = Instantiate(assets.PanelPrefab, rect, false);
                BindTree(content);
                var panel = content.GetComponent<NpcDialoguePanel>();
                if (!panel || !Window) throw new InvalidOperationException("Original conversation controller/window is unbound.");
                foreach (var loop in content.GetComponentsInChildren<LoopVerticalScrollRect>(true))
                {
                    if (loop.DefaultPrefabSource?.Prefab == null) throw new InvalidOperationException("Original conversation row template is unbound.");
                    PrefabSource.SetValue(loop, new Pool(loop.prefabSource, this));
                }
                panel.Init(dialogue);
                content.SetActive(true); bindingRoot.SetActive(true);
                foreach (var close in content.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(value.Windows);
                Window.Show();
            }
            catch { Close(); throw; }
        }

        private void BindTree(GameObject root)
        {
            context.BindNativeTree(root);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanel>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanelItem>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanelItemContent>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanelOption>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanelOptionItem>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialoguePanelRewardItemContent>(true)) item.Configure(context.Dialogues);
            foreach (var item in root.GetComponentsInChildren<NpcDialogueAwardHandler>(true)) item.Configure(context.Awards);
            foreach (var item in root.GetComponentsInChildren<StaticHyperLinkClickListener>(true)) item.Configure(context.HyperLinks);
            foreach (var item in root.GetComponentsInChildren<UITouchPass>(true)) item.Configure(context.HyperLinks);
            foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = context.PostSound;
            foreach (var sound in root.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = context.PostSound;
            foreach (var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = context.PostSound;
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
        }
        public void Close()
        {
            if (context == null) return;
            uiBinding?.Dispose(); uiBinding = null;
            if (bindingRoot) { bindingRoot.SetActive(false); Destroy(bindingRoot); }
            bindingRoot = null; content = null;
            skinClip?.Dispose(); skinClip = null;
            sounds?.Dispose(); sounds = null; context = null;
        }
        private void OnDestroy() => Close();
    }
}
