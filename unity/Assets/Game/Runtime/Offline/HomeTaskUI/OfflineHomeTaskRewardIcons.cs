using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using TaskUI = Zjcs.Official.HomeTaskUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeTaskUI
{
    public sealed class OfflineHomeTaskRewardIcons : TaskUI.IItemIconManager, IDisposable
    {
        readonly GameObject root;
        readonly GameObjectPool<ItemIcon> pool;
        readonly ItemIconAttachmentManager attachments;
        readonly IDisposable registration;
        readonly OfflineHomeMainlineTaskData tasks;
        readonly List<IDisposable> instanceBindings = new List<IDisposable>();
        RectTransform defaultTransform;
        bool disposed;

        public OfflineHomeTaskRewardIcons(Transform parent, IProfessionItemIconContext context,
            FontAssetFontService fonts, Action<AudioClip, GameObject> postSound,
            OfflineHomeMainlineTaskData tasks = null, OfflineHomeCurrencyAssets assets = null)
        {
            if (!parent || context == null || fonts == null || postSound == null)
                throw new ArgumentException("Original task icon services must be bound before activation.");
            this.tasks = tasks ?? OfflineHomeMainlineTaskData.LoadDefault();
            assets = assets ?? OfflineHomeCurrencyAssets.LoadDefault();
            var rewards = Resources.Load<OfflineProfessionImageCatalog>("Home/OfflineHomeTaskRewardImages");
            if (!rewards || rewards.Entries?.Length != 7)
                throw new InvalidOperationException("Original mainline reward images are unbound.");
            var merged = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
            merged.Entries = rewards.Entries.Concat(assets.images.Entries)
                .GroupBy(entry => entry.AssetGuid).Select(group => group.First()).ToArray();
            OfflineProfessionImageResourceLoader images;
            try { images = new OfflineProfessionImageResourceLoader(merged); }
            finally { Object.Destroy(merged); }
            attachments = new ItemIconAttachmentManager(images, () => assets.attachmentConfig, new GAction());
            root = new GameObject("HomeTaskRewardIconPools", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            OfflineUiBindings binding = null;
            binding = new OfflineUiBindings {
                Localize = key => this.tasks.Texts.ContainsKey(key) ? this.tasks.Text(key) : assets.Text(key),
                Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont,
                Images = _ => images,
                Pool = (scope, prefab, slot, keepWorld) => Create(prefab, slot, keepWorld)
            };
            registration = OfflineUiBindingModule.Register(root.transform, binding);
            pool = new GameObjectPool<ItemIcon>(root.transform, (slot, _, active) => {
                var icon = Create(assets.itemIconPrefab, slot, false);
                if (active) icon.SetActive(true);
                return icon;
            }, "HomeTaskItemIconDefault");

            GameObject Create(GameObject prefab, Transform slot, bool keepWorld)
            {
                var staging = new GameObject("InactiveTaskIconBindings", typeof(RectTransform));
                staging.SetActive(false);
                staging.transform.SetParent(root.transform, false);
                try {
                    var instance = Object.Instantiate(prefab, staging.transform, false);
                    instance.SetActive(false);
                    instanceBindings.Add(OfflineUiBindingModule.Register(instance.transform, binding));
                    foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
                        if (text.font) fonts.SetupFont(text.font);
                    foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                        text.Localizer = key => this.tasks.Texts.ContainsKey(key) ? this.tasks.Text(key) : assets.Text(key);
                    foreach (var icon in instance.GetComponentsInChildren<ItemIcon>(true)) {
                        icon.Bind(context);
                        var bridge = icon.gameObject.AddComponent<OfflineHomeTaskRewardIcon>();
                        bridge.Bind(icon);
                    }
                    foreach (var attachment in instance.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
                    foreach (var sound in instance.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = postSound;
                    foreach (var progress in instance.GetComponentsInChildren<WorldItemProgress>(true))
                        progress.Compact = value => CultureNumberArg.Compact(value).ToString();
                    instance.transform.SetParent(slot, keepWorld);
                    return instance;
                }
                finally { Object.Destroy(staging); }
            }
        }

        public TaskUI.ItemIcon GetIcon(int classId, Transform parent)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTaskRewardIcons));
            tasks.CopyItemRow(classId); // Refuse items outside the verified default-pool scope.
            var icon = pool.Borrow(parent);
            icon.SetUIGray(false);
            var rect = icon.GetComponent<RectTransform>();
            if (defaultTransform) Copy(rect, defaultTransform);
            else {
                defaultTransform = new GameObject("TaskIconDefaultTransform", typeof(RectTransform)).GetComponent<RectTransform>();
                defaultTransform.SetParent(root.transform, false);
                Copy(defaultTransform, rect);
            }
            icon.Init(classId);
            return icon.GetComponent<OfflineHomeTaskRewardIcon>();
        }

        // Selected UIUtils.CopyRectTransform, preserving assignment order.
        static void Copy(RectTransform target, RectTransform source)
        {
            target.pivot = source.pivot; target.sizeDelta = source.sizeDelta;
            target.anchorMax = source.anchorMax; target.anchorMin = source.anchorMin;
            target.anchoredPosition = source.anchoredPosition; target.localScale = source.localScale;
            target.localRotation = source.localRotation; target.localPosition = source.localPosition;
        }
        public void RecycleIcon(TaskUI.ItemIcon icon)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTaskRewardIcons));
            if (!(icon is OfflineHomeTaskRewardIcon bridge)) throw new ArgumentException("Foreign task icon.", nameof(icon));
            pool.Return(bridge.Native);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pool.Destory(); attachments.Dispose();
            foreach (var binding in instanceBindings) binding.Dispose();
            instanceBindings.Clear(); registration.Dispose(); Object.Destroy(root);
        }
    }
}
