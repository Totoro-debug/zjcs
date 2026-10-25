using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterNativePortraitLease : MonoBehaviour
    {
        private Texture2D texture;
        public Sprite Sprite { get; private set; }
        public void Initialize(Texture2D captured)
        {
            texture = captured;
            Sprite = Sprite.Create(captured, new Rect(0, 0, captured.width, captured.height), new Vector2(.5f, .5f), 100);
            Sprite.name = "Official captured player head";
        }
        private void OnDestroy()
        {
            if (Sprite != null) Destroy(Sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
