using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Zjcs.Presentation.Character
{
    // SafeArea.StaticGetApplySafeArea / ApplySafeAreaIsolated, with the reference
    // phone's top inset retained by the Windows portrait presentation adapter.
    [DefaultExecutionOrder(-1)]
    internal sealed class CharacterSafeArea : MonoBehaviour
    {
        internal const float ReferenceTopInset = 137f;
        private float paddingTop, paddingBottom;
        private bool disableTop, disableBottom;
        private Vector2 lastSize;
        private bool ready;

        internal static void Restore(Transform root, string sourcePath, Dictionary<string, object> templates)
        {
            var bindings = CharacterModelPackage.AsObject(templates["safe_area_bindings"], "safe area bindings");
            foreach (var entry in bindings)
            {
                if (entry.Key != sourcePath && !entry.Key.StartsWith(sourcePath + "/", StringComparison.Ordinal)) continue;
                Transform target = entry.Key == sourcePath ? root : root.Find(entry.Key.Substring(sourcePath.Length + 1));
                if (target == null) continue;
                var record = CharacterModelPackage.AsObject(entry.Value, "safe area record");
                var fields = CharacterModelPackage.AsObject(record["fields"], "safe area fields");
                var area = target.GetComponent<CharacterSafeArea>();
                if (area == null) area = target.gameObject.AddComponent<CharacterSafeArea>();
                area.paddingTop = Convert.ToSingle(fields["PaddingTop"], CultureInfo.InvariantCulture);
                area.paddingBottom = Convert.ToSingle(fields["PaddingBottom"], CultureInfo.InvariantCulture);
                area.disableTop = Convert.ToInt32(fields["DisableTopAdapt"], CultureInfo.InvariantCulture) != 0;
                area.disableBottom = Convert.ToInt32(fields["DisableBottomAdapt"], CultureInfo.InvariantCulture) != 0;
                area.ready = true;
                area.Apply();
            }
        }

        private void OnEnable() { if (ready) Apply(); }
        private void Start() { if (ready) Apply(); }
        private void LateUpdate()
        {
            if (ready && lastSize != new Vector2(Screen.width, Screen.height)) Apply();
        }

        private void Apply()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = (RectTransform)canvas.rootCanvas.transform;
            var self = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
            float width = root.rect.width, height = root.rect.height;
            if (width <= 0 || height <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
            Rect system = Screen.safeArea;
            float x = system.xMin * width / Screen.width;
            float right = system.xMax * width / Screen.width;
            float bottom = disableBottom ? 0f : system.yMin * height / Screen.height + paddingBottom;
            float safeTop = Mathf.Min(system.yMax * height / Screen.height, height - ReferenceTopInset * width / 1080f);
            float top = disableTop ? height : safeTop + paddingTop;
            self.anchorMin = Vector2.zero; self.anchorMax = Vector2.one;
            self.pivot = Vector2.zero; self.sizeDelta = Vector2.zero; self.anchoredPosition = Vector2.zero;
            var area = new Rect(x, bottom, Mathf.Max(0, right - x), Mathf.Max(0, top - bottom));
            self.sizeDelta += area.size - self.rect.size;
            var corners = new Vector3[4]; root.GetWorldCorners(corners);
            Vector3 origin = self.worldToLocalMatrix.MultiplyPoint3x4(corners[0]);
            self.anchoredPosition += new Vector2(origin.x + area.x, origin.y + area.y);
            lastSize = new Vector2(Screen.width, Screen.height);
        }
    }
}
