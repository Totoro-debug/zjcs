using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
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
