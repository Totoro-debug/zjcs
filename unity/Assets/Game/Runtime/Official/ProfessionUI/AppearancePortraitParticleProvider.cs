namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class AppearancePortraitParticleProvider : MonoBehaviour
{
	[SerializeField]
	private List<ParticleSystemRenderer> mParticleSystemRenderers;

	private List<ParticleSystemRenderer> mRenderers;

	public List<ParticleSystemRenderer> GetParticleSystemRenderers()
	{
		if (mRenderers == null)
		{
			mRenderers = new List<ParticleSystemRenderer>();
			foreach (ParticleSystemRenderer mParticleSystemRenderer in mParticleSystemRenderers)
			{
				if ((Object)(object)mParticleSystemRenderer != (Object)null)
				{
					mRenderers.Add(mParticleSystemRenderer);
				}
			}
		}
		return mRenderers;
	}
}

}
