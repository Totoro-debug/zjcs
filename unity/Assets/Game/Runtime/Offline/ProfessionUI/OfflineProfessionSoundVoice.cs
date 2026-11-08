using System.Collections;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // The voice lifetime remains independent of its originating UI window.
    public sealed class OfflineProfessionSoundVoice : MonoBehaviour
    {
        private Coroutine fade;
        public void FadeIn(float seconds, int curve)
        {
            if (seconds <= 0) return;
            if (fade != null) StopCoroutine(fade);
            GetComponent<AudioSource>().volume = 0;
            fade = StartCoroutine(Fade(0, 1, seconds, curve, false));
        }
        public void Stop(float seconds, int curve)
        {
            if (fade != null) StopCoroutine(fade);
            if (seconds <= 0) { Destroy(gameObject); return; }
            fade = StartCoroutine(Fade(GetComponent<AudioSource>().volume, 0, seconds, curve, true));
        }
        private IEnumerator Fade(float from, float to, float seconds, int curve, bool release)
        {
            if (curve != 1) throw new System.NotSupportedException("Inspect the original Wwise fade curve: " + curve);
            var source = GetComponent<AudioSource>();
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                float position = Mathf.Clamp01((Time.realtimeSinceStartup - start) / seconds);
                source.volume = Mathf.LerpUnclamped(from, to, Mathf.Sin(position * Mathf.PI * .5f));
                yield return null;
            }
            source.volume = to; fade = null;
            if (release) Destroy(gameObject);
        }
        public void ReleaseAfter(float seconds) => StartCoroutine(Release(seconds));
        private IEnumerator Release(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Destroy(gameObject);
        }
    }
}
