// Migrated current official application source; see status material runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;
using UnityEngine;

namespace Zjcs.Official.BattleMaterialPlayables
{
public class PlayableMaterialBoundingBoxProvider : PlayableMaterialPropertyProvider
{
	public string Key = "_BoundingBox";

	public override void Play(MeshMaterialProperty meshMaterialProperty)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		meshMaterialProperty.SetVector(Key, (Vector4)meshMaterialProperty.RendererBoundsSize);
	}
}

}
