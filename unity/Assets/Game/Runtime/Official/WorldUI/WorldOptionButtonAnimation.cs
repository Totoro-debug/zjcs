using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Official.WorldUI
{
    // The original option Button controller contains Normal and Pressed clips.
    // Play those same clips with Windows pointer events.
    public sealed class WorldOptionButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Animation animationPlayer;
        private void OnEnable()
        {
            animationPlayer = GetComponent<Animation>();
            Play("Normal");
        }
        private void Play(string clip)
        {
            if (animationPlayer == null || animationPlayer[clip] == null) return;
            animationPlayer.Play(clip);
            animationPlayer.Sample();
        }
        public void OnPointerDown(PointerEventData data) => Play("Pressed");
        public void OnPointerUp(PointerEventData data) => Play("Normal");
        public void OnPointerExit(PointerEventData data) => Play("Normal");
    }
}
