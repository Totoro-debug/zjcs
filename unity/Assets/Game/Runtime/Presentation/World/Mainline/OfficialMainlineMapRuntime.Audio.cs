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
            if (mapClassId != 11 && mapClassId != 12 && mapClassId != 14) return;
            var musicObject = new GameObject("OfficialMap" + mapClassId + "Bgm");
            mapMusic = musicObject.AddComponent<OfficialMap11MusicPlayer>();
            mapMusic.Initialize(() => MapMusicVolume?.Invoke() ?? 1f, mapClassId);
            if (mapClassId == 14) return;
            var voiceObject = new GameObject("OfficialMap" + mapClassId + "DialogueVoice");
            voiceObject.transform.SetParent(transform, false);
            mapVoice = voiceObject.AddComponent<OfficialMap11StoryAudioPlayer>();
            mapVoice.Initialize(() => MapVoiceVolume?.Invoke() ?? 1f, mapClassId);
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
