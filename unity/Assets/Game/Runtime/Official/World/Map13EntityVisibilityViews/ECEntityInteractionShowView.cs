// Migrated original application/read-query code; see Map13 visibility-view receipt.
using System;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.World.Map13EntityRendering;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
[Serializable]
public class ECEntityInteractionShowView : ECViewBase, IEcEntityPlayerInteractionUpdateEventReceiver, IEventReceiver
{
	[Tooltip("不同物品来源情况下显示的节点， 不是我的那肯定就不用处理这些逻辑了")]
	[SerializeField]
	private EntitySourceTypeShowNode[] mEntitySourceTypeShows;

	[SerializeField]
	private GameObject mLifeSliderGo;

	[Tooltip("物品存在时间的倒计时")]
	[SerializeField]
	private SlicedFilledRender mLifeSlider;

	[Tooltip("当可以与物件交互或者是属于我的物件的时候，显示")]
	[SerializeField]
	private List<GameObject> mOwnerShowObjs;

	[Tooltip("当不可以与物件交互的时候，显示")]
	[SerializeField]
	private List<GameObject> mNotOwnerShowObjs;

	[Tooltip("当不可以与物件交互的时候，这些图片半透明")]
	[SerializeField]
	private SpriteRendererList mSpriteRenderers;

	[SerializeField]
	private Color mIsOwnerColor = Color.white;

	[SerializeField]
	private Color mIsNotOwnerColor = Color.gray;

	private Map13EntitySourceQuery mEntitySourceComponent;

	private Map13EntityInteractionQuery mEntityPlayerInteractionComponent;

	private WaitForSeconds mOneSec = new WaitForSeconds(1f);

	protected override void Load()
	{
		base.Load();
		mEntitySourceComponent = GetComponent<Map13EntitySourceQuery>();
		if (mEntitySourceComponent == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return;
		}
		mEntityPlayerInteractionComponent = GetComponent<Map13EntityInteractionQuery>();
		if (mEntityPlayerInteractionComponent == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else
		{
			mEntityPlayerInteractionComponent.OnInteractingPlayerUpdate += (IEcEntityPlayerInteractionUpdateEventReceiver)this;
			if ((Object)(object)mSpriteRenderers != (Object)null)
			{
				mSpriteRenderers.OnFadeInCompleted += RefreshInteractionShow;
			}
			RefreshSource();
			RefreshInteractionShow();
		}
	}

	protected override void Unload()
	{
		if (mEntityPlayerInteractionComponent != null)
		{
			mEntityPlayerInteractionComponent.OnInteractingPlayerUpdate -= (IEcEntityPlayerInteractionUpdateEventReceiver)this;
		}
		if ((Object)(object)mSpriteRenderers != (Object)null)
		{
			mSpriteRenderers.OnFadeInCompleted -= RefreshInteractionShow;
		}
		base.Unload();
	}

	private void RefreshSource()
	{
		bool flag = EntityOwnerIsMe() || EntityCanInteractOrInteracting();
		EcEntitySourceType entitySourceType = mEntitySourceComponent.EntitySourceType;
		EntitySourceTypeShowNode[] array = mEntitySourceTypeShows;
		foreach (EntitySourceTypeShowNode entitySourceTypeShowNode in array)
		{
			GameObject[] showNodes = entitySourceTypeShowNode.ShowNodes;
			foreach (GameObject val in showNodes)
			{
				if (!((Object)(object)val == (Object)null))
				{
					val.SetActive(flag && entitySourceType == entitySourceTypeShowNode.SourceType);
				}
			}
		}
		float duration = mEntitySourceComponent.GetDuration();
		if (flag && (Object)(object)mLifeSlider != (Object)null && duration > 0f)
		{
			mLifeSliderGo.SetActive(true);
			StartCoroutine(Cooldown(duration));
		}
		else if ((Object)(object)mLifeSliderGo != (Object)null)
		{
			mLifeSliderGo.SetActive(false);
		}
	}

	private IEnumerator Cooldown(float duration)
	{
		while (duration > 0f)
		{
			float num = Map13PresentationServices.For(mServiceProvider).CalcEndIntervalS(mEntitySourceComponent.CreateTime.Value, duration);
			if (num <= 0f)
			{
				break;
			}
			mLifeSlider.FillAmount = num / duration;
			yield return mOneSec;
		}
	}

	private void RefreshInteractionShow()
	{
		bool flag = EntityCanInteractOrInteracting();
		foreach (GameObject mNotOwnerShowObj in mNotOwnerShowObjs)
		{
			if ((Object)(object)mNotOwnerShowObj == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
			else
			{
				mNotOwnerShowObj.SetActive(!flag);
			}
		}
		foreach (GameObject mOwnerShowObj in mOwnerShowObjs)
		{
			if ((Object)(object)mOwnerShowObj == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
			else
			{
				mOwnerShowObj.SetActive(flag);
			}
		}
		SetColor(!flag);
	}

	private void SetColor(bool mask = false)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mSpriteRenderers != (Object)null)
		{
			Color color = (mask ? mIsNotOwnerColor : mIsOwnerColor);
			mSpriteRenderers.SetColor(color, mSpriteRenderers.IsIn);
		}
	}

	private bool EntityOwnerIsMe()
	{
		return mEntitySourceComponent.IsMine(EntityOwnershipType.Player, Map13PresentationServices.For(mServiceProvider).PlayerId);
	}

	private bool EntityCanInteractOrInteracting()
	{
		if (!mEntityPlayerInteractionComponent.IsPlayerInteracting(Map13PresentationServices.For(mServiceProvider).PlayerId))
		{
			return mEntityPlayerInteractionComponent.CanInteract(Map13PresentationServices.For(mServiceProvider).PlayerId);
		}
		return true;
	}

	void IEcEntityPlayerInteractionUpdateEventReceiver.EntityPlayerInteractionComponent_Update(Map13EntityInteractionQuery playerInteractionComponenet)
	{
		RefreshInteractionShow();
	}

	public ECEntityInteractionShowView()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected Obj, but got Unknown
	}
}

}
