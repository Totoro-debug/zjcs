using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    /// <summary>
    /// Binds the pinned TreasureDetailDialog prefab for read-only map reward previews.
    /// The map has no online item service, so only locally verified item fields are filled.
    /// </summary>
    public sealed class OfficialTreasureDetailPopup : MonoBehaviour, IPointerClickHandler
    {
        private readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private GameObject canvasObject;
        private Action closed;
        private WorldPopupWindow popup;
        public bool IsShown { get; private set; }

        public static OfficialTreasureDetailPopup Open(Transform parent, GameObject prefab,
            OfficialMap11UiAssets assets, int itemId,
            string itemName, string itemTypeName, string qualityName, long ownedCount, Action onClosed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (prefab == null) throw new InvalidOperationException("Official TreasureDetailDialog prefab is missing.");
            if (assets == null) throw new InvalidOperationException("Official map item assets are missing.");
            if (assets.Item(itemId) == null || assets.ItemQualityId(itemId) < 0)
                throw new InvalidOperationException("Official Map11 item metadata is missing: " + itemId);

            var canvasObject = new GameObject("TreasureDetailCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 121;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
            Image mask = canvasObject.AddComponent<Image>();
            mask.color = new Color(0, 0, 0, 0);
            mask.raycastTarget = true;

            GameObject root = Instantiate(prefab, canvasObject.transform, false);
            root.name = prefab.name;
            var view = canvasObject.AddComponent<OfficialTreasureDetailPopup>();
            view.canvasObject = canvasObject;
            view.closed = onClosed;
            view.Bind(root.transform, assets, itemId, itemName, itemTypeName, qualityName, ownedCount);
            view.popup = root.GetComponent<WorldPopupWindow>();
            if (view.popup != null)
            {
                view.popup.CloseByClickMask = true;
                view.popup.SetMaskClickAction(view.Close);
                view.popup.SetCanvasSortingOrder(canvas.sortingOrder + 1);
                view.popup.OnShow += _ => view.IsShown = true;
                view.popup.Show();
            }
            return view;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerCurrentRaycast.gameObject == gameObject)
                popup?.OnMaskClick();
        }

        private void Bind(Transform root, OfficialMap11UiAssets assets,
            int itemId, string itemName, string itemTypeName, string qualityName, long ownedCount)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                nodes[child == root ? string.Empty : RelativePath(root, child)] = child;

            SetText("BgDialog1/Content/TopGo/NameGo/NameText", itemName);
            SetText("BgDialog1/Content/TopGo/CountGo/CountContent/CountText", ownedCount.ToString(CultureInfo.InvariantCulture));
            SetText("BgDialog1/Content/TopGo/CountGo/CountContent/MaxCountText", string.Empty);
            SetText("BgDialog1/Content/TopGo/LevelText", string.Empty);
            SetText("BgDialog1/Content/TopGo/SpieceInfoGo/CountText", string.Empty);

            SetText("BgDialog1/Content/TopGo/TypeText", itemTypeName);
            SetText("BgDialog1/Content/TopGo/QualityText", qualityName);

            SetImage("BgDialog1/Content/TopGo/IconSlot/Icon", assets.Item(itemId));
            SetImage("BgDialog1/Content/TopGo/QualityBg", assets.ItemQuality(itemId));
            SetActive("BgDialog1/Content/TopGo/CountGo", true);
            SetActive("BgDialog1/Content/TopGo/QualityText", true);
            SetActive("BgDialog1/Content/TopGo/ItemRemainTimeContent", false);
            SetActive("BgDialog1/Content/TopGo/SpieceInfoGo", false);
            SetActive("BgDialog1/Content/TopGo/TreasureStarInfo", false);
            SetActive("BgDialog1/Content/MiddleGo", false);
            SetActive("BgDialog1/Content/MiddleGo/DialogButtonGroup", false);
            SetActive("BgDialog1/ItemDetailGmTool", false);

            SetActive("BgDialog1/Content/TopGo/RightUpBtns/ShareBtn", false);
            Button cancel = Find("BgDialog1/Content/MiddleGo/ButtonsGo/CommonBtn_11")?.GetComponent<Button>();
            if (cancel != null) cancel.onClick.AddListener(Close);
        }

        public void Close()
        {
            if (popup != null)
            {
                popup.Hide(DestroyPopup);
                return;
            }
            DestroyPopup();
        }

        private void DestroyPopup()
        {
            closed?.Invoke();
            if (canvasObject != null) Destroy(canvasObject);
            else Destroy(gameObject);
        }

        private Transform Find(string path) => nodes.TryGetValue(path, out Transform node) ? node : null;

        private void SetActive(string path, bool active)
        {
            Transform node = Find(path);
            if (node != null) node.gameObject.SetActive(active);
        }

        private void SetText(string path, string value)
        {
            Transform node = Find(path);
            if (node == null) return;
            TMP_Text text = node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private void SetImage(string path, Sprite sprite)
        {
            Transform node = Find(path);
            Image image = node == null ? null : node.GetComponent<Image>();
            if (image != null) image.sprite = sprite;
        }

        private static string RelativePath(Transform root, Transform child)
        {
            var names = new Stack<string>();
            for (Transform current = child; current != null && current != root; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }
    }
}
