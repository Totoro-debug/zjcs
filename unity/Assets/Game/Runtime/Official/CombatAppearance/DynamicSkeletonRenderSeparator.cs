using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
// Original 884_156692 application pool, ordering and visibility decisions.
[ExecuteAlways]
public class DynamicSkeletonRenderSeparator : MonoBehaviour, IProfessionAppearancePartSeparator
{
    private IProfessionSceneSeparatorRenderer skeletonRenderer;
    private IProfessionSceneSeparatorBackend backend;
    public IReadOnlyList<IProfessionSceneMeshPart> PartsRenderers => backend.PartsRenderers;
    public event ProfessionPartSharedMaterial OnGetSharedMaterial
    {
        add { backend.OnGetSharedMaterial += value; }
        remove { backend.OnGetSharedMaterial -= value; }
    }
    public void ClearSharedMaterials() => backend.ClearSharedMaterials();
    void IProfessionAppearancePartSeparator.ReInitPartRenderers(int layer, int extra, int increment, int order)
        => ReInitPartRenderers(layer, extra, increment, order, true);
	private Queue<IProfessionSceneMeshPart> mPartsRendererPool = new Queue<IProfessionSceneMeshPart>();

	public static DynamicSkeletonRenderSeparator AddDynamicToSkeletonRenderer(IProfessionSceneSeparatorRenderer skeletonRenderer, int sortingLayerID = 0, int extraPartsRenderers = 0, int sortingOrderIncrement = 5, int baseSortingOrder = 0, bool addMinimumPartsRenderers = true)
	{
		if (skeletonRenderer == null || skeletonRenderer is Object unitySource && !unitySource)
		{
            // Original null-source branch returns without adding a component.
			return null;
		}
		DynamicSkeletonRenderSeparator dynamicSkeletonRenderSeparator = skeletonRenderer.GameObject.AddComponent<DynamicSkeletonRenderSeparator>();
		dynamicSkeletonRenderSeparator.skeletonRenderer = skeletonRenderer;
        dynamicSkeletonRenderSeparator.backend = skeletonRenderer.CreateSeparatorBackend();
		skeletonRenderer.Initialize(false, false);
		dynamicSkeletonRenderSeparator.backend.OptimizeRender = true;
		dynamicSkeletonRenderSeparator.ReInitPartRenderers(sortingLayerID, extraPartsRenderers, sortingOrderIncrement, baseSortingOrder, addMinimumPartsRenderers);
		dynamicSkeletonRenderSeparator.backend.OnEnable();
		return dynamicSkeletonRenderSeparator;
	}

	public void ReInitPartRenderers(int sortingLayerID = 0, int extraPartsRenderers = 0, int sortingOrderIncrement = 5, int baseSortingOrder = 0, bool addMinimumPartsRenderers = true)
	{
		int num = extraPartsRenderers;
		if (addMinimumPartsRenderers)
		{
			num = extraPartsRenderers + skeletonRenderer.SeparatorSlots.Count + 1;
		}
		Transform transform = skeletonRenderer.GameObject.transform;
		List<IProfessionSceneMeshPart> partsRenderers = backend.PartsRenderers;
		int count = partsRenderers.Count;
		if (num < count)
		{
			for (int num2 = count - 1; num2 >= num; num2--)
			{
				partsRenderers[num2].GameObject.SetActive(false);
				mPartsRendererPool.Enqueue(partsRenderers[num2]);
				partsRenderers.RemoveAt(num2);
			}
		}
		else
		{
			IProfessionSceneMeshPart val = default;
			for (int i = count; i < num; i++)
			{
				if (!mPartsRendererPool.TryDequeue(out val))
				{
					val = backend.NewPartsRendererGameObject(transform, i.ToString(), 0);
					MeshRenderer meshRenderer = val.MeshRenderer;
					meshRenderer.sortingLayerID = sortingLayerID;
					meshRenderer.sortingOrder = baseSortingOrder + i * sortingOrderIncrement;
				}
				val.GameObject.SetActive(true);
				partsRenderers.Add(val);
			}
		}
		ResetVisibility();
	}

	public void ClearAndDestroyPartsRenderers()
	{
		for (int num = backend.PartsRenderers.Count - 1; num >= 0; num--)
		{
			Object.Destroy(backend.PartsRenderers[num].GameObject);
			backend.PartsRenderers.RemoveAt(num);
		}
	}

	public void ResetVisibility()
	{
		foreach (IProfessionSceneMeshPart partsRenderer in backend.PartsRenderers)
		{
			if (partsRenderer != null && !(partsRenderer is Object unityPart && !unityPart) && partsRenderer.MeshRenderer.isVisible)
			{
				skeletonRenderer.OnBecameVisible();
				return;
			}
		}
		skeletonRenderer.OnBecameInvisible();
	}
}
}
