using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Profession = Zjcs.Official.ProfessionUI.Profession;

namespace Zjcs.Offline.PartnersUI
{
    // The owning UI input host forwards the same global pointer-down event used
    // by the original controller. Window creation/closing stays with that host.
    public sealed class OfflinePartnerAdventureTipServices : IPartnerAdventureTipServices
    {
        private readonly PartnerNativeDetailPresentationAssets assets;
        private readonly Action<GameObject> close;
        private event Action<Vector2> pointerDown;
        public Camera UiCamera { get; }
        public int ActiveIconLeases { get; private set; }
        public int PointerSubscriberCount => pointerDown?.GetInvocationList().Length ?? 0;
        public OfflinePartnerAdventureTipServices(PartnerNativeDetailPresentationAssets assets,
            Camera camera, Action<GameObject> close)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            UiCamera = camera;
            this.close = close ?? throw new ArgumentNullException(nameof(close));
        }
        public IProfessionAssetOperation<Sprite> LoadIcon(int category, int value)
        {
            string guid;
            switch (category)
            {
                case 0: guid = assets.Settings.GetMapGroupData(value).IconSpriteProxy.AssetGuid; break;
                case 1: guid = assets.Settings.GetProfessionData((Zjcs.Official.ProfessionUI.Profession)value).IconSpriteProxy.AssetGuid; break;
                case 2: guid = assets.Settings.GetPreferenceData((NpcGiftType)value).IconSpriteProxy.AssetGuid; break;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
            var sprite = assets.LoadIcon(guid);
            ActiveIconLeases++;
            return new IconLease(sprite, () => ActiveIconLeases--);
        }
        public void HookGlobalPointerDown(Action<Vector2> handler) => pointerDown += handler;
        public void UnhookGlobalPointerDown(Action<Vector2> handler) => pointerDown -= handler;
        public void DispatchPointerDown(Vector2 screenPosition) => pointerDown?.Invoke(screenPosition);
        public void Close(GameObject window) => close(window);
        public void BindTree(GameObject root)
        {
            if (!root || root.activeInHierarchy) throw new InvalidOperationException("Bind the original adventure tip before activation.");
            foreach (var item in root.GetComponentsInChildren<PopTipNpcAdventure>(true)) item.Configure(this);
            foreach (var item in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) item.Localizer = assets.GetText;
        }
        private sealed class IconLease : IProfessionAssetOperation<Sprite>
        {
            private Sprite sprite;
            private Action release;
            internal IconLease(Sprite sprite, Action release) { this.sprite = sprite; this.release = release; }
            public bool IsValid => release != null;
            public int Status => IsValid && sprite ? 2 : 3;
            public Sprite Result => IsValid ? sprite : throw new ObjectDisposedException(nameof(IconLease));
            public event Action<IProfessionAssetOperation<Sprite>> Completed { add { value?.Invoke(this); } remove { } }
            public void Dispose() { var action = release; release = null; sprite = null; action?.Invoke(); }
        }
    }
}
