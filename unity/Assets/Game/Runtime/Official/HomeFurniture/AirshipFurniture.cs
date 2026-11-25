// Original airship controller; resource, display and trade owners are explicit ports.
using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Official.HomeFurniture
{
    public interface IAirshipSpriteLease : IDisposable
    {
        Sprite Sprite { get; }
    }

    public interface IAirshipFurnitureServices
    {
        IDisposable SubscribeDisplayItemChange(Action<int, Sprite> callback);
        IAirshipSpriteLease LoadItemBackground(bool repaired);
        void PlayAnimation(SkeletonAnimation skeleton, string animation, bool loop);
        void OpenTradePanel();
    }

    public class AirshipFurniture : FurnitureBase
    {
        [SerializeField] private SkeletonAnimation mAirship;
        [SerializeField] private string mAirshipActiveAnim;
        [SerializeField] private List<SpriteRenderer> mItemBgList;
        [SerializeField] private List<SpriteRenderer> mItemIconList;
        [SerializeField] private List<SpriteRenderer> mLightSpriteRenderers;

        private readonly List<IAirshipSpriteLease> backgrounds = new List<IAirshipSpriteLease>();
        private List<Material> lightMaterials;
        private IDisposable displaySubscription;
        public IAirshipFurnitureServices AirshipServices { get; set; }

        private void OnEnable()
        {
            if (AirshipServices == null) throw new InvalidOperationException("Bind airship services before activation.");
            displaySubscription = AirshipServices.SubscribeDisplayItemChange(OnDisplayItemChange);
        }

        private void OnDisable()
        {
            displaySubscription?.Dispose();
            displaySubscription = null;
            foreach (IAirshipSpriteLease background in backgrounds) background.Dispose();
            backgrounds.Clear();
        }

        private void OnDisplayItemChange(int index, Sprite sprite)
        {
            if (index < mItemIconList.Count) mItemIconList[index].sprite = sprite;
        }

        protected override void OnClick() => AirshipServices.OpenTradePanel();

        public override void UpdateState()
        {
            base.UpdateState();
            bool repaired = mFurnitureState == EFurnitureState.Functional;
            if (repaired) AirshipServices.PlayAnimation(mAirship, mAirshipActiveAnim, true);
            SetBg(repaired);
            SetLightAtten(repaired ? 1 : 0);
        }

        private void SetBg(bool repaired)
        {
            IAirshipSpriteLease lease = AirshipServices.LoadItemBackground(repaired);
            backgrounds.Add(lease);
            foreach (SpriteRenderer background in mItemBgList) background.sprite = lease.Sprite;
        }

        private void SetLightAtten(float value)
        {
            if (lightMaterials == null)
            {
                lightMaterials = new List<Material>();
                foreach (SpriteRenderer renderer in mLightSpriteRenderers) lightMaterials.Add(renderer.material);
            }
            foreach (Material material in lightMaterials) material.SetFloat(Shader.PropertyToID("_LightAtten"), value);
        }
    }
}
