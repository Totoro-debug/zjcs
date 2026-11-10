// Official GameRoundCharacterItem; external data supplied at the local presentation seam.
namespace Zjcs.Official.BattleRoundUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameRoundCharacterItem : UIBase, IGameRoundCharacterItem
{
	public Image FactionBgImg;

	public GameObject MainPlayerEffect;

	public GameRoundPlayerPortrait PlayerPortrait;

	public Image MonsterIconImg;

	public Image MonsterHurtImg;

	public GameObject NpcIconGO;

	public Image NpcIconImg;

	public Animator Animator;

	public float DeadTime = 0.5f;

	private static readonly int sHurt = Animator.StringToHash("Hurt");

	private static readonly int sDead = Animator.StringToHash("Dead");

	private static readonly int sIdle = Animator.StringToHash("Idle");

	private static readonly int sActing = Animator.StringToHash("Acting");

	private Action mOnDeadCallback;

	private object mRoundRuntimeData;

	private bool mIsCurrent;

	private bool mIsWaitingPlayerInfo;

	private BattleRoundRole mGameCharacter;

	private IBattleRoundMimic mSkillAsyncFirstPlayer;

	private Coroutine mDeadCoro;

	private WaitForSeconds mDeadWaitForSeconds;

	private Action<long> mOnSkillAsyncTargetInitialAction;

	private IBattleRoundPlayerRequest mLiteInfoCallback;

	private IDisposable mOnSkillAsyncTargetInitialActionDisposable;

	private GameEffectData mGameEffectData;

	private IBattleRoundCardBindings bindings;

	public void Bind(IBattleRoundCardBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));

	public object RoundRuntimeData => mRoundRuntimeData;

	public bool IsCurrent => mIsCurrent;

	public Transform Transform { get; private set; }

	private void Awake()
	{
		if (mOnSkillAsyncTargetInitialAction == null)
		{
			mOnSkillAsyncTargetInitialAction = OnSkillAsyncTargetInitial;
		}
		Transform = ((Component)this).transform;
	}

	public void Init(object data, bool isCurrent, BattleRoundRole gameCharacterComponent)
	{
		InitWrapper(() =>
		{
			mRoundRuntimeData = data;
			mIsCurrent = isCurrent;
			mGameCharacter = gameCharacterComponent;
		});
	}

	public void Refresh(object data, bool isCurrent, BattleRoundRole gameCharacterComponent)
	{
		Init(data, isCurrent, gameCharacterComponent);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		OnGameObjectPoolClear();
		mSkillAsyncFirstPlayer = mGameCharacter.Mimic;
		if (mSkillAsyncFirstPlayer != null)
		{
			mOnSkillAsyncTargetInitialActionDisposable = mSkillAsyncFirstPlayer.Subscribe(mOnSkillAsyncTargetInitialAction);
		}
		mGameEffectData = bindings.GetFaction(mGameCharacter);
		RefreshBg();
		RefreshIcon();
	}

	protected override void OnDisableAndInited()
	{
		mOnSkillAsyncTargetInitialActionDisposable?.Dispose();
		mOnSkillAsyncTargetInitialActionDisposable = null;
		ResetData();
		base.OnDisableAndInited();
	}

	protected override void OnDestroy()
	{
		mLiteInfoCallback?.Dispose();
		mLiteInfoCallback = null;
		mOnSkillAsyncTargetInitialAction = null;
		base.OnDestroy();
	}

	private void RefreshIcon()
	{
		MainPlayerEffect.SetActive(false);
		if (mGameCharacter.NpcClassId.HasValue) { RefreshNpcIcon(mGameCharacter.NpcClassId.Value); return; }
		if (mGameCharacter.PlayerId.HasValue) { RefreshPlayerIcon(mGameCharacter.PlayerId.Value); return; }
		if (mGameCharacter.PetEntityClassId.HasValue) { RefreshMonsterIcon(mGameCharacter.PetEntityClassId.Value, isPet: true); return; }
		if (mGameCharacter.HasGuard)
		{
			if (mGameCharacter.GuardKind == BattleRoundGuardKind.Arena
				|| mGameCharacter.GuardKind == BattleRoundGuardKind.LegionFight
				|| mGameCharacter.GuardKind == BattleRoundGuardKind.MultiGroupRivalry)
			{
				BattleRoundPlayer info = mGameCharacter.GuardAppearance;
				RefreshPlayerHeadTemp(info.Sex, info.Profession, info.AppearanceData);
			}
		}
		else if (mSkillAsyncFirstPlayer != null)
		{
			if (mSkillAsyncFirstPlayer.TryGetTargetPlayerId(out var playerId)) RefreshMimicsMonsterIcon(playerId);
		}
		else RefreshMonsterIcon(mGameCharacter.MonsterId, isPet: false);
	}

	private void OnSkillAsyncTargetInitial(long component)
	{
		RefreshPlayerIcon(component);
	}

	private void RefreshNpcIcon(int npcClassId)
	{
		NpcIconGO.gameObject.SetActive(true);
		if (!bindings.TryGetNpcIcon(npcClassId, out string icon))
			Debug.LogError("[Config] can`t find npc Info, npcClassId:" + npcClassId);
		else NpcIconImg.sprite = bindings.GetSprite(icon);
	}

	private void RefreshPlayerIcon(long playerId)
	{
		if (playerId == bindings.MainPlayer.Id)
		{
			BattleRoundPlayer mainPlayer = bindings.MainPlayer;
			RefreshPlayerHead(playerId, mainPlayer.Sex, mainPlayer.Profession, mainPlayer.AppearanceData);
		}
		else
		{
			RefreshOtherPlayerSpine(playerId);
		}
	}

	private void RefreshMimicsMonsterIcon(long playerId)
	{
		RefreshPlayerIcon(playerId);
		PlayerPortrait.SetPortraitColor(bindings.MimicsMonsterColor);
	}

	private void RefreshOtherPlayerSpine(long playerId)
	{
		if (!mIsWaitingPlayerInfo)
		{
			mIsWaitingPlayerInfo = true;
			(mLiteInfoCallback ?? (mLiteInfoCallback = bindings.CreatePlayerRequest(OnPlayerInfoGot))).Request(playerId);
		}
	}

	private void OnPlayerInfoGot(BattleRoundPlayer info)
	{
		mIsWaitingPlayerInfo = false;
		RefreshPlayerHead(info.Id, info.Sex, info.Profession, info.AppearanceData);
	}

	private void RefreshPlayerHead(long playerId, Sex sex, int profession, object appearanceData)
	{
		PlayerPortrait.SetPortraitData(playerId, sex, profession, appearanceData);
		PlayerPortrait.SetPortraitFactionImg(mGameEffectData.FactionRoundImgETC2Path);
		PlayerPortrait.SetPortraitActive(active: true);
		MainPlayerEffect.SetActive(playerId == bindings.MainPlayer.Id);
	}

	private void RefreshPlayerHeadTemp(Sex sex, int profession, object appearanceData)
	{
		PlayerPortrait.SetPortraitData(sex, profession, appearanceData);
		PlayerPortrait.SetPortraitFactionImg(mGameEffectData.FactionRoundImgETC2Path);
		PlayerPortrait.SetPortraitActive(active: true);
	}

	private void RefreshMonsterIcon(int monsterId, bool isPet)
	{
		string iconName = isPet ? "ItemIcon/GameHeadIcon/Pet/G_" + monsterId : "ItemIcon/GameHeadIcon/Monster/G_" + monsterId;
		Sprite dynamicTextureSprite = bindings.GetSprite(iconName);
		MonsterIconImg.sprite = dynamicTextureSprite;
		((Component)MonsterIconImg).gameObject.SetActive(true);
		MonsterHurtImg.sprite = dynamicTextureSprite;
	}

	private void RefreshBg()
	{
		FactionBgImg.sprite = bindings.GetSprite(mGameEffectData.FactionRoundImgPath);
	}

	private void ResetData()
	{
		mRoundRuntimeData = null;
		mIsCurrent = false;
		mIsWaitingPlayerInfo = false;
		mGameCharacter = null;
		mSkillAsyncFirstPlayer = null;
		mOnDeadCallback = null;
		StopDeadDelay();
	}

	public void OnGameObjectPoolReset()
	{
		ResetData();
		ResetInitState();
	}

	public void OnGameObjectPoolClear()
	{
		PlayerPortrait.SetPortraitActive(active: false);
		((Component)MonsterIconImg).gameObject.SetActive(false);
		NpcIconGO.gameObject.SetActive(false);
	}

	public void SetHurt()
	{
		Animator.SetTrigger(sHurt);
	}

	public void SetDead(Action callback)
	{
		Animator.SetTrigger(sDead);
		mOnDeadCallback = callback;
		StopDeadDelay();
		StartDeadDelay();
	}

	public void SetIdle()
	{
		Animator.ResetTrigger(sActing);
		Animator.SetTrigger(sIdle);
	}

	public void SetActing()
	{
		Animator.ResetTrigger(sIdle);
		Animator.SetTrigger(sActing);
	}

	private void StartDeadDelay()
	{
		mDeadCoro = ((MonoBehaviour)this).StartCoroutine(DeadDelay());
	}

	private IEnumerator DeadDelay()
	{
		if (mDeadWaitForSeconds == null)
		{
			mDeadWaitForSeconds = new WaitForSeconds(DeadTime);
		}
		yield return mDeadWaitForSeconds;
		mOnDeadCallback?.Invoke();
		StopDeadDelay();
	}

	private void StopDeadDelay()
	{
		if (mDeadCoro != null)
		{
			((MonoBehaviour)this).StopCoroutine(mDeadCoro);
			mDeadCoro = null;
		}
	}

	public void UpdateRoundData(object newData)
	{
		mRoundRuntimeData = newData;
	}

	public void SetSiblingIndex(int index)
	{
		Transform.SetSiblingIndex(index);
	}
}

}
