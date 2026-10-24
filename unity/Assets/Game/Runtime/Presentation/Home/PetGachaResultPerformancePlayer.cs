using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Opening;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    internal sealed class PetGachaPerformanceData
    {
        public string manifest;
        public PetGachaAnimationConfig single, ten;
        public PetGachaHundredConfig hundred;
        public PetGachaRewardPerformance reward;
        public PetGachaActorPerformance actor;
        public PetGachaPerformanceSound[] sounds;
        public float collect_effect_time, award_floating_item_interval, award_collect_sfx_interval,
            show_item_interval;
    }

    [Serializable]
    internal sealed class PetGachaAnimationConfig
    {
        public float protected_time, interval_time;
        public string scene_animation;
        public PetGachaActionAnimation[] actions;
    }

    [Serializable]
    internal sealed class PetGachaActionAnimation
    {
        public string sound;
        public PetGachaAnimationClip[] animations;
    }

    [Serializable]
    internal sealed class PetGachaAnimationClip
    {
        public string name;
        public int loop_times;
        public float exit_time, mix_duration;
    }

    [Serializable]
    internal sealed class PetGachaHundredConfig
    {
        public float show_time;
        public int show_count, drop_interval_frames, drop_interval_count;
    }

    [Serializable]
    internal sealed class PetGachaRewardPerformance
    {
        public float normal_drop_delay, special_drop_delay, item_translate_delay, item_show_delay;
        public PetGachaEffectBinding[] drop_effects, collect_effects, show_effects, quality_effects;
        public PetGachaSoundBinding[] quality_sounds;
        public string translate_effect, collect_feedback_effect;
    }

    [Serializable]
    internal sealed class PetGachaActorPerformance
    {
        public float[] anchor_position, scale, collect_target_position;
    }

    [Serializable]
    internal sealed class PetGachaEffectBinding
    {
        public int quality;
        public string resource;
    }

    [Serializable]
    internal sealed class PetGachaSoundBinding
    {
        public int quality;
        public string @event;
    }

    [Serializable]
    internal sealed class PetGachaPerformanceSound
    {
        public string @event, file;
        public bool stop;
    }

    public sealed class PetGachaResultPerformancePlayer : MonoBehaviour
    {
        private const string PerformerPath = "/PetGachaSubPanel/PetGachaResultPerformer/";
        private const string DefaultAnimation = "home/idle_01";
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioSource> soundSources = new Dictionary<string, AudioSource>(StringComparer.Ordinal);
        private readonly List<GameObject> cards = new List<GameObject>();
        private HomeFeatureRuntime host;
        private HomeAssetCatalog catalog;
        private HomeUiRenderer renderer;
        private IReadOnlyList<OfflinePetGachaAward> awards;
        private Transform rewardLayer;
        private Transform collectTarget;
        private Button skipButton;
        private AudioSource actionSource;
        private PetGachaPerformanceData performance;
        private Dictionary<string, object> effectPrefabs;
        private PetGachaRuntimeEffectRenderer effects;
        private Animator actorAnimator;
        private bool fastForward;
        private bool completed;
        private Action onComplete;
        private int workingAwards;
        private List<Vector2> dropPolygon;
        private float nextCollectSoundAt;

        public void Begin(HomeFeatureRuntime runtime, HomeAssetCatalog assets, HomeUiRenderer stageRenderer,
            IReadOnlyList<OfflinePetGachaAward> result, int drawCount, Button skip, Action finished)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            catalog = assets ?? throw new ArgumentNullException(nameof(assets));
            renderer = stageRenderer ?? throw new ArgumentNullException(nameof(stageRenderer));
            awards = result ?? throw new ArgumentNullException(nameof(result));
            if (drawCount != 1 && drawCount != 10 && drawCount != 100 || awards.Count != drawCount)
                throw new ArgumentException("Pet Gacha performance result count is invalid.", nameof(result));
            skipButton = skip;
            onComplete = finished;
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(Skip);
                skipButton.onClick.AddListener(Skip);
            }
            actionSource = gameObject.AddComponent<AudioSource>();
            actionSource.playOnAwake = false;
            actionSource.spatialBlend = 0f;
            HomeAudioSettings.Register(actionSource, HomeAudioChannel.Sound);
            StartCoroutine(PrepareAndPlay(drawCount));
        }

        public void Skip()
        {
            if (skipButton == null || !skipButton.interactable || completed) return;
            fastForward = true;
        }

        private IEnumerator PrepareAndPlay(int drawCount)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "PetGacha");
            string performancePath = Path.Combine(root, "performance.json");
            string effectsPath = Path.Combine(root, "performance-effects.json");
            if (!File.Exists(performancePath) || !File.Exists(effectsPath))
                throw new FileNotFoundException("Official Pet Gacha performance data is missing.");
            performance = JsonUtility.FromJson<PetGachaPerformanceData>(File.ReadAllText(performancePath));
            if (performance == null || performance.manifest != "854_156383" || performance.reward == null
                || performance.single == null || performance.ten == null || performance.hundred == null)
                throw new FormatException("Official Pet Gacha performance data is incomplete.");
            Dictionary<string, object> effectRoot = OpeningJson.Object(
                OpeningJson.Parse(File.ReadAllBytes(effectsPath)), "Pet Gacha effects");
            effectPrefabs = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (object row in OpeningJson.Array(effectRoot["prefabs"], "Pet Gacha effect prefabs"))
            {
                Dictionary<string, object> prefab = OpeningJson.Object(row, "Pet Gacha effect prefab");
                effectPrefabs[OpeningJson.String(prefab, "key")] = prefab["root"];
            }
            Dictionary<string, object> performanceObject = OpeningJson.Object(
                OpeningJson.Parse(File.ReadAllBytes(performancePath)), "Pet Gacha performance");
            dropPolygon = ReadDropPolygon(performanceObject);
            rewardLayer = renderer.Find(PerformerPath + "SlotContent")?.transform;
            collectTarget = renderer.Find(PerformerPath + "CollectTarget")?.transform;
            if (rewardLayer == null || collectTarget == null)
                throw new InvalidOperationException("Official Pet Gacha performance anchors are missing.");
            effects = gameObject.AddComponent<PetGachaRuntimeEffectRenderer>();
            effects.Configure(catalog, rewardLayer, effectPrefabs);
            BuildActor();
            yield return LoadAudio(root);
            if (completed) yield break;

            PetGachaAnimationConfig actionConfig = drawCount == 1 ? performance.single : performance.ten;
            PetGachaActionAnimation action = PlayAction(actionConfig);
            float protectedTime = Mathf.Max(0f, actionConfig.protected_time);
            if (drawCount > 1)
            {
                yield return Wait(protectedTime);
                if (!fastForward && skipButton != null) skipButton.interactable = true;
            }
            if (fastForward)
            {
                Complete();
                yield break;
            }

            if (drawCount == 100)
                yield return PlayHundred();
            else
                yield return PlaySmallDraw(drawCount > 1);
            if (!fastForward)
            {
                while (workingAwards > 0) yield return null;
                yield return Wait(performance.collect_effect_time);
            }
            Complete();
        }

        private IEnumerator LoadAudio(string root)
        {
            foreach (PetGachaPerformanceSound sound in performance.sounds ?? Array.Empty<PetGachaPerformanceSound>())
            {
                if (sound == null || string.IsNullOrEmpty(sound.@event) || sound.stop || string.IsNullOrEmpty(sound.file))
                    continue;
                string path = Path.Combine(root, sound.file.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
                {
                    Debug.LogWarning("Official Pet Gacha audio is unavailable: " + sound.@event);
                    continue;
                }
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri.AbsoluteUri, AudioType.OGGVORBIS))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("Official Pet Gacha audio could not be decoded for " + sound.@event
                            + ": " + request.error);
                        continue;
                    }
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                    if (clip != null)
                    {
                        clip.name = sound.@event;
                        clips[sound.@event] = clip;
                    }
                }
            }
        }

        private void BuildActor()
        {
            Transform target = renderer.Find(PerformerPath + "PlayerModel_UI")?.transform;
            if (target == null) throw new InvalidOperationException("Official Pet Gacha character anchor is missing.");
            GameObject model = CharacterCreationNativeModelFactory.CreateWorldModel(
                host.ActiveProfileSlotId, host.SavesRoot, out _);
            model.name = "PetGachaPlayer_" + host.ActiveProfileSlotId;
            model.transform.SetParent(target, false);
            float[] position = performance.actor?.anchor_position;
            float[] scale = performance.actor?.scale;
            if (position == null || position.Length < 2 || scale == null || scale.Length < 2)
                throw new InvalidOperationException("Official Pet Gacha character placement is incomplete.");
            model.transform.localPosition = new Vector3(position[0], position[1], 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(scale[0], scale[1], 1f);
            actorAnimator = model.GetComponent<Animator>();
            if (actorAnimator == null)
                throw new InvalidOperationException("Official native character model has no Animator.");
            actorAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actorAnimator.applyRootMotion = false;
            PlayAnimator(DefaultAnimation);
        }

        private PetGachaActionAnimation PlayAction(PetGachaAnimationConfig config)
        {
            if (config == null || config.actions == null || config.actions.Length == 0) return null;
            PetGachaActionAnimation action = config.actions[UnityEngine.Random.Range(0, config.actions.Length)];
            PlaySound(action.sound);
            if (!string.IsNullOrEmpty(config.scene_animation))
                renderer.PlayAnimation(config.scene_animation);
            if (action.animations != null && action.animations.Length > 0)
                StartCoroutine(PlayActionSequence(action.animations));
            return action;
        }

        private IEnumerator PlayActionSequence(PetGachaAnimationClip[] sequence)
        {
            for (int index = 0; index < sequence.Length; index++)
            {
                PetGachaAnimationClip clip = sequence[index];
                if (clip == null || string.IsNullOrEmpty(clip.name)) continue;
                PlayAnimator(clip.name);
                if (clip.loop_times < 0 || index == sequence.Length - 1) yield break;
                yield return Wait(AnimatorClipLength(clip.name));
            }
        }

        private float AnimatorClipLength(string name)
        {
            foreach (AnimationClip clip in actorAnimator.runtimeAnimatorController.animationClips)
                if (clip.name == name || clip.name.EndsWith("/" + name.Split('/').Last(), StringComparison.Ordinal))
                    return Mathf.Max(0.05f, clip.length);
            return 0.35f;
        }

        private void PlayAnimator(string requestedName)
        {
            if (actorAnimator == null || string.IsNullOrEmpty(requestedName)) return;
            string shortName = requestedName.Split('/').Last();
            string[] candidates = { requestedName, "Base Layer." + requestedName,
                shortName, "Base Layer." + shortName };
            foreach (string candidate in candidates)
            {
                int hash = Animator.StringToHash(candidate);
                if (!actorAnimator.HasState(0, hash)) continue;
                actorAnimator.Play(hash, 0, 0f);
                return;
            }
            actorAnimator.Play(shortName, 0, 0f);
        }

        private IEnumerator PlaySmallDraw(bool special)
        {
            for (int index = 0; index < awards.Count; index++)
            {
                if (fastForward) yield break;
                StartCoroutine(ShowAward(awards[index], special));
                if (index + 1 < awards.Count) yield return Wait(performance.show_item_interval);
            }
        }

        private IEnumerator PlayHundred()
        {
            List<OfflinePetGachaAward> ranked = awards
                .OrderByDescending(value => AwardItemTypeScore(value.ItemId))
                .ThenByDescending(value => value.Quality)
                .Take(Mathf.Min(performance.hundred.show_count, awards.Count)).ToList();
            var selected = new HashSet<OfflinePetGachaAward>(ranked);
            float interval = performance.hundred.show_time / Mathf.Max(1, performance.hundred.show_count);
            int shown = 0;
            foreach (OfflinePetGachaAward award in awards)
            {
                if (!selected.Contains(award)) continue;
                if (fastForward) yield break;
                StartCoroutine(ShowAward(award, false));
                shown++;
                yield return Wait(interval);
                if (performance.hundred.drop_interval_count > 0
                    && shown % performance.hundred.drop_interval_count == 0)
                {
                    for (int frame = 0; frame < performance.hundred.drop_interval_frames && !fastForward; frame++)
                        yield return null;
                }
            }
        }

        private IEnumerator ShowAward(OfflinePetGachaAward award, bool special)
        {
            workingAwards++;
            GameObject card = null;
            try
            {
                if (award == null) yield break;
                PetGachaItem item = host.PetGachaCatalog.Item(award.ItemId);
                if (item == null) throw new InvalidOperationException("Pet Gacha result item is missing: " + award.ItemId);
                Vector2 targetPosition = RandomDropPoint();
                Vector2 startPosition = new Vector2(targetPosition.x, 880f);
                if (!fastForward)
                    yield return Wait(special ? performance.reward.special_drop_delay
                        : performance.reward.normal_drop_delay);
                if (fastForward) yield break;
                PlayBoundEffect(performance.reward.drop_effects, item.Quality, startPosition);
                card = CreateRewardCard(award, item, startPosition);
                float fallDuration = special ? 0.34f : 0.28f;
                yield return MoveCard(card, startPosition, targetPosition, fallDuration);
                if (fastForward) yield break;

                PlayBoundEffect(performance.reward.show_effects, item.Quality, targetPosition);
                PlayBoundEffect(performance.reward.quality_effects, item.Quality, targetPosition);
                PlayQualitySound(item.Quality);
                if (special) yield return Wait(performance.reward.item_show_delay);
                if (fastForward) yield break;
                if (award.ConvertedItemId > 0 && award.ConvertedCount > 0)
                {
                    yield return Wait(performance.reward.item_translate_delay);
                    if (fastForward) yield break;
                    effects.Play(performance.reward.translate_effect, targetPosition);
                    PlaySound("PetGacha_Animation_2");
                    PetGachaItem converted = host.PetGachaCatalog.Item(award.ConvertedItemId);
                    Sprite convertedSprite = converted == null ? null : catalog.FindSprite(converted.IconFile);
                    Image icon = card == null ? null : card.transform.Find("Icon")?.GetComponent<Image>();
                    if (icon != null && convertedSprite != null) icon.sprite = convertedSprite;
                }

                Vector2 collectPosition = LocalPosition(collectTarget as RectTransform);
                yield return MoveCard(card, targetPosition, collectPosition,
                    Mathf.Max(0.1f, performance.reward.item_translate_delay));
                if (fastForward) yield break;
                PlayBoundEffect(performance.reward.collect_effects, item.Quality, collectPosition);
                effects.Play(performance.reward.collect_feedback_effect, collectPosition);
                if (Time.unscaledTime >= nextCollectSoundAt)
                {
                    PlaySound("Main_Get_2");
                    nextCollectSoundAt = Time.unscaledTime + performance.award_collect_sfx_interval;
                }
                yield return Wait(performance.collect_effect_time);
            }
            finally
            {
                if (card != null)
                {
                    cards.Remove(card);
                    Destroy(card);
                }
                workingAwards = Mathf.Max(0, workingAwards - 1);
            }
        }

        private GameObject CreateRewardCard(OfflinePetGachaAward award, PetGachaItem item, Vector2 position)
        {
            var root = new GameObject("PetGachaDrop_" + award.ItemId.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform));
            root.transform.SetParent(rewardLayer, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(126f, 126f);

            var frameObject = new GameObject("Quality", typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(root.transform, false);
            Stretch((RectTransform)frameObject.transform);
            Image frame = frameObject.GetComponent<Image>();
            frame.sprite = catalog.FindSprite(host.PetGachaCatalog.QualityBackground(item.Quality));
            frame.preserveAspect = true;
            frame.raycastTarget = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            Stretch((RectTransform)iconObject.transform);
            RectTransform iconRect = (RectTransform)iconObject.transform;
            iconRect.offsetMin = new Vector2(12f, 12f);
            iconRect.offsetMax = new Vector2(-12f, -12f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = catalog.FindSprite(item.IconFile);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            root.transform.SetAsLastSibling();
            cards.Add(root);
            return root;
        }

        private IEnumerator MoveCard(GameObject card, Vector2 from, Vector2 to, float duration)
        {
            if (card == null || fastForward) yield break;
            RectTransform rect = card.transform as RectTransform;
            float started = Time.unscaledTime;
            while (!fastForward && rect != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.01f, duration));
                float eased = 1f - (1f - t) * (1f - t);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                if (t >= 1f) break;
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = fastForward ? to : to;
        }

        private void PlayBoundEffect(PetGachaEffectBinding[] bindings, int quality, Vector2 position)
        {
            PetGachaEffectBinding binding = bindings?.FirstOrDefault(value => value.quality == quality)
                ?? bindings?.FirstOrDefault(value => value.quality == Mathf.Clamp(quality, 1, 5));
            if (!string.IsNullOrEmpty(binding?.resource)) effects.Play(binding.resource, position);
        }

        private void PlayQualitySound(int quality)
        {
            PetGachaSoundBinding binding = performance.reward.quality_sounds?.FirstOrDefault(value => value.quality == quality);
            if (!string.IsNullOrEmpty(binding?.@event)) PlaySound(binding.@event);
        }

        private int AwardItemTypeScore(int itemId)
        {
            PetGachaItem item = host.PetGachaCatalog.Item(itemId);
            // The pinned Pet Gacha pool uses item type 1 for currency; all other
            // entries in its hundred-draw pool have score 0 in FormulaHandler.
            return item != null && item.ItemType == 1 ? 1 : 0;
        }

        private void PlaySound(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (performance.sounds != null && performance.sounds.Any(value => value.@event == eventName && value.stop))
            {
                if (soundSources.TryGetValue(eventName, out AudioSource stopping)) stopping.Stop();
                return;
            }
            if (!clips.TryGetValue(eventName, out AudioClip clip) || clip == null) return;
            if (!soundSources.TryGetValue(eventName, out AudioSource source) || source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                soundSources[eventName] = source;
                HomeAudioSettings.Register(source, HomeAudioChannel.Sound);
            }
            source.PlayOneShot(clip);
        }

        private static List<Vector2> ReadDropPolygon(Dictionary<string, object> source)
        {
            var result = new List<Vector2>();
            foreach (object entry in OpeningJson.Array(source["drop_range"], "Pet Gacha drop range"))
            {
                Dictionary<string, object> point = OpeningJson.Object(entry, "Pet Gacha drop point");
                result.Add(new Vector2((float)OpeningJson.Number(point, "x"),
                    (float)OpeningJson.Number(point, "y")));
            }
            if (result.Count < 3) throw new FormatException("Official Pet Gacha drop range is invalid.");
            return result;
        }

        private Vector2 RandomDropPoint()
        {
            float minX = dropPolygon.Min(value => value.x), maxX = dropPolygon.Max(value => value.x);
            float minY = dropPolygon.Min(value => value.y), maxY = dropPolygon.Max(value => value.y);
            for (int attempt = 0; attempt < 128; attempt++)
            {
                Vector2 point = new Vector2(UnityEngine.Random.Range(minX, maxX), UnityEngine.Random.Range(minY, maxY));
                bool inside = false;
                for (int i = 0, j = dropPolygon.Count - 1; i < dropPolygon.Count; j = i++)
                {
                    Vector2 a = dropPolygon[i], b = dropPolygon[j];
                    if ((a.y > point.y) != (b.y > point.y)
                        && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                if (inside) return point;
            }
            return dropPolygon[0];
        }

        private static Vector2 LocalPosition(RectTransform rect) => rect == null ? Vector2.zero : rect.anchoredPosition;

        private IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (!fastForward && Time.unscaledTime < end) yield return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            foreach (AudioSource source in soundSources.Values)
                if (source != null) source.Stop();
            effects?.Stop();
            Action callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            HomeAudioSettings.Unregister(actionSource);
            foreach (AudioSource source in soundSources.Values)
                HomeAudioSettings.Unregister(source);
            foreach (AudioClip clip in clips.Values)
                if (clip != null) Destroy(clip);
            clips.Clear();
        }
    }
}
