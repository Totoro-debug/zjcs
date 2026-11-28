using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {
public static class TreasureViewLanguage
{
    public static string GetPropertyNameKey(int type)=>"PropType."+(ProfessionPropertyType)type;
    public static string FormatPropString(int type,long value,bool includeUnit=true,bool includePropDesc=true,long targetValue=-1L)
        =>ProfessionPropertyLanguage.FormatPropString((ProfessionPropertyType)type,value,includeUnit,includePropDesc,targetValue);
    public sealed class PropValue:ProfessionPropertyLanguage.PropValue
    {
        public PropValue(int type,long value,bool includeUnit=true,bool includePropDesc=true,long targetValue=-1L)
            :base((ProfessionPropertyType)type,value,includeUnit,includePropDesc,targetValue){}
    }
public class ReplaceTextArg
	{
		public readonly object Arg;

		public readonly string OldValue;

		public readonly string NewValue;

		public ReplaceTextArg(object arg, string oldValue, string newValue)
		{
			Arg = arg;
			OldValue = oldValue;
			NewValue = newValue;
		}

		public override string ToString()
		{
			return Arg?.ToString()?.Replace(OldValue, NewValue) ?? string.Empty;
		}
	}
}

}
