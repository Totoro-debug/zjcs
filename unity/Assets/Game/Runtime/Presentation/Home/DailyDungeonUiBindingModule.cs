using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    /// <summary>Restores official daily-dungeon view bindings for every shared native prefab.</summary>
    public sealed class DailyDungeonUiBindingModule
    {
        private readonly TMP_FontAsset font;
        private DungeonOfficialDifficultyTitle[] difficultyTitles;
        private readonly Dictionary<string, DungeonOfficialPrefabBinding> dungeonOfficialBindings =
            new Dictionary<string, DungeonOfficialPrefabBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> dungeonUiSprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly HashSet<string> ResultPrefabs = new HashSet<string>(StringComparer.Ordinal)
        {
            "GameEndPanelDailyDungeon", "DailyDungeonEndPanelItem", "DailyDungeonEndPaneAwardlItem",
            "AwardPanelItem", "ItemAwardIcon", "ItemIcon"
        };

        public DailyDungeonUiBindingModule(TMP_FontAsset officialFont)
        {
            font = officialFont != null ? officialFont : throw new ArgumentNullException(nameof(officialFont));
        }

        internal string DifficultyTitle(int dungeonId, int difficulty)
        {
            if (difficultyTitles == null) LoadBinding("GameEndPanelDailyDungeon");
            DungeonOfficialDifficultyTitle title = difficultyTitles?.SingleOrDefault(value =>
                value.dungeonId == dungeonId && value.difficulty == difficulty);
            return title?.title ?? throw new InvalidOperationException(
                "Official dungeon result difficulty title is missing: " + dungeonId + "/" + difficulty);
        }

        public void Apply(GameObject root, string prefabName)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            DungeonOfficialPrefabBinding binding = LoadBinding(prefabName);
            if (prefabName == "GameEndPanelDailyDungeon")
            {
                if (string.IsNullOrEmpty(binding.resultPopup?.showClipResource))
                    throw new InvalidOperationException("Original dungeon result popup binding is missing.");
                var popup = root.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>()
                    ?? root.AddComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
                popup.mShowClip = Resources.Load<AnimationClip>(binding.resultPopup.showClipResource);
                if (popup.mShowClip == null || popup.mShowClip.name != binding.resultPopup.showClipName)
                    throw new InvalidOperationException("Original dungeon result animation is missing.");
                popup.CloseByClickMask = binding.resultPopup.closeByClickMask;
            }
            foreach (DungeonOfficialTransformBinding transformBinding in binding.transforms)
            {
                Transform node = FindNode(root.transform, transformBinding.path,
                    transformBinding.objects);
                if (node == null)
                    throw new InvalidOperationException("Official dungeon UI Transform binding does not match prefab "
                        + prefabName + " at " + transformBinding.path);
                node.localRotation = ReadQuaternion(transformBinding.localRotation);
                node.localScale = ReadVector3(transformBinding.localScale, Vector3.one);
                if (transformBinding.isRectTransform)
                {
                    RectTransform rect = node as RectTransform;
                    if (rect == null)
                        throw new InvalidOperationException("Official dungeon UI RectTransform binding does not match prefab "
                            + prefabName + " at " + transformBinding.path);
                    rect.anchorMin = ReadVector2(transformBinding.anchorMin, rect.anchorMin);
                    rect.anchorMax = ReadVector2(transformBinding.anchorMax, rect.anchorMax);
                    rect.pivot = ReadVector2(transformBinding.pivot, rect.pivot);
                    rect.sizeDelta = ReadVector2(transformBinding.sizeDelta, rect.sizeDelta);
                    rect.anchoredPosition = ReadVector2(transformBinding.anchoredPosition, rect.anchoredPosition);
                }
                else node.localPosition = ReadVector3(transformBinding.localPosition, node.localPosition);
            }

            foreach (DungeonOfficialSafeAreaBinding item in binding.safeAreas ?? Array.Empty<DungeonOfficialSafeAreaBinding>())
            {
                Transform node = FindNode(root.transform, item.path, item.objects);
                if (node == null) throw new InvalidOperationException("Official dungeon SafeArea binding is missing: " + item.path);
                var safe = node.GetComponent<Zjcs.Official.WorldUI.WorldSafeArea>()
                    ?? node.gameObject.AddComponent<Zjcs.Official.WorldUI.WorldSafeArea>();
                safe.PaddingTop = item.paddingTop;
                safe.PaddingBottom = item.paddingBottom;
                safe.DisableTopAdapt = item.disableTopAdapt;
                safe.DisableBottomAdapt = item.disableBottomAdapt;
                safe.enabled = item.enabled;
            }
            foreach (DungeonOfficialAspectRatioBinding item in binding.aspectRatioFitters ?? Array.Empty<DungeonOfficialAspectRatioBinding>())
            {
                Transform node = FindNode(root.transform, item.path, item.objects);
                if (node == null) throw new InvalidOperationException("Official dungeon aspect-ratio binding is missing: " + item.path);
                var fitter = node.GetComponent<AspectRatioFitter>() ?? node.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectRatio = item.aspectRatio;
                fitter.aspectMode = (AspectRatioFitter.AspectMode)item.aspectMode;
                fitter.enabled = item.enabled;
            }
            foreach (DungeonOfficialRaycastBinding item in binding.raycastGraphics ?? Array.Empty<DungeonOfficialRaycastBinding>())
            {
                Transform node = FindNode(root.transform, item.path, item.objects);
                if (node == null) throw new InvalidOperationException("Original dungeon raycast Graphic is missing: " + item.path);
                var graphic = node.GetComponent<Zjcs.Official.WorldUI.NoDrawingRayCast>()
                    ?? node.gameObject.AddComponent<Zjcs.Official.WorldUI.NoDrawingRayCast>();
                graphic.color = ReadColor(item.color);
                graphic.raycastTarget = item.raycastTarget;
                if (item.raycastPadding != null && item.raycastPadding.Length == 4)
                    graphic.raycastPadding = new Vector4(item.raycastPadding[0], item.raycastPadding[1], item.raycastPadding[2], item.raycastPadding[3]);
                graphic.enabled = item.enabled;
            }

            int imageCount = 0;
            foreach (DungeonOfficialImageBinding imageBinding in binding.images)
            {
                Transform node = FindNode(root.transform, imageBinding.path,
                    imageBinding.objects);
                Image image = node == null ? null : node.GetComponent<Image>();
                if (image == null)
                    throw new InvalidOperationException("Official dungeon UI Image binding does not match prefab "
                        + prefabName + " at " + imageBinding.path);
                image.enabled = imageBinding.enabled;
                image.color = ReadColor(imageBinding.color);
                image.type = (Image.Type)imageBinding.type;
                image.preserveAspect = imageBinding.preserveAspect;
                image.raycastTarget = imageBinding.raycastTarget;
                image.maskable = imageBinding.maskable;
                if (!string.IsNullOrEmpty(imageBinding.materialResource))
                {
                    image.material = Resources.Load<Material>(imageBinding.materialResource);
                    if (image.material == null || !image.material.shader.isSupported)
                        throw new InvalidOperationException("Official dungeon UI material is unavailable: "
                            + imageBinding.materialResource);
                }
                if (imageBinding.sprite != null)
                {
                    DungeonOfficialSpriteBinding spriteBinding = imageBinding.sprite;
                    image.sprite = LoadBoundDungeonUiSprite(spriteBinding);
                }
                else image.sprite = null;
                imageCount++;
            }

            int textCount = 0;
            foreach (DungeonOfficialTextBinding textBinding in binding.texts)
            {
                Transform node = FindNode(root.transform, textBinding.path,
                    textBinding.objects);
                TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
                if (text == null)
                    throw new InvalidOperationException("Official dungeon UI TMP binding does not match prefab "
                        + prefabName + " at " + textBinding.path);
                text.font = font;
                text.text = textBinding.text ?? string.Empty;
                text.fontSize = textBinding.fontSize;
                text.fontWeight = (FontWeight)textBinding.fontWeight;
                text.fontStyle = (FontStyles)textBinding.fontStyle;
                text.enableAutoSizing = textBinding.autoSizing;
                text.fontSizeMin = textBinding.fontSizeRange != null && textBinding.fontSizeRange.Length > 0
                    ? textBinding.fontSizeRange[0] : textBinding.fontSize;
                text.fontSizeMax = textBinding.fontSizeRange != null && textBinding.fontSizeRange.Length > 1
                    ? textBinding.fontSizeRange[1] : textBinding.fontSize;
                text.alignment = (TextAlignmentOptions)textBinding.alignment;
                text.enableWordWrapping = textBinding.wordWrap;
                text.overflowMode = (TextOverflowModes)textBinding.overflow;
                if (textBinding.margin != null && textBinding.margin.Length == 4)
                    text.margin = new Vector4(textBinding.margin[0], textBinding.margin[1],
                        textBinding.margin[2], textBinding.margin[3]);
                text.color = ReadColor(textBinding.color);
                textCount++;
            }

            World.OfficialDungeonEntryBindings.Apply(root, prefabName,
                (clip, emitter) => World.OfficialDungeonUiAudio.Post(clip, emitter, (source, active) =>
                {
                    if (active) HomeAudioSettings.Register(source, HomeAudioChannel.Sound);
                    else HomeAudioSettings.Unregister(source);
                }));

            if (imageCount != binding.images.Length || textCount != binding.texts.Length)
                throw new InvalidOperationException("Official dungeon UI binding count mismatch: " + prefabName);
        }

        internal DungeonOfficialPrefabBinding LoadBinding(string prefabName)
        {
            if (dungeonOfficialBindings.TryGetValue(prefabName, out DungeonOfficialPrefabBinding cached))
                return cached;
            bool leaderboard = prefabName == "DailyDungeonTopPanel" || prefabName == "TopRankTeamItem";
            bool result = ResultPrefabs.Contains(prefabName);
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI",
                result ? "result-bindings.json" : leaderboard ? "leaderboard-bindings.json" : "official-bindings.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official dungeon UI bindings are missing.", path);
            DungeonOfficialBindingsDocument document = JsonUtility.FromJson<DungeonOfficialBindingsDocument>(
                File.ReadAllText(path));
            if (document == null || document.manifest != (leaderboard || result ? "884_156692" : "871_156598") || document.prefabs == null)
                throw new FormatException("Official dungeon UI bindings have an unsupported manifest.");
            if (result) difficultyTitles = document.difficulties;
            DungeonOfficialPrefabBinding binding = document.prefabs.FirstOrDefault(value =>
                value != null && string.Equals(value.name, prefabName, StringComparison.Ordinal));
            if (binding == null || binding.transforms == null || binding.images == null || binding.texts == null)
                throw new FormatException("Official dungeon UI binding is incomplete: " + prefabName);
            dungeonOfficialBindings.Add(prefabName, binding);
            return binding;
        }

        private Sprite LoadBoundDungeonUiSprite(DungeonOfficialSpriteBinding binding)
        {
            if (binding == null || string.IsNullOrEmpty(binding.file)) return null;
            if (binding.file.Contains("..") || Path.IsPathRooted(binding.file))
                throw new FormatException("Official dungeon UI sprite path is invalid: " + binding.file);
            string relative = binding.file.Replace('/', Path.DirectorySeparatorChar);
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", relative);
            if (!File.Exists(path)) throw new FileNotFoundException("Official dungeon UI sprite is missing.", path);
            string cacheKey = binding.file;
            if (dungeonUiSprites.TryGetValue(cacheKey, out Sprite cached)) return cached;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), true))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Official dungeon UI sprite could not be decoded: " + path);
            }
            Vector2 pivot = ReadVector2(binding.pivot, new Vector2(0.5f, 0.5f));
            Vector4 border = ReadVector4(binding.border, Vector4.zero);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                Mathf.Max(0.01f, binding.pixelsPerUnit), 0, SpriteMeshType.FullRect, border);
            sprite.name = binding.name;
            dungeonUiSprites.Add(cacheKey, sprite);
            return sprite;
        }

        internal static Transform FindNode(Transform root, string siblingPath,
            string[] objectNames)
        {
            if (string.IsNullOrEmpty(siblingPath)) return root;
            Transform current = root;
            string[] indices = siblingPath.Split('/');
            for (int depth = 0; depth < indices.Length; depth++)
            {
                if (!int.TryParse(indices[depth], out int index) || current == null) return null;
                string expectedName = objectNames != null && depth + 1 < objectNames.Length
                    ? objectNames[depth + 1] : null;
                Transform indexed = index >= 0 && index < current.childCount ? current.GetChild(index) : null;
                if (indexed != null && (expectedName == null || indexed.name == expectedName))
                {
                    current = indexed;
                    continue;
                }

                Transform named = null;
                for (int child = 0; child < current.childCount; child++)
                {
                    Transform candidate = current.GetChild(child);
                    if (!string.Equals(candidate.name, expectedName, StringComparison.Ordinal)) continue;
                    if (named != null) return null;
                    named = candidate;
                }
                if (named == null) return null;
                current = named;
            }
            return current;
        }

        internal static Color ReadColor(float[] values)
        {
            return values != null && values.Length == 4
                ? new Color(values[0], values[1], values[2], values[3]) : Color.white;
        }

        private static Vector2 ReadVector2(float[] values, Vector2 fallback)
        {
            return values != null && values.Length == 2 ? new Vector2(values[0], values[1]) : fallback;
        }

        private static Vector4 ReadVector4(float[] values, Vector4 fallback)
        {
            return values != null && values.Length == 4
                ? new Vector4(values[0], values[1], values[2], values[3]) : fallback;
        }

        private static Vector3 ReadVector3(float[] values, Vector3 fallback)
        {
            return values != null && values.Length == 3
                ? new Vector3(values[0], values[1], values[2]) : fallback;
        }

        private static Quaternion ReadQuaternion(float[] values)
        {
            return values != null && values.Length == 4
                ? new Quaternion(values[0], values[1], values[2], values[3]) : Quaternion.identity;
        }

    }

    [Serializable]
    internal sealed class DungeonOfficialBindingsDocument
    {
        public string manifest;
        public DungeonOfficialPrefabBinding[] prefabs;
        public DungeonOfficialDifficultyTitle[] difficulties;
    }

    [Serializable]
    internal sealed class DungeonOfficialDifficultyTitle
    {
        public int dungeonId, difficulty;
        public string title;
    }

    [Serializable]
    internal sealed class DungeonOfficialPrefabBinding
    {
        public string name;
        public DungeonLeaderboardBinding leaderboard;
        public DungeonLeaderboardAchievementBinding[] achievementRows;
        public DungeonOfficialTransformBinding[] transforms;
        public DungeonOfficialImageBinding[] images;
        public DungeonOfficialTextBinding[] texts;
        public DungeonOfficialSafeAreaBinding[] safeAreas;
        public DungeonOfficialAspectRatioBinding[] aspectRatioFitters;
        public DungeonOfficialRaycastBinding[] raycastGraphics;
        public DungeonOfficialEntryButtonBinding[] entryButtons;
        public DungeonOfficialPopupBinding resultPopup;
    }

    [Serializable]
    internal sealed class DungeonOfficialPopupBinding
    {
        public string showClipResource, showClipName;
        public bool closeByClickMask;
        public int maskType;
    }

    [Serializable]
    internal sealed class DungeonLeaderboardAchievementBinding
    {
        public string path;
        public DungeonLeaderboardState[] states;
        public DungeonLeaderboardColorState[] colors;
    }

    [Serializable]
    internal sealed class DungeonLeaderboardColorState
    {
        public int state;
        public string[] objects;
        public float[] color;
    }

    [Serializable]
    internal sealed class DungeonLeaderboardBinding
    {
        public string mScrollRect, mContent, mMeTopItem, mRankText;
        public DungeonLeaderboardState[] mEmptyStateSwitcher, mMyTopStateSwitcher, mMeStateSwitcher, mTopStateSwticher;
    }

    [Serializable]
    internal sealed class DungeonLeaderboardState
    {
        public int state;
        public string[] objects;
    }

    [Serializable]
    internal sealed class DungeonOfficialRaycastBinding
    {
        public string path;
        public string[] objects;
        public bool enabled, raycastTarget;
        public float[] color, raycastPadding;
    }

    [Serializable]
    internal sealed class DungeonOfficialEntryButtonBinding
    {
        public string type, button;
    }

    [Serializable]
    internal sealed class DungeonOfficialSafeAreaBinding
    {
        public string path;
        public string[] objects;
        public bool enabled, disableTopAdapt, disableBottomAdapt;
        public float paddingTop, paddingBottom;
    }

    [Serializable]
    internal sealed class DungeonOfficialAspectRatioBinding
    {
        public string path;
        public string[] objects;
        public bool enabled;
        public int aspectMode;
        public float aspectRatio;
    }

    [Serializable]
    internal sealed class DungeonOfficialTransformBinding
    {
        public string path;
        public string[] objects;
        public bool isRectTransform;
        public float[] localPosition;
        public float[] localRotation;
        public float[] localScale;
        public float[] anchorMin;
        public float[] anchorMax;
        public float[] anchoredPosition;
        public float[] sizeDelta;
        public float[] pivot;
    }

    [Serializable]
    internal sealed class DungeonOfficialImageBinding
    {
        public string path;
        public string[] objects;
        public bool enabled;
        public float[] color;
        public int type;
        public bool preserveAspect;
        public bool raycastTarget;
        public bool maskable;
        public string materialResource;
        public DungeonOfficialSpriteBinding sprite;
    }

    [Serializable]
    internal sealed class DungeonOfficialTextBinding
    {
        public string path;
        public string[] objects;
        public string text;
        public float fontSize;
        public int fontWeight;
        public int fontStyle;
        public bool autoSizing;
        public float[] fontSizeRange;
        public int alignment;
        public bool wordWrap;
        public int overflow;
        public float[] margin;
        public float[] color;
    }

    [Serializable]
    internal sealed class DungeonOfficialSpriteBinding
    {
        public string file;
        public string name;
        public float[] pivot;
        public float[] border;
        public float pixelsPerUnit;
    }

}
