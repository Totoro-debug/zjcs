// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public abstract class AwardListPanelBasePath : MonoBehaviour
{
	[SerializeField]
	protected RectTransform mSlot;

	public RectTransform Slot => mSlot;

	public abstract bool CanSetItem();

	public virtual void SetItem(AwardListPanelItem awardListPanelItem)
	{
	}

	public virtual void RemoveItem(AwardListPanelItem item)
	{
	}
}
}
