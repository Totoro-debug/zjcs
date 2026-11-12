using System.Collections;
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
public class CharacterDamageUI : MonoBehaviour, IGameObjectPoolItem
{
	[SerializeField]
	private EffectUIType mType;

	[SerializeField]
	private DamageNumberUI mNumberUi;

	[SerializeField]
	private MultiLanguageText mTipUi;

	[SerializeField]
	private ElementConfrontationUI mElementConfrontationUI;

	[SerializeField]
	private GameObject mBreakResilienceGo;

	[SerializeField]
	private MultiLanguageText mTrueDamageText;

	public SpriteRenderer DamageTypeImage;

	public AttachToObjUI myAttach;

	[SerializeField]
	private SpriteRenderer mAddImage;

	[SerializeField]
	private SpriteRenderer mBgImage;

	[SerializeField]
	private GameObject mContentGo;

	[SerializeField]
	private MeshRenderer mEffectSeq;

	[SerializeField]
	private float mShowTime;

	[SerializeField]
	private float mHeight;

	[SerializeField]
	private Vector3 mSceneOffset;

	private Action<CharacterDamageUI> mOnShowTimeCompletion;

	private Coroutine mShowTimeCompleteCoroutine;

	private WaitForSeconds mWaitForSeconds;

	private Transform mTransformCache;

	private Transform mAttachObjectTrans;

	private EcEntityIdentifier mEntityId;

	public float Height => mHeight;

	public EffectUIType Type => mType;

	public EcEntityIdentifier EntityId => mEntityId;

	private void Awake()
	{


		mTransformCache = ((Component)this).GetComponent<Transform>();
		mWaitForSeconds = new WaitForSeconds(mShowTime);
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
		ClearData();
	}

	public void Init(MyEntity entity, long damageValue, DamageColor damageColor, Action<CharacterDamageUI> onShowTimeCompletion, bool isBreakResilience, bool isElementAffect, float elementConfrontVal = 0f, bool trueDamage = false)
	{




		if ((Object)(object)entity == (Object)null)
		{
			return;
		}
		Init(entity, onShowTimeCompletion);
		UpdateView(damageValue, damageColor);
		if (!RefreshBreakResilience(isBreakResilience))
		{
			Color textColor = damageColor.TextColor;
			if (!RefreshElementConfrontation(isElementAffect, elementConfrontVal, textColor))
			{
				RefreshTrueDamage(trueDamage, textColor);
			}
		}
	}

	public void Init(MyEntity entity, Action<CharacterDamageUI> onShowTimeCompletion)
	{
		if (!((Object)(object)entity == (Object)null))
		{
			mEntityId = entity.Id;
			mOnShowTimeCompletion = onShowTimeCompletion;
			UpdateAttach(((Component)entity).gameObject);
			SetGameObjectActive(active: true);
			StopmShowTimeCompleteCoroutine();
			mShowTimeCompleteCoroutine = ((MonoBehaviour)this).StartCoroutine(DoShowTimeComplete());
		}
	}

	public void UpdateText(string key)
	{
		mTipUi.SetKey(key);
	}

	private bool RefreshBreakResilience(bool isBreakResilience)
	{
		if ((Object)(object)mBreakResilienceGo != (Object)null)
		{
			mBreakResilienceGo.SetActive(isBreakResilience);
			return isBreakResilience;
		}
		return false;
	}

	private bool RefreshTrueDamage(bool isTrueDamage, Color color)
	{

		if ((Object)(object)mTrueDamageText != (Object)null)
		{
			((Component)mTrueDamageText).gameObject.SetActive(isTrueDamage);
			mTrueDamageText.Color = color.linear;
			return isTrueDamage;
		}
		return false;
	}

	private void ProcessElementImmunity()
	{
		SetGameObjectActive(active: false);
	}

	private void SetGameObjectActive(bool active)
	{
		if (mNumberUi)
		{
			((Component)mNumberUi).gameObject.SetActive(active);
		}
		if (DamageTypeImage)
		{
			((Component)DamageTypeImage).gameObject.SetActive(active);
		}
		if (mAddImage)
		{
			((Renderer)mAddImage).enabled = active;
		}
		if (mEffectSeq)
		{
			((Renderer)mEffectSeq).enabled = active;
		}
		if (mBgImage)
		{
			((Component)mBgImage).gameObject.SetActive(active);
		}
	}

	private void StopmShowTimeCompleteCoroutine()
	{
		if (mShowTimeCompleteCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mShowTimeCompleteCoroutine);
			mShowTimeCompleteCoroutine = null;
		}
	}

	private IEnumerator DoShowTimeComplete()
	{
		yield return mWaitForSeconds;
		mOnShowTimeCompletion?.Invoke(this);
	}

	private void OnDisable()
	{
		ClearData();
		StopmShowTimeCompleteCoroutine();
	}

	private void ClearData()
	{
		mEntityId = EcEntityIdentifier.sDefault;
		mAttachObjectTrans = null;
		if (mBreakResilienceGo)
		{
			mBreakResilienceGo.SetActive(false);
		}
	}

	public void UpdateOffset(Vector3 offset)
	{








		mTransformCache.position = mAttachObjectTrans.position + offset + mSceneOffset;
		Vector3 localPosition = mTransformCache.localPosition;
		localPosition.z = 0f;
		mTransformCache.localPosition = localPosition;
	}

	private void UpdateView(long damageValue, DamageColor color)
	{
		UpdateText(damageValue, color);
		UpdateImage(color);
	}

	private void UpdateAttach(GameObject attachObj)
	{

		mAttachObjectTrans = attachObj.transform;
		UpdateOffset(Vector3.zero);
	}

	private void UpdateText(long damageValue, DamageColor color)
	{






		Color color2 = color.TextColor;
		if (mType == EffectUIType.AbyssNormalDamageUI || mType == EffectUIType.AbyssCritDamageUI || mType == EffectUIType.AbyssBlockDamageUI)
		{
			color2 = Color.white;
		}
		if (IsShowTipType())
		{
			if (mTipUi)
			{
				mTipUi.Color = color2.linear;
			}
		}
		else
		{
			string battleNumberString = Zjcs.Official.World.NativeLabelUI.Framework.NumberFormatHelper.FormatBattleByFourWidth(Math.Abs(damageValue), new string[] { "", "万", "亿", "兆", "京" });
			((Component)mNumberUi).gameObject.SetActive(true);
			mNumberUi.SetContent(battleNumberString, color2);
		}
	}

	private bool IsShowTipType()
	{
		if (Type != EffectUIType.ImmunityDamageUI && Type != EffectUIType.DodgeDamageUI)
		{
			return Type == EffectUIType.BlindingDamageUI;
		}
		return true;
	}

	private bool RefreshElementConfrontation(bool isElementAffect, float elementConfrontVal, Color color)
	{

		if (!mElementConfrontationUI || IsShowTipType())
		{
			return false;
		}
		mElementConfrontationUI.Reset();
		if (!isElementAffect)
		{
			return false;
		}
		mElementConfrontationUI.SetContent(elementConfrontVal, color);
		if (Mathf.Approximately(elementConfrontVal, 1f))
		{
			ProcessElementImmunity();
		}
		return true;
	}

	private void UpdateImage(DamageColor iconColor)
	{



		if ((Object)(object)DamageTypeImage != (Object)null)
		{
			DamageTypeImage.color = iconColor.IconImageColor;
		}
		if ((Object)(object)mAddImage != (Object)null)
		{
			mAddImage.color = iconColor.AddImageColor;
		}
		if (mBgImage)
		{
			mBgImage.color = iconColor.BgImageColor;
		}
	}
}
}
