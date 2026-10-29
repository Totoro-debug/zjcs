using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
public class BaseSortGroupItem<T> : MonoBehaviour where T : Enum
{
	public T SortType;

	public bool IsReverse;

	public StateSwitcher<bool> ReverseStateSwitch;

	public Button ReverseBtn;

	private Action<bool> mReverseChangeNotifyFunc;

	private void Awake()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected Obj, but got Unknown
		if ((Object)(object)ReverseBtn != (Object)null)
		{
			((UnityEvent)ReverseBtn.onClick).AddListener((UnityAction)(() =>
			{
				SetIsReverseWithNotify(!IsReverse);
			}));
		}
	}

	public void SetIsReverseWithNotify(bool value)
	{
		IsReverse = value;
		ReverseStateSwitch.SetState(IsReverse);
		if (mReverseChangeNotifyFunc != null)
		{
			mReverseChangeNotifyFunc(IsReverse);
		}
	}

	public void SetIsReverse(bool value)
	{
		IsReverse = value;
		ReverseStateSwitch.SetState(IsReverse);
	}

	public void SetReverseChangeNotifyFunc(Action<bool> func)
	{
		mReverseChangeNotifyFunc = func;
	}
}

}
