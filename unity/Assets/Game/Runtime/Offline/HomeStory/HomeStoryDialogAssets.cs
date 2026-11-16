using System;
using UnityEngine;

namespace Zjcs.Offline.HomeStory
{
    public sealed class HomeStoryDialogAssets : ScriptableObject
    {
        [Serializable] public sealed class VoiceBinding { public string EventKey; public AudioClip Clip; }
        public GameObject OptionTalkDialog;
        public GameObject InvisibleMask;
        public TextAsset StorySource;
        public VoiceBinding[] Voices;

        public static HomeStoryDialogAssets LoadDefault()
        {
            var assets = Resources.Load<HomeStoryDialogAssets>("Home/HomeStoryDialogAssets");
            if (!assets || !assets.OptionTalkDialog || !assets.InvisibleMask || !assets.StorySource || assets.Voices == null)
                throw new InvalidOperationException("The original story dialogue is not bound.");
            return assets;
        }
    }
}
