// Selected original acceleration UI; explicit local services only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Player = Zjcs.Official.HomeFastHangUpUI.IHomeFastHangUpPlayer;
using FastHangUpInfo = Zjcs.Official.HomeFastHangUpUI.HomeFastHangUpInfo;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpUI
{
internal static class HomeFastHangUpMath
{
	public static long Clamp(long val, long min, long max)
	{
		return Math.Max(min, Math.Min(max, val));
	}
}

}
