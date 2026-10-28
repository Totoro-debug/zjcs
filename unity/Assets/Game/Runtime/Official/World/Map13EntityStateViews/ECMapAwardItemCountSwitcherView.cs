// Migrated source application/query code; see map13-entity-state-views receipt.
using Object = UnityEngine.Object;
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using WorldStageComponent = Zjcs.Official.World.Map13EntityStateViews.Map13WorldStageQuery;
using StageResultAwardItemComponent = Zjcs.Official.World.Map13EntityStateViews.Map13AwardItemQuery;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityStateViews
{
[Serializable]
public class ECMapAwardItemCountSwitcherView : ECViewBase
{
	[Serializable]
	private class ItemCountRangeConfig
	{
		public int StartCount;

		public int EndCount;

		public List<GameObject> ShowGameObjects;

		public void SetActive(bool isActive)
		{
			foreach (GameObject showGameObject in ShowGameObjects)
			{
				showGameObject.SetActive(isActive);
			}
		}

		public bool IsRange(long count)
		{
			if (StartCount <= count)
			{
				return EndCount >= count;
			}
			return false;
		}
	}

	[SerializeField]
	private List<GameObject> mItemGos;

	[SerializeField]
	private List<ItemCountRangeConfig> mItemCountRangeConfigs;

	[SerializeField]
	private SpriteRendererList mSpriteRendererList;

	private WorldStageComponent mWorldStageComponent;

	private StageResultAwardItemComponent mStageResultAwardItemComponent;

	private long mCount;

	protected override void Load()
	{
		base.Load();
		mWorldStageComponent = GetComponent<WorldStageComponent>();
		mStageResultAwardItemComponent = GetComponent<StageResultAwardItemComponent>();
		mSpriteRendererList.OnFadeInCompleted += OnSpriteRendererListFadeInCompleted;
		mSpriteRendererList.OnFadeInStart += OnSpriteRendererListFadeInStart;
		mSpriteRendererList.OnFadeOutStart += OnSpriteRendererListFadeInStart;
		OnSpriteRendererListFadeInCompleted();
	}

	protected override void Unload()
	{
		mSpriteRendererList.OnFadeInCompleted -= OnSpriteRendererListFadeInCompleted;
		mSpriteRendererList.OnFadeInStart -= OnSpriteRendererListFadeInStart;
		mSpriteRendererList.OnFadeOutStart -= OnSpriteRendererListFadeInStart;
		base.Unload();
	}

	private void OnSpriteRendererListFadeInStart()
	{
		UpdateItemCount(0L);
	}

	private void OnSpriteRendererListFadeInCompleted()
	{
		int stateComponentIndex = mWorldStageComponent.GetStateComponentIndex(StageComponentType.ResultAwardItem);
		if (stateComponentIndex < 0)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else
		{
			mCount = 0L;
			if (mStageResultAwardItemComponent.TryGetAwardItemInfo(stateComponentIndex, out var result))
			{
				mCount = result.Count;
			}
			UpdateItemCount(mCount);
		}
	}

	private void UpdateItemCount(long itemCount)
	{
		int count = mItemGos.Count;
		for (int i = 0; i < count; i++)
		{
			mItemGos[i].SetActive(i < itemCount);
		}
		count = mItemCountRangeConfigs.Count;
		ItemCountRangeConfig itemCountRangeConfig = null;
		for (int j = 0; j < count; j++)
		{
			if (mItemCountRangeConfigs[j].IsRange(itemCount))
			{
				itemCountRangeConfig = mItemCountRangeConfigs[j];
			}
			mItemCountRangeConfigs[j].SetActive(isActive: false);
		}
		itemCountRangeConfig?.SetActive(isActive: true);
	}
}

}
