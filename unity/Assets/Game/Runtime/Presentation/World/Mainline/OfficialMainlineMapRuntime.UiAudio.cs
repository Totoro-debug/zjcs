using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        // The desktop host registers this source in the existing Sound settings channel.
        public Action<AudioSource, bool> MapSoundRegistration { get; set; }

        private void PostMap12PopupSound(AudioClip clip, GameObject emitter) =>
            OfficialDungeonUiAudio.Post(clip, emitter, MapSoundRegistration);
    }
}
