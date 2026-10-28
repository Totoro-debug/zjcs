using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Zjcs.Presentation.Character
{
    // UIVideoPlayer/VideoRTOutput. Local URL replaces the original GUID loader.
    internal sealed class OfficialSkillSkinVideo : MonoBehaviour
    {
        private VideoPlayer player;
        private RawImage image;
        private RenderTexture texture;
        public event Action<uint, uint> VideoPrepared;

        public void Initialize(RectTransform screen, Dictionary<string, object> binding)
        {
            image = screen.GetComponent<RawImage>();
            if (image == null) image = screen.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.isLooping = Convert.ToBoolean(binding["m_Looping"]);
            player.skipOnDrop = Convert.ToBoolean(binding["m_SkipOnDrop"]);
            player.waitForFirstFrame = Convert.ToBoolean(binding["m_WaitForFirstFrame"]);
            player.aspectRatio = (VideoAspectRatio)Convert.ToInt32(binding["m_AspectRatio"]);
            player.timeUpdateMode = (VideoTimeUpdateMode)Convert.ToInt32(binding["m_TimeUpdateMode"]);
            player.playbackSpeed = Convert.ToSingle(binding["m_PlaybackSpeed"]);
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.prepareCompleted += Prepared;
            player.frameReady += FrameReady;
            player.errorReceived += Failed;
            Apply(null);
        }

        public void Play(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Official skill preview video is absent.", path);
            player.Stop();
            Apply(null);
            player.url = path;
            player.Prepare();
        }

        private void Prepared(VideoPlayer source)
        {
            if (source.width == 0 || source.height == 0) return;
            if (texture == null || texture.width != source.width || texture.height != source.height) {
                Release();
                // Media Foundation writes RGBA surfaces on Windows. RGB565 from
                // the Android prefab produces an unsupported video render target.
                RenderTextureFormat format = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor
                    ? RenderTextureFormat.ARGB32 : (RenderTextureFormat)4;
                texture = RenderTexture.GetTemporary((int)source.width, (int)source.height, 0, format);
            }
            source.targetTexture = texture;
            VideoPrepared?.Invoke(source.width, source.height);
            source.sendFrameReadyEvents = true;
            source.Play();
        }

        private void FrameReady(VideoPlayer source, long frame) { Apply(texture); source.sendFrameReadyEvents = false; }
        private void Failed(VideoPlayer source, string message) { Debug.LogError("Official skill preview: " + message); }
        private void Apply(Texture value) { image.texture = value; Color color = image.color; color.a = value == null ? 0f : 1f; image.color = color; }
        private void Release() { if (player != null) player.targetTexture = null; if (texture != null) RenderTexture.ReleaseTemporary(texture); texture = null; }
        private void OnDisable() { if (player != null) player.Stop(); if (image != null) Apply(null); }
        private void OnDestroy() { Release(); }
    }
}
