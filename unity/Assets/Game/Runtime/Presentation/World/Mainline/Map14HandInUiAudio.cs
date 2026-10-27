using System.Collections;
using UnityEngine;
namespace Zjcs.Presentation.World
{
    // Local replacement for Wwise posting; gain/edits are baked from the original event.
    public sealed class Map14HandInUiAudio : MonoBehaviour
    {
        public static void Post(AudioClip clip, GameObject emitter)
        {
            if (clip == null) return;
            var host = new GameObject("Map14HandInSound:" + clip.name);
            host.transform.position = emitter.transform.position;
            DontDestroyOnLoad(host);
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.loop = false;
            source.clip = clip; source.Play();
            host.AddComponent<Map14HandInUiAudio>().StartCoroutine(Remove(host, clip.length));
        }
        private static IEnumerator Remove(GameObject host, float duration)
        { yield return new WaitForSecondsRealtime(duration); Destroy(host); }
    }
}
