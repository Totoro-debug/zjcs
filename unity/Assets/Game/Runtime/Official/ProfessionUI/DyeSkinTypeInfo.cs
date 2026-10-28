namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

[System.Serializable]
public class DyeSkinTypeInfo
{
	public SkinType SkinType;

	public int Channel;

	public int Index;

	public int VOffset;

	public const int __ID__ = 923410965;

	public bool LimitIndex => Index > 0;

	public DyeSkinTypeInfo()
	{
	}

}

}
