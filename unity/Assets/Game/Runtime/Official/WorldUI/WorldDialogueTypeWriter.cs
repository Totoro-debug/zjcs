using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Port of TMP_TypeWriterEffect; MathUtils.Lerp(int) uses decimal banker rounding.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class WorldDialogueTypeWriter : MonoBehaviour
    {
        private TMP_Text text;
        private Coroutine playing;
        private Action<bool> stopped;
        public bool IsPlaying { get; private set; }

        public void StartType(float interval, Action<bool> callback = null)
        {
            StopType();
            text = GetComponent<TMP_Text>();
            stopped = callback;
            if (!string.IsNullOrEmpty(text.text)) playing = StartCoroutine(Write(interval));
        }

        private IEnumerator Write(float interval)
        {
            IsPlaying = true;
            text.ForceMeshUpdate();
            int count = text.textInfo.characterCount;
            float duration = count * interval;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                text.maxVisibleCharacters = (int)Math.Round((decimal)count * (decimal)Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            text.maxVisibleCharacters = int.MaxValue;
            IsPlaying = false;
            playing = null;
            stopped?.Invoke(false);
            stopped = null;
        }

        public void StopType()
        {
            if (playing == null) return;
            IsPlaying = false;
            StopCoroutine(playing);
            playing = null;
            stopped?.Invoke(true);
            stopped = null;
            text.maxVisibleCharacters = int.MaxValue;
        }

        private void OnDisable() => StopType();
    }
}
