// Migrated current official application source; see status material runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;
using UnityEngine;

namespace Zjcs.Official.BattleMaterialPlayables
{
public abstract class PlayableMaterialPropertyProvider : MonoBehaviour
{
	public abstract void Play(MeshMaterialProperty meshMaterialProperty);

	public virtual void ProcessFrame(MeshMaterialProperty meshMaterialProperty)
	{
	}

	public virtual void Resume(MeshMaterialProperty meshMaterialProperty)
	{
	}
}

}
