using System;
using UnityEngine;
using Zjcs.Official.StoryMasks;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    // All chapters use the same native global mask. The map supplies its existing
    // overlay root; no chapter creates mask geometry or simulates button input.
    public sealed class NativeStoryMaskHost : MonoBehaviour, IStoryMaskHost
    {
        private Transform windowsRoot;
        private GameObject lastWindow;
        private bool opening;
        public StoryMaskManager Manager { get; private set; }
        public GameObject LastNonTipWindow => FindLastWindow();
        public event Action<GameObject> OnWindowClosing;
        public event Action<GameObject> OnWindowClosed;
        public event Action<bool> OnOpeningWindowStateChanged;

        public static NativeStoryMaskHost Create(Transform root)
        {
            GameObject prefab = Resources.Load<GameObject>("StoryUI/Mask/StoryMask");
            if (!prefab) throw new InvalidOperationException("Original native StoryMask is not packaged.");
            var staging = new GameObject("Story mask inactive binding", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(root, false);
            var stagingRect = staging.GetComponent<RectTransform>();
            stagingRect.anchorMin = Vector2.zero;
            stagingRect.anchorMax = Vector2.one;
            stagingRect.offsetMin = stagingRect.offsetMax = Vector2.zero;
            GameObject instance = Instantiate(prefab, staging.transform, false);
            var host = instance.AddComponent<NativeStoryMaskHost>();
            host.windowsRoot = root;
            Canvas rootCanvas = root.GetComponentInParent<Canvas>();
            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
                canvas.worldCamera = rootCanvas ? rootCanvas.worldCamera : null;
            host.Manager = new StoryMaskManager(instance.GetComponent<StoryMask>(), host);
            host.Manager.Enable();
            instance.transform.SetParent(root, false);
            Destroy(staging);
            return host;
        }

        public GameObject LoadPrefab(string originalGuid)
        {
            if (originalGuid != "8578cbcb823445046ba91b8e152fe61b")
                throw new InvalidOperationException("Unbound original story mask GUID: " + originalGuid);
            return Resources.Load<GameObject>("StoryUI/Mask/Mask");
        }

        private GameObject FindLastWindow()
        {
            if (!windowsRoot) return null;
            WorldPopupWindow last = null;
            foreach (var popup in windowsRoot.GetComponentsInChildren<WorldPopupWindow>(false))
            {
                var metadata = popup.GetComponent<NativeStoryWindowMetadata>();
                if (metadata && metadata.WindowType == 3) continue; // Original WindowType.Tip.
                if (!last || (popup.Canvas ? popup.Canvas.sortingOrder : 0) >= (last.Canvas ? last.Canvas.sortingOrder : 0)) last = popup;
            }
            return last ? last.gameObject : null;
        }

        private void Update()
        {
            GameObject current = FindLastWindow();
            if (current != lastWindow)
            {
                if (lastWindow) { OnWindowClosing?.Invoke(lastWindow); OnWindowClosed?.Invoke(lastWindow); }
                lastWindow = current;
            }
            var popup = current ? current.GetComponent<WorldPopupWindow>() : null;
            bool nowOpening = popup && popup.State == WorldPopupWindow.EState.Showing;
            if (opening != nowOpening) { opening = nowOpening; OnOpeningWindowStateChanged?.Invoke(opening); }
        }

        private void OnEnable() => Manager?.Enable();
        private void OnDisable() => Manager?.Disable();
    }
}
