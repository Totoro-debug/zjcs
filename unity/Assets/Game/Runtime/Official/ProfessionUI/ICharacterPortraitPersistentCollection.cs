namespace Zjcs.Official.ProfessionUI
{
public interface ICharacterPortraitPersistentCollection
{
	void ClearAllData();

	int ClearAllExpireData();

	void SaveData(string fileName, byte[] data);

	bool TryGetData(string fileName, out byte[] data);
}

}
