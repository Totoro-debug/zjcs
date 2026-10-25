using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    [DisallowMultipleComponent]
    public sealed class CharacterPetEquipmentView : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private static PetGachaRuntimeCatalog sharedRules;
        private static OfficialPetGachaAssetCatalog sharedModels;
        private static TMP_FontAsset selectorFont;
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly List<GameObject> rows = new List<GameObject>();

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private Transform overlayParent;
        private RectTransform petRoot;
        private RectTransform equippedState;
        private RectTransform emptyState;
        private RectTransform modelRoot;
        private Button emptyButton;
        private Button equippedButton;
        private GameObject selector;
        private RectTransform selectorContent;
        private GameObject rowTemplate;
        private RenderTexture previewTexture;
        private Camera previewCamera;
        private GameObject previewModel;
        private long shownPetIndex = -1;
        private int displayedPlan = -1;

        public bool IsSelectorOpen => selector != null && selector.activeSelf;
        public event Action Changed;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, RectTransform petSelectRoot,
            RectTransform equipContent, RectTransform emptyContent, RectTransform petModelRoot,
            RectTransform emptySelectButton, RectTransform equippedSelectButton, Transform uiRoot,
            GameObject selectorInstance = null, GameObject selectorItemInstance = null)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression ?? throw new ArgumentNullException(nameof(characterProgression));
            petRoot = petSelectRoot != null ? petSelectRoot : throw new ArgumentNullException(nameof(petSelectRoot));
            equippedState = equipContent != null ? equipContent : throw new ArgumentNullException(nameof(equipContent));
            emptyState = emptyContent != null ? emptyContent : throw new ArgumentNullException(nameof(emptyContent));
            modelRoot = petModelRoot != null ? petModelRoot : throw new ArgumentNullException(nameof(petModelRoot));
            overlayParent = uiRoot != null ? uiRoot : throw new ArgumentNullException(nameof(uiRoot));
            selector = selectorInstance != null ? selectorInstance
                : InstantiateOfficialPrefab(assets.PetPlanSelectorPanel, overlayParent);
            selector.name = "OfficialPetPlanSelector";
            selector.SetActive(false);
            selector.transform.SetParent(overlayParent, false);
            RectTransform selectorRoot = selector.GetComponent<RectTransform>();
            if (selectorRoot == null)
                throw new InvalidOperationException("The official pet selector prefab root is not a RectTransform.");
            selectorRoot.anchorMin = Vector2.zero;
            selectorRoot.anchorMax = Vector2.one;
            selectorRoot.offsetMin = Vector2.zero;
            selectorRoot.offsetMax = Vector2.zero;
            Canvas standaloneCanvas = selector.GetComponent<Canvas>();
            if (standaloneCanvas == null)
                throw new InvalidOperationException("The official pet selector root canvas is missing.");
            foreach (Canvas canvas in selector.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            RectTransform viewport = FindDeep(selector.transform, "Viewport") as RectTransform;
            selectorContent = viewport == null ? null : FindDeep(viewport, "Content") as RectTransform;
            if (viewport == null || selectorContent == null)
                throw new FormatException("The original pet selector has no ScrollRect viewport/content.");
            Transform scrollRoot = FindDeep(selector.transform, "ScrollRect ");
            ScrollRect scroll = scrollRoot == null ? selector.GetComponentInChildren<ScrollRect>(true)
                : scrollRoot.GetComponent<ScrollRect>();
            if (scroll == null) scroll = (scrollRoot ?? viewport).gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = selectorContent;
            scroll.horizontal = false;
            scroll.vertical = true;

            VerticalLayoutGroup layout = selectorContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = selectorContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            ContentSizeFitter fitter = selectorContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = selectorContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            rowTemplate = selectorItemInstance != null ? selectorItemInstance
                : InstantiateOfficialPrefab(assets.PetPlanSelectorItem, selectorContent);
            rowTemplate.name = "PetSelectorRowTemplate";
            rowTemplate.SetActive(false);
            rowTemplate.transform.SetParent(selectorContent, false);
            if (rowTemplate.GetComponent<RectTransform>() == null)
                throw new InvalidOperationException("The official pet selector item root is not a RectTransform.");
            BindClick(FindDeep(selector.transform, "ConfirmBtn") as RectTransform, CloseSelector);

            if (sharedRules == null) sharedRules = OfficialPetGachaAssets.LoadDefault();
            if (sharedModels == null) sharedModels = OfficialPetGachaAssetCatalog.LoadDefault();
            sharedModels.Validate(sharedRules);

            emptyButton = BindClick(emptySelectButton, OpenSelector);
            RectTransform emptyContentImage = emptySelectButton != null && emptySelectButton.parent != null
                ? emptySelectButton.parent.Find("Image") as RectTransform : null;
            if (emptyContentImage != emptySelectButton)
                BindClick(emptyContentImage, OpenSelector);
            equippedButton = BindClick(equippedSelectButton, OpenSelector);
            CreatePreviewSurface();
            ShowFollowing();
        }

        public void ShowFollowing()
        {
            displayedPlan = -1;
            RefreshCurrent();
        }

        public void OpenFollowingSelector()
        {
            ShowFollowing();
            OpenSelector();
        }

        public void ShowPlan(int planIndex)
        {
            displayedPlan = Mathf.Clamp(planIndex, 0, 2);
            RefreshCurrent();
        }

        public void RefreshCurrent()
        {
            if (progression == null || petRoot == null) return;
            long petIndex = displayedPlan >= 0
                ? progression.Player.Skills.Plans[displayedPlan].PetIndex
                : progression.PetProgression.State.FollowPetIndex;
            OfflinePetInstance pet = petIndex > 0
                ? progression.PetProgression.State.Pets.FirstOrDefault(value => value.Index == petIndex)
                : null;
            bool hasPet = pet != null;
            emptyState.gameObject.SetActive(!hasPet);
            equippedState.gameObject.SetActive(hasPet);
            if (hasPet) ShowPetModel(pet);
            else ClearPetModel();
        }

        public void SetVisible(bool visible)
        {
            if (!visible) CloseSelector();
            if (petRoot != null) petRoot.gameObject.SetActive(visible);
            if (previewCamera != null) previewCamera.enabled = visible && previewModel != null;
        }

        public bool HandleEscape()
        {
            if (!IsSelectorOpen) return false;
            CloseSelector();
            return true;
        }

        private void CreatePreviewSurface()
        {
            var surface = new GameObject("PetModelPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            surface.transform.SetParent(modelRoot, false);
            RectTransform rect = (RectTransform)surface.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage image = surface.GetComponent<RawImage>();
            image.raycastTarget = false;

            previewTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPetPreview",
                antiAliasing = 2,
                useMipMap = false,
                autoGenerateMips = false
            };
            previewTexture.Create();
            image.texture = previewTexture;

            var cameraObject = new GameObject("PetPreviewCamera", typeof(Camera));
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.orthographic = true;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.targetTexture = previewTexture;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 200f;
            previewCamera.enabled = false;
        }

        private void ShowPetModel(OfflinePetInstance pet)
        {
            if (shownPetIndex == pet.Index && previewModel != null) return;
            ClearPetModel();
            PetGachaPetProfile profile = sharedRules.PetProfile(pet.ClassId);
            GameObject prefab = profile == null ? null : sharedModels.NativeModel(pet.ClassId, profile.UIModel);
            if (prefab == null)
            {
                Debug.LogWarning("The official character pet preview model is unavailable: " + pet.ClassId);
                return;
            }

            previewModel = Instantiate(prefab);
            previewModel.name = "EquippedPet_" + pet.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            previewModel.transform.localScale *= profile.ModelScale;
            Vector3 localPosition = previewModel.transform.localPosition;
            localPosition.y = profile.ModelOffsetY;
            previewModel.transform.localPosition = localPosition;
            foreach (Transform child in previewModel.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = PreviewLayer;
            Animator animator = previewModel.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.Play("idle_01", 0, 0f);

            Renderer[] renderers = previewModel.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled).ToArray();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("The official character pet preview has no renderers: " + pet.ClassId);
                Destroy(previewModel);
                previewModel = null;
                return;
            }
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            float aspect = modelRoot.rect.height <= 0f ? 1f
                : Mathf.Max(0.25f, modelRoot.rect.width / modelRoot.rect.height);
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            previewCamera.orthographicSize = Mathf.Max(0.05f, halfHeight * 1.16f);
            float distance = Mathf.Max(2f, bounds.extents.magnitude * 3f);
            previewCamera.transform.SetPositionAndRotation(
                bounds.center + Vector3.back * distance, Quaternion.identity);
            previewCamera.enabled = petRoot.gameObject.activeInHierarchy;
            shownPetIndex = pet.Index;
        }

        private void ClearPetModel()
        {
            if (previewModel != null) Destroy(previewModel);
            previewModel = null;
            shownPetIndex = -1;
            if (previewCamera != null) previewCamera.enabled = false;
        }

        private void OpenSelector()
        {
            if (selector == null) throw new InvalidOperationException("The official pet selector was not initialized.");
            if (selector.activeSelf) return;
            ApplySelectorFont(selector);
            selector.SetActive(true);
            selector.transform.SetAsLastSibling();
            IReadOnlyList<OfflinePetInstance> candidates = displayedPlan >= 0
                ? progression.PetProgression.Candidates(displayedPlan)
                : progression.PetProgression.FollowingCandidates();
            long selected = displayedPlan >= 0
                ? progression.Player.Skills.Plans[displayedPlan].PetIndex
                : progression.PetProgression.State.FollowPetIndex;
            foreach (OfflinePetInstance candidate in candidates)
                AddCandidate(selectorContent, candidate, selected);
            AddCandidate(selectorContent, null, selected);
            LayoutRebuilder.ForceRebuildLayoutImmediate(selectorContent);
            ScrollRect scroll = selector.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void AddCandidate(RectTransform content, OfflinePetInstance pet, long selectedIndex)
        {
            GameObject row = InstantiateOfficialPrefab(rowTemplate, content);
            ApplySelectorFont(row);
            row.name = pet == null ? "NoPet" : "Pet_" + pet.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(row);
            RectTransform rowRect = row.transform as RectTransform;
            float rowHeight = Mathf.Max(1f, rowRect.rect.height, rowRect.sizeDelta.y);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null) rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = rowHeight;
            bool hasPet = pet != null;
            SetActiveDeep(row.transform, "HasPet", hasPet);
            SetActiveDeep(row.transform, "NoPet", !hasPet);
            long index = hasPet ? pet.Index : -1;
            BindClick(row.transform as RectTransform, () => SelectCandidate(index));
            if (hasPet)
            {
                PetGachaItem item = sharedRules.Item(pet.ClassId);
                PetGachaPetProfile profile = sharedRules.PetProfile(pet.ClassId);
                string fallbackName = item == null ? pet.ClassId.ToString() : item.Name;
                SetTextDeep(FindDeep(row.transform, "Name"),
                    string.IsNullOrWhiteSpace(pet.Name) ? fallbackName : pet.Name);
                SetTextDeep(FindDeep(row.transform, "LevelText"), pet.Level.ToString());
                SetTextDeep(FindDeep(row.transform, "CombatRating"),
                    progression.PetProgression.CombatRating(pet).ToString());
                SetSpriteDeep(FindDeep(row.transform, "IconImg"), item == null ? null : LoadSprite(item.IconFile));
                SetSpriteDeep(FindDeep(row.transform, "TypeImg"), profile == null ? null : LoadSprite(profile.TypeIconFile));
                string qualityBackground = sharedRules.QualityBackground(pet.Quality);
                SetSpriteDeep(FindDeep(row.transform, "BgImg"), LoadSprite(qualityBackground));
                RefreshCandidateSkills(row.transform, profile);
            }
            RefreshCandidateSelection(row.transform, hasPet, index == selectedIndex);
            row.SetActive(true);
        }

        private void RefreshCandidateSkills(Transform row, PetGachaPetProfile profile)
        {
            Transform[] slots = row.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name.StartsWith("ItemIconPetSkill", StringComparison.Ordinal))
                .ToArray();
            int[] skillIds = profile == null ? Array.Empty<int>() : profile.SupportSkillIds ?? Array.Empty<int>();
            for (int index = 0; index < slots.Length; index++)
            {
                int skillId = index < skillIds.Length ? skillIds[index] : 0;
                PetGachaItem skill = skillId == 0 ? null : sharedRules.Item(skillId);
                Transform icon = FindDeep(slots[index], "icon");
                if (icon == null) icon = slots[index];
                SetSpriteDeep(icon, skill == null ? null : LoadSprite(skill.IconFile));
                slots[index].gameObject.SetActive(skill != null);
            }
        }

        private static GameObject InstantiateOfficialPrefab(GameObject prefab, Transform parent)
        {
            if (prefab == null) throw new InvalidOperationException("An official pet selector prefab is not bound.");
            RectTransform sourceRoot = prefab.GetComponent<RectTransform>();
            if (sourceRoot == null)
                throw new InvalidOperationException("The official pet selector prefab root is not a RectTransform.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            if (instance != null) return instance;
            throw new InvalidOperationException("The official pet selector prefab root could not be cloned.");
        }

        private static void ApplySelectorFont(GameObject root)
        {
            if (selectorFont == null)
                selectorFont = Resources.Load<TMP_FontAsset>("Commerce/FZY4K-Dynamic");
            if (selectorFont == null)
                throw new InvalidOperationException("The official dynamic character font is not installed.");
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                text.font = selectorFont;
        }

        private void SelectCandidate(long petIndex)
        {
            ProgressionResult result = displayedPlan >= 0
                ? progression.PetProgression.EquipPlanPet(displayedPlan, petIndex)
                : progression.PetProgression.SetFollowingPet(petIndex);
            if (result.Status == ProgressionResultStatus.InvalidState)
            {
                Debug.LogWarning("Official pet selection could not be saved: " + result.Message);
                return;
            }
            foreach (GameObject row in rows)
            {
                bool hasPet = !row.name.Equals("NoPet", StringComparison.Ordinal);
                long index = hasPet && long.TryParse(row.name.Substring("Pet_".Length), out long parsed) ? parsed : -1;
                RefreshCandidateSelection(row.transform, hasPet, index == petIndex);
            }
            RefreshCurrent();
            Changed?.Invoke();
        }

        private void RefreshCandidateSelection(Transform row, bool hasPet, bool selected)
        {
            Transform state = FindDeep(row, hasPet ? "HasPet" : "NoPet");
            if (state == null) return;
            Transform normal = FindDeep(state, "Nor");
            Transform chosen = FindDeep(state, "Sel");
            if (normal != null) normal.gameObject.SetActive(!selected);
            if (chosen != null) chosen.gameObject.SetActive(selected);
            Transform checkmark = FindDeep(state, "Checkmark");
            if (checkmark != null) checkmark.gameObject.SetActive(selected && hasPet);
        }

        private void CloseSelector()
        {
            if (selector != null) selector.SetActive(false);
            foreach (GameObject row in rows)
                if (row != null) Destroy(row);
            rows.Clear();
        }

        private static Button BindClick(RectTransform node, Action action)
        {
            if (node == null) return null;
            Button button = node.GetComponent<Button>();
            if (button == null) button = node.gameObject.AddComponent<Button>();
            Graphic graphic = button.targetGraphic ?? node.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image hitTarget = node.gameObject.AddComponent<Image>();
                hitTarget.color = new Color(1f, 1f, 1f, 0f);
                graphic = hitTarget;
            }
            graphic.raycastTarget = true;
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => action());
            return button;
        }

        private static void SetSpriteDeep(Transform target, Sprite sprite)
        {
            if (target == null) return;
            Image image = target.GetComponent<Image>() ?? target.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
                image.preserveAspect = true;
            }
        }

        private static void SetTextDeep(Transform target, string value)
        {
            if (target == null) return;
            Text legacy = target.GetComponent<Text>() ?? target.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = value;
                return;
            }
            foreach (Component component in target.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !component.GetType().Name.Contains("TextMeshPro", StringComparison.Ordinal))
                    continue;
                PropertyInfo text = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (text != null && text.CanWrite && text.PropertyType == typeof(string))
                {
                    text.SetValue(component, value);
                    return;
                }
            }
        }

        private static Sprite LoadSprite(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            string safeName = Path.GetFileName(fileName);
            if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
                throw new FormatException("An official pet UI sprite path is not a local filename: " + fileName);
            if (sprites.TryGetValue(safeName, out Sprite sprite)) return sprite;
            string path = Path.Combine(Application.streamingAssetsPath, "PetGacha", "sprites", safeName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("An official pet UI sprite is missing: " + safeName);
                return null;
            }
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = safeName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Destroy(texture);
                throw new InvalidDataException("An official pet UI sprite could not be decoded: " + safeName);
            }
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = Path.GetFileNameWithoutExtension(safeName);
            sprites.Add(safeName, sprite);
            return sprite;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDeep(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetActiveDeep(Transform root, string name, bool active)
        {
            Transform target = FindDeep(root, name);
            if (target != null) target.gameObject.SetActive(active);
        }

        private void OnDestroy()
        {
            CloseSelector();
            ClearPetModel();
            if (previewCamera != null) Destroy(previewCamera.gameObject);
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
        }
    }
}
