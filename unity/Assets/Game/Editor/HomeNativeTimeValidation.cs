using System;
using UnityEditor;
using UnityEngine;
using Spine.Unity;
using Zjcs.Official.HomeNativeTime;
using Zjcs.Offline.HomeNativeTime;
using Object = UnityEngine.Object;

namespace Zjcs.Editor
{
    public static class HomeNativeTimeValidation
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var root = new GameObject("OwnedNativeHomeClockValidation");
            root.SetActive(false);
            var dynamicAnimation = root.AddComponent<Animation>();
            var clip = new AnimationClip { legacy = true };
            dynamicAnimation.AddClip(clip, "clock-probe");
            var animatorObject = new GameObject("Animator"); animatorObject.transform.SetParent(root.transform, false);
            var animator = animatorObject.AddComponent<Animator>();
            var skeletonObject = new GameObject("Skeleton"); skeletonObject.transform.SetParent(root.transform, false);
            var skeleton = skeletonObject.AddComponent<SkeletonAnimation>();
            float delta = .25f, scale = 3;
            var clock = new OfflineHomeNativeTime(() => delta, () => scale);
            try
            {
                clock.Events.Publish(new HomeTimeDriveAnimationData { Animation = dynamicAnimation, IsRegister = true });
                Require(dynamicAnimation["clock-probe"].speed == 1, "Inactive original driver ignores registrations.");
                clock.Enable();
                clock.Events.Publish(new HomeTimeDriveAnimationData { Animation = dynamicAnimation, IsRegister = true });
                clock.Events.Publish(new HomeTimeDriveAnimatorData { Animator = animator, IsRegister = true });
                clock.Events.Publish(new HomeTimeDriveSkeletonData { SkeletonAnimationList = new[] { skeleton }, IsRegister = true });
                Require((int)skeleton.UpdateTiming == 0, "Original registration switches Spine to manual update.");
                clock.Tick();
                Require(dynamicAnimation["clock-probe"].speed == 3 && animator.speed == 3,
                    "Original clock applies the manager scale to real Animation and Animator objects.");
                scale += .0005f; clock.Tick();
                Require(dynamicAnimation["clock-probe"].speed == 3, "Original scale threshold prevents a redundant speed update.");
                scale = 8; clock.Tick();
                Require(dynamicAnimation["clock-probe"].speed == 8 && animator.speed == 8,
                    "A changed manager scale reaches every registered object.");
                clock.Events.Publish(new HomeTimeDriveAnimatorData { Animator = animator, IsRegister = false });
                Require(animator.speed == 1, "Original unregister restores Animator speed.");
                clock.Disable();
                Require(dynamicAnimation["clock-probe"].speed == 1 && (int)skeleton.UpdateTiming == 1,
                    "Original disable resets remaining dynamic animation and skeleton objects.");
                clock.Enable(); scale = 12; clock.Tick();
                Require(dynamicAnimation["clock-probe"].speed == 1, "Re-enabling cannot retain stale dynamic registrations.");
                clock.Dispose();
                clock.Events.Publish(new HomeTimeDriveAnimationData { Animation = dynamicAnimation, IsRegister = true });
                Require(dynamicAnimation["clock-probe"].speed == 1, "Destroyed original driver releases its message subscriptions.");
                Debug.Log("HOME_NATIVE_TIME_PASSED: actual Unity animation speeds, Spine registration, scale boundary, reset and disposal.");
            }
            finally { clock.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(clip); }
        }
    }
}
