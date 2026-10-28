namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
public interface ICharacterPortraitManager
{
	CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor requestor);

	void ClearAllCharacterPortraitPersistenceData();
}

}
