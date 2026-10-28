namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public class FontAssetFontConfig
{
	public struct Info
	{
		public string FontPath;

		public string RawFontPath;

		public string Hash;

		public int Length;
	}

	public string DefaultFontAsset;

	public Dictionary<string, Info> FontAssetInfoDict;
}

}
