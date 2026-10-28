// Original framework-firstpass-source/UIToggleStatusBase.cs; logging and local UI service bindings adapted.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
public abstract class UIToggleStatusBase : MonoBehaviour
{
	protected Toggle myToggle;

	private void Awake()
	{
		myToggle = ((Component)this).GetComponentInParent<Toggle>();
		if ((UnityEngine.Object)(object)myToggle != (UnityEngine.Object)null)
		{
			((UnityEvent<bool>)(object)myToggle.onValueChanged).AddListener((UnityAction<bool>)OnToggleValueChanged);
		}
	}

	private void OnEnable()
	{
		if ((UnityEngine.Object)(object)myToggle != (UnityEngine.Object)null)
		{
			PlayEffect(myToggle.isOn, instant: true);
		}
	}

	private void OnToggleValueChanged(bool value)
	{
		PlayEffect(value);
	}

	public abstract void PlayEffect(bool value, bool instant = false);
}
}
