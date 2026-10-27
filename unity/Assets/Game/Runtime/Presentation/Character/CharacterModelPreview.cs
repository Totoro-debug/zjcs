using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.Character
{
    internal sealed class CharacterModelPackage : IDisposable
    {
        private const string UriPrefix = "res://assets/characters/";
        private readonly FileStream stream;
        private readonly ZipArchive archive;
        private readonly Dictionary<string, ZipArchiveEntry> entries;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> fileHashes;
        private readonly Dictionary<string, object> catalog;

        public Dictionary<string, object> Catalog => catalog;

        private CharacterModelPackage()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Character/character-models.zip");
            if (!File.Exists(path))
                throw new FileNotFoundException("The pinned offline character model package is missing.", path);
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            entries = archive.Entries.ToDictionary(value => value.FullName, StringComparer.Ordinal);
            Dictionary<string, object> manifest = AsObject(OfficialEquipmentJson.Parse(
                Encoding.UTF8.GetString(ReadUnverifiedBytes("character-model-manifest.json"))), "character model manifest");
            if (!manifest.TryGetValue("manifest", out object manifestVersion)
                || (Convert.ToString(manifestVersion, CultureInfo.InvariantCulture) != "854_156383"
                    && Convert.ToString(manifestVersion, CultureInfo.InvariantCulture) != "871_156598")
                || !manifest.TryGetValue("files", out object rawFiles))
                throw new FormatException("Character model package has an unregistered source manifest.");
            fileHashes = AsObject(rawFiles, "character model package hashes");
            string catalogText = ReadText(UriPrefix + "wardrobe/catalog.json");
            catalog = AsObject(OfficialEquipmentJson.Parse(catalogText), "character model catalog");
        }

        public static CharacterModelPackage OpenDefault() => new CharacterModelPackage();

        public string ReadText(string name)
        {
            return Encoding.UTF8.GetString(ReadBytes(name));
        }

        public byte[] ReadBytes(string name)
        {
            string entryName = name.StartsWith(UriPrefix, StringComparison.Ordinal)
                ? "assets/characters/" + name.Substring(UriPrefix.Length) : name;
            byte[] bytes = ReadUnverifiedBytes(entryName);
            if (!fileHashes.TryGetValue(entryName, out object expected)
                || !string.Equals(Convert.ToString(expected, CultureInfo.InvariantCulture), Hash(bytes),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Character model package entry hash mismatch: " + entryName);
            return bytes;
        }

        private byte[] ReadUnverifiedBytes(string entryName)
        {
            if (!entries.TryGetValue(entryName, out ZipArchiveEntry entry))
                throw new FileNotFoundException("Character model package entry is missing: " + entryName);
            using (Stream input = entry.Open())
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }

        public Texture2D LoadMask(string textureName)
        {
            if (!catalog.TryGetValue("masks", out object raw)
                || !(raw is Dictionary<string, object> masks)
                || !masks.TryGetValue(textureName, out object uri)) return null;
            return LoadTextureFile("mask_" + textureName, Convert.ToString(uri, CultureInfo.InvariantCulture));
        }

        public bool TrySkinDyeInfo(string textureName, out int skinType, out bool hasDye)
        {
            skinType = -1;
            hasDye = false;
            if (!catalog.TryGetValue("skins", out object raw)
                || !(raw is Dictionary<string, object> skins)) return false;
            foreach (KeyValuePair<string, object> entry in skins)
            {
                if (!(entry.Value is Dictionary<string, object> skin)) continue;
                if (Convert.ToString(skin["SkinName"], CultureInfo.InvariantCulture) != textureName) continue;
                skinType = Convert.ToInt32(skin["SkinType"], CultureInfo.InvariantCulture);
                hasDye = skin.TryGetValue("HasDye", out object value) && Convert.ToBoolean(value);
                return true;
            }
            return false;
        }

        private Texture2D LoadTextureFile(string name, string uri)
        {
            if (textures.TryGetValue(name, out Texture2D cached)) return cached;
            byte[] bytes = ReadBytes(uri);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.LoadImage(bytes, false);
            textures[name] = texture;
            return texture;
        }

        public Texture2D LoadTexture(string name)
        {
            if (textures.TryGetValue(name, out Texture2D texture)) return texture;
            Dictionary<string, object> paths = AsObject(catalog["textures"], "character textures");
            if (!paths.TryGetValue(name, out object rawPath))
                throw new FileNotFoundException("Character mesh texture is not indexed: " + name);
            byte[] bytes = ReadBytes(Convert.ToString(rawPath, CultureInfo.InvariantCulture));
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!UnityEngine.ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Character texture could not be decoded: " + name);
            }
            textures.Add(name, texture);
            return texture;
        }

        public static Dictionary<string, object> AsObject(object value, string label)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(label + " is not a JSON object.");
        }

        public static List<object> AsArray(object value, string label)
        {
            if (value is List<object> result) return result;
            throw new FormatException(label + " is not a JSON array.");
        }

        public static string String(Dictionary<string, object> value, string key)
        {
            return value != null && value.TryGetValue(key, out object result)
                ? Convert.ToString(result, CultureInfo.InvariantCulture) : string.Empty;
        }

        public static float[] Floats(object value, string label)
        {
            return AsArray(value, label).Select(item => Convert.ToSingle(item, CultureInfo.InvariantCulture)).ToArray();
        }

        public void Dispose()
        {
            archive.Dispose();
            stream.Dispose();
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    [DisallowMultipleComponent]
    public sealed class CharacterModelPreview : MonoBehaviour
    {
        private sealed class ModelMeshPart
        {
            public Texture2D Texture;
            public string TextureName;
            public Vector2[] Uv;
            public int[] Triangles;
            public Color Color;
            public int Order;
            public Vector2[][] Frames;
        }

        private static CharacterModelPackage sharedPackage;
        private OfflineCharacterProgressionService progression;
        private WardrobeResolution previewAppearance;
        private string characterSlotId;
        private string savesRoot;
        private RectTransform modelRect;
        private Vector2 renderedSize;
        private float modelScale;
        private Vector2 modelCenter;
        private readonly List<CharacterModelMeshGraphic> graphics = new List<CharacterModelMeshGraphic>();
        private GameObject nativeWorldRoot;
        private GameObject nativeModel;
        private Animator nativeAnimator;
        private Camera nativeCamera;
        private RawImage nativeImage;
        private RenderTexture nativeTexture;
        private Coroutine wardrobeTransition;
        private string currentNativeAnimation = "home/idle_01";
        private string wardrobeAnimType = "idle";
        private sealed class NativeFollower
        {
            public RectTransform Target, Skeleton;
            public string BoneName;
            public Transform Bone;
            public bool FollowLocalScale, FollowRotation, FollowFlip;
        }
        private readonly List<NativeFollower> nativeFollowers = new List<NativeFollower>();

        internal void InitializeOriginalFollowers(TextAsset source, Dictionary<string, RectTransform> nodes)
        {
            var metadata = CharacterModelPackage.AsObject(OfficialEquipmentJson.Parse(source.text), "skill UI metadata");
            if (!(metadata["character_followers"] is Dictionary<string, object> bindings))
                throw new InvalidOperationException("Original character AttachmentFollower bindings are unavailable.");
            nativeFollowers.Clear();
            foreach (var pair in bindings)
            {
                var binding = CharacterModelPackage.AsObject(pair.Value, "AttachmentFollower");
                string skeleton = Convert.ToString(binding["SkeletonGraphic"], CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(skeleton) || !nodes.TryGetValue(skeleton, out var mount) || mount != modelRect) continue;
                if (!nodes.TryGetValue(pair.Key, out var target))
                    throw new InvalidOperationException("Original AttachmentFollower node is unavailable: " + pair.Key);
                nativeFollowers.Add(new NativeFollower { Target = target, Skeleton = mount,
                    BoneName = Convert.ToString(binding["BoneName"], CultureInfo.InvariantCulture),
                    FollowLocalScale = Convert.ToInt32(binding["FollowLocalScale"]) != 0,
                    FollowRotation = Convert.ToInt32(binding["FollowRotation"]) != 0,
                    FollowFlip = Convert.ToInt32(binding["FollowSkeletonTransformFlipX"]) != 0 });
            }
            UpdateOriginalFollowers();
        }

        private void UpdateOriginalFollowers()
        {
            if (nativeModel == null) return;
            foreach (NativeFollower follower in nativeFollowers)
            {
                if (follower.Bone == null)
                    follower.Bone = nativeModel.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == follower.BoneName);
                if (follower.Bone == null) throw new InvalidOperationException("Original follower bone is unavailable: " + follower.BoneName);
                // AttachmentFollower.GetBoneLocalPosition + sibling-target LateUpdate path.
                Vector3 bone = nativeModel.transform.InverseTransformPoint(follower.Bone.position);
                follower.Target.position = follower.Skeleton.TransformPoint(bone * 100f);
                if (follower.FollowRotation)
                    follower.Target.rotation = follower.Skeleton.rotation * Quaternion.Euler(0f, 0f,
                        follower.Bone.eulerAngles.z - nativeModel.transform.eulerAngles.z);
                if (follower.FollowLocalScale || follower.FollowFlip)
                {
                    Vector3 scale = follower.Target.localScale;
                    if (follower.FollowLocalScale) { scale.x = follower.Bone.localScale.x; scale.y = follower.Bone.localScale.y; }
                    if (follower.FollowFlip) scale.x *= Mathf.Sign(follower.Skeleton.lossyScale.x);
                    follower.Target.localScale = scale;
                }
            }
        }

        public void Initialize(OfflineCharacterProgressionService characterProgression)
        {
            Initialize(characterProgression, null);
        }

        public void Initialize(OfflineCharacterProgressionService characterProgression,
            WardrobeResolution appearanceOverride)
        {
            Initialize(characterProgression, appearanceOverride, null, null);
        }

        public void Initialize(OfflineCharacterProgressionService characterProgression,
            WardrobeResolution appearanceOverride, string selectedSlotId, string profileSavesRoot)
        {
            progression = characterProgression != null ? characterProgression
                : throw new ArgumentNullException(nameof(characterProgression));
            previewAppearance = appearanceOverride;
            characterSlotId = selectedSlotId;
            savesRoot = string.IsNullOrWhiteSpace(profileSavesRoot)
                ? Application.persistentDataPath : profileSavesRoot;
            modelRect = transform as RectTransform;
            if (modelRect == null) throw new InvalidOperationException("The character preview must be mounted to a RectTransform.");
            if (sharedPackage == null) sharedPackage = CharacterModelPackage.OpenDefault();
            Refresh();
        }

        public void Refresh()
        {
            if (progression == null || modelRect == null) return;
            foreach (CharacterModelMeshGraphic graphic in graphics)
                if (graphic != null) Destroy(graphic.gameObject);
            graphics.Clear();

            if (!string.IsNullOrWhiteSpace(characterSlotId))
            {
                RefreshNativeModel();
                renderedSize = modelRect.rect.size;
                return;
            }

            CharacterModelPackage package = sharedPackage ?? (sharedPackage = CharacterModelPackage.OpenDefault());
            Dictionary<string, object> catalog = package.Catalog;
            string sex = progression.Player.Wardrobe.Sex == 1 ? "female" : "male";
            string profession = progression.Player.Wardrobe.Profession.ToString(CultureInfo.InvariantCulture);
            Dictionary<string, object> profiles = CharacterModelPackage.AsObject(catalog["profiles"], "character profiles");
            Dictionary<string, object> sexProfiles = CharacterModelPackage.AsObject(profiles[sex], sex + " profiles");
            if (!sexProfiles.TryGetValue(profession, out object profileRaw))
                throw new InvalidOperationException("The character profession has no official model profile: " + profession);
            Dictionary<string, object> profile = CharacterModelPackage.AsObject(profileRaw, "character model profile");
            string pose = Convert.ToString(profile["pose"], CultureInfo.InvariantCulture);
            Dictionary<string, object> poses = CharacterModelPackage.AsObject(catalog["poses"], "character poses");
            Dictionary<string, object> sexPoses = CharacterModelPackage.AsObject(poses[sex], sex + " poses");
            string poseUri = Convert.ToString(sexPoses[pose], CultureInfo.InvariantCulture);
            Dictionary<string, object> poseData = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(package.ReadText(poseUri)), "character idle pose");
            Dictionary<string, object> poseParts = CharacterModelPackage.AsObject(poseData["parts"], "character pose parts");
            Dictionary<string, object> partIndex = CharacterModelPackage.AsObject(
                CharacterModelPackage.AsObject(catalog["parts"], "character parts")[sex], sex + " parts");
            WardrobeResolution appearance = previewAppearance ?? progression.ResolveAppearance();

            var meshUris = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<int, int> skin in appearance.SkinByType.OrderBy(pair => pair.Key))
            {
                string key;
                if (skin.Key == 7 || skin.Key == 8)
                    key = "profession_weapon_" + skin.Value.ToString(CultureInfo.InvariantCulture);
                else if (partIndex.TryGetValue(skin.Value.ToString(CultureInfo.InvariantCulture), out object partName))
                    key = Convert.ToString(partName, CultureInfo.InvariantCulture);
                else
                    key = "skin_" + skin.Value.ToString(CultureInfo.InvariantCulture);
                if (poseParts.TryGetValue(key, out object uri))
                    meshUris[key] = Convert.ToString(uri, CultureInfo.InvariantCulture);
            }

            var meshParts = new List<ModelMeshPart>();
            foreach (KeyValuePair<string, string> mesh in meshUris)
            {
                byte[] compressed = package.ReadBytes(mesh.Value);
                string json;
                using (var input = new MemoryStream(compressed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    json = Encoding.UTF8.GetString(output.ToArray());
                }
                foreach (object rawPart in CharacterModelPackage.AsArray(
                    OfficialEquipmentJson.Parse(json), "character mesh parts"))
                    meshParts.Add(ParsePart(package, CharacterModelPackage.AsObject(rawPart, "character mesh part")));
            }

            if (meshParts.Count == 0) throw new InvalidOperationException("The selected character has no official idle mesh parts.");
            CalculateBounds(meshParts);
            foreach (ModelMeshPart part in meshParts.OrderBy(value => value.Order))
            {
                var instance = new GameObject("CharacterMesh_" + part.Order, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(CharacterModelMeshGraphic));
                instance.transform.SetParent(transform, false);
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                CharacterModelMeshGraphic graphic = instance.GetComponent<CharacterModelMeshGraphic>();
                graphic.Initialize(part.Texture, part.Uv, part.Triangles, part.Color, part.Frames,
                    modelScale, modelCenter, poseData.TryGetValue("fps", out object fps)
                        ? Convert.ToSingle(fps, CultureInfo.InvariantCulture) : 24f);
                graphic.raycastTarget = false;
                if (package.TrySkinDyeInfo(part.TextureName, out int skinType, out bool hasDye) && hasDye)
                {
                    Texture2D mask = package.LoadMask(part.TextureName);
                    Dictionary<int, Vector4> channels = DyeChannels(skinType);
                    if (mask != null && channels.Count > 0) graphic.ConfigureDye(mask, channels);
                }
                graphics.Add(graphic);
            }
            renderedSize = modelRect.rect.size;
        }

        public void SetPreviewAppearance(WardrobeResolution appearance)
        {
            previewAppearance = appearance;
            if (progression != null) Refresh();
        }

        public void EnterWardrobe()
        {
            wardrobeAnimType = "idle";
            if (!PlayNativeAnimation(AppearanceAnimationName("idle", "01")))
                throw new InvalidOperationException("The official wardrobe idle animation is missing from the native character model.");
        }

        public void LeaveWardrobe()
        {
            if (wardrobeTransition != null) StopCoroutine(wardrobeTransition);
            wardrobeTransition = null;
            wardrobeAnimType = "idle";
            if (!PlayNativeAnimation("home/idle_01"))
                throw new InvalidOperationException("The official home idle animation is missing from the native character model.");
        }

        public void PlayWardrobeCategory(string nextAnimType, IReadOnlyList<string> animInputs,
            float transitionExitTime, float transitionMixDuration)
        {
            if (string.IsNullOrEmpty(nextAnimType) || animInputs == null || animInputs.Count < 2) return;
            if (string.Equals(wardrobeAnimType, nextAnimType, StringComparison.Ordinal)) return;

            string profession = WardrobeProfessionId();
            string transitionName = profession + "/show_" + wardrobeAnimType + "_" + nextAnimType;
            string loopName = profession + "/" + animInputs[0] + "_" + animInputs[1];
            if (!HasNativeAnimation(transitionName) || !HasNativeAnimation(loopName))
                throw new InvalidOperationException("The official wardrobe animation is missing: " + transitionName + " -> " + loopName);

            wardrobeAnimType = nextAnimType;
            if (wardrobeTransition != null) StopCoroutine(wardrobeTransition);
            PlayNativeAnimation(transitionName);
            wardrobeTransition = StartCoroutine(ContinueWardrobeTransition(transitionName, loopName,
                transitionExitTime, transitionMixDuration));
        }

        private void LateUpdate()
        {
            UpdateOriginalFollowers();
            if (progression == null || modelRect == null || modelRect.rect.size == renderedSize) return;
            if (nativeModel != null)
            {
                FitNativeCamera();
                renderedSize = modelRect.rect.size;
            }
            else
                Refresh();
        }

        private void RefreshNativeModel()
        {
            EnsureNativePreview();
            if (nativeModel != null) Destroy(nativeModel);
            nativeModel = CharacterCreationNativeModelFactory.CreatePreviewModel(characterSlotId, savesRoot,
                previewAppearance ?? progression.ResolveAppearance());
            nativeModel.name = "CharacterPageNativeModel";
            nativeModel.transform.SetParent(nativeWorldRoot.transform, false);
            nativeModel.transform.localPosition = Vector3.zero;
            nativeModel.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(nativeModel, 31);
            nativeAnimator = nativeModel.GetComponent<Animator>();
            if (nativeAnimator == null || nativeAnimator.runtimeAnimatorController == null)
                throw new InvalidOperationException("The official character preview prefab has no native Animator.");
            nativeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            nativeAnimator.applyRootMotion = false;
            if (!PlayNativeAnimation(currentNativeAnimation))
                throw new InvalidOperationException("The official character model does not contain animation " + currentNativeAnimation);
            FitNativeCamera();
        }

        private void EnsureNativePreview()
        {
            if (nativeWorldRoot != null) return;

            var imageObject = new GameObject("NativeCharacterPreviewImage", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(transform, false);
            imageObject.transform.SetAsFirstSibling();
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            // SkeletonGraphic places the mesh relative to its pivot at 100 UI units per world unit.
            imageRect.anchorMin = ((RectTransform)transform).pivot;
            imageRect.anchorMax = imageRect.anchorMin;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            nativeImage = imageObject.GetComponent<RawImage>();
            nativeImage.raycastTarget = false;
            nativeImage.color = Color.white;

            nativeTexture = new RenderTexture(512, 768, 24, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPageNativePreview",
                hideFlags = HideFlags.DontSave
            };
            nativeTexture.Create();
            nativeImage.texture = nativeTexture;

            nativeWorldRoot = new GameObject("CharacterPageNativePreviewWorld");
            // RenderTexture geometry stays in world units while the UI canvas changes its screen scale.
            nativeWorldRoot.transform.localPosition = Vector3.zero;
            nativeWorldRoot.transform.localRotation = Quaternion.identity;
            nativeWorldRoot.transform.localScale = Vector3.one;
            var cameraObject = new GameObject("CharacterPageNativePreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(nativeWorldRoot.transform, false);
            nativeCamera = cameraObject.GetComponent<Camera>();
            nativeCamera.clearFlags = CameraClearFlags.SolidColor;
            nativeCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            nativeCamera.orthographic = true;
            nativeCamera.aspect = 2f / 3f;
            nativeCamera.cullingMask = 1 << 31;
            nativeCamera.targetTexture = nativeTexture;
            nativeCamera.nearClipPlane = 0.01f;
            nativeCamera.farClipPlane = 200f;
            nativeCamera.allowHDR = false;
            nativeCamera.allowMSAA = false;
            nativeCamera.depth = -10f;
            nativeCamera.enabled = true;
        }

        private void FitNativeCamera()
        {
            if (nativeCamera == null || nativeModel == null) return;
            nativeModel.GetComponent<CharacterNativeAttachmentGeometry>()?.RefreshForCapture();
            Renderer[] renderers = nativeModel.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            Bounds bounds = new Bounds(nativeModel.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.forceRenderingOff) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds)
                throw new InvalidOperationException("The official native character model has no visible mesh renderers.");
            nativeCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / nativeCamera.aspect) * 1.08f;
            nativeCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y,
                bounds.center.z - Mathf.Max(bounds.extents.z + 10f, 20f));
            RectTransform imageRect = nativeImage.rectTransform;
            imageRect.sizeDelta = new Vector2(nativeCamera.orthographicSize * 2f * nativeCamera.aspect,
                nativeCamera.orthographicSize * 2f) * 100f;
            Vector3 center = nativeModel.transform.InverseTransformPoint(bounds.center);
            imageRect.anchoredPosition = new Vector2(center.x, center.y) * 100f;
        }

        private bool PlayNativeAnimation(string animationName, float mixDuration = 0f)
        {
            if (nativeAnimator == null || !HasNativeAnimation(animationName)) return false;
            int stateHash = Animator.StringToHash("Base Layer." + animationName);
            if (mixDuration > 0f) nativeAnimator.CrossFadeInFixedTime(stateHash, mixDuration, 0, 0f);
            else nativeAnimator.Play(stateHash, 0, 0f);
            nativeAnimator.Update(0f);
            currentNativeAnimation = animationName;
            return true;
        }

        private bool HasNativeAnimation(string animationName)
        {
            return nativeAnimator != null && nativeAnimator.runtimeAnimatorController != null
                && nativeAnimator.HasState(0, Animator.StringToHash("Base Layer." + animationName));
        }

        private System.Collections.IEnumerator ContinueWardrobeTransition(string transitionName, string loopName,
            float exitTime, float mixDuration)
        {
            int transitionHash = Animator.StringToHash("Base Layer." + transitionName);
            while (nativeAnimator != null && nativeAnimator.gameObject != null)
            {
                AnimatorStateInfo state = nativeAnimator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == transitionHash && state.normalizedTime >= exitTime) break;
                yield return null;
            }
            if (PlayNativeAnimation(loopName, mixDuration)) wardrobeTransition = null;
        }

        private string AppearanceAnimationName(string input0, string input1)
        {
            return WardrobeProfessionId() + "/" + input0 + "_" + input1;
        }

        private string WardrobeProfessionId()
        {
            Dictionary<string, object> profiles = CharacterModelPackage.AsObject(
                sharedPackage.Catalog["profiles"], "character model profiles");
            string sex = progression.Player.Wardrobe.Sex == 1 ? "female" : "male";
            Dictionary<string, object> sexProfiles = CharacterModelPackage.AsObject(profiles[sex], sex + " profiles");
            string profession = progression.Player.Wardrobe.Profession.ToString(CultureInfo.InvariantCulture);
            if (!sexProfiles.TryGetValue(profession, out object profileRaw))
                throw new InvalidOperationException("The official model catalog has no profession profile: " + profession);
            Dictionary<string, object> profile = CharacterModelPackage.AsObject(profileRaw, "character model profile");
            string pose = Convert.ToString(profile["pose"], CultureInfo.InvariantCulture);
            int separator = pose.IndexOf('/', StringComparison.Ordinal);
            if (separator <= 0)
                throw new FormatException("The official character idle pose has no animation group: " + pose);
            return pose.Substring(0, separator);
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void OnDestroy()
        {
            if (nativeImage != null) nativeImage.texture = null;
            if (nativeCamera != null) nativeCamera.targetTexture = null;
            if (nativeTexture != null)
            {
                nativeTexture.Release();
                Destroy(nativeTexture);
            }
            if (nativeWorldRoot != null) Destroy(nativeWorldRoot);
        }

        private void OnEnable()
        {
            if (nativeWorldRoot != null) nativeWorldRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (nativeWorldRoot != null) nativeWorldRoot.SetActive(false);
        }

        /// <summary>Official dye HSV offsets for one skin type, keyed by shader channel.</summary>
        private Dictionary<int, Vector4> DyeChannels(int skinType)
        {
            var result = new Dictionary<int, Vector4>();
            if (progression == null) return result;
            IReadOnlyList<Dictionary<string, object>> configs = progression.Catalog.Rows("character_skin_dye");
            IReadOnlyList<Dictionary<string, object>> colors = progression.Catalog.Rows("character_skin_color");
            foreach (OfflineAppearanceDye dye in progression.Player.Wardrobe.Dyes)
            {
                Dictionary<string, object> config = configs.FirstOrDefault(value =>
                    Convert.ToInt32(value["DyeType"], CultureInfo.InvariantCulture) == dye.Type);
                if (config == null
                    || !(config.TryGetValue("DyeSkinTypeInfoList", out object raw)
                        && raw is List<object> infos)) continue;
                Dictionary<string, object> info = infos.OfType<Dictionary<string, object>>()
                    .FirstOrDefault(value => Convert.ToInt32(value["SkinType"], CultureInfo.InvariantCulture) == skinType);
                if (info == null) continue;
                int h = dye.Hue, sat = dye.Saturation, v = dye.Value;
                if (dye.ColorId > 0)
                {
                    Dictionary<string, object> color = colors.FirstOrDefault(value =>
                        Convert.ToInt32(value["Id"], CultureInfo.InvariantCulture) == dye.ColorId);
                    if (color != null)
                    {
                        h = Convert.ToInt32(color["H"], CultureInfo.InvariantCulture);
                        sat = Convert.ToInt32(color["S"], CultureInfo.InvariantCulture);
                        v = Convert.ToInt32(color["V"], CultureInfo.InvariantCulture);
                    }
                }
                int offset = Convert.ToInt32(info.TryGetValue("VOffset", out object rawOffset)
                    ? rawOffset : 0, CultureInfo.InvariantCulture);
                int channel = Convert.ToInt32(info["Channel"], CultureInfo.InvariantCulture);
                result[channel] = DyeVector(config, h, sat, v + offset);
            }
            return result;
        }

        private static Vector4 DyeVector(Dictionary<string, object> config, int h, int s, int v)
        {
            Color baseColor = HsvToLinear(
                Convert.ToInt32(config["BaseH"], CultureInfo.InvariantCulture),
                Convert.ToInt32(config["BaseS"], CultureInfo.InvariantCulture),
                Convert.ToInt32(config["BaseV"], CultureInfo.InvariantCulture));
            Color target = HsvToLinear(h, s, v);
            Color.RGBToHSV(baseColor, out float baseH, out float baseS, out float baseV);
            Color.RGBToHSV(target, out float hh, out float ss, out float vv);
            return new Vector4(hh - baseH, baseS <= 0f ? 0f : (ss - baseS) / baseS,
                baseV <= 0f ? 0f : (vv - baseV) / baseV, 0f);
        }

        private static Color HsvToLinear(int h, int s, int v)
        {
            return Color.HSVToRGB(Mathf.Clamp01(h / 360f), Mathf.Clamp01(s / 100f),
                Mathf.Clamp01(v / 100f)).linear;
        }

        private ModelMeshPart ParsePart(CharacterModelPackage package, Dictionary<string, object> data)
        {
            string textureName = Convert.ToString(data["texture"], CultureInfo.InvariantCulture);
            Vector2[] uv = Pairs(CharacterModelPackage.Floats(data["uv"], "character mesh UVs"));
            int[] triangles = CharacterModelPackage.AsArray(data["triangles"], "character mesh triangles")
                .Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
            Color color = Color.white;
            if (data.TryGetValue("color", out object colorRaw))
            {
                float[] values = CharacterModelPackage.Floats(colorRaw, "character mesh color");
                if (values.Length >= 4) color = new Color(values[0], values[1], values[2], values[3]);
            }
            var frames = new List<Vector2[]>();
            if (data.TryGetValue("idle", out object idleRaw))
            {
                foreach (object rawFrame in CharacterModelPackage.AsArray(idleRaw, "character idle frames"))
                {
                    Dictionary<string, object> frame = CharacterModelPackage.AsObject(rawFrame, "character idle frame");
                    if (frame.TryGetValue("vertices", out object vertices))
                        frames.Add(Pairs(CharacterModelPackage.Floats(vertices, "character frame vertices")));
                }
            }
            if (frames.Count == 0)
                frames.Add(Pairs(CharacterModelPackage.Floats(data["vertices"], "character mesh vertices")));
            return new ModelMeshPart
            {
                Texture = package.LoadTexture(textureName),
                TextureName = textureName,
                Uv = uv,
                Triangles = triangles,
                Color = color,
                Order = Convert.ToInt32(data["order"], CultureInfo.InvariantCulture),
                Frames = frames.ToArray()
            };
        }

        private void CalculateBounds(IEnumerable<ModelMeshPart> parts)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector2[] frame in parts.SelectMany(part => part.Frames))
            foreach (Vector2 point in frame)
            {
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
            Vector2 size = modelRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = new Vector2(207f, 337f);
            float width = Mathf.Max(1f, maxX - minX);
            float height = Mathf.Max(1f, maxY - minY);
            modelScale = Mathf.Min(size.x / width, size.y / height) * 0.96f;
            modelCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        private static Vector2[] Pairs(float[] values)
        {
            if (values.Length % 2 != 0) throw new FormatException("Character mesh data must contain x/y pairs.");
            var result = new Vector2[values.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            return result;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CharacterModelMeshGraphic : MaskableGraphic
    {
        private Texture texture;
        private Vector2[] uv = Array.Empty<Vector2>();
        private int[] triangles = Array.Empty<int>();
        private Color meshColor = Color.white;
        private Vector2[][] frames = Array.Empty<Vector2[]>();
        private float scale = 1f;
        private Vector2 center;
        private float framesPerSecond = 24f;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public void ConfigureDye(Texture2D mask, IReadOnlyDictionary<int, Vector4> channels)
        {
            Shader shader = Shader.Find("Zjcs/CharacterCreation/OfficialMeshFrameDye");
            if (shader == null || mask == null || channels == null || channels.Count == 0) return;
            var dye = new Material(shader) { hideFlags = HideFlags.DontSave };
            dye.mainTexture = texture;
            dye.SetTexture("_DyeTex", mask);
            foreach (KeyValuePair<int, Vector4> channel in channels)
                dye.SetVector("_DyeHsv" + channel.Key.ToString(CultureInfo.InvariantCulture), channel.Value);
            material = dye;
        }

        public void Initialize(Texture source, Vector2[] meshUv, int[] meshTriangles, Color color,
            Vector2[][] animationFrames, float sourceScale, Vector2 sourceCenter, float fps)
        {
            texture = source;
            uv = meshUv ?? Array.Empty<Vector2>();
            triangles = meshTriangles ?? Array.Empty<int>();
            meshColor = color;
            frames = animationFrames ?? Array.Empty<Vector2[]>();
            scale = sourceScale;
            center = sourceCenter;
            framesPerSecond = Mathf.Max(1f, fps);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (frames.Length > 1) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (frames.Length == 0 || uv.Length == 0 || triangles.Length < 3) return;
            Vector2[] positions = frames[Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % frames.Length];
            if (positions.Length != uv.Length) return;
            Color vertexColor = color * meshColor;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 position = positions[i];
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3((position.x - center.x) * scale,
                    (center.y - position.y) * scale, 0f);
                vertex.color = vertexColor;
                vertex.uv0 = uv[i];
                helper.AddVert(vertex);
            }
            for (int i = 0; i + 2 < triangles.Length; i += 3)
                helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
