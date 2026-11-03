// Migrated current official application source; see status material runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Official.BattleMaterialPlayables
{
public class PlayableMaterialPropertyStencilProvider : PlayableMaterialPropertyProvider
{
	private const string StencilKey = "_StencilComp";

	[SerializeField]
	private CompareFunction mCompareFunction;

	public override void Play(MeshMaterialProperty meshMaterialProperty)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		meshMaterialProperty.UseFloat("_StencilComp");
		meshMaterialProperty.SetFloat("_StencilComp", (float)mCompareFunction);
	}

	public override void Resume(MeshMaterialProperty meshMaterialProperty)
	{
		base.Resume(meshMaterialProperty);
		meshMaterialProperty.ReturnFloat("_StencilComp");
	}
}

}
