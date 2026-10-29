using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
[Serializable]
public class MapDialogIconPreviewSlot
{
	[SerializeField]
	private Transform mPreviewSlot;

	public Transform PreviewSlot => mPreviewSlot;

	public MiniMapMarkContext MiniMap { get; set; }

	public GameObject CreateMapEntityIcon(GameObject prefab)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = MiniMapMarkUIUtils.Instantiate(prefab, mPreviewSlot, MiniMap);
		val.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
		return val;
	}
}

}
