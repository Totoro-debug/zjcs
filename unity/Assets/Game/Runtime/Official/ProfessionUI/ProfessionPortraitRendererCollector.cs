namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public static class ProfessionPortraitRendererCollector
{
    private static readonly int sColorMask = Shader.PropertyToID("_ColorMask");

	public static void CollectPortraitRenderers(IProfessionPortraitRendererGraph rendererGraph, List<MeshRenderer> list, Comparer<MeshRenderer> comparer)
	{
		list.Clear();
		MeshRenderer item = default;
		if (rendererGraph.HasSeparator)
		{
			IReadOnlyList<MeshRenderer> partsRenderers = rendererGraph.SeparatedRenderers;
			int count = partsRenderers.Count;
			for (int i = 0; i < count; i++)
			{
				MeshRenderer val = partsRenderers[i];
				list.Add(val);
			}
		}
		else if (rendererGraph.Root.TryGetComponent<MeshRenderer>(out item))
		{
			list.Add(item);
		}
		if (rendererGraph.LoadedParts != null)
		{
			foreach (ProfessionPortraitRenderPart appearanceSkeletonPartEntry2 in rendererGraph.LoadedParts)
			{
				list.Add(appearanceSkeletonPartEntry2.SkeletonTarget.GetComponent<MeshRenderer>());
			}
		}
		list.Sort(comparer);
	}

	public static void CollectPortraitAndParticleRenderers(IProfessionPortraitRendererGraph rendererGraph, List<Renderer> list, Comparer<Renderer> comparer, bool includeParticle)
	{
		list.Clear();
		MeshRenderer item = default;
		if (rendererGraph.HasSeparator)
		{
			IReadOnlyList<MeshRenderer> partsRenderers = rendererGraph.SeparatedRenderers;
			int count = partsRenderers.Count;
			for (int i = 0; i < count; i++)
			{
				MeshRenderer val = partsRenderers[i];
				list.Add((Renderer)(object)val);
			}
		}
		else if (rendererGraph.Root.TryGetComponent<MeshRenderer>(out item))
		{
			list.Add((Renderer)(object)item);
		}
		if (rendererGraph.LoadedParts != null)
		{
			AppearancePortraitParticleProvider appearancePortraitParticleProvider = default;
			foreach (ProfessionPortraitRenderPart appearanceSkeletonPartEntry2 in rendererGraph.LoadedParts)
			{
				list.Add((Renderer)(object)appearanceSkeletonPartEntry2.SkeletonTarget.GetComponent<MeshRenderer>());
				if (!includeParticle || !appearanceSkeletonPartEntry2.GameObject.TryGetComponent<AppearancePortraitParticleProvider>(out appearancePortraitParticleProvider))
				{
					continue;
				}
				List<ParticleSystemRenderer> particleSystemRenderers = appearancePortraitParticleProvider.GetParticleSystemRenderers();
				if (particleSystemRenderers.Count <= 0)
				{
					continue;
				}
				foreach (ParticleSystemRenderer item3 in particleSystemRenderers)
				{
					((Renderer)item3).material.SetFloat(sColorMask, 15f);
				}
				list.AddRange((IEnumerable<Renderer>)particleSystemRenderers);
			}
		}
		list.Sort(comparer);
	}
}

}
