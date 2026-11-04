// Migrated from the selected 884 client application; not animation SDK code.
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.Map13EntityRendering;

namespace Zjcs.Official.BattleUnitUI
{
public class HpSliderEffect : MonoBehaviour
{
	public SlicedFilledRender ShieldSlider;

	[Header("显示用血条")]
	public SlicedFilledRender HpSlider;

	public SlicedFilledRender HpAddSlider;

	public SlicedFilledRender HpSubSlider;

	public float MinValue;

	public float MaxValue = 1f;

	[Header("特效部分")]
	public float OnceUpdateDurationS = 0.3f;

	public GameObject OutLineEffect;

	public Transform HpAddEffectTsf;

	public Animator HpAddEffectTopAnim;

	public string HpAddEffectTopAnimPlayKey;

	private int mHpAddEffectTopAnimKeyHash;

	private float mMyValue;

	private float myMaxSizeX;

	private float mCurrentValue;

	private CancellationTokenSource mCancellationToken;

	public float Value
	{
		get
		{
			return mMyValue;
		}
		set
		{
			value = Mathf.Clamp(value, MinValue, MaxValue);
			float preHp = mMyValue;
			mMyValue = value;
			Play(preHp);
		}
	}

	protected virtual void Awake()
	{

		mHpAddEffectTopAnimKeyHash = Animator.StringToHash(HpAddEffectTopAnimPlayKey);
		myMaxSizeX = HpSlider.Size.x;
		OutLineEffect.SetActive(false);
		HpSlider.FillAmount = 0f;
		HpAddSlider.FillAmount = 0f;
		HpSubSlider.FillAmount = 0f;
		ShieldSlider.FillAmount = 0f;
	}

	private void OnDestroy()
	{
		StopTask();
	}

	public void Init(float hp)
	{
		mMyValue = hp;
		HpSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
		HpAddSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
		HpSubSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
	}

	public void Init(float hp, float maxValue, float minValue = 0f)
	{
		mMyValue = hp;
		MinValue = minValue;
		MaxValue = maxValue;
		HpSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
		HpAddSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
		HpSubSlider.FillAmount = GetSizeX(mMyValue, MaxValue);
	}

	public void Refresh(float hp, float minValue, float maxValue, long maxShield = 0L, long curShield = 0L)
	{
		MinValue = minValue;
		MaxValue = maxValue;
		Value = hp;
		ShieldSlider.FillAmount = GetSizeX(curShield, maxShield);
	}

	protected virtual bool Play(float preHp)
	{

		float hpFrom;
		SlicedFilledRender slicedFilledRender;
		if (mCancellationToken == null)
		{
			hpFrom = preHp;
			slicedFilledRender = SetSlider(preHp);
		}
		else
		{
			StopTask();
			hpFrom = mCurrentValue;
			slicedFilledRender = SetSlider(mCurrentValue);
		}
		if ((Object)(object)slicedFilledRender == (Object)null)
		{
			return false;
		}
		mCancellationToken = new CancellationTokenSource();
		DoUpdateHpTask(slicedFilledRender, hpFrom, mCancellationToken.Token).Forget();
		return true;
	}

	private SlicedFilledRender SetSlider(float preHp)
	{
		if (preHp > Value)
		{
			HpSlider.FillAmount = GetSizeX(Value);
			HpSubSlider.FillAmount = GetSizeX(preHp);
			HpAddSlider.FillAmount = 0f;
			StopEffect();
			return HpSubSlider;
		}
		if (preHp < Value)
		{
			HpSlider.FillAmount = GetSizeX(preHp);
			HpAddSlider.FillAmount = GetSizeX(Value);
			HpSubSlider.FillAmount = 0f;
			PlayEffect();
			return HpSlider;
		}
		Stop();
		return null;
	}

		// Restore the original application async method from its compiled state
		// machine, retaining UniTask.NextFrame and its cancellation semantics.
		protected virtual async UniTask DoUpdateHpTask(SlicedFilledRender slider, float hpFrom, CancellationToken token)
	{
		mCurrentValue = hpFrom;
		float changeHp = Value - mCurrentValue;
		float passTime = 0f;
		while (passTime <= OnceUpdateDurationS)
		{
			mCurrentValue = hpFrom + changeHp * passTime / OnceUpdateDurationS;
			if (mCurrentValue < Value) mCurrentValue = Mathf.Clamp(mCurrentValue, mCurrentValue, Value);
			else mCurrentValue = Mathf.Clamp(mCurrentValue, Value, mCurrentValue);
			float sizeX = GetSizeX(mCurrentValue);
			slider.FillAmount = sizeX;
			if (changeHp > 0f) EffectFollow(sizeX * myMaxSizeX - HpAddEffectTsf.parent.localPosition.x);
				await UniTask.NextFrame(token, false);
			passTime += Time.deltaTime;
		}
		Stop();
	}

	protected virtual void Stop()
	{
		HpAddSlider.FillAmount = 0f;
		HpSubSlider.FillAmount = 0f;
		HpSlider.FillAmount = GetSizeX(Value);
		StopTask();
		StopEffect();
	}

	private void StopTask()
	{
		if (mCancellationToken != null)
		{
			mCancellationToken.Cancel();
			mCancellationToken.Dispose();
			mCancellationToken = null;
		}
	}

	private void PlayEffect()
	{
		if (!((Object)(object)OutLineEffect == (Object)null))
		{
			OutLineEffect.SetActive(true);
			if ((Object)(object)HpAddEffectTopAnim != (Object)null)
			{
				HpAddEffectTopAnim.SetBool(mHpAddEffectTopAnimKeyHash, true);
			}
		}
	}

	private void StopEffect()
	{
		if ((Object)(object)OutLineEffect != (Object)null)
		{
			OutLineEffect.SetActive(false);
		}
		if ((Object)(object)HpAddEffectTopAnim != (Object)null)
		{
			HpAddEffectTopAnim.SetBool(mHpAddEffectTopAnimKeyHash, false);
		}
	}

	private void EffectFollow(float posX)
	{



		HpAddEffectTsf.localPosition = new Vector3(posX, HpAddEffectTsf.localPosition.y, HpAddEffectTsf.localPosition.z);
	}

	private float GetSizeX(float hp)
	{
		return GetSizeX(hp, MaxValue, MinValue);
	}

	private float GetSizeX(float curValue, float maxValue, float minValue = 0f)
	{
		if (maxValue - minValue <= 0f)
		{
			return 0f;
		}
		return (curValue - minValue) / (maxValue - minValue);
	}
}
}
