// Official client 884_156692; original request/handler control flow.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
namespace Zjcs.Official.BattleRoundUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public class GameEffectData
{
	public Color FactionColor;

	public string FactionRoundImgPath;

	public string FactionRoundImgETC2Path;

	public Color HpColor;

	public Color HpAddColor;

	public Color HpSubColor;

	public GameObject FillPointEffect;

	public GameObject OutLineEffect;
}

}
