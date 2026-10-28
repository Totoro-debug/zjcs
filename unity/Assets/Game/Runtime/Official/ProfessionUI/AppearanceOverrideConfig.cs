namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;

[Serializable]
public class AppearanceOverrideConfig
{
	public AppearanceOverrideType Type;

	public List<int> MaleOverrideSkinIdList;

	public List<int> FemaleOverrideSkinIdList;
}

}
