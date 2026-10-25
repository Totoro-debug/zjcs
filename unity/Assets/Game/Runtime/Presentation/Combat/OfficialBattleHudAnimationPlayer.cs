using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleHudAnimationPlayer : MonoBehaviour
    {
        private sealed class CompiledTrack
        {
            public string path;
            public string property;
            public AnimationCurve curve;
        }

        private sealed class CompiledAnimation
        {
            public string owner;
            public string name;
            public float duration;
            public bool loop;
            public CompiledTrack[] tracks;
        }

        private sealed class Playback
        {
            public CompiledAnimation animation;
            public float elapsed;
        }

        private readonly Dictionary<string, GameObject> targets = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, CompiledAnimation> animations = new Dictionary<string, CompiledAnimation>();
        private readonly Dictionary<string, BattleHudNode> animationSourceNodes =
            new Dictionary<string, BattleHudNode>();
        private readonly Dictionary<Graphic, Material> materialInstances = new Dictionary<Graphic, Material>();
        private readonly List<Playback> playbacks = new List<Playback>();
        private bool manualTick;

        public void Initialize(BattleHudNode[] sourceNodes, Dictionary<string, GameObject> builtNodes,
            bool useManualTick = false)
        {
            targets.Clear();
            animations.Clear();
            animationSourceNodes.Clear();
            playbacks.Clear();
            manualTick = useManualTick;
            if (builtNodes != null)
                foreach (KeyValuePair<string, GameObject> node in builtNodes)
                    targets[node.Key] = node.Value;
            if (sourceNodes == null) return;

            foreach (BattleHudNode node in sourceNodes)
            {
                if (node == null) continue;
                if (!string.IsNullOrEmpty(node.path)) animationSourceNodes[node.path] = node;
                if (node.animations == null) continue;
                foreach (BattleHudAnimation animation in node.animations)
                {
                    if (animation == null || string.IsNullOrEmpty(animation.name)) continue;
                    string owner = string.IsNullOrEmpty(animation.owner) ? node.path : animation.owner;
                    animations[Key(owner, animation.name)] = Compile(animation, owner);
                }
            }
        }

        public void PlayDefaultAnimations()
        {
            foreach (KeyValuePair<string, BattleHudNode> entry in animationSourceNodes)
            {
                if (entry.Value == null || !entry.Value.animation_play_automatically) continue;
                string defaultAnimation = entry.Value == null ? null : entry.Value.default_animation;
                if (!string.IsNullOrEmpty(defaultAnimation)) Play(entry.Key, defaultAnimation);
            }
        }

        public bool Play(string ownerPath, string animationName)
        {
            if (string.IsNullOrEmpty(ownerPath) || string.IsNullOrEmpty(animationName)
                || !animations.TryGetValue(Key(ownerPath, animationName), out CompiledAnimation animation))
                return false;

            for (int i = playbacks.Count - 1; i >= 0; i--)
                if (playbacks[i].animation.owner == ownerPath)
                    playbacks.RemoveAt(i);
            var playback = new Playback { animation = animation };
            playbacks.Add(playback);
            Sample(playback, 0f);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || playbacks.Count == 0) return;
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                Playback playback = playbacks[i];
                playback.elapsed += deltaTime;
                float duration = playback.animation.duration;
                float sampleTime = duration <= 0f ? 0f : playback.animation.loop
                    ? Mathf.Repeat(playback.elapsed, duration) : Mathf.Min(playback.elapsed, duration);
                Sample(playback, sampleTime);
                if (duration <= 0f || (!playback.animation.loop && playback.elapsed >= duration))
                    playbacks.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (!manualTick) Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            foreach (Material material in materialInstances.Values)
                if (material != null) Destroy(material);
            materialInstances.Clear();
        }

        private void Sample(Playback playback, float time)
        {
            foreach (CompiledTrack track in playback.animation.tracks)
            {
                if (!targets.TryGetValue(track.path, out GameObject target) || target == null) continue;
                Apply(target, track.property, track.curve.Evaluate(time));
            }
        }

        private CompiledAnimation Compile(BattleHudAnimation source, string owner)
        {
            var tracks = new List<CompiledTrack>();
            float endTime = 0f;
            if (source.tracks != null)
                foreach (BattleHudAnimationTrack track in source.tracks)
                {
                    if (track == null || string.IsNullOrEmpty(track.path) || string.IsNullOrEmpty(track.property)
                        || track.keys == null || track.keys.Length == 0) continue;
                    var keys = new Keyframe[track.keys.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        BattleHudAnimationKey key = track.keys[i];
                        keys[i] = new Keyframe(key.time, key.value, key.in_tangent, key.out_tangent);
                        endTime = Mathf.Max(endTime, key.time);
                    }
                    var curve = new AnimationCurve(keys)
                    {
                        preWrapMode = WrapMode.ClampForever,
                        postWrapMode = WrapMode.ClampForever
                    };
                    tracks.Add(new CompiledTrack { path = track.path, property = track.property, curve = curve });
                }
            float duration = source.duration > 0f ? source.duration : endTime;
            return new CompiledAnimation
            {
                owner = owner,
                name = source.name,
                duration = duration,
                loop = source.loop,
                tracks = tracks.ToArray()
            };
        }

        private void Apply(GameObject target, string property, float value)
        {
            switch (property)
            {
                case "m_IsActive":
                    bool active = value >= 0.5f;
                    if (target.activeSelf != active) target.SetActive(active);
                    return;
                case "scale.x": SetScale(target.transform, 0, value); return;
                case "scale.y": SetScale(target.transform, 1, value); return;
                case "scale.z": SetScale(target.transform, 2, value); return;
                case "rotation.x": SetRotation(target.transform, 0, value); return;
                case "rotation.y": SetRotation(target.transform, 1, value); return;
                case "rotation.z": SetRotation(target.transform, 2, value); return;
                case "m_AnchoredPosition.x": SetAnchoredPosition(target.transform as RectTransform, 0, value); return;
                case "m_AnchoredPosition.y": SetAnchoredPosition(target.transform as RectTransform, 1, value); return;
                case "m_SizeDelta.x": SetSizeDelta(target.transform as RectTransform, 0, value); return;
                case "m_SizeDelta.y": SetSizeDelta(target.transform as RectTransform, 1, value); return;
                case "m_FillAmount":
                    Image image = target.GetComponent<Image>();
                    if (image != null) image.fillAmount = value;
                    return;
                case "material._SquenceRate": SetSequenceRate(target, value); return;
                case "m_Alpha":
                    CanvasGroup group = target.GetComponent<CanvasGroup>();
                    if (group == null) group = target.AddComponent<CanvasGroup>();
                    group.alpha = value;
                    return;
                case "m_Color.r": SetColor(target, 0, value); return;
                case "m_Color.g": SetColor(target, 1, value); return;
                case "m_Color.b": SetColor(target, 2, value); return;
                case "m_Color.a":
                case "m_fontColor.a": SetColor(target, 3, value); return;
                case "m_fontColor.r": SetColor(target, 0, value); return;
                case "m_fontColor.g": SetColor(target, 1, value); return;
                case "m_fontColor.b": SetColor(target, 2, value); return;
            }
        }

        private static void SetScale(Transform target, int axis, float value)
        {
            Vector3 scale = target.localScale;
            if (axis == 0) scale.x = value;
            else if (axis == 1) scale.y = value;
            else scale.z = value;
            target.localScale = scale;
        }

        private static void SetRotation(Transform target, int axis, float value)
        {
            Vector3 rotation = target.localEulerAngles;
            if (axis == 0) rotation.x = value;
            else if (axis == 1) rotation.y = value;
            else rotation.z = value;
            target.localEulerAngles = rotation;
        }

        private static void SetAnchoredPosition(RectTransform target, int axis, float value)
        {
            if (target == null) return;
            Vector2 position = target.anchoredPosition;
            if (axis == 0) position.x = value;
            else position.y = value;
            target.anchoredPosition = position;
        }

        private static void SetSizeDelta(RectTransform target, int axis, float value)
        {
            if (target == null) return;
            Vector2 size = target.sizeDelta;
            if (axis == 0) size.x = value;
            else size.y = value;
            target.sizeDelta = size;
        }

        private static void SetColor(GameObject target, int channel, float value)
        {
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                Color color = graphic.color;
                SetChannel(ref color, channel, value);
                graphic.color = color;
            }
            SpriteRenderer sprite = target.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                Color color = sprite.color;
                SetChannel(ref color, channel, value);
                sprite.color = color;
            }
        }

        private void SetSequenceRate(GameObject target, float value)
        {
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic == null) return;
            if (!materialInstances.TryGetValue(graphic, out Material instance))
            {
                Material source = graphic.material;
                if (source == null || !source.HasProperty("_SquenceRate")) return;
                instance = new Material(source);
                materialInstances.Add(graphic, instance);
                graphic.material = instance;
            }
            if (instance.HasProperty("_SquenceRate")) instance.SetFloat("_SquenceRate", value);
        }

        private static void SetChannel(ref Color color, int channel, float value)
        {
            if (channel == 0) color.r = value;
            else if (channel == 1) color.g = value;
            else if (channel == 2) color.b = value;
            else color.a = value;
        }

        private static string Key(string owner, string name) => owner + "\n" + name;
    }
}
