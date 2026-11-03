using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{

public class MiniMapPanelTitle : MonoBehaviour
{
	public MultiLanguageText TitleTxt;

	public void SetKey(string key)
	{
		TitleTxt.SetKey(key);
	}

	public void SetKey(string key, params object[] args)
	{
		TitleTxt.SetKey(key, args);
	}
}
}
