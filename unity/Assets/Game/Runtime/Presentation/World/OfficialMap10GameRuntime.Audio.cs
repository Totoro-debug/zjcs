using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        public Func<float> MapMusicVolume { get; set; }
        private OfficialMap11MusicPlayer mapMusic;

        private void InitializeMapAudio()
        {
            if (mapClassId != 11) return;
            var musicObject = new GameObject("OfficialMap11Bgm");
            mapMusic = musicObject.AddComponent<OfficialMap11MusicPlayer>();
            mapMusic.Initialize(() => MapMusicVolume?.Invoke() ?? 1f);
        }

        private void DisposeMapAudio()
        {
            if (mapMusic != null) mapMusic.StopAndRelease();
            mapMusic = null;
        }
    }
}
