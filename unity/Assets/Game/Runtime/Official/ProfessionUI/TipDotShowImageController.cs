// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
public class TipDotShowImageController : MonoBehaviour
{
	private enum TipDotShowType
	{
		Normal,
		Count,
		LongCount
	}

	[SerializeField]
	private TipDotNodeLinker mLinker;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldStateSwitcher<TipDotShowType> mShowStates;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldMultiLanguageText mDataText;

	private void OnEnable()
	{
		mLinker.OnStateChanged += new Action<TipDotStateInfo>(OnTipDotChanged);
		OnTipDotChanged(mLinker.StateInfo);
	}

	private void OnDisable()
	{
		mLinker.OnStateChanged -= new Action<TipDotStateInfo>(OnTipDotChanged);
	}

	private void OnTipDotChanged(TipDotStateInfo info)
	{
		if (!mLinker.IsShowData || string.IsNullOrEmpty(info.Data))
		{
			mShowStates.SetState(TipDotShowType.Normal);
			return;
		}
		string text = TryHandleOutMaxNum(info.Data);
		mShowStates.SetState((text.Length < 3) ? TipDotShowType.Count : TipDotShowType.LongCount);
		mDataText.SetKey(text);
	}

	private string TryHandleOutMaxNum(string data)
	{
		string text = data;
		if (int.TryParse(text, out var result) && result > 99)
		{
			text = "99+";
		}
		return text;
	}
}
}
