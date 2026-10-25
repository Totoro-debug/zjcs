using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>GameRoundUI.UpdateFrameIconPos, including wrap around the left endpoint.</summary>
    public sealed class OpeningTimelineMotion : MonoBehaviour
    {
        private float start, target, distance, width, elapsed, duration;
        private RectTransform rect;
        public void MoveTo(float x,float barWidth,float moveDuration)
        {
            rect=(RectTransform)transform;
            start=rect.anchoredPosition.x; target=x; width=barWidth;
            distance=x-start;
            if (start>x) distance+=width;
            duration=moveDuration; elapsed=0;
            enabled=true;
        }
        private void Update()
        {
            elapsed+=Time.deltaTime;
            float progress=Mathf.Clamp01(elapsed/duration);
            float x=start+distance*progress;
            if (x>width*.5f) x-=width;
            rect.anchoredPosition=new Vector2(progress>=1?target:x,0);
            if (progress>=1) enabled=false;
        }
    }
}
