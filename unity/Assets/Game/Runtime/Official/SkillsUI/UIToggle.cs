// Original framework-firstpass-source/UIToggle.cs; logging and local UI service bindings adapted.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
[RequireComponent(typeof(Toggle))]
public class UIToggle : MonoBehaviour
{
	private Toggle m_Toggle;

	private UIToggleGroup mGroup;

	protected List<UIToggleStatusBase> mUIToggleStatusList = new List<UIToggleStatusBase>();


	public Toggle Toggle
	{
		get
		{
			if (!((UnityEngine.Object)(object)m_Toggle == (UnityEngine.Object)null))
			{
				return m_Toggle;
			}
			return m_Toggle = ((Component)this).GetComponent<Toggle>();
		}
	}

	public UIToggleGroup Group
	{
		get
		{
			return mGroup;
		}
		set
		{
			mGroup = value;
			if ((UnityEngine.Object)(object)mGroup != (UnityEngine.Object)null)
			{
				Toggle.group = mGroup.myGroup;
			}
			else
			{
				Toggle.group = null;
			}
		}
	}

	public bool isOn
	{
		get
		{
			return Toggle.isOn;
		}
		set
		{
			Toggle.isOn = value;
		}
	}

	public bool interactable
	{
		get
		{
			return ((Selectable)Toggle).interactable;
		}
		set
		{
			((Selectable)Toggle).interactable = value;
		}
	}

	public event Action<GameObject, bool> OnValueChanged;

	private void Awake()
	{
		((UnityEvent<bool>)(object)Toggle.onValueChanged).AddListener((UnityAction<bool>)OnToggleValueChange);
		if (!((UnityEngine.Object)(object)Toggle.group != (UnityEngine.Object)null))
		{
			return;
		}
		UIToggleGroup component = ((Component)Toggle.group).GetComponent<UIToggleGroup>();
		if ((UnityEngine.Object)(object)component == (UnityEngine.Object)null)
		{
			Debug.LogError("Toggle的group上没有UIToggleGroup组件");
		}
		Group = component;
	}

	private void OnDestroy()
	{
		((UnityEvent<bool>)(object)Toggle.onValueChanged).RemoveListener((UnityAction<bool>)OnToggleValueChange);
	}

	private void OnToggleValueChange(bool isOn)
	{
		OnValueChanged?.Invoke(((Component)this).gameObject, isOn);
		if ((UnityEngine.Object)(object)Group != (UnityEngine.Object)null)
		{
			Group.ToggleValueChangedHandler(this);
		}
	}
}
}
