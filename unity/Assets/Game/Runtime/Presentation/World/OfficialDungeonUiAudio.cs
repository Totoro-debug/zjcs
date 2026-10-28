using System;
using System.Collections;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    // The original Wwise event gain and edits are baked into the verified local clip.
    // Keep its emitter alive when PopupWindow.Hide destroys its original parent.
    public sealed class OfficialDungeonUiAudio : MonoBehaviour
    {
        private AudioSource source;
        private Action<AudioSource, bool> registration;

        public static void Post(AudioClip clip, GameObject emitter, Action<AudioSource, bool> register)
        {
            if (clip == null) return;
            var host = new GameObject("OfficialDungeonUiSound:" + clip.name);
            host.transform.position = emitter.transform.position;
            DontDestroyOnLoad(host);
            var player = host.AddComponent<OfficialDungeonUiAudio>();
            player.source = host.AddComponent<AudioSource>();
            player.source.playOnAwake = false;
            player.source.spatialBlend = 0f;
            player.source.loop = false;
            player.source.clip = clip;
            player.registration = register;
            register?.Invoke(player.source, true);
            player.source.Play();
            player.StartCoroutine(player.Remove(clip.length));
        }

        private IEnumerator Remove(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            while (source != null && source.isPlaying) yield return null;
            Unregister();
            Destroy(gameObject);
        }

        private void Unregister()
        {
            Action<AudioSource, bool> callback = registration;
            registration = null;
            callback?.Invoke(source, false);
        }

        private void OnDestroy() => Unregister();
    }
}
