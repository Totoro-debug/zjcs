// Original client component; see grid entity runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.BattleStatusPlayables;
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;


namespace Zjcs.Official.BattleGridItemEntities
{
[Serializable]
public class ECFightRoleCharacterView : ECViewBase, IFightGetUsePlayable
{
	[SerializeField]
	private SpriteRendererList mSpriteList;

	[SerializeField]
	private BattleRolePlayerHud mPlayerHUD;

	[SerializeField]
	private BattleRolePlayerAppearance mPlayerAppearance;

	[Tooltip("一开始就死亡的Playable。Source和Target都是自己")]
	[SerializeField]
	private GameObject mDieOnStartPlayable;

	[Tooltip("死亡Playable。Source和Target都是自己")]
	[SerializeField]
	private GameObject mDiePlayable;

	[Tooltip("在开始的时候尝试播放死亡动画")]
	[SerializeField]
	private bool mTryDiePlayableOnStart = true;

	[Tooltip("出现Playable，杀端重启不会重复播放。Source和Target都是自己")]
	[SerializeField]
	private GameObject mGeneratePlayable;

	[Tooltip("延迟时间,小于等于0表示立即显示")]
	[SerializeField]
	private float mGenerateDelayTime;

	[Tooltip("出现特效是否需要延迟播放")]
	[SerializeField]
	private bool mGeneratePlayableNeedDelay;

	[Tooltip("延迟显示的部件")]
	[SerializeField]
	private GameObject[] mDelayVisibilityParts;

	private BattleRoleCharacterViewData mCharacterComponent;

	private MyPlayable mDiePlayableHandle;

	private MyPlayable mGeneratePlayableHandle;

	private bool mInitAnimator;

	private long mDelayVisibilityCoroutine;

	private const float kTransparentAlpha = 0.001f;


	public IMyPlayableManager MyPlayableManager { get; set; }


	public IBattleGridPlayableEnvironment GamePlayManager { get; set; }


	public IBattleGridPlayableEnvironment FightRoot { get; set; }

	public UnityEngine.Object NameText
	{
		get
		{
			if ((Object)(object)mPlayerHUD == (Object)null)
			{
				return null;
			}
			return mPlayerHUD.NameTxt;
		}
	}

	protected override void Load()
	{
		base.Load();
		mCharacterComponent = GetComponent<BattleRoleCharacterViewData>();
		mInitAnimator = false;
		SetVisibilityPartsActive(active: true);
		SetTransparent();
		if ((Object)(object)mPlayerAppearance != (Object)null)
		{
			BattleRolePlayerAppearance playerAnimatorAppearance = mPlayerAppearance;
			playerAnimatorAppearance.OnAnimatorChanged = (Action)Delegate.Combine(playerAnimatorAppearance.OnAnimatorChanged, new Action(OnAnimatorInited));
		}
		InitHud();
		if (mCharacterComponent != null)
		{
			mCharacterComponent.OnDie += new Action<bool, long>(OnDie);
		}
		if ((Object)(object)mPlayerAppearance == (Object)null)
		{
			OnAnimatorInited();
		}
		if (!mIsStatic)
		{
			FadeIn();
		}
		else
		{
			SetOpaque();
		}
		IBattleRolePlayerAppearanceData component = GetComponent<IBattleRolePlayerAppearanceData>();
		if (component != null)
		{
			RefreshAppearance(component.CharacterComponent.PlayerId, component.Sex, component.CharacterComponent.Profession, component.AppearanceData);
		}
	}

	protected override void Unload()
	{
		StopDiePlayable();
		StopGeneratePlayable();
		if (mDelayVisibilityCoroutine != 0L)
		{
			StopCoroutine(mDelayVisibilityCoroutine);
			mDelayVisibilityCoroutine = 0L;
		}
		if (mCharacterComponent != null)
		{
			mCharacterComponent.OnDie -= new Action<bool, long>(OnDie);
		}
		if ((Object)(object)mPlayerAppearance != (Object)null)
		{
			BattleRolePlayerAppearance playerAnimatorAppearance = mPlayerAppearance;
			playerAnimatorAppearance.OnAnimatorChanged = (Action)Delegate.Remove(playerAnimatorAppearance.OnAnimatorChanged, new Action(OnAnimatorInited));
		}
		mInitAnimator = false;
		mCharacterComponent = null;
		base.Unload();
	}

	private void OnDie(bool die, long playerId = 0L)
	{
		UpdateDiePlayable(die, isOnStart: false);
	}

	private void UpdateDiePlayable(bool isDie, bool isOnStart)
	{
		if (mInitAnimator)
		{
			if (isDie)
			{
				PlayDiePlayable(isOnStart);
			}
			else
			{
				StopDiePlayable();
			}
		}
	}

	private void OnAnimatorInited()
	{
		mInitAnimator = true;
		if (mTryDiePlayableOnStart && mCharacterComponent != null)
		{
			UpdateDiePlayable(mCharacterComponent.Die, isOnStart: true);
		}
		if (mCharacterComponent != null && mCharacterComponent.IsNew)
		{
			PlayGeneratePlayable();
			DelayVisibilityPreset();
		}
	}

	public void ForcePlayDiePlayable()
	{
		if (mCharacterComponent != null && mCharacterComponent.Die)
		{
			StopDiePlayable();
			UpdateDiePlayable(mCharacterComponent.Die, isOnStart: false);
		}
	}

	private void DelayVisibilityPreset()
	{
		if (!(mGenerateDelayTime <= 0f))
		{
			SetVisibilityPartsActive(active: false);
			if (mDelayVisibilityCoroutine != 0L)
			{
				StopCoroutine(mDelayVisibilityCoroutine);
			}
			mDelayVisibilityCoroutine = StartCoroutine(DelayVisibilityRoutine());
		}
	}

	private IEnumerator DelayVisibilityRoutine()
	{
		yield return (object)new WaitForSeconds(mGenerateDelayTime);
		mDelayVisibilityCoroutine = 0L;
		SetVisibilityPartsActive(active: true);
		PlayDelayedGeneratePlayable();
	}

	private void SetTransparent()
	{
		if (!((Object)(object)mSpriteList == (Object)null))
		{
			mSpriteList.Alpha = 0.001f;
		}
	}

	private void SetOpaque()
	{
		if (!((Object)(object)mSpriteList == (Object)null))
		{
			mSpriteList.Alpha = 1f;
		}
	}

	private void FadeIn()
	{
		if (!((Object)(object)mSpriteList == (Object)null))
		{
			mSpriteList.FadeIn();
		}
	}

	private void InitHud()
	{
		if (!((Object)(object)mPlayerHUD == (Object)null))
		{
			IBattleRoleNpcViewData component = GetComponent<IBattleRoleNpcViewData>();
			if (component != null)
			{
				mPlayerHUD.InitByNpcClassId(component.NpcClassId);
			}
			else if (mCharacterComponent != null)
			{
				mPlayerHUD.InitByPlayerId(mCharacterComponent.Character.PlayerId);
			}
		}
	}

	private void RefreshAppearance(long playerId, int sex, int profession, object appearanceData)
	{
		if (appearanceData != null && !((Object)(object)mPlayerAppearance == (Object)null))
		{
			mPlayerAppearance.RefreshAppearance(playerId, sex, profession, appearanceData);
		}
	}

	public void SetAppearanceRuntimeDataOverride(object runtimeDataOverride)
	{
		if (!((Object)(object)mPlayerAppearance == (Object)null))
		{
			mPlayerAppearance.SetRuntimeDataOverride(runtimeDataOverride);
		}
	}

	private void PlayDiePlayable(bool isOnStart)
	{
		if ((Object)(object)mDiePlayableHandle != (Object)null)
		{
			return;
		}
		GameObject val = ((!isOnStart) ? mDiePlayable : mDieOnStartPlayable);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		MyEntity componentInParent = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		if (!((Object)(object)componentInParent == (Object)null))
		{
			GameObject gameObject = ((Component)componentInParent).gameObject;
			AnimationController animationController = default;
			if (gameObject.TryGetComponent<AnimationController>(out animationController))
			{
				animationController.ForceResumeAllPause();
			}
			mDiePlayableHandle = MyPlayableManager.Play(val, gameObject, gameObject);
		}
	}

	private void StopDiePlayable()
	{
		if (!((Object)(object)mDiePlayableHandle == (Object)null))
		{
			mDiePlayableHandle.Stop();
			mDiePlayableHandle = null;
		}
	}

	private void PlayGeneratePlayable()
	{
		if (!((Object)(object)mDiePlayableHandle != (Object)null) && !GamePlayManager.IsFastforward && !FightRoot.NotPlayGeneratePlayable && (Object)(object)mGeneratePlayable != (Object)null && !mGeneratePlayableNeedDelay)
		{
			PlayGeneratePlayableImpl();
		}
	}

	private void PlayDelayedGeneratePlayable()
	{
		if ((Object)(object)mGeneratePlayable != (Object)null && mGeneratePlayableNeedDelay)
		{
			PlayGeneratePlayableImpl();
		}
	}

	private void StopGeneratePlayable()
	{
		if (!((Object)(object)mGeneratePlayableHandle == (Object)null))
		{
			mGeneratePlayableHandle.Stop();
			mGeneratePlayableHandle = null;
		}
	}

	public void SetVisibilityPartsActive(bool active)
	{
		if (mDelayVisibilityParts == null)
		{
			return;
		}
		GameObject[] array = mDelayVisibilityParts;
		foreach (GameObject val in array)
		{
			if ((Object)(object)val != (Object)null)
			{
				val.SetActive(active);
			}
		}
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)mDieOnStartPlayable != (Object)null)
			{
				playables.Add(mDieOnStartPlayable);
			}
			if ((Object)(object)mDiePlayable != (Object)null)
			{
				playables.Add(mDiePlayable);
			}
			if ((Object)(object)mGeneratePlayable != (Object)null)
			{
				playables.Add(mGeneratePlayable);
			}
		}
	}

	private void PlayGeneratePlayableImpl()
	{
		MyEntity componentInParent = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		if (!((Object)(object)componentInParent == (Object)null))
		{
			GameObject gameObject = ((Component)componentInParent).gameObject;
			mGeneratePlayableHandle = MyPlayableManager.Play(mGeneratePlayable, gameObject, gameObject);
		}
	}
}
}
