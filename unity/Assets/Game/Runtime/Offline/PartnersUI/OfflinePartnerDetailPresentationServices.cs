using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerDetailPresentationServices : IPartnerDetailPresentationServices, IPartnerDetailLiveControllerServices
    {
        private readonly OfflinePartnerDetailConfiguration configuration;
        private readonly PartnerNativeDetailActorAssets actors;
        private readonly PartnerNativeDetailPresentationAssets presentation;
        private readonly OfflinePartnerUiTextureResources textures;
        private readonly OfflinePartnerDetailLiveServices live;
        private readonly OfflinePartnerAdventureTipServices tips;
        private readonly Action<Vector3, int, int, int> popupTip;
        // Official DesktopDevice -> DeviceUtility.GetMemorySizeLevel.
        public int MemorySizeLevel
        {
            get
            {
                int size = SystemInfo.systemMemorySize;
                if (size < 3000) return 0;
                if (size < 4000) return 1;
                return 2;
            }
        }
        public OfflinePartnerDetailPresentationServices(OfflinePartnerDetailConfiguration configuration,
            PartnerNativeDetailActorAssets actors, PartnerNativeDetailPresentationAssets presentation,
            OfflinePartnerUiTextureResources textures, OfflinePartnerDetailLiveServices live,
            OfflinePartnerAdventureTipServices tips, Action<Vector3, int, int, int> popupTip)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.actors = actors ? actors : throw new ArgumentNullException(nameof(actors));
            this.presentation = presentation ? presentation : throw new ArgumentNullException(nameof(presentation));
            this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
            this.live = live ?? throw new ArgumentNullException(nameof(live));
            this.tips = tips ?? throw new ArgumentNullException(nameof(tips));
            this.popupTip = popupTip ?? throw new ArgumentNullException(nameof(popupTip));
        }
        public PartnerDetailAnimationInfo GetFriendshipAnimation(int npcId, int level) => configuration.GetFriendshipAnimation(npcId, level);
        public PartnerFullBodyImageType GetFullBodyImagePath(int npcId, out string path) => configuration.GetFullBodyImagePath(npcId, out path);
        public string GetNpcStartingAnimName(int npcId) => OfflinePartnerDetailConfiguration.GetNpcStartingAnimName(npcId);
        public IProfessionAssetOperation<Sprite> LoadSprite(string path) => new Lease<Sprite>(presentation.LoadSprite(path));
        public IProfessionAssetOperation<GameObject> LoadPrefab(string path) => new Lease<GameObject>(actors.LoadPrefab(path));
        public GameObject InstantiateKeepScale(GameObject prefab, Transform parent)
        {
            var result = InstantiateInactive(prefab, parent);
            // Original EUIInstantiateFlag.KeepScale = Position|Rotation|AutoActive.
            result.transform.localPosition = Vector3.zero;
            result.transform.localRotation = Quaternion.identity;
            BindTree(result); result.SetActive(true);
            return result;
        }
        public NpcDetailLiveItem Instantiate(GameObject prefab, Transform parent)
        {
            // Original generic UIUtils overload defaults to AutoActive only.
            var result = InstantiateInactive(prefab, parent);
            BindTree(result); result.SetActive(true);
            return result.GetComponent<NpcDetailLiveItem>() ?? throw new InvalidOperationException("Original live controller is unbound.");
        }
        public Component GetSkeletonGraphic(GameObject model)
        {
            if (model && model.name.StartsWith("NpcModel_UI_10611", StringComparison.Ordinal))
                return null;
            var proxy = model.GetComponent<SkeletonGraphicProxy>();
            if (!proxy) throw new InvalidOperationException("Restore the original detail model's graphic proxy.");
            if (!proxy.SkeletonGraphic)
                throw new InvalidOperationException("Original detail model has no native graphic binding.");
            return proxy.SkeletonGraphic;
        }
        public void PlayAnimation(Component graphic, string animation, bool loop)
        {
            var actor = graphic.GetComponent<PartnerNativeUiActor>();
            if (!actor) throw new InvalidOperationException("Original detail graphic has no native actor.");
            actor.PlayAnimation(animation, loop);
        }
        public void DestroyChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--) Object.Destroy(parent.GetChild(index).gameObject);
        }
        public int RandomRange(int minimum, int maximum) => UnityEngine.Random.Range(minimum, maximum);
        public IProfessionAssetOperation<Sprite> LoadAdventureIcon(int category, int value) => tips.LoadIcon(category, value);
        public void PopupAdventureTip(Vector3 position, int mapGroup, int profession, int preference) => popupTip(position, mapGroup, profession, preference);
        public void BindTree(GameObject root)
        {
            if (!root || root.activeInHierarchy) throw new InvalidOperationException("Bind original detail presentation before activation.");
            foreach (var item in root.GetComponentsInChildren<NpcDetailPanelDetailContent>(true)) item.Configure(this);
            foreach (var item in root.GetComponentsInChildren<NpcAdventureConditionContent>(true)) item.Configure(this);
            foreach (var item in root.GetComponentsInChildren<NpcDetailLiveController>(true)) item.Configure(this);
            live.BindTree(root); textures.BindTree(root);
        }
        private static GameObject InstantiateInactive(GameObject prefab, Transform parent)
        {
            if (!prefab) throw new InvalidOperationException("Packaged original detail prefab is unavailable.");
            var staging = new GameObject("Inactive original detail resource binding", typeof(RectTransform));
            staging.SetActive(false); staging.transform.SetParent(parent, false);
            var result = Object.Instantiate(prefab, staging.transform, false); result.SetActive(false);
            result.transform.SetParent(parent, false); Object.Destroy(staging);
            return result;
        }
        private sealed class Lease<T> : IProfessionAssetOperation<T> where T : Object
        {
            private T asset;
            private bool disposed;
            internal Lease(T value) { asset = value; }
            public bool IsValid => !disposed;
            public int Status => !disposed && asset ? 2 : 3;
            public T Result => disposed ? throw new ObjectDisposedException(nameof(Lease<T>)) : asset;
            public event Action<IProfessionAssetOperation<T>> Completed { add { value?.Invoke(this); } remove { } }
            public void Dispose() { disposed = true; asset = null; }
        }
    }
}
