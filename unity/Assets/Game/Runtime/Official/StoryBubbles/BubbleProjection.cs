using UnityEngine;

namespace Zjcs.Official.StoryBubbles
{
    public static class BubbleProjection
    {
        // Original UIUtils.WorldToUIPos; the original main camera and canvas transform are significant.
        public static Vector3 WorldToUIPos(Vector3 pos, Canvas canvas) => WorldToUIPos(pos, canvas, Camera.main);
        public static Vector3 WorldToUIPos(Vector3 pos, Canvas canvas, Camera camera)
        {
            Rect pixelRect = canvas.pixelRect;
            Vector3 val = camera.WorldToViewportPoint(pos);
            Vector3 val2 = new Vector3(pixelRect.width / canvas.scaleFactor * (val.x - 0.5f),
                pixelRect.height / canvas.scaleFactor * (val.y - 0.5f), 0f);
            return canvas.transform.TransformPoint(val2);
        }
    }
}
