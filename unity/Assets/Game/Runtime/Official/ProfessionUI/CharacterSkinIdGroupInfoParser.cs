namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public class CharacterSkinIdGroupInfoParser : ProfessionAppearanceKeyTable<CharacterSkinIdGroupInfo>
{
	public void PostParse()
	{
		foreach (KeyValuePair<int, CharacterSkinIdGroupInfo> config in configs)
		{
			config.Value.BuildList();
		}
	}

}

}
