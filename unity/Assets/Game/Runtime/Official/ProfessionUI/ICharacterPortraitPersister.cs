namespace Zjcs.Official.ProfessionUI
{
public interface ICharacterPortraitPersister
{
	bool TryGetPersistentTexture(CharacterPortraitDetailRequestData request, out byte[] imageData);

	void SetPersistentTexture(CharacterPortraitDetailRequestData request, byte[] bsImageData);

	void ClearAllStorage();
}

}
