using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Atlas coordinates start at the page's top left. Source attachment UVs
    // describe the untrimmed image; Unity UVs start at the bottom left.
    public sealed class ProfessionNativeAtlasRegion
    {
        private readonly int pageWidth, pageHeight, left, top, width, height, offsetLeft, offsetBottom, originalWidth, originalHeight, rotation;
        public ProfessionNativeAtlasRegion(int pageWidth, int pageHeight, int left, int top, int width, int height,
            int offsetLeft, int offsetBottom, int originalWidth, int originalHeight, int rotation)
        {
            if (pageWidth <= 0 || pageHeight <= 0 || width <= 0 || height <= 0 || originalWidth <= 0 || originalHeight <= 0 ||
                left < 0 || top < 0 || offsetLeft < 0 || offsetBottom < 0 ||
                (long)offsetLeft + width > originalWidth || (long)offsetBottom + height > originalHeight ||
                (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270))
                throw new ArgumentException("Original atlas region dimensions, trim or rotation are invalid.");
            int packedWidth = rotation == 90 || rotation == 270 ? height : width;
            int packedHeight = rotation == 90 || rotation == 270 ? width : height;
            if ((long)left + packedWidth > pageWidth || (long)top + packedHeight > pageHeight)
                throw new ArgumentException("Original atlas region is outside its page.");
            this.pageWidth = pageWidth; this.pageHeight = pageHeight; this.left = left; this.top = top;
            this.width = width; this.height = height; this.offsetLeft = offsetLeft; this.offsetBottom = offsetBottom;
            this.originalWidth = originalWidth; this.originalHeight = originalHeight; this.rotation = rotation;
        }
        public Vector2 Map(Vector2 sourceUv)
        {
            Finite(sourceUv.x); Finite(sourceUv.y);
            float x = sourceUv.x * originalWidth - offsetLeft;
            float y = sourceUv.y * originalHeight - (originalHeight - offsetBottom - height);
            float pageX, pageY;
            switch (rotation)
            {
                case 0: pageX = x; pageY = y; break;
                case 90: pageX = y; pageY = width - x; break;
                case 180: pageX = width - x; pageY = height - y; break;
                default: pageX = height - y; pageY = x; break;
            }
            return new Vector2((left + pageX) / pageWidth, 1f - (top + pageY) / pageHeight);
        }
        public Vector2[] MapMeshUvs(float[] sourceUvs)
        {
            if (sourceUvs == null || sourceUvs.Length == 0 || sourceUvs.Length % 2 != 0)
                throw new ArgumentException("Original mesh UV pairs are invalid.");
            var result = new Vector2[sourceUvs.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Map(new Vector2(sourceUvs[2 * i], sourceUvs[2 * i + 1]));
            return result;
        }
        public Vector2[] CornerUvs()
        {
            // BL, TL, TR, BR in the original unrotated attachment.
            var result = new Vector2[4];
            for (int i = 0; i < result.Length; i++)
            {
                float x = offsetLeft + (i >= 2 ? width : 0);
                float y = originalHeight - offsetBottom - (i == 0 || i == 3 ? 0 : height);
                result[i] = Map(new Vector2(x / originalWidth, y / originalHeight));
            }
            return result;
        }
        public Vector2[] LocalCorners(float attachmentWidth, float attachmentHeight, float x, float y, float scaleX, float scaleY, float rotationDegrees)
        {
            Finite(attachmentWidth); Finite(attachmentHeight); Finite(x); Finite(y); Finite(scaleX); Finite(scaleY); Finite(rotationDegrees);
            var result = new Vector2[4];
            float angle = rotationDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int i = 0; i < result.Length; i++)
            {
                float localX = ((offsetLeft + (i >= 2 ? width : 0)) / (float)originalWidth - .5f) * attachmentWidth * scaleX;
                float localY = ((offsetBottom + (i == 0 || i == 3 ? 0 : height)) / (float)originalHeight - .5f) * attachmentHeight * scaleY;
                result[i] = new Vector2(x + cos * localX - sin * localY, y + sin * localX + cos * localY);
            }
            return result;
        }
        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original atlas input must be finite."); }
    }
}
