// Original animation-name switching; the shared local environment supplies day/night events.
using System;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.HomeFurniture;

namespace Zjcs.Official.HomeFruitTreeWorld
{
    public class HomeSpineDayNightSwitcher : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation mSkeletonAnimation;
        [SerializeField] private string mDayAnim;
        [SerializeField] private string mNightAnim;
        private Action<bool> mOnDayNightChange;
        public IHomeDayNightServices EnvironmentServices { get; set; }
        private void Awake() => mOnDayNightChange = OnDayNightChange;
        private void OnEnable()
        {
            EnvironmentServices.OnDayNightChange += mOnDayNightChange;
            OnDayNightChange(EnvironmentServices.IsPlayableDay);
        }
        private void OnDisable() => EnvironmentServices.OnDayNightChange -= mOnDayNightChange;
        private void OnDayNightChange(bool isDay) => mSkeletonAnimation.AnimationName = isDay ? mDayAnim : mNightAnim;
    }
}
