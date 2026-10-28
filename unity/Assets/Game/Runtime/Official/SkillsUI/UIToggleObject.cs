// Original framework-firstpass-source/UIToggleObject.cs; logging and local UI service bindings adapted.
using UnityEngine;

namespace Zjcs.Official.SkillsUI
{
public class UIToggleObject : UIToggleStatusBase
{
	public GameObject[] myActiveObj;

	public GameObject[] myDeactiveObj;

	public override void PlayEffect(bool value, bool instant = false)
	{
		if (myActiveObj != null)
		{
			GameObject[] array = myActiveObj;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetActive(value);
			}
		}
		if (myDeactiveObj != null)
		{
			GameObject[] array = myDeactiveObj;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetActive(!value);
			}
		}
	}
}
}
