namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AppendSkinConfig
{
	[SerializeField]
	private AppendSkinType mAppendSkinType;

	[SerializeField]
	private List<string> mAppendSkins;

	public AppendSkinType AppendSkinType => mAppendSkinType;

	public List<string> AppendSkins => mAppendSkins;
}

}
