using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Zjcs.Offline.BattleNativePlayables
{
    internal sealed class BattleAfterimageTexturePool
    {
        private const long MaxBytes = 5242880L;
        private const int MaxLength = 128;
        private readonly Dictionary<Vector2Int, Stack<RTHandle>> available = new Dictionary<Vector2Int, Stack<RTHandle>>();
        private readonly HashSet<RTHandle> rented = new HashSet<RTHandle>();
        private readonly HashSet<RTHandle> owned = new HashSet<RTHandle>();
        private readonly Dictionary<RenderTexture, RTHandle> handlesByTexture = new Dictionary<RenderTexture, RTHandle>();
        private long currentBytes;

        public RTHandle GetRT(int width, int height)
        {
            long size = Size(width, height);
            if (currentBytes + size > MaxBytes)
            {
                width = Mathf.Clamp(width, 0, MaxLength);
                height = Mathf.Clamp(height, 0, MaxLength);
                size = Size(width, height);
            }
            var key = new Vector2Int(width, height);
            if (!available.TryGetValue(key, out Stack<RTHandle> stack))
                available.Add(key, stack = new Stack<RTHandle>());
            RTHandle result = stack.Count > 0 ? stack.Pop() : Allocate(width, height);
            rented.Add(result);
            currentBytes += size;
            return result;
        }

        public void ReturnRT(RenderTexture texture)
        {
            if (texture == null || !handlesByTexture.TryGetValue(texture, out RTHandle handle)
                || !rented.Remove(handle)) return;
            var key = new Vector2Int(texture.width, texture.height);
            currentBytes -= Size(key.x, key.y);
            if (!available.TryGetValue(key, out Stack<RTHandle> stack))
                available.Add(key, stack = new Stack<RTHandle>());
            stack.Push(handle);
        }

        public void Dispose()
        {
            foreach (RTHandle texture in owned) RTHandles.Release(texture);
            owned.Clear(); rented.Clear(); available.Clear(); handlesByTexture.Clear(); currentBytes = 0;
        }

        private RTHandle Allocate(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 0);
            RTHandle result = RTHandles.Alloc(descriptor, FilterMode.Point, TextureWrapMode.Clamp,
                false, 1, 0f, string.Empty);
            owned.Add(result);
            handlesByTexture.Add(result.rt, result);
            return result;
        }

        private static long Size(int width, int height) => (long)width * height * 4;
    }
}
