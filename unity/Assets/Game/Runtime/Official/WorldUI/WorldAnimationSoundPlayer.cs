using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // launch-source/AnimationSoundPlayer.cs; Wwise Post uses the local UI audio service.
    public sealed class WorldAnimationSoundPlayer : MonoBehaviour
    {
        public List<AudioClip> mEvents;
        [NonSerialized] public Action<AudioClip, GameObject> Post;

        public void PostEvent(int index)
        {
            if (index < 0 || index >= mEvents.Count)
                Debug.LogError($"index out of range, index:{index}, event count:{mEvents.Count}");
            AudioClip audio = mEvents[index];
            Post?.Invoke(audio, gameObject);
        }
    }
}
