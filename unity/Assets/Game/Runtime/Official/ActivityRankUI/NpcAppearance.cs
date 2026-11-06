using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ActivityRankUI
{
    // Original AppearanceSystem.NpcAppearance; original configuration lookup is
    // supplied by its host, and native Spine players implement IAppearanceAnimComponent.
    [RequireComponent(typeof(AppearanceCombineSkin))]
    public sealed class NpcAppearance : MonoBehaviour
    {
        public AppearanceCombineSkin AppearanceCombineSkin;
        [SerializeField] private Component mSkeletonGraphic;
        [SerializeField] private Component mSkeletonAnimation;
        [SerializeField] private bool mAutoSetAnimation = true;
        private void Reset() => AppearanceCombineSkin = GetComponent<AppearanceCombineSkin>();
        public void RefreshAppearance(int npcId)
        {
            AppearanceCombineSkin.SetSkin($"body_{npcId}");
            if (!mAutoSetAnimation) return;
            string animation = $"npc_{npcId}/idle_01";
            if (mSkeletonGraphic != null) mSkeletonGraphic.GetComponent<IPartnerLazyUiGraphic>().PlayAnimation(animation, true);
            if (mSkeletonAnimation != null) mSkeletonAnimation.GetComponent<IAppearanceAnimComponent>().PlayAnimation(animation, true);
        }
    }
}
