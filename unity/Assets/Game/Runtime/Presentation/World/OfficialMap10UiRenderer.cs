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

        public OfficialMap10UiRenderer(OfficialMap10UiCatalog catalog)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        public GameObject Mount(Transform parent, Action<string, GameObject> bind = null)
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

            OfficialBattleHudCatalog battleHud = OfficialBattleHudCatalog.LoadDefault();
            foreach (OfficialMap10UiPanel panel in catalog.Panels)
            {
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
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                ? renderer.Find(path) : null;
        }

        public bool SetText(string panelKey, string path, string value)
        {
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                && renderer.SetText(path, value);
        }

        public bool SetSprite(string panelKey, string path, Sprite sprite)
        {
            return renderers.TryGetValue(panelKey, out OfficialBattleHudRenderer renderer)
                && renderer.SetSprite(path, sprite);
        }

        public bool SetNodeActive(string panelKey, string path, bool active)
        {
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
            if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
            canvasObject = null;
        }
    }
}
