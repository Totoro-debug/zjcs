// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public class MaskSetting
{
	[Tooltip("遮罩偏移(世界Go:世界坐标相对偏移；UI Go:本地坐标相对偏移)")]
	public Vector3 Offset = Vector3.zero;

	[Tooltip("遮罩缩放")]
	public Vector3 Scale = Vector3.one;

	[Tooltip("是否显示遮罩(压暗)")]
	public bool ShowMask = true;

	[Tooltip("提示语的Key（text_ui.csv中的key）")]
	public int TipUIKeyId;

	public object[] TipArgs;

	public static readonly Color DefaultColor = new Color(0f, 0f, 0f, 199f / 255f);

	public static readonly Color DefaultHideColor = new Color(0f, 0f, 0f, 0f);

	private Color mMaskColor = DefaultColor;

	public Color MaskColor
	{
		get
		{

			return mMaskColor;
		}
		set
		{

			mMaskColor = value;
		}
	}

	public void SetTipArguments(params object[] args)
	{
		TipArgs = args;
	}

	public MaskSetting()
	{

	}

	static MaskSetting()
	{

	}
}

}
