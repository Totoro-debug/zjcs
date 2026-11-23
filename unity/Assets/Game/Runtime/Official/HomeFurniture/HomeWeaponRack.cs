// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeWeaponRack : MonoBehaviour
{
	public Transform PSlot;

	public Transform SSlot;

	private int mPWeaponId;

	private int mSWeaponId;

	private readonly List<BaseSkeletonFollower> mSkeletonFollowers = new List<BaseSkeletonFollower>();

	private readonly HashSet<int> mEffectiveSkinIds = new HashSet<int>();

	private readonly HashSet<SkinType> mMutexLostTypes = new HashSet<SkinType>();

	private readonly HomeFurnitureLifetime mDisposableBag = new HomeFurnitureLifetime();
    public IHomeWeaponRackServices Services { get; set; }

	private void Awake()
	{
		mDisposableBag.Add(Services.SubscribeHomeChange(OnHomeChange));
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	private void OnEnable()
	{
		if (Services.MainPlayer != null)
		{
			Services.MainPlayer.OnAppearanceUpdate += OnAppearanceUpdate;
			TryRefreshWeapon();
		}
	}

	private void OnDisable()
	{
		if (Services.MainPlayer != null)
		{
			Services.MainPlayer.OnAppearanceUpdate -= OnAppearanceUpdate;
		}
	}

	private void OnHomeChange()
	{
		TryRefreshWeapon();
	}

	private void OnAppearanceUpdate(AppearanceOperateSource operateSource)
	{
		TryRefreshWeapon();
	}

	private void TryRefreshWeapon()
	{
		IProfessionPlayerAppearanceSource playerData = Services.PlayerData;
		AppearanceData appearanceData = (AppearanceData)playerData.AppearanceData;
		Services.Appearance.ResolveAppendAppearanceMutex(appearanceData, playerData.Sex, mEffectiveSkinIds, mMutexLostTypes);
		if (Services.Appearance.TryGetFinalSkinId(appearanceData, SkinType.pWeapon, playerData.Sex, (Profession)playerData.Profession, mEffectiveSkinIds, mMutexLostTypes, out var skinId) && skinId != mPWeaponId)
		{
			mPWeaponId = skinId;
			ApplyWeapon(skinId, PSlot);
		}
		if (Services.Appearance.TryGetFinalSkinId(appearanceData, SkinType.sWeapon, playerData.Sex, (Profession)playerData.Profession, mEffectiveSkinIds, mMutexLostTypes, out var skinId2) && skinId2 != mSWeaponId)
		{
			mSWeaponId = skinId2;
			ApplyWeapon(skinId2, SSlot);
		}
	}

	private void ApplyWeapon(int id, Transform slot)
	{
		for (int index = slot.childCount - 1; index >= 0; index--) Object.Destroy(slot.GetChild(index).gameObject);
		if (id == 0)
		{
			return;
		}
		SpineSkinInfo spineSkinInfo = Services.Tables.SpineSkinInfoParser.Get(id);
		if (string.Equals(spineSkinInfo.SkinName, "emptySkin"))
		{
			return;
		}
		string playerPartPrefabRenderer = ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(PlayerPartMode.Home, spineSkinInfo.SkinName);
		GameObject result = mDisposableBag.Add(Services.LoadPrefab(playerPartPrefabRenderer)).Result;
		Services.Instantiate(result, slot, false).GetComponentsInChildren<BaseSkeletonFollower>(mSkeletonFollowers);
		foreach (BaseSkeletonFollower mSkeletonFollower in mSkeletonFollowers)
		{
			Transform transform = ((Component)mSkeletonFollower).transform;
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;
		}
	}
}

}
