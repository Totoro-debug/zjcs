namespace Zjcs.Official.HomeHudUI
{
using UnityEngine;

public class PetEvolutionRedDotItem : PetRedDotItemBase
{
	public IHomePetEvolutionServices Services { get; set; }

	[SerializeField]
	private bool mIsAllPet;

	public override bool ShowRedDot()
	{
		IHomePetEvolutionServices mainPlayer = Services;
		if (mIsAllPet)
		{
			foreach (long item in mainPlayer.GetAllPetIndices())
			{
				if (mainPlayer.PetCanEvolution(item))
				{
					return true;
				}
			}
			return false;
		}
		return mainPlayer.PetCanEvolution(mPetId);
	}
}

}
