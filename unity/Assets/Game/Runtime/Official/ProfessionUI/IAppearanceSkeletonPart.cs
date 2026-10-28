namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface IAppearanceSkeletonPart : IAppearancePart
{
	GameObject SkeletonTarget { get; }

	bool HasAnim { get; }

	void TryPlayAnim(string sourceAnim);
}

}
