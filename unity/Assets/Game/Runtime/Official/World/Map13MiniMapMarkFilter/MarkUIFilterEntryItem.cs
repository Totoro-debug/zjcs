using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
public class MarkUIFilterEntryItem : MonoBehaviour, IGameObjectPoolItem
{
	[SerializeField]
	private Image mIcon;

	[SerializeField]
	private MultiLanguageText mDescription;

	public void Init(Sprite iconSprite, string des)
	{
		mIcon.sprite = iconSprite;
		mDescription.SetKey(des);
	}

	public void OnGameObjectPoolClear()
	{
		mIcon.sprite = null;
		mDescription.SetKey(null);
	}

	public void OnGameObjectPoolReset()
	{
	}
}

}
