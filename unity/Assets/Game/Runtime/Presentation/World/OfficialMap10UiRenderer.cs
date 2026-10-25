using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap10UiRenderer : IDisposable
    {
        private readonly OfficialMap10UiCatalog catalog;
        private readonly Dictionary<string, OfficialBattleHudRenderer> renderers =
            new Dictionary<string, OfficialBattleHudRenderer>(StringComparer.Ordinal);
        private GameObject canvasObject;
        private readonly Dictionary<string, GameObject> nativePanels = new Dictionary<string, GameObject>();
        private CanvasScaler parentScaler;
        private Vector2 previousResolution;
        private CanvasScaler.ScreenMatchMode previousMatchMode;
        private float previousMatch;

        public OfficialMap10UiRenderer(OfficialMap10UiCatalog catalog)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        public GameObject Mount(Transform parent, Action<string, GameObject> bind = null, int mapClassId = 10)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (canvasObject != null) throw new InvalidOperationException("Official Map10 UI is already mounted.");
            canvasObject = new GameObject("OfficialMap10UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (mapClassId == 11)
            {
                TMPro.TMP_SpriteAsset common = Resources.Load<TMPro.TMP_SpriteAsset>("Map11/UI/Common");
                if (common == null) throw new System.IO.InvalidDataException("Official Common sprite asset is missing.");
                TMPro.MaterialReferenceManager.AddSpriteAsset(common.hashCode, common);
                // TMP resolves a named sprite tag by its current name. The original
                // asset retains a serialized hash from before it was renamed Common.
                TMPro.MaterialReferenceManager.AddSpriteAsset(TMPro.TMP_TextUtilities.GetSimpleHashCode(common.name), common);
                OfficialMap11UiData data = JsonUtility.FromJson<OfficialMap11UiData>(
                    Resources.Load<TextAsset>("Map11/UI/catalog").text);
                OfficialMap11CanvasScaler original = data.worldBindings.canvasScaler;
                Canvas activeCanvas = canvas.rootCanvas;
                parentScaler = activeCanvas.GetComponent<CanvasScaler>();
                previousResolution = parentScaler.referenceResolution;
                previousMatchMode = parentScaler.screenMatchMode;
                previousMatch = parentScaler.matchWidthOrHeight;
                parentScaler.referenceResolution = new Vector2(original.referenceResolution[0], original.referenceResolution[1]);
                parentScaler.screenMatchMode = (CanvasScaler.ScreenMatchMode)original.screenMatchMode;
                parentScaler.matchWidthOrHeight = original.matchWidthOrHeight;
                scaler.referenceResolution = parentScaler.referenceResolution;
                scaler.screenMatchMode = parentScaler.screenMatchMode;
            }

            OfficialBattleHudCatalog battleHud = OfficialBattleHudCatalog.LoadDefault();
            foreach (OfficialMap10UiPanel panel in catalog.Panels)
            {
                if (mapClassId == 11 && panel.key == "WorldCanvas")
                {
                    GameObject restored = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map11/UI/WorldCanvas"),
                        canvasObject.transform, false);
                    restored.name = panel.key;
                    nativePanels.Add(panel.key, restored);
                    foreach (Transform child in restored.GetComponentsInChildren<Transform>(true))
                    {
                        // Native paths retain the official hierarchy for the existing control binder.
                        string relative = UnityPath(restored.transform, child);
                        bind?.Invoke("/" + panel.key + relative, child.gameObject);
                    }
                    restored.GetComponent<Zjcs.Official.WorldUI.WorldPanel>().Initialize(false);
                    continue;
                }
                var renderer = new OfficialBattleHudRenderer(battleHud, catalog.FindSprite);
                GameObject panelObject = renderer.CreateSubtree(panel.BuildRoot(), canvasObject.transform, bind);
                panelObject.name = panel.key;
                if (panel.key != "WorldCanvas") panelObject.SetActive(false);
                renderers.Add(panel.key, renderer);
            }
            return canvasObject;
        }

        public GameObject Find(string panelKey, string path)
        {
            if (nativePanels.TryGetValue(panelKey, out GameObject panel))
            {
                string relative = path.Substring(("/" + panelKey).Length).TrimStart('/');
                return relative.Length == 0 ? panel : panel.transform.Find(relative)?.gameObject;
            }
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                ? renderer.Find(path) : null;
        }

        public bool SetText(string panelKey, string path, string value)
        {
            if (nativePanels.ContainsKey(panelKey))
            {
                TMPro.TMP_Text text = Find(panelKey, path)?.GetComponent<TMPro.TMP_Text>();
                if (text == null) return false;
                text.text = value ?? string.Empty;
                return true;
            }
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                && renderer.SetText(path, value);
        }

        public bool SetSprite(string panelKey, string path, Sprite sprite)
        {
            if (nativePanels.ContainsKey(panelKey))
            {
                Image image = Find(panelKey, path)?.GetComponent<Image>();
                if (image == null) return false;
                image.sprite = sprite;
                return true;
            }
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                && renderer.SetSprite(path, sprite);
        }

        public bool SetNodeActive(string panelKey, string path, bool active)
        {
            if (nativePanels.ContainsKey(panelKey))
            {
                GameObject node = Find(panelKey, path);
                if (node == null) return false;
                node.SetActive(active);
                return true;
            }
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                && renderer.SetActive(path, active);
        }

        public bool SetPanelActive(string panelKey, bool active)
        {
            if (canvasObject == null) throw new InvalidOperationException("Mount the official Map10 UI before changing panel state.");
            Transform panel = canvasObject.transform.Find(panelKey);
            if (panel == null) return false;
            panel.gameObject.SetActive(active);
            return true;
        }

        public void Dispose()
        {
            renderers.Clear();
            nativePanels.Clear();
            if (parentScaler != null)
            {
                parentScaler.referenceResolution = previousResolution;
                parentScaler.screenMatchMode = previousMatchMode;
                parentScaler.matchWidthOrHeight = previousMatch;
                parentScaler = null;
            }
            if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
            canvasObject = null;
        }

        private static string UnityPath(Transform root, Transform child)
        {
            string path = string.Empty;
            for (Transform cursor = child; cursor != root; cursor = cursor.parent) path = "/" + cursor.name + path;
            return path;
        }
    }
}
