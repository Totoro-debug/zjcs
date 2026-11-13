// Selected original client methods; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using FastHangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowFastHangUpInfo;
using Player = Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowPlayer;
namespace Zjcs.Official.HomeHangUpWindowsUI
{
public enum EPropHandleType
{
	BattleBasic = 0,
	NoBattleBasic = 1,
	Skill = 2,
	Equip = 3,
	EquipResonance = 4,
	Treasure = 5,
	TreasureSuit = 6,
	Gem = 7,
	Cooking = 8,
	Extra = 9,
	NpcFriendshipLevel = 10,
	Pet = 11,
	LostGoddess = 12,
	PeriodIcCar = 13,
	Legion = 14,
	EquipCabinet = 15,
	Astrological = 16,
	Appearance = 17,
	NpcTalentTree = 18,
	GmProp = 100
}

}
