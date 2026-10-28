namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public class ProfessionAppearance : MonoBehaviour
{
	public AppearanceCharacter CharacterAppearance;

	public AppendSkinType AppendSkinType;

    private IProfessionAppearancePreviewBindings mPreviewBindings;
    private IProfessionAppearancePreviewBindings PreviewBindings => mPreviewBindings ?? ProfessionAppearancePreviewBindings.Required;
    public void ConfigurePreview(IProfessionAppearancePreviewBindings bindings)
    { mPreviewBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public void InitAppearance(int profession, Sex sex, object appearanceData, string animationName)
	{
		ProfessionAppearancePreview.SetProfessionAppearance(PreviewBindings, PreviewBindings.Player.Config, CharacterAppearance, sex, profession, appearanceData, AppendSkinType);
		AppearanceUI appearanceUI;
		if (((Component)CharacterAppearance).TryGetComponent<AppearanceUI>(out appearanceUI))
		{
			appearanceUI.PlayAnimation(animationName, loop: true);
		}
	}
}

}
