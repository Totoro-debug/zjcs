using TMPro;
using UnityEngine;

namespace Zjcs.Offline.StoryBubbles
{
    public sealed class BubbleFontBinding : MonoBehaviour
    {
        public TMP_FontAsset Source;
        private BubbleFontInstance instance;
        private void Awake()
        {
            instance = new BubbleFontInstance(Source);
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = instance.Font;
        }
        private void OnDestroy() => instance?.Dispose();
    }
}
