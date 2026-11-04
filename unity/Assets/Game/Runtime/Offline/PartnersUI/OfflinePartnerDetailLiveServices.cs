using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerDetailLiveServices : IPartnerDetailLiveItemServices
    {
        private readonly PartnerNativeDetailActorAssets assets;
        private readonly Func<int, PartnerDetailPresentationInfo> npcInfo;
        private readonly Func<int, string> livePath;
        private sealed class Operation : IProfessionAssetOperation<ScriptableObject>
        {
            private ScriptableObject asset;
            private bool disposed;
            internal Operation(ScriptableObject value) { asset = value; }
            public bool IsValid => !disposed;
            public int Status => !disposed && asset ? 2 : 3;
            public ScriptableObject Result => disposed ? throw new ObjectDisposedException(nameof(Operation)) : asset;
            public event Action<IProfessionAssetOperation<ScriptableObject>> Completed
            { add { value?.Invoke(this); } remove { } }
            public void Dispose() { disposed = true; asset = null; }
        }
        public OfflinePartnerDetailLiveServices(PartnerNativeDetailActorAssets assets,
            Func<int, PartnerDetailPresentationInfo> npcInfo, Func<int, string> livePath)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.npcInfo = npcInfo ?? throw new ArgumentNullException(nameof(npcInfo));
            this.livePath = livePath ?? throw new ArgumentNullException(nameof(livePath));
        }
        private static PartnerNativeUiActor Actor(Component component)
        {
            var actor = component ? component.GetComponent<PartnerNativeUiActor>() : null;
            return actor ? actor : throw new InvalidOperationException("Bind the original live graphic to its native actor.");
        }
        public PartnerDetailPresentationInfo GetNpcInfo(int npcId) => npcInfo(npcId);
        public IPartnerSequenceGraphic Resolve(Component graphic) => Actor(graphic);
        public bool HasSkeletonData(Component graphic) => Actor(graphic).SkeletonDataAsset;
        public void Initialize(Component graphic, bool overwrite) => Actor(graphic).Graphic.Initialize(overwrite);
        public bool TryGetLiveImagePath(int npcId, out string path)
        { path = livePath(npcId); return !string.IsNullOrEmpty(path); }
        public IProfessionAssetOperation<ScriptableObject> LoadSkeleton(string path) => new Operation(assets.LoadAsset(path));
        public void SetSkeletonData(Component graphic, ScriptableObject asset)
            => Actor(graphic).SelectAsset(asset as PartnerNativeUiSkeletonAsset ??
                throw new ArgumentException("Bind the exact original live skeleton."), false);
        public void PlayAnimation(Component graphic, string name, bool loop, float mixDuration, bool endResumeLast)
            => (Actor(graphic).Pose ?? throw new InvalidOperationException("Initialize the original live graphic before playing."))
                .Play(name, loop, mixDuration, endResumeLast);
        public void BindTree(GameObject root)
        {
            if (!root || root.activeInHierarchy)
                throw new InvalidOperationException("Bind the original live prefab before activation.");
            foreach (var item in root.GetComponentsInChildren<NpcDetailLiveItem>(true)) item.Configure(this);
            foreach (var sequence in root.GetComponentsInChildren<SkeletonGraphicAnimSequence>(true)) sequence.Configure(Resolve);
        }
    }
}
