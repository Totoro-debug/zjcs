namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AppearancePreset
{
	[SerializeField]
	private string mId;

	[SerializeField]
	private Sex mSex;

	[SerializeField]
	private List<string> mSkinNames = new List<string>();

	[SerializeField]
	private List<string> mAppendSkins = new List<string>();

	[SerializeField]
	private List<AppearancePresetDye> mDyes = new List<AppearancePresetDye>();

	public string Id => mId;

	public Sex Sex => mSex;

	public List<string> SkinNames => mSkinNames;

	public List<string> AppendSkins => mAppendSkins;

	public List<AppearancePresetDye> Dyes => mDyes;
}

}
