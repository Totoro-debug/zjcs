using System;
using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeHudUI;

namespace Zjcs.Official.HomeStoryCharacter
{
    // Bound by the original dialogue panel's owner to local assets and settings.
    public interface IStoryPortraitServices
    {
        IHomeHudAssetHandle<GameObject> LoadPortrait(string path);
        RegionCodeEnum Region();
        bool IsVoiceOver(int roleId);
    }

    // StoryUtils.TimeLerp: preserve the first immediate sample, the strict
    // duration comparison and the next-frame cancellation check.
    public static class StoryPortraitTime
    {
        public static async UniTask TimeLerp(float duration, Action<float> callback,
            CancellationToken cancellationToken = default)
        {
            if (duration <= 0f) { callback?.Invoke(1f); return; }
            float time = 0f;
            do
            {
                time += Time.deltaTime;
                callback?.Invoke(Mathf.Min(1f, time / duration));
                if (time > duration) return;
                await UniTask.NextFrame();
            } while (!cancellationToken.IsCancellationRequested);
        }
    }
}
