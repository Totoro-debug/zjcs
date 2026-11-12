
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
public class ElementConfrontationUI : MonoBehaviour
{
	[SerializeField]
	private SpriteRenderer mEffectImg;

	[SerializeField]
	private SpriteRenderer mEffectUIMesh;

	[SerializeField]
	private SpriteRenderer mEffectMeshRender;

	[SerializeField]
	private GameObject mBgGo;

	[SerializeField]
	private MultiLanguageText mDescribeText;

	[SerializeField]
	private Animator mAnimator;

	[SerializeField]
	private string mElementDefenseKey = "";

	[SerializeField]
	private string mElementConfrontWeak = "";

	[SerializeField]
	private string mElementConfrontStrength = "";

	[SerializeField]
	private string mElementConfrontImmunity = "";

	private const string mDiKanTrigger = "DiKangTrigger";

	private const string mDaKeTrigger = "DaKeTrigger";

	private const string mKeZhiTrigger = "KeZhiTrigger";

	private string mSetTriggerName = "";

	public float ElementConfrontThreshold { get; set; }

	public void Reset()
	{
		mDescribeText.SetKey("");
		SetUIShowState(active: false);
	}

	private void SetUIShowState(bool active)
	{
		((Component)mDescribeText).gameObject.SetActive(active);
		mBgGo.SetActive(active);
		((Renderer)mEffectMeshRender).enabled = active;
		((Renderer)mEffectImg).enabled = active;
		((Renderer)mEffectUIMesh).enabled = active;
	}

	public void SetContent(float confrontVal, Color color)
	{

		if (Mathf.Approximately(confrontVal, 0f))
		{
			mDescribeText.SetKey("");
			return;
		}
		SetUIShowState(active: true);
		mDescribeText.Color = color.linear;
		if (confrontVal > 0f)
		{
			mSetTriggerName = "DiKangTrigger";
			if (Mathf.Approximately(confrontVal, 1f))
			{
				mDescribeText.SetKey(mElementConfrontImmunity);
			}
			else
			{
				mDescribeText.SetKey(mElementDefenseKey);
			}
		}
		else if (Mathf.Abs(confrontVal) < ElementConfrontThreshold)
		{
			mDescribeText.SetKey(mElementConfrontWeak);
			mSetTriggerName = "KeZhiTrigger";
		}
		else
		{
			mDescribeText.SetKey(mElementConfrontWeak);
			mSetTriggerName = "KeZhiTrigger";
		}
	}

	private void SetElementConfrontStrength()
	{
		mDescribeText.SetKey(mElementConfrontStrength);
		mSetTriggerName = "DaKeTrigger";
	}

	private void OnEnable()
	{
		if (!string.IsNullOrEmpty(mSetTriggerName))
		{
			mAnimator.SetTrigger(mSetTriggerName);
		}
	}
}
}
