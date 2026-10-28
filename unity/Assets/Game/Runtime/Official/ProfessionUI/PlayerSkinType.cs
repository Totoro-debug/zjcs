namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;

[Serializable]
public class PlayerSkinType
{
	[SerializeField]
	private SkinType mSkinType;

	[SerializeField]
	private bool mBaseSkin;

	[SerializeField]
	private bool mCanNormalSet;

	public SkinType SkinType => mSkinType;

	public bool BaseSkin => mBaseSkin;

	public bool CanNormalSet => mCanNormalSet;
}

}
