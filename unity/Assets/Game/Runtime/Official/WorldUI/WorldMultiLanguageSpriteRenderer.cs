using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public interface IWorldLanguageSpriteHandle : IDisposable
    {
        Sprite Result { get; }
        void Release(bool tryUnloadAsset);
    }

    public interface IWorldLanguageSpriteServices
    {
        string CurrentLanguage { get; }
        void Register(WorldMultiLanguageSpriteRenderer renderer);
        void Unregister(WorldMultiLanguageSpriteRenderer renderer);
        IWorldLanguageSpriteHandle LoadSprite(string path);
    }

    // Original MultiLanguageSpriteRenderer / MultiLanguageUI behavior with the
    // language registry and resource loader supplied by the owning scene.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldMultiLanguageSpriteRenderer : MonoBehaviour
    {
        [SerializeField] private string mKey;
        private SpriteRenderer mSpriteRenderer;
        private IWorldLanguageSpriteHandle mAssetHandle;
        public IWorldLanguageSpriteServices Services { get; set; }
        public SpriteRenderer SpriteRenderer => mSpriteRenderer ? mSpriteRenderer : (mSpriteRenderer = GetComponent<SpriteRenderer>());

        private void Awake()
        {
            if (Services == null) throw new InvalidOperationException("Bind language sprite services before activation.");
            Services.Register(this);
        }

        private void Start() => Refresh(Services.CurrentLanguage);

        public void Refresh(string language)
        {
            mAssetHandle?.Release(true);
            mAssetHandle = Services.LoadSprite($"Language/{language}/{mKey}");
            SpriteRenderer.sprite = mAssetHandle.Result;
        }

        private void OnDestroy()
        {
            mAssetHandle?.Dispose();
            Services?.Unregister(this);
        }
    }
}
