namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;


public interface IAppearanceAnimComponent
{
	IProfessionAppearanceAnimationState AnimState { get; }

	void PlayAnimation(string animationName, bool loop);
}

}
