// Selected original roster helper; see partner-roster-native-source-manifest.json.
using UnityEngine;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.PartnersUI
{

public class TabObjectTransition : Zjcs.Official.WorldUI.WorldTabTransition
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
