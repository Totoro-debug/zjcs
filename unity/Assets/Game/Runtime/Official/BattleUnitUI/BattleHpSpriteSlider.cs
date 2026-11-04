// Selected original application; live battle, spectator and effect pool operations use explicit bindings.
namespace Zjcs.Official.BattleUnitUI
{
using Zjcs.Official.World.Map13EntityRendering;
using Object = UnityEngine.Object;
using UnityEngine;

public class BattleHpSpriteSlider : MonoBehaviour
{
	public SlicedFilledRender HpSlider;

	public SlicedFilledRender HpAddSlider;

	public SlicedFilledRender HpSubSlider;

	public HpSliderEffect myEffectSlider;

	public Transform FillPointEffectSlot;

	public Transform OutLineEffectSlot;

	private GameObject mFillPointEffect;

	private GameObject mOutLineEffect;

	private IBattleHpSliderBindings mBattle;

	private IBattleHpSliderBindings mBattleHpAndShieldHandle;

	private GameEffectData mData;


	public void Init(IBattleHpSliderBindings battle)
	{
		mBattle = battle;
	}

	public void OnStart()
	{
		if ((Object)(object)myEffectSlider != (Object)null)
		{
			myEffectSlider.Init(mBattle.Hp, mBattle.LastMaxHp);
			OnHpOrShieldChange(mBattle.LastMaxHp, mBattle.Hp, 0L, 0L);
		}
		mBattleHpAndShieldHandle = mBattle;
		mBattleHpAndShieldHandle.OnHpOrShieldChange += OnHpOrShieldChange;
		mBattleHpAndShieldHandle.OnEnable();
		TryAddSpactatedPlayerIdEvent();
	}

	public void OnEnd()
	{
		if (mBattleHpAndShieldHandle != null)
		{
			mBattleHpAndShieldHandle.OnHpOrShieldChange -= OnHpOrShieldChange;
			mBattleHpAndShieldHandle.OnDisable();
			mBattleHpAndShieldHandle = null;
		}
		TryRemoveSpactatedPlayerIdEvent();
		ReturnSliderEffect();
	}

	private void TryAddSpactatedPlayerIdEvent()
	{
		if (mBattle.WatchesPlayer)
		{
			mBattle.OnSpactatedPlayerId += OnSpactatedPlayerId;
		}
	}

	private void TryRemoveSpactatedPlayerIdEvent()
	{
		if (mBattle != null)
		{
			mBattle.OnSpactatedPlayerId -= OnSpactatedPlayerId;
		}
	}

	private void OnSpactatedPlayerId(long playerId)
	{
		UpdateSlider(mBattle.GetData());
	}

	private void OnHpOrShieldChange(long maxHp, long curHp, long maxShield, long curShield)
	{
		if ((Object)(object)myEffectSlider != (Object)null)
		{
			UpdateSlider(mBattle.GetData());
			myEffectSlider.Refresh(curHp, 0f, maxHp, maxShield, curShield);
		}
	}

	private void UpdateSlider(GameEffectData data)
	{



		mData = data;
		HpSlider.Color = data.HpColor;
		HpAddSlider.Color = data.HpAddColor;
		HpSubSlider.Color = data.HpSubColor;
		ReturnSliderEffect();
		mFillPointEffect = mBattle.Borrow(data.FillPointEffect, FillPointEffectSlot);
		mOutLineEffect = mBattle.Borrow(data.OutLineEffect, OutLineEffectSlot);
		if ((Object)(object)mFillPointEffect != (Object)null)
		{
			myEffectSlider.HpAddEffectTopAnim = mFillPointEffect.GetComponent<Animator>();
		}
	}

	private void ReturnSliderEffect()
	{
		if ((Object)(object)mFillPointEffect != (Object)null)
		{
			mBattle.Return(mFillPointEffect, mData.FillPointEffect);
		}
		if ((Object)(object)mOutLineEffect != (Object)null)
		{
			mBattle.Return(mOutLineEffect, mData.OutLineEffect);
		}
		mFillPointEffect = null;
		mOutLineEffect = null;
	}
}

}
