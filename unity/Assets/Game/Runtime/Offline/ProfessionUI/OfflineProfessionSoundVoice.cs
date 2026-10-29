using System.Collections;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // The voice lifetime remains independent of its originating UI window.
    public sealed class OfflineProfessionSoundVoice : MonoBehaviour
    {
        public void ReleaseAfter(float seconds) => StartCoroutine(Release(seconds));
        private IEnumerator Release(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Destroy(gameObject);
        }
    }
}
