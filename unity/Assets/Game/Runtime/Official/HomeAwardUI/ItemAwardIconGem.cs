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
public class ItemAwardIconGem : MonoBehaviour
{
	[SerializeField]
	public HomeAwardIcon mItemAwardIcon;

	private void Awake()
	{
		mItemAwardIcon.OnInitCompleted += MItemAwardIcon_OnInitCompleted;
	}

	private void MItemAwardIcon_OnInitCompleted()
	{
		IHomeAwardGemIcon itemIconGem = mItemAwardIcon.ItemIcon as IHomeAwardGemIcon;
		if (!(itemIconGem == null))
		{
			itemIconGem.SetCount();
		}
	}

	private void OnDestroy()
	{
		if ((Object)mItemAwardIcon != (Object)null)
		{
			mItemAwardIcon.OnInitCompleted -= MItemAwardIcon_OnInitCompleted;
		}
	}
}
}
