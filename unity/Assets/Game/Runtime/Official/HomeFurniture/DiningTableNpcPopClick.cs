// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;
public class DiningTableNpcPopClick : ClickableHomeObj
{
	public IHomeFurnitureServices Services { get; set; }

	protected override void OnValidClick()
	{
		Services.PopupNpcPanel(1);
	}
}

}
