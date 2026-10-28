namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public interface ICharacterPortraitBuilder
{
	void HandleAppearanceCharacter(Component characterAppearance, ICharacterPortraitRequestor requestor);
}

}
