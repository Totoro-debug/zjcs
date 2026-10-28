// Original framework-firstpass-source/UIToggleGroup.cs; logging and local UI service bindings adapted.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
[RequireComponent(typeof(ToggleGroup))]
public sealed class UIToggleGroup : MonoBehaviour
{
	public ToggleGroup myGroup;

	[Tooltip("ToggleOn的状态是否可交互")]
	public bool InteractableWhenIsOn;

	public GameObject CurrentSelect
	{
		get
		{
			using (IEnumerator<Toggle> enumerator = myGroup.ActiveToggles().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					return ((Component)enumerator.Current).gameObject;
				}
			}
			return null;
		}
	}

	public event Action<GameObject> onToggleChanged;

	private void Reset()
	{
		myGroup = ((Component)this).GetComponent<ToggleGroup>();
	}

	public void ToggleValueChangedHandler(UIToggle toggle)
	{
		if (!InteractableWhenIsOn)
		{
			toggle.interactable = !toggle.isOn;
		}
		onToggleChanged?.Invoke(((Component)toggle).gameObject);
	}
}
}
