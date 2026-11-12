using Zjcs.Official.ProfessionUI;
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
[Serializable]
public class CharacterDamageUIPoolPresent
{
	public AssetGuidProxy<GameObject> EffectUIPrefabProxy;

	public int PoolInitSize;
}
}
