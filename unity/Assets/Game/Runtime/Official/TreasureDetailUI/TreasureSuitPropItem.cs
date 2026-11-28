// Selected original suit UI; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using PropType = System.Int32;
using Object = UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

[RequireComponent(typeof(ProfessionPlayerPropItem))]
public class TreasureSuitPropItem : MonoBehaviour
{
    private ITreasurePropertyTables Tables;
    public void Configure(ITreasurePropertyTables tables) => Tables=tables??throw new ArgumentNullException(nameof(tables));

	[SerializeField]
	private WorldMultiLanguageText mConditionText;

	[SerializeField]
	private List<Image> mImages;

	[SerializeField]
	private Color ActivedColor;

	[SerializeField]
	private Color UnActivedColor;

	private ProfessionPlayerPropItem _PropItem;

	private ProfessionPlayerPropItem PropItem
	{
		get
		{
			if ((Object)(object)_PropItem == (Object)null)
			{
				_PropItem = ((Component)this).GetComponent<ProfessionPlayerPropItem>();
			}
			return _PropItem;
		}
	}

	public void RefreshProp(TreasureSuitPropCondition condition, int propId, PropType propType, bool active)
	{
		Tables.GetProps(propId, 0).TryGetValue(propType, out var value);
		PropItem.Init((ProfessionPropertyType)propType, value, -1L);
		mConditionText.SetKey(TreasureSuitLanguage.GetEnumKey(condition));
		Color color = (active ? ActivedColor : UnActivedColor);
		mConditionText.Color = color;
		PropItem.myPropTypeText.Color = color;
		PropItem.myPropValueText.Color = color;
		foreach (Image mImage in mImages)
		{
			((Graphic)mImage).color = color;
		}
	}
}

}
