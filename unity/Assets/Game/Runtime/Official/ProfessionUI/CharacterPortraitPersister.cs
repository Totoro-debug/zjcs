namespace Zjcs.Official.ProfessionUI
{
using System;
using Cysharp.Threading.Tasks;

public class CharacterPortraitPersister : ICharacterPortraitPersister
{
	private const int kCodeVersion = 3;

	private ICharacterPortraitPersistentCollection mPersistentCollection;

	private IPersistentState<CharacterPortraitPersisterState> mPersistentState;

	public CharacterPortraitPersister(IPersistentState<CharacterPortraitPersisterState> persistentState, string sandboxRoot)
	{

		mPersistentCollection = new CharacterPortraitPersistentCollection(sandboxRoot);
		mPersistentState = persistentState;
		if (mPersistentState.State.CodeVersion != 3)
		{
			ClearAllStorage();
			mPersistentState.State.CodeVersion = 3;
			UniTaskExtensions.Forget(mPersistentState.WriteStateAsync());
		}
		ClearAllExpirePersistentStorage();
	}

	private string GetDataName(long id, long hashCode)
	{
		return $"{id}_{hashCode}";
	}

	public bool TryGetPersistentTexture(CharacterPortraitDetailRequestData request, out byte[] imageData)
	{

		try
		{
			string dataName = GetDataName(request.RequestId, request.AppearanceHashCode);
			return mPersistentCollection.TryGetData(dataName, out imageData) && imageData.Length != 0;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogException(ex);
			imageData = null;
			return false;
		}
	}

	public void SetPersistentTexture(CharacterPortraitDetailRequestData request, byte[] bsImageData)
	{

		if (bsImageData == null || bsImageData.Length == 0)
		{
			UnityEngine.Debug.LogError("SetPersistentTexture texture.BsPngData 是null");
			return;
		}
		try
		{
			string dataName = GetDataName(request.RequestId, request.AppearanceHashCode);
			mPersistentCollection.SaveData(dataName, bsImageData);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogException(ex);
		}
	}

	private void ClearAllExpirePersistentStorage()
	{

		int num = mPersistentCollection.ClearAllExpireData();
		UnityEngine.Debug.Log("ClearAllExpirePersistentStorage, 删除" + num + "个过期的肖像数据");
	}

	public void ClearAllStorage()
	{

		UnityEngine.Debug.Log("ClearAllStorage， 删除所有肖像数据");
		mPersistentCollection.ClearAllData();
	}
}

}
