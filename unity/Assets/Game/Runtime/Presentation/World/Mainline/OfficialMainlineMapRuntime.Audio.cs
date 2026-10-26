using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        public Func<float> MapMusicVolume { get; set; }
        public Func<float> MapVoiceVolume { get; set; }
        private OfficialMap11MusicPlayer mapMusic;
        private OfficialMap11StoryAudioPlayer mapVoice;

        private void InitializeMapAudio()
        {
            if (mapClassId != 11) return;
            var musicObject = new GameObject("OfficialMap11Bgm");
            mapMusic = musicObject.AddComponent<OfficialMap11MusicPlayer>();
            mapMusic.Initialize(() => MapMusicVolume?.Invoke() ?? 1f);
            var voiceObject = new GameObject("OfficialMap11DialogueVoice");
            voiceObject.transform.SetParent(transform, false);
            mapVoice = voiceObject.AddComponent<OfficialMap11StoryAudioPlayer>();
            mapVoice.Initialize(() => MapVoiceVolume?.Invoke() ?? 1f);
        }

        private void DisposeMapAudio()
        {
            if (mapMusic != null) mapMusic.StopAndRelease();
            mapMusic = null;
            if (mapVoice != null)
            {
                mapVoice.StopVoice();
                Destroy(mapVoice.gameObject);
            }
            mapVoice = null;
        }
    }
}
