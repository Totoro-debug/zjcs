using System;
using UnityEngine;

namespace Zjcs.Offline.HomeStory
{
    // Values come from the selected official StorySettings asset.
    public sealed class HomeStorySettings
    {
        [Serializable] private sealed class Payload { public string manifest; public Settings settings; }
        [Serializable] private sealed class Settings
        {
            public string mDialogVoiceStopEventKey;
            public int[] mVoiceOverIds;
            public string TalkBubblePlayablePath;
        }
        private readonly Settings settings;
        private HomeStorySettings(Settings settings) { this.settings = settings; }
        public string DialogVoiceStopEventKey => settings.mDialogVoiceStopEventKey;
        public string TalkBubblePlayablePath => settings.TalkBubblePlayablePath;
        public bool IsVoiceOver(int roleId) => Array.IndexOf(settings.mVoiceOverIds, roleId) >= 0;

        public static HomeStorySettings LoadDefault()
        {
            var source = Resources.Load<TextAsset>("Home/story-settings");
            if (!source) throw new InvalidOperationException("The original story settings are not bound.");
            var payload = JsonUtility.FromJson<Payload>(source.text);
            if (payload == null || payload.manifest != "884_156692" || payload.settings == null
                || payload.settings.mVoiceOverIds == null || string.IsNullOrEmpty(payload.settings.mDialogVoiceStopEventKey))
                throw new InvalidOperationException("The original story settings are invalid.");
            return new HomeStorySettings(payload.settings);
        }
    }
}
