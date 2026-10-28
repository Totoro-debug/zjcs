using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    public sealed class HomeUiAnimationPlayer : MonoBehaviour
    {
        private sealed class BoundTrack
        {
            public Transform Target;
            public string Property;
            public AnimationCurve Curve;
        }

        private Dictionary<string, GameObject> nodes;
        private Coroutine playback;

        public bool IsPlaying => playback != null;
        public float PlaybackTime { get; private set; }
        public float PlaybackDeltaTime { get; private set; }

        public void Initialize(Dictionary<string, GameObject> nodeMap)
        {
            nodes = nodeMap;
        }

        public bool Play(HomeUiAnimation animation, Action completed, bool? loop)
        {
            if (animation == null || animation.tracks == null) return false;
            var tracks = new List<BoundTrack>(animation.tracks.Length);
            float duration = Math.Max(0f, animation.duration);
            foreach (HomeUiAnimationTrack source in animation.tracks)
            {
                if (source?.keys == null || source.keys.Length == 0
                    || string.IsNullOrEmpty(source.path) || string.IsNullOrEmpty(source.property)
                    || nodes == null || !nodes.TryGetValue(source.path, out GameObject target) || target == null)
                    continue;
                HomeUiAnimationKey[] keys = source.keys.OrderBy(value => value.time).ToArray();
                var keyframes = new Keyframe[keys.Length];
                for (int index = 0; index < keys.Length; index++)
                {
                    HomeUiAnimationKey key = keys[index];
                    keyframes[index] = new Keyframe(key.time, key.value,
                        key.in_step ? float.PositiveInfinity : key.in_slope,
                        key.out_step ? float.PositiveInfinity : key.out_slope);
                    duration = Mathf.Max(duration, key.time);
                }
                tracks.Add(new BoundTrack
                {
                    Target = target.transform,
                    Property = source.property,
                    Curve = new AnimationCurve(keyframes)
                });
            }
            if (tracks.Count == 0) return false;
            if (playback != null) StopCoroutine(playback);
            if (duration <= 0f)
            {
                // Official default-state clips can contain only a pose at time zero.
                playback = null;
                foreach (BoundTrack track in tracks) Apply(track, track.Curve.Evaluate(0f));
                completed?.Invoke();
                return true;
            }
            playback = StartCoroutine(Run(tracks, duration, loop ?? animation.loop, completed));
            return true;
        }

        private IEnumerator Run(List<BoundTrack> tracks, float duration, bool loop, Action completed)
        {
            float elapsed = 0f;
            do
            {
                PlaybackTime = elapsed;
                PlaybackDeltaTime = Time.unscaledDeltaTime;
                float time = loop ? elapsed % duration : Mathf.Min(elapsed, duration);
                foreach (BoundTrack track in tracks) Apply(track, track.Curve.Evaluate(time));
                if (!loop && elapsed >= duration) break;
                elapsed += PlaybackDeltaTime;
                yield return null;
            }
            while (true);

            playback = null;
            completed?.Invoke();
        }

        private static void Apply(BoundTrack track, float value)
        {
            Transform target = track.Target;
            if (target == null) return;
            RectTransform rect = target as RectTransform;
            switch (track.Property)
            {
                case "m_AnchoredPosition.x":
                case "m_AnchoredPosition.y":
                    if (rect == null) return;
                    Vector2 position = rect.anchoredPosition;
                    if (track.Property.EndsWith(".x", StringComparison.Ordinal)) position.x = value;
                    else position.y = value;
                    rect.anchoredPosition = position;
                    return;
                case "m_SizeDelta.x":
                case "m_SizeDelta.y":
                    if (rect == null) return;
                    Vector2 size = rect.sizeDelta;
                    if (track.Property.EndsWith(".x", StringComparison.Ordinal)) size.x = value;
                    else size.y = value;
                    rect.sizeDelta = size;
                    return;
                case "scale.x":
                case "scale.y":
                case "scale.z":
                    Vector3 scale = target.localScale;
                    SetAxis(ref scale, track.Property[6], value);
                    target.localScale = scale;
                    return;
                case "rotation.x":
                case "rotation.y":
                case "rotation.z":
                    Vector3 rotation = target.localEulerAngles;
                    SetAxis(ref rotation, track.Property[9], value);
                    target.localEulerAngles = rotation;
                    return;
                case "m_IsActive":
                    target.gameObject.SetActive(value >= 0.5f);
                    return;
                case "m_Alpha":
                    CanvasGroup group = target.GetComponent<CanvasGroup>();
                    Graphic alphaGraphic = target.GetComponent<Graphic>();
                    if (group != null) group.alpha = value;
                    else if (alphaGraphic != null)
                    {
                        Color alphaColor = alphaGraphic.color;
                        alphaColor.a = value;
                        alphaGraphic.color = alphaColor;
                    }
                    return;
                case "m_Color.r":
                case "m_Color.g":
                case "m_Color.b":
                case "m_Color.a":
                    Graphic graphic = target.GetComponent<Graphic>();
                    if (graphic == null) return;
                    Color color = graphic.color;
                    SetColor(ref color, track.Property[8], value);
                    graphic.color = color;
                    return;
            }
        }

        private static void SetAxis(ref Vector3 vector, char axis, float value)
        {
            if (axis == 'x') vector.x = value;
            else if (axis == 'y') vector.y = value;
            else vector.z = value;
        }

        private static void SetColor(ref Color color, char channel, float value)
        {
            if (channel == 'r') color.r = value;
            else if (channel == 'g') color.g = value;
            else if (channel == 'b') color.b = value;
            else color.a = value;
        }
    }
}
