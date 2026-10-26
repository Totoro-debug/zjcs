using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    // Wwise sound MakeUpGain is applied after the voice volume setting. Keep
    // positive dB gains without AudioSource.volume clamping them to one.
    [DisallowMultipleComponent]
    public sealed class OfficialWorldVoiceGain : MonoBehaviour
    {
        private volatile float amplitude = 1f;
        public void SetDecibels(float decibels) => amplitude = (float)Math.Pow(10d, decibels / 20d);
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float value = amplitude;
            if (value == 1f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= value;
        }
    }
}
