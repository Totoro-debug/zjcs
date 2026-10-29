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
public class MiniMapMarkFilterItem : MonoBehaviour, IGameObjectPoolItem
{
	[SerializeField]
	private Image mIcon;

	[SerializeField]
	private TabItem mTabItem;

	[SerializeField]
	private MultiTextsControler mTextsControler;

	private Action<string, bool> mOnValueChangedHook;

	public bool Selected
	{
		get
		{
			return mTabItem.isOn;
		}
		set
		{
			mTabItem.SetIsOnWithoutNotify(value);
		}
	}

	public string MarkName { get; private set; }

	private void Awake()
	{
		((UnityEvent<bool>)mTabItem.OnValueChanged).AddListener((UnityAction<bool>)OnValueChanged);
	}

	private void OnDestroy()
	{
		((UnityEvent<bool>)mTabItem.OnValueChanged).RemoveListener((UnityAction<bool>)OnValueChanged);
	}

	public void Init(string name, Sprite icon, string des, Action<string, bool> onValueChangedCallback)
	{
		mIcon.sprite = icon;
		mTextsControler.SetMultiTextKey(des);
		MarkName = name;
		mOnValueChangedHook = onValueChangedCallback;
	}

	private void OnValueChanged(bool isOn)
	{
		mOnValueChangedHook?.Invoke(MarkName, isOn);
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
		mOnValueChangedHook = null;
		MarkName = null;
		mIcon.sprite = null;
		mTextsControler.SetMultiTextKey(null);
	}
}

}
