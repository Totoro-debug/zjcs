// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System;

namespace Zjcs.Official.World.Map13Entities
{

[Serializable]
public class EntityMapUIDisplayInfo
{
	public int EntityClassId;

	public int GroupId;

	public int MapClassId;

	public string AreaTag;

	public string Tag;

	public string DialogTitleIcon;

	public string DialogIconPrefab;

	public string IconHeadPrefab;

	public string MiniMapMark;

	public string EntityNameKey;

	public string EntityDescKey;

	public const int __ID__ = 287856547;





	public int GetTypeId()
	{
		return 287856547;
	}



	public override string ToString()
	{
		return "{ EntityClassId:" + EntityClassId + ",GroupId:" + GroupId + ",MapClassId:" + MapClassId + ",AreaTag:" + AreaTag + ",Tag:" + Tag + ",DialogTitleIcon:" + DialogTitleIcon + ",DialogIconPrefab:" + DialogIconPrefab + ",IconHeadPrefab:" + IconHeadPrefab + ",MiniMapMark:" + MiniMapMark + ",EntityNameKey:" + EntityNameKey + ",EntityDescKey:" + EntityDescKey + ",}";
	}
}
}
