// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;
using UnityEngine;

public class TreasurePromoteTab : MonoBehaviour
{
	public TreasurePromotePanel.TabType TabType;
}

}
