namespace Zjcs.Official.ProfessionUI
{
using System;
using System.IO;

public class CharacterPortraitPersistentCollection : ICharacterPortraitPersistentCollection
{
	public const string kRootFolderName = "CharacterPortrait";

	private const int kTTLDays = 3;

	private string mFolderPath;

	public CharacterPortraitPersistentCollection(string sandboxRoot)
	{
		mFolderPath = Path.Combine(sandboxRoot, "CharacterPortrait");
		if (!Directory.Exists(mFolderPath))
		{
			Directory.CreateDirectory(mFolderPath);
		}
	}

	public void SaveData(string fileName, byte[] data)
	{
		File.WriteAllBytes(Path.Combine(mFolderPath, fileName), data);
	}

	public bool TryGetData(string fileName, out byte[] data)
	{
		string path = Path.Combine(mFolderPath, fileName);
		if (!File.Exists(path))
		{
			data = null;
			return false;
		}
		data = File.ReadAllBytes(path);
		return true;
	}

	public int ClearAllExpireData()
	{
		int num = 0;
		foreach (string item in Directory.EnumerateFiles(mFolderPath))
		{
			FileInfo fileInfo = new FileInfo(item);
			if (fileInfo.LastAccessTimeUtc.AddDays(3.0) < DateTime.UtcNow)
			{
				fileInfo.Delete();
				num++;
			}
		}
		return num;
	}

	public void ClearAllData()
	{
		Directory.Delete(mFolderPath, recursive: true);
		Directory.CreateDirectory(mFolderPath);
	}
}

}
