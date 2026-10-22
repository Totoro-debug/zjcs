using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// One world-space battle damage label. The official float prefab (TextMesh + AnimationClip) is
    /// not migrated yet, so the label keeps the official font, size and colour and uses a fixed
    /// short rise/fade motion in its place.
    /// </summary>
    public sealed class OfficialBattleDamageFloat : MonoBehaviour
    {
        public const float Duration = 0.9f;
        private const float Rise = 1.2f;

        private TextMeshPro label;
        private Vector3 start;
        private Color color;
        private float elapsed;

        public static OfficialBattleDamageFloat Spawn(Transform parent, TMP_FontAsset font,
            string text, Color tint, Vector3 position)
        {
            var gameObject = new GameObject("BattleDamageFloat");
            if (parent != null) gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            var label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            label.color = tint;
            label.enableWordWrapping = false;
            gameObject.transform.localScale = Vector3.one * 0.02f;
            var result = gameObject.AddComponent<OfficialBattleDamageFloat>();
            result.label = label;
            result.start = position;
            result.color = tint;
            result.elapsed = 0f;
            return result;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);
            if (label != null)
            {
                transform.position = start + new Vector3(0f, Rise * progress, 0f);
                color.a = 1f - progress;
                label.color = color;
            }
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
