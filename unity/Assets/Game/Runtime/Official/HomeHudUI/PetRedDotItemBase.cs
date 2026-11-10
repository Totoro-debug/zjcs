namespace Zjcs.Official.HomeHudUI
{
public abstract class PetRedDotItemBase : RedDotItemBase
{
	protected long mPetId = -1L;

	public void SetPet(long petId)
	{
		mPetId = petId;
		SetDirty();
	}
}

}
