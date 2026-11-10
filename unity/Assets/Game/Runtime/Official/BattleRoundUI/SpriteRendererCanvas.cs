using UnityEngine;

namespace Zjcs.Official.BattleRoundUI
{
    public sealed class SpriteRendererCanvas : MonoBehaviour
    {
        [Range(0f, 1f)] public float Alpha = 1f;
        [SerializeField] private SpriteRenderer[] mSpriteRenderers;
        [SerializeField] private TextMesh[] mTextMeshs;

        private void Awake() => RefreshAlpha();

        public void RefreshAlpha()
        {
            if (mSpriteRenderers != null)
                foreach (SpriteRenderer renderer in mSpriteRenderers)
                    if (renderer != null)
                    {
                        Color color = renderer.color;
                        color.a = Alpha;
                        renderer.color = color;
                    }
            if (mTextMeshs != null)
                foreach (TextMesh text in mTextMeshs)
                    if (text != null)
                    {
                        Color color = text.color;
                        color.a = Alpha;
                        text.color = color;
                    }
        }

        private void OnDidApplyAnimationProperties() => RefreshAlpha();
    }
}
