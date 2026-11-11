// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;


[Serializable]
public class HomeFadeList
{
	public List<GameObject> ActiveList;

	public List<SpriteRenderer> FadeSpriteList;

	public List<SkeletonRenderer> FadeSpineList;

	public float FadeDuration;

	private float[] mFadeSpriteDefaultAlpha;

	private float mAlpha = 1f;

	private HomeFadeCancellationTokenDisposable mHomeFadeCancellationTokenDisposable;

	private float Alpha
	{
		get
		{
			return mAlpha;
		}
		set
		{
			if (mAlpha == 0f && value > 0f)
			{
				SetSpriteActive(active: true);
				SetSpineActive(active: true);
			}
			else if (mAlpha > 0f && value == 0f)
			{
				SetSpriteActive(active: true);
				SetSpineActive(active: true);
			}
			SetSpriteAlpha(value);
			SetSpineAlpha(value);
			mAlpha = value;
		}
	}

	public void Init()
	{
		List<SpriteRenderer> fadeSpriteList = FadeSpriteList;
		if (fadeSpriteList != null && fadeSpriteList.Count > 0)
		{
			mFadeSpriteDefaultAlpha = new float[FadeSpriteList.Count];
			int i = 0;
			for (int count = FadeSpriteList.Count; i < count; i++)
			{
				mFadeSpriteDefaultAlpha[i] = FadeSpriteList[i].color.a;
			}
		}
	}

	public void SetActiveStatus(bool isActive)
	{
		if (ActiveList != null)
		{
			foreach (GameObject active in ActiveList)
			{
				active.SetActive(isActive);
			}
		}
		ReleaseCancellationTokenSource();
		mHomeFadeCancellationTokenDisposable = new HomeFadeCancellationTokenDisposable();
		float alpha = (isActive ? 1 : 0);
		Fade(mHomeFadeCancellationTokenDisposable.Token, alpha).Forget();
	}

	public void SetActiveStatusImmediately(bool isActive)
	{
		if (ActiveList != null)
		{
			foreach (GameObject active in ActiveList)
			{
				active.SetActive(isActive);
			}
		}
		Alpha = (isActive ? 1 : 0);
	}

	public void ReleaseCancellationTokenSource()
	{
		mHomeFadeCancellationTokenDisposable?.Dispose();
		mHomeFadeCancellationTokenDisposable = null;
	}

	private async UniTaskVoid Fade(CancellationToken cancellationToken, float alpha)
	{
		float time = 0f;
		while (time < FadeDuration)
		{
			time += Time.deltaTime;
			Alpha = Mathf.Lerp(Alpha, alpha, time / FadeDuration);
			await UniTask.Yield(cancellationToken);
		}
		Alpha = alpha;
	}

	private void SetSpriteAlpha(float alpha)
	{
		if (FadeSpriteList != null)
		{
			for (int i = 0; i < FadeSpriteList.Count; i++)
			{
				SpriteRenderer spriteRenderer = FadeSpriteList[i];
				Color color = spriteRenderer.color;
				color.a = alpha * mFadeSpriteDefaultAlpha[i];
				spriteRenderer.color = color;
			}
		}
	}

	private void SetSpriteActive(bool active)
	{
		if (FadeSpriteList == null)
		{
			return;
		}
		foreach (SpriteRenderer fadeSprite in FadeSpriteList)
		{
			fadeSprite.gameObject.SetActive(active);
		}
	}

	private void SetSpineAlpha(float alpha)
	{
		if (FadeSpineList == null)
		{
			return;
		}
		foreach (SkeletonRenderer fadeSpine in FadeSpineList)
		{
			fadeSpine.Skeleton.A = alpha;
		}
	}

	private void SetSpineActive(bool active)
	{
		if (FadeSpineList == null)
		{
			return;
		}
		foreach (SkeletonRenderer fadeSpine in FadeSpineList)
		{
			fadeSpine.gameObject.SetActive(active);
		}
	}
}
}
