using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace AppearanceSystem.SpineExt
{
    [ExecuteAlways]
    public sealed class DynamicSkeletonRenderSeparator : SkeletonRenderSeparator
    {
        private readonly Queue<SkeletonPartsRenderer> mPartsRendererPool =
            new Queue<SkeletonPartsRenderer>();

        public static DynamicSkeletonRenderSeparator AddDynamicToSkeletonRenderer(
            SkeletonRenderer skeletonRenderer, int sortingLayerID = 0, int extraPartsRenderers = 0,
            int sortingOrderIncrement = 5, int baseSortingOrder = 0, bool addMinimumPartsRenderers = true)
        {
            if (skeletonRenderer == null) return null;

            DynamicSkeletonRenderSeparator separator =
                skeletonRenderer.gameObject.AddComponent<DynamicSkeletonRenderSeparator>();
            separator.skeletonRenderer = skeletonRenderer;
            skeletonRenderer.Initialize(false, false);
            separator.ReInitPartRenderers(sortingLayerID, extraPartsRenderers,
                sortingOrderIncrement, baseSortingOrder, addMinimumPartsRenderers);
            separator.OnEnable();
            return separator;
        }

        public void ReInitPartRenderers(int sortingLayerID = 0, int extraPartsRenderers = 0,
            int sortingOrderIncrement = 5, int baseSortingOrder = 0, bool addMinimumPartsRenderers = true)
        {
            int targetCount = extraPartsRenderers;
            if (addMinimumPartsRenderers)
                targetCount += skeletonRenderer.separatorSlots.Count + 1;

            Transform parent = skeletonRenderer.transform;
            List<SkeletonPartsRenderer> renderers = partsRenderers;
            int currentCount = renderers.Count;
            if (targetCount < currentCount)
            {
                for (int i = currentCount - 1; i >= targetCount; i--)
                {
                    renderers[i].gameObject.SetActive(false);
                    mPartsRendererPool.Enqueue(renderers[i]);
                    renderers.RemoveAt(i);
                }
            }
            else
            {
                for (int i = currentCount; i < targetCount; i++)
                {
                    if (!mPartsRendererPool.TryDequeue(out SkeletonPartsRenderer renderer))
                    {
                        renderer = SkeletonPartsRenderer.NewPartsRendererGameObject(parent, i.ToString(), 0);
                        renderer.MeshRenderer.sortingLayerID = sortingLayerID;
                        renderer.MeshRenderer.sortingOrder = baseSortingOrder + i * sortingOrderIncrement;
                    }
                    renderer.gameObject.SetActive(true);
                    renderers.Add(renderer);
                }
            }
            ResetVisibility();
        }

        public void ClearAndDestroyPartsRenderers()
        {
            for (int i = partsRenderers.Count - 1; i >= 0; i--)
            {
                Destroy(partsRenderers[i].gameObject);
                partsRenderers.RemoveAt(i);
            }
        }

        public void ResetVisibility()
        {
            foreach (SkeletonPartsRenderer renderer in partsRenderers)
            {
                if (renderer != null && renderer.MeshRenderer.isVisible)
                {
                    skeletonRenderer.OnBecameVisible();
                    return;
                }
            }
            skeletonRenderer.OnBecameInvisible();
        }
    }
}
