namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class PlayerWeapon : APlayerAppearanceInfo
{
	public Component WeaponSkeleton;

	public Component WeaponSkeletonUI;

	public Transform Slot;

	public bool IsUI;

	[SerializeField]
	protected bool mFaceRight = true;

	[SerializeField]
	protected int mProfession;

	protected IProfessionWeaponSkeleton mSkeletonComponent;

	private GameObject mLoadWeapon;

	private CharacterPartPoolEntry mLoadedEntry;

	protected string mSkinName;

	public Action OnWeaponLoaded;

    private IProfessionPlayerWeaponBindings mWeaponBindings;
    private IProfessionPlayerWeaponBindings WeaponBindings => mWeaponBindings ?? ProfessionPlayerWeaponBindings.Required;
    public void ConfigureWeapon(IProfessionPlayerWeaponBindings bindings)
    { mWeaponBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	protected IProfessionWeaponSkeleton SkeletonComponent
	{
		get
		{
			if (mSkeletonComponent == null)
			{
				IProfessionWeaponSkeleton val;
				if (!((Object)(object)WeaponSkeleton != (Object)null))
				{
					IProfessionWeaponSkeleton weaponSkeletonUI = (IProfessionWeaponSkeleton)(object)WeaponSkeletonUI;
					val = weaponSkeletonUI;
				}
				else
				{
					IProfessionWeaponSkeleton weaponSkeletonUI = (IProfessionWeaponSkeleton)(object)WeaponSkeleton;
					val = weaponSkeletonUI;
				}
				mSkeletonComponent = val;
			}
			return mSkeletonComponent;
		}
	}

	public GameObject LoadWeapon => mLoadWeapon;

	protected virtual void OnDestroy()
	{
		ReleasePart();
	}

	public override void RefreshAppearance(Sex sex, int profession, AppearanceRuntimeData data)
	{
		mProfession = profession;
		try
		{
			UpdateSkin(data);
		}
		catch (Exception exception)
		{
            Debug.LogException(exception);
		}
		SetDir(mFaceRight, profession);
	}

	protected void SetDir(bool faceRight, int profession)
	{
		mFaceRight = faceRight;
		string playerWeaponAnimName = ProfessionWeaponAnimationNames.GetPlayerWeaponAnimName(WeaponBindings.Player, profession, faceRight);
		if (string.IsNullOrEmpty(playerWeaponAnimName))
		{
			string name = SkeletonComponent.SkinName;
			if (!string.IsNullOrEmpty(name))
			{
				playerWeaponAnimName = ProfessionWeaponAnimationNames.GetPlayerWeaponAnimName(WeaponBindings.Player, name.Contains(WeaponBindings.Player.GetProfessionIdStr((int)Profession.Zhanshi)) ? (int)Profession.Zhanshi : (int)Profession.Fashi, faceRight);
			}
		}
		if (!string.IsNullOrEmpty(playerWeaponAnimName) && SkeletonComponent != null)
		{
			SkeletonComponent.PlayAnimation(playerWeaponAnimName, loop: true);
		}
	}

	private void UpdateSkin(AppearanceRuntimeData data)
	{

		if (!data.SkinMap.TryGetValue(SkinType.pWeapon, out var value) || string.Equals(value, mSkinName))
		{
			return;
		}
		if ((Object)(object)WeaponSkeleton != (Object)null || (Object)(object)WeaponSkeletonUI != (Object)null)
		{
			SkeletonComponent.SetSkin(value);
			return;
		}
		ReleasePart();
		mLoadedEntry = WeaponBindings.Parts.RentStandalonePart(SkinType.pWeapon, value, Slot, IsUI, !IsUI);
		if ((Object)(object)mLoadedEntry.GoPart == (Object)null)
		{
            Debug.LogError("PlayerWeapon UpdateSkin GetStandalonePart failed mLoadedEntry.GoPart == null, skinType:" + SkinType.pWeapon + " skinName:" + value);
			return;
		}
		GameObject val = (mLoadWeapon = mLoadedEntry.GoPart);
		mSkeletonComponent = val.GetComponentInChildren<IProfessionWeaponSkeleton>(true);
		Transform transform = val.transform;
		transform.localScale = Vector3.one;
		transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		mSkinName = value;
		try
		{
			OnWeaponLoaded?.Invoke();
		}
		catch (Exception exception)
		{
            Debug.LogException(exception);
			throw;
		}
	}

	private void ReleasePart()
	{
		if (mLoadedEntry != null && mLoadedEntry.Valid)
		{
			WeaponBindings.Parts.ReturnPart(mLoadedEntry, tryUnloadAllAssets: false);
			mLoadedEntry = null;
		}
	}
}

}
