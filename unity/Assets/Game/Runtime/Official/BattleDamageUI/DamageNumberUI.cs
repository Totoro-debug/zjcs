
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
public class DamageNumberUI : MonoBehaviour
{
	[SerializeField]
	private TextMesh mNumberText;

	public void SetContent(string number, Color color)
	{




		mNumberText.text = number;
		Color linear = color.linear;
		linear.a = mNumberText.color.a;
		mNumberText.color = linear;
	}
}
}
