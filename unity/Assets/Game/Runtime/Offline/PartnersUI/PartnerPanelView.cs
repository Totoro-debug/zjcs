using System;
using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerPanelContext
    {
        public IPartnerRosterUiServices Roster;
        public IPartnerStarGodUiServices StarGod;
        public IPartnerDialogueUiServices Dialogues;
        public IProfessionWindowService Windows;
        public IProfessionImageResourceLoader Images;
        public TMP_FontAsset Font;
        public Func<string, string> Localize;
        public Func<int, bool> IsFunctionLimit;
        public Action<GameObject> BindNativeTree;
        public Action<AudioClip, GameObject> PostSound;
    }

    // Own only the binding lifetime. The original prefab owns every control,
    // layout, state switch and transition in the three-page partner window.
    public sealed class PartnerPanelView : MonoBehaviour, IPartnerPanelServices
    {
        private sealed class DialogueBindings : IPartnerDialogueUiServices
        {
            private readonly IPartnerDialogueUiServices source;
            private readonly PartnerPanelView owner;
            internal DialogueBindings(IPartnerDialogueUiServices source, PartnerPanelView owner) { this.source = source; this.owner = owner; }
            public bool MainPlayerAvailable => source.MainPlayerAvailable;
            public string PlayerName => source.PlayerName;
            public int DialogueStartNodeId => source.DialogueStartNodeId;
            public IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs => source.Npcs;
            public IReadOnlyDictionary<long, PartnerDialogueData> Dialogues => source.Dialogues;
            public event Action<PartnerDialogueData> DialogueAdded { add => source.DialogueAdded += value; remove => source.DialogueAdded -= value; }
            public event Action<PartnerDialogueData> DialogueRemoved { add => source.DialogueRemoved += value; remove => source.DialogueRemoved -= value; }
            public event Action<PartnerDialogueData> DialogueUpdated { add => source.DialogueUpdated += value; remove => source.DialogueUpdated -= value; }
            public IReadOnlyList<int> GetNextNodes(int dialogue, int node) => source.GetNextNodes(dialogue, node);
            public bool IsNodeUnlocked(int dialogue, int node) => source.IsNodeUnlocked(dialogue, node);
            public GameObject Instantiate(GameObject prefab, Transform parent, bool reset)
            {
                var instance = source.Instantiate(prefab, parent, reset); owner.BindTree(instance); return instance;
            }
            public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => source.InitializeNpcIcon(icon, npc);
            public void ShowError(int code) => source.ShowError(code);
            public void ShowDialogue(PartnerDialogueData dialogue) => source.ShowDialogue(dialogue);
        }
        private sealed class Pool : LoopScrollPrefabSource
        {
            private readonly LoopScrollPrefabSource original;
            private readonly PartnerPanelView owner;
            internal Pool(LoopScrollPrefabSource original, PartnerPanelView owner) { this.original = original; this.owner = owner; }
            public Transform GetPoolRoot() => original.GetPoolRoot();
            public GameObject GetObject(int index)
            {
                var value = original.GetObject(index); owner.BindTree(value); return value;
            }
            public void ReturnObject(Transform value, int index) => original.ReturnObject(value, index);
        }
        private static readonly FieldInfo PrefabSource = typeof(LoopScrollRectBase).GetField("mPrefabSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private PartnerPanelContext context;
        private GameObject bindingRoot, content;
        private Func<string, string> previousLocalize;
        private Func<TMP_FontAsset> previousFont;
        private Func<Image, IProfessionImageResourceLoader> previousImages;
        private Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        private IProfessionAppearanceSkinClipBindings previousSkinClip;
        private OfflineProfessionNativeSkinClipBindings skinClip;
        private DialogueBindings dialogueBindings;
        public GameObject Content => content;
        public WorldPopupWindow Window => content ? content.GetComponent<WorldPopupWindow>() : null;

        public void Open(OfficialPartnerPanelAssetCatalog assets, PartnerPanelContext value,
            NpcPanelTabType defaultTab = NpcPanelTabType.NpcList)
        {
            if (!assets || value == null || value.Roster == null || value.StarGod == null || value.Dialogues == null ||
                value.Windows == null || value.Images == null || !value.Font || value.Localize == null ||
                value.IsFunctionLimit == null || value.BindNativeTree == null || value.PostSound == null)
                throw new ArgumentException("Bind the complete original partner window's local services before opening it.");
            if (context != null) throw new InvalidOperationException("Original partner window is already open.");
            context = value;
            dialogueBindings = new DialogueBindings(value.Dialogues, this);
            previousLocalize = WorldMultiLanguageText.Localize; previousFont = WorldMultiLanguageText.LanguageFont;
            previousImages = ProfessionImageResourceBridge.Provider; previousPool = ProfessionGameObjectPoolBridge.Provider;
            previousSkinClip = ProfessionAppearanceSkinClipBindings.Current;
            try
            {
                skinClip = new OfflineProfessionNativeSkinClipBindings(previousSkinClip);
                ProfessionAppearanceSkinClipBindings.Current = skinClip;
                WorldMultiLanguageText.Localize = assets.CreateLocalizer(value.Localize); WorldMultiLanguageText.LanguageFont = () => value.Font;
                bindingRoot = new GameObject("OriginalPartnerWindowBinding", typeof(RectTransform));
                var rect = (RectTransform)bindingRoot.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                bindingRoot.SetActive(false);
                ProfessionImageResourceBridge.Provider = image => image && image.transform.IsChildOf(rect) ? value.Images : previousImages?.Invoke(image);
                ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => {
                    if (scope && scope.IsChildOf(rect))
                    {
                        var instance = Instantiate(prefab, parent, keepWorld); BindTree(instance); return instance;
                    }
                    if (previousPool != null) return previousPool(scope, prefab, parent, keepWorld);
                    throw new InvalidOperationException("Original item pool request is outside this partner window.");
                };
                content = Instantiate(assets.PanelPrefab, rect, false);
                BindTree(content);
                var panel = content.GetComponent<NpcPanel>();
                if (!panel || !Window) throw new InvalidOperationException("Original partner panel/window components are unbound.");
                panel.Configure(this); panel.SetDefaultShowTab(defaultTab);
                foreach (var list in content.GetComponentsInChildren<NpcPanelNpcList>(true)) list.Configure(value.Roster);
                foreach (var list in content.GetComponentsInChildren<NpcPanelStarGodContent>(true)) list.Configure(value.StarGod);
                foreach (var list in content.GetComponentsInChildren<NpcPanelDialogueList>(true)) list.Configure(dialogueBindings);
                foreach (var loop in content.GetComponentsInChildren<LoopVerticalScrollRect>(true))
                {
                    if (loop.DefaultPrefabSource?.Prefab == null) throw new InvalidOperationException("Original partner loop template is unbound.");
                    PrefabSource.SetValue(loop, new Pool(loop.prefabSource, this));
                }
                content.SetActive(true); bindingRoot.SetActive(true);
                // Awake locates each original parent PopupWindow. Initialization
                // follows activation so its destroyed-window subscription is real.
                foreach (var close in content.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(value.Windows);
                Window.Show();
            }
            catch { Close(); throw; }
        }
        private void BindTree(GameObject root)
        {
            context.BindNativeTree(root);
            foreach (var item in root.GetComponentsInChildren<NpcPanelNpcItem>(true)) item.Configure(context.Roster);
            foreach (var item in root.GetComponentsInChildren<StarGodNpcItem>(true)) item.Configure(context.StarGod);
            foreach (var item in root.GetComponentsInChildren<NpcPanelDialogueListNpcItem>(true)) item.Configure(dialogueBindings);
            foreach (var item in root.GetComponentsInChildren<NpcPanelDialogueListDialogueItem>(true)) item.Configure(dialogueBindings);
            foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = context.PostSound;
            foreach (var sound in root.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) sound.Post = context.PostSound;
            foreach (var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = context.PostSound;
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
        }
        bool IPartnerPanelServices.IsFunctionLimit(int functionType) => context.IsFunctionLimit(functionType);
        void IPartnerPanelServices.InitDialogueList(GameObject owner)
        {
            var list = owner.GetComponent<NpcPanelDialogueList>();
            if (!list) throw new InvalidOperationException("Original dialogue page is unbound.");
            list.Init();
        }
        public void Close()
        {
            if (context == null) return;
            if (bindingRoot) { bindingRoot.SetActive(false); Destroy(bindingRoot); }
            content = null; bindingRoot = null;
            WorldMultiLanguageText.Localize = previousLocalize; WorldMultiLanguageText.LanguageFont = previousFont;
            ProfessionImageResourceBridge.Provider = previousImages; ProfessionGameObjectPoolBridge.Provider = previousPool;
            ProfessionAppearanceSkinClipBindings.Current = previousSkinClip;
            skinClip?.Dispose(); skinClip = null; context = null;
            dialogueBindings = null;
            previousLocalize = null; previousFont = null; previousImages = null; previousPool = null; previousSkinClip = null;
        }
        private void OnDestroy() => Close();
    }
}
